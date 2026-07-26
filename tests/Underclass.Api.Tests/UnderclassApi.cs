using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using mvdmio.ASP.Jobs;
using Npgsql;
using Testcontainers.PostgreSql;
using Underclass.Api.Jobs;
using Underclass.Contracts;
using Underclass.Domain;

namespace Underclass.Api.Tests;

/// <summary>
///    One Postgres for the whole suite, and the controllable clock and rolls the hosts booted against it share.
/// </summary>
/// <remarks>
///    <para>
///       The Town's identity is a constant — the slice has one hardcoded player — so two tests running at once
///       would be playing the same Town. Every test class here therefore joins
///       <see cref="UnderclassApiCollection" />, which xUnit runs one test at a time, and each test starts by
///       emptying the tables.
///    </para>
///    <para>
///       The clock the hosts run on starts well beyond any real date. That is what keeps the scheduled
///       resolution job out of the way: a job booked for an Expedition due in 2099 cannot fire during a test, so
///       whenever an Expedition does resolve here it is because something read the Town — which is the path
///       these tests exist to prove.
///    </para>
///    <para>
///       That clock is never wound back between tests. It only ever moves forward, like the real one, because a
///       <see cref="FakeTimeProvider" /> refuses to go backwards and the API holds the instance it was built
///       with. Nothing here depends on an absolute date, so each test simply starts from wherever the one before
///       it left off.
///    </para>
///    <para>
///       One API serves the whole suite unless a test explicitly takes it away, which is what
///       <see cref="ShutDownAsync" /> and <see cref="BringUp" /> are for. Booting a host is the expensive part of
///       these tests, and a host that stays up is also a host that is not repeatedly shut down — see
///       <see cref="UnderclassHost.DisposeAsync" /> for why that matters.
///    </para>
/// </remarks>
public sealed class UnderclassApi : IAsyncLifetime
{
   /// <summary>Where the shared clock starts. Far enough ahead that no resolution job can come due mid-test.</summary>
   private static readonly DateTimeOffset ClockStart = new(2099, 1, 1, 12, 0, 0, TimeSpan.Zero);

   /// <summary>The setting Program.cs reads its database out of.</summary>
   private const string ConnectionStringSetting = "DbConnection";

   /// <summary>The tables a Town lives in, in an order that can be emptied without tripping a foreign key.</summary>
   private const string TruncateTown = """
      TRUNCATE underclass.expedition_reports,
               underclass.expeditions,
               underclass.explorers,
               underclass.gear,
               underclass.materials,
               underclass.towns
      """;

   /// <summary>The same image docker-compose.yml runs, so the tests prove the wiring against the real thing.</summary>
   private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:18-alpine").Build();

   private UnderclassHost? _host;

   /// <summary>The clock every host in this suite runs on. Advance it to fast-forward an Expedition.</summary>
   internal FakeTimeProvider Clock { get; } = new(ClockStart);

   /// <summary>The rolls every host in this suite makes. Tell it whether Explorers come back.</summary>
   internal ScriptedRandom Random { get; } = new();

   internal string ConnectionString => _postgres.GetConnectionString();

   /// <inheritdoc />
   public async Task InitializeAsync()
   {
      await _postgres.StartAsync();

      // An environment variable rather than a test-host configuration source, because Program.cs reads
      // "DbConnection" off builder.Configuration before it calls Build(): anything layered on through
      // ConfigureAppConfiguration is applied at Build() time and so arrives after the API has already read the
      // setting. Environment variables are in builder.Configuration from the moment it exists, and they override
      // appsettings.json — which is what stops these tests from running against a developer's own database.
      Environment.SetEnvironmentVariable(ConnectionStringSetting, ConnectionString);
   }

   /// <inheritdoc />
   public async Task DisposeAsync()
   {
      await ShutDownAsync();

      Environment.SetEnvironmentVariable(ConnectionStringSetting, null);

      await _postgres.DisposeAsync();
   }

   /// <summary>
   ///    An API serving an empty database, so the first read creates the Town a new player starts with. Explorers
   ///    survive by default; a test that is about dying says so.
   /// </summary>
   public async Task<UnderclassHost> StartAsync()
   {
      Random.ExplorersSurvive = true;

      var host = BringUp();

      // After the host, never before: emptying the tables needs the schema the host's migration creates. Safe to
      // do with the API already up, because a Town row is only written once something reads it.
      await EmptyTownAsync();

      return host;
   }

