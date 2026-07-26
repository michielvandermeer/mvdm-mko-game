using mvdmio.ASP.Jobs;
using Underclass.Api;
using Underclass.Api.Endpoints;
using Underclass.Api.Jobs;
using Underclass.Data;
using Underclass.Domain;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetValue<string>("DbConnection")
                       ?? throw new InvalidOperationException("No 'DbConnection' is configured. See appsettings.json.");

builder.Services.AddUnderclassData(connectionString);

// Time and chance are injected everywhere, including here: the domain never reads a clock or a global random
// source, which is what keeps outcomes reproducible and keeps every roll on the server.
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IRandomSource>(new SeededRandomSource(builder.Configuration.GetValue<int?>("RandomSeed")));

builder.Services.AddScoped<TownService>();

// Postgres-backed job storage, so an Expedition's resolution appointment survives a restart.
builder.Services.AddJobs(jobs => jobs.UsePostgresStorage("Underclass", connectionString));
builder.Services.RegisterJob<ResolveExpeditionJob>();

var app = builder.Build();

// Not guarded: booting against a stale schema would serve errors rather than failing the start.
await app.Services.MigrateUnderclassDatabaseAsync();

app.MapUnderclassApi();

await app.RunAsync();

/// <summary>Named so that the integration tests can boot this exact application through a test host.</summary>
public partial class Program;