   /// <summary>
   ///    Takes the API away. Requests cannot be served and no job can run until something brings it back, which is
   ///    how a test stages downtime.
   /// </summary>
   public async Task ShutDownAsync()
   {
      if (_host is null)
         return;

      var host = _host;
      _host = null;

      await host.DisposeAsync();
   }

   /// <summary>
   ///    Brings an API up on the same database and the same clock, leaving every row where it is. Returns the one
   ///    already running if it never went away.
   /// </summary>
   public UnderclassHost BringUp()
   {
      if (_host is not null)
         return _host;

      _host = new UnderclassHost(this);

      // Forces the host up — and with it the schema migration Program.cs runs before serving anything.
      _ = _host.Services;

      return _host;
   }

   /// <summary>Moves the shared clock to an instant, which is how an Expedition is fast-forwarded to due.</summary>
   public void AdvanceTo(DateTimeOffset instant)
   {
      Clock.SetUtcNow(instant);
   }

   /// <summary>
   ///    Empties the Town's tables directly, rather than through a host. Connecting to the container by hand is
   ///    the point: if a host were somehow configured against another database, its Town would survive this and
   ///    the very first assertion about a fresh Town would fail.
   /// </summary>
   private async Task EmptyTownAsync()
   {
      await using var connection = new NpgsqlConnection(ConnectionString);
      await connection.OpenAsync();

      await using var truncate = new NpgsqlCommand(TruncateTown, connection);
      await truncate.ExecuteNonQueryAsync();
   }
}

/// <summary>
///    An API under test: the real application, on the suite's database, clock and rolls. Wraps the HTTP calls
///    the client will make so a test reads as a sequence of player actions rather than of requests.
/// </summary>
public sealed class UnderclassHost : WebApplicationFactory<Program>
{
   private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

   private readonly UnderclassApi _api;

   internal UnderclassHost(UnderclassApi api)
   {
      _api = api;
   }

   /// <inheritdoc />
   protected override void ConfigureWebHost(IWebHostBuilder builder)
   {
      // Not "Development": that would load appsettings.Development.json, and a test should be told where its
      // database is rather than inheriting a developer's. Which database that is comes from an environment
      // variable the fixture sets — see UnderclassApi.InitializeAsync for why it cannot be set from here.
      builder.UseEnvironment("Testing");

      // The same two seams the domain tests use, injected into the whole stack: a clock a test can move and
      // rolls a test can dictate. Everything else is the production wiring.
      builder.ConfigureTestServices(
         services => {
            services.AddSingleton<TimeProvider>(_api.Clock);
            services.AddSingleton<IRandomSource>(_api.Random);
         }
      );
   }

   /// <inheritdoc />
   /// <remarks>
   ///    Shutting the host down stops the job runner's Postgres instance registration, which reaches for the
   ///    database connection factory that the very same shutdown may already have disposed. That ordering is a
   ///    wart in how the two libraries this slice consumes shut down, it happens strictly after the last request
   ///    this host will ever serve, and in the real API the process is on its way out anyway. It must not fail a
   ///    test, so exactly that exception is swallowed here and nothing else is.
   /// </remarks>
   public override async ValueTask DisposeAsync()
   {
      try
      {
         await base.DisposeAsync();
      }
      catch (ObjectDisposedException)
      {
         // See the remarks above.
      }
   }

   /// <summary>Reads the Town, which also settles accrual and resolves anything that has come due.</summary>
   public async Task<TownResponse> ReadTownAsync()
   {
      using var client = CreateClient();

      return await GetAsync<TownResponse>(client, "/api/town");
   }

   /// <summary>The Area catalogue, priced per depth.</summary>
   public async Task<List<AreaResponse>> ReadAreasAsync()
   {
      using var client = CreateClient();

      return await GetAsync<List<AreaResponse>>(client, "/api/areas");
   }

   /// <summary>What can be crafted, and what each Material would make of it.</summary>
   public async Task<CraftingOptionsResponse> ReadCraftingOptionsAsync()
   {
      using var client = CreateClient();

      return await GetAsync<CraftingOptionsResponse>(client, "/api/crafting/options");
   }

   public Task<CraftResponse> CraftAsync(string baseType, string material)
   {
      return PostAsync<CraftResponse>("/api/crafting/craft", new CraftRequest(baseType, material));
   }

   public Task<TownResponse> EquipAsync(Guid explorerId, Guid gearId)
   {
      return PostAsync<TownResponse>($"/api/explorers/{explorerId}/equip", new EquipRequest(gearId));
   }

   public Task<TownResponse> SalvageAsync(Guid gearId)
   {
      return PostAsync<TownResponse>($"/api/gear/{gearId}/salvage", content: null);
   }

   public Task<TownResponse> RecruitAsync()
   {
      return PostAsync<TownResponse>("/api/explorers/recruit", content: null);
   }

   public Task<PreviewResponse> PreviewAsync(Guid explorerId, string area, int depth)
   {
      return PostAsync<PreviewResponse>("/api/expeditions/preview", new DispatchRequest(explorerId, area, depth));
   }

   public Task<DispatchResponse> DispatchAsync(Guid explorerId, string area, int depth)
   {
      return PostAsync<DispatchResponse>("/api/expeditions", new DispatchRequest(explorerId, area, depth));
   }

   /// <summary>
   ///    Posts a command that is expected to be refused, and hands back the reason. Asserts the refusal arrived
   ///    as a 400 carrying a reason — story 11 and 19 are about a refusal being legible, which a 500 is not.
   /// </summary>
   public async Task<RejectionResponse> ExpectRefusalAsync(string path, object? content)
   {
      using var client = CreateClient();

      using var response = await client.PostAsJsonAsync(path, content, Json);

      Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);

      var rejection = await response.Content.ReadFromJsonAsync<RejectionResponse>(Json);

      Assert.NotNull(rejection);
      Assert.False(string.IsNullOrWhiteSpace(rejection.Reason));
      Assert.False(string.IsNullOrWhiteSpace(rejection.Message));

      return rejection;
   }

   /// <summary>
   ///    How many resolution jobs are booked and still waiting to run. Counted rather than merely checked,
   ///    because job storage is not emptied between tests: what a test can say is that dispatch added an
   ///    appointment, not that this is the only one in the table.
   /// </summary>
   public async Task<int> PendingResolutionJobsAsync()
   {
      using var scope = Services.CreateScope();

      var jobs = scope.ServiceProvider.GetRequiredService<IJobScheduler>();

      var scheduled = await jobs.GetScheduledJobsAsync<ResolveExpeditionJob>();

      return scheduled.Count();
   }

   private async Task<T> PostAsync<T>(string path, object? content)
   {
      using var client = CreateClient();

      using var response = await client.PostAsJsonAsync(path, content, Json);

      await ThrowIfRefusedAsync(response);

      return await ReadAsync<T>(response);
   }

   private static async Task<T> GetAsync<T>(HttpClient client, string path)
   {
      using var response = await client.GetAsync(path);

      await ThrowIfRefusedAsync(response);

      return await ReadAsync<T>(response);
   }

   private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
   {
      var value = await response.Content.ReadFromJsonAsync<T>(Json);

      Assert.NotNull(value);

      return value;
   }

   /// <summary>
   ///    Turns an unexpected refusal into a failure that names the reason, so a broken test reads as
   ///    "ExplorerBusy" rather than as a deserialization error further down.
   /// </summary>
   private static async Task ThrowIfRefusedAsync(HttpResponseMessage response)
   {
      if (response.IsSuccessStatusCode)
         return;

      var body = await response.Content.ReadAsStringAsync();

      Assert.Fail($"{response.RequestMessage?.Method} {response.RequestMessage?.RequestUri?.PathAndQuery} answered {(int)response.StatusCode}: {body}");
   }
}

/// <summary>
///    An <see cref="IRandomSource" /> a test dictates. Survival is the only roll a test here cares about, so it
///    is a switch rather than a queue: these tests are about whether an outcome reaches the database and the
///    client, and the domain suite is where the shape of the outcome itself is pinned down.
/// </summary>
public sealed class ScriptedRandom : IRandomSource
{
   /// <summary>Whether Explorers come back from the Expeditions resolved from here.</summary>
   public bool ExplorersSurvive { get; set; } = true;

   /// <inheritdoc />
   /// <remarks>
   ///    Survival chance is clamped well inside [0, 1] — the safest Expedition can still kill and the deepest
   ///    can still be survived — so a roll at either end of the range decides the outcome whatever the odds.
   /// </remarks>
   public double NextDouble()
   {
      return ExplorersSurvive ? 0.0 : 0.999;
   }

   /// <inheritdoc />
   /// <remarks>The low end of whatever range is asked for, so Gear power and recruit skill are repeatable.</remarks>
   public int Next(int minInclusive, int maxExclusive)
   {
      return minInclusive;
   }
}

/// <summary>
///    Binds every API test class to one database, one clock and one Town. xUnit runs a collection's tests one at
///    a time, which is what stops two tests from playing the same hardcoded Town at once.
/// </summary>
[CollectionDefinition(Name)]
public sealed class UnderclassApiCollection : ICollectionFixture<UnderclassApi>
{
   public const string Name = "Underclass API";
}
