# Coding Standards

## Language

- Name domain concepts with the terms of [`CONTEXT.md`](CONTEXT.md), capitalised as it writes them (Explorer,
  Expedition, Gear, Material, Loot, Area, Town, Workshop, Artisan, Salvage, Master Smith), in identifiers, test names,
  comments, and player-facing strings alike; its _Avoid_ lines list the synonyms to replace. A concept it lacks is
  invented language (rename it) or a glossary gap (flag it to the user).
- Write British English in prose and identifiers alike (`Catalogue`, `colour`, `Centred`, `serialise`); framework names
  keep their own spelling (`Color`, `JsonSerializer`).
- Write every string the player reads as a whole sentence. A refusal's sentence lives in `Rejections.Describe` on the
  server and the client shows it as-is: one wording, owned where the rule is.

## Formatting

- Indent with 3 spaces and format by hand, touching only the lines you change: with no `.editorconfig`, `dotnet format`
  reindents whole files to 4 spaces.
- Open an initializer, a switch expression, or a lambda body on the same line (`new TownState {`, `material switch {`,
  `async () => {`); give a block's braces lines of their own.
- Leave braces off a body that is one statement on one line. Brace a body that spans lines, and brace every arm of an
  `if`/`else` once one arm needs braces.
- Leave a blank line after a guard clause, and before a `return` that follows other statements.
- Break a long call one argument per line, with `)` on a line of its own. Break a long parameter list (a method or a
  positional record) one per line, closing on the last parameter: `double SurvivalChance);`.
- Wrap comment prose at 120 columns; code lines run as long as they need.
- Use `var` for every local.
- Seal every non-static class and record class. `Program` is the one exception: partial, so the API tests can host it.
- Inject dependencies through a primary constructor: `TownService(TownRepository repository, TimeProvider clock, …)`.
- Name a `CancellationToken` `ct`, as `ct = default` on public methods; an override keeps the base's name.
- Name an argument when it is a bare literal whose meaning the call does not show: `SendTo(…, depth: 1)`,
  `EquippedGear: null`, `bad: true`.
- Sort `using`s with `System.*` first, then the rest alphabetically ignoring case (`Microsoft.*`, `mvdmio.*`, `Npgsql`).
  The client has implicit usings off (Godot's SDK leaves them off): write every `using` a client file needs.
- Name a lambda parameter for what it holds (`explorer =>`, `candidate =>`, `entry =>`) in `src/`, `client/`, and the
  API suite. The domain suite also uses single letters in short assertions (`e => e.Id == TestTown.Elara`).

## Comments and docs

- Spend comments on why: the reason for a number, the invariant a line protects, the story it serves. Cite stories by
  number ("Story 17"), ADRs by path, and spec sections by name.
- Give every type a `<summary>` and every positional record parameter a `<param>`. Document a member when its name
  leaves out what it guarantees or why; self-describing members (`Accepted`, `New`, `ToContract`) go bare.
- Lay out XML docs as `src/` does: a one-line `/// <summary>…</summary>` when it fits; otherwise the text indented four
  spaces after `///`, a `<para>` inside `<remarks>` indented three more, and `<see cref="X" />` with a space before
  `/>`. `ScriptedRandom` in `TestSupport.cs` is the lone outlier, not a second style.
- Comment a non-obvious column in `schema.sql` with a `--` line above it.
- Change `client/godot/README.md` with what it states: the key table with `Actions.Register`, the running steps with
  the ports and commands they name.

## Where things live

- Keep every rule, roll, outcome, and estimate in `Underclass.Domain`, run on the server:
  [ADR 0002](docs/adr/0002-server-authoritative-from-day-one.md). When the client needs a number to show, add it to a
  server response, as `GET /api/crafting/options` was added, rather than deriving it.
- Keep `Underclass.Domain` free of infrastructure (ASP.NET, the database, Godot, I/O), with no package references in its
  csproj: that is what lets the domain suite play the whole loop at unit speed.
- Keep `Underclass.Contracts` referencing nothing, the domain least of all: the client takes the payload shapes from it
  without taking a single rule.
- Keep content in code: Areas, Materials, and Gear base types are enums plus `Catalogue`. The database stores only what
  a player did, naming content by its enum name.
- Read time only from the injected `TimeProvider` and chance only from the injected `IRandomSource`.
  `SeededRandomSource` holds the one `Random` and `Program.cs` the one `TimeProvider.System`: that is what lets a test
  fast-forward an Expedition and pin its outcome.
- Settle a continuous quantity on read, from a stored `last_settled_at`, and schedule a discrete event as a job through
  `IJobScheduler.PerformAtAsync`; the world advances only when something reads it:
  [ADR 0003](docs/adr/0003-scheduled-events-and-lazy-accrual-instead-of-a-tick-loop.md).
- Keep resolution idempotent and reachable from any read: `Town.Snapshot()` settles before it reads, and a job only
  reads the Town. A job that never runs then delays the news instead of losing it (ADR 0003).
- Take Gear out of the economy only through the death of the Explorer carrying it, and keep a dead Explorer's row with
  status `Dead`: [ADR 0001](docs/adr/0001-permadeath-is-the-only-gear-sink.md). Salvage recycles rather than drains, so
  it returns strictly fewer Materials than crafting consumed.

## Domain

- Add a player action as a public method on `Town` that returns `CommandResult` or `CommandResult<T>`, calls `Settle()`
  first, and calls `MarkChanged()` with every mutation: `TownService` saves only a Town whose `HasChanges` is set, and
  the domain suite never persists, so it misses a forgotten call.
- Refuse with `CommandResult.Rejected(RejectionReason.X)`: a refusal is an ordinary answer. Throw only for a broken
  invariant (`InvalidOperationException` naming the ids involved) or an enum value with no definition
  (`ArgumentOutOfRangeException`).
- Add each `RejectionReason` with a `<summary>` and an arm in `Rejections.Describe`: the `_` arm would tell the player
  only "That cannot be done."
- Derive whatever preview and dispatch both show from `Town.Proposal`, and fix at dispatch every number the player
  confirmed (the survival chance), rolling against the stored value: the odds shown are the odds rolled (story 17).
- Attribute an outcome to when it came due (`CompletesAt`), resolve due Expeditions oldest first, and settle accrual in
  whole `AccrualInterval`s, carrying the remainder: a player who looks late, or often, gets what one who watched gets.
- Draw survival rolls with `NextDouble()` and integer rolls (Gear power, recruit skill) with `Next(min, maxExclusive)`:
  the domain suite scripts each kind separately. Adding or reordering a roll shifts every later roll of a seeded run, so
  re-read the economy curve afterwards.
- Put every tuning number in `Catalogue` with a comment on what it is tuned to do. Keep safe Areas survivable but
  unprofitable, and reward growing faster than threat with depth: ADR 0001's _Consequences_, watched by
  `EconomyCurveTests`.
- Give each identity a typed `readonly record struct XId(Guid Value)` with `New()`; raw `Guid`s appear only in contracts
  and SQL rows.
- Model state as `sealed record`s with positional parameters, changed through `with`. `TownState` is the one mutable
  shape: it holds no rules and is what the database stores. `TownSnapshot` hands out copies.
- Hold gold as `long`: `bigint` in the schema, `long` in the contracts.

## Persistence

- Write SQL as raw string literals with `:name` parameters (the `mvdmio.Database.PgSQL` syntax), passed as
  `Dictionary<string, object?>`, and alias every selected column `AS camelCase` to its row record.
- Store enums by name (`ToString()`, `Enum.Parse`): a renumbered enum cannot reinterpret existing rows, and the tables
  read by hand.
- Store instants as `timestamptz`: write `.UtcDateTime`, read back through `ToOffset`.
- Write a Town only through `TownRepository.SaveAsync`, which rewrites every child row in one transaction. A new child
  table needs `town_id … REFERENCES underclass.towns (town_id) ON DELETE CASCADE`, a place in `DeletionOrder` and in
  `SaveAsync`'s insert sequence (parents first), and a place in the API suite's `TruncateTown`.
- Change the schema with a new `IDbMigration` class in `Underclass.Data`, named `_<yyyyMMddHHmm>_<Name>`. `schema.sql`
  is applied only to an empty database, so an edit there alone never reaches an existing one (the compose volume), and
  the API suite's fresh database hides the gap. When folding a migration into `schema.sql`, raise its
  `-- Migration version:` header to that migration's identifier, or a fresh database runs the migration twice.
- Write SQL keywords in upper case and names in snake_case, mark every column `NOT NULL` or `NULL`, and name indexes
  `ix_<table>_<purpose>`.

## API

- Keep an endpoint thin: parse each enum name with `Rejections.TryParseName` (bare `Enum.TryParse` also accepts
  ordinals), run the command through `TownService`, and answer with the Town it produced through `ContractMapper`'s
  `ToContract()`, so the client re-renders from one payload.
- Answer a refusal with `Rejections.ToResult` and an unknown name with `Rejections.Malformed`, each a 400 carrying a
  `RejectionResponse`: a refusal arrives as a reason, never as a 500.
- Route every read through `TownService` as well as every command: a read settles the Town, and `TownService` saves
  what settling changed.
- Book a resolution job for anything that comes due, as `TownService.DispatchAsync` does.

## Contracts

- Target `net8.0` and use only what it offers (C# 12; no `System.Threading.Lock`, which the domain uses): the Godot
  client compiles this project.
- Name a payload `<Thing>Request` or `<Thing>Response`: a positional record of `List<>` and `Dictionary<string, int>`,
  leaving the domain's `IReadOnly*` types in the domain.
- Send enum values as their names, in `string`s. Give readers a class of constants and predicates like
  `ExplorerStatuses`, with extension methods rather than computed record properties: a property would be serialised
  into the payload.

## Client

- Grey a control only from server-supplied numbers, as an affordance; the server's refusal stays the answer that counts.
- Render durations against `TownResponse.ServerTime` plus the local time elapsed since that snapshot arrived: the local
  clock is untrusted (ADR 0002).
- Talk to the server only through `GameApi`: its Godot `HttpRequest` completes on the main thread, while a
  `System.Net.Http` continuation that touches the scene tree crashes intermittently.
- Render each panel from the whole `TownResponse` through `ITownView.Accept`, rebuilt rather than patched, and register
  a new panel in `Hud._views`.
- Wire a panel's `event Action<…>? XRequested` in `Main.WirePanels`: call `GameApi`, return on `Refused(result)`, then
  `Accept` the Town it answered with.
- Start async work from a Godot callback through `Main.Run`, which shows the player whatever escapes; an `async void`
  handler would lose it.
- Declare every class deriving from a Godot type `partial`: Godot's source generators require it (GD0001). Declare a
  field assigned in `_Ready` as `= null!`.
- Register an input action in `Actions.Register`, named along the Town's axes rather than the screen's.

## Tests

- Cover a rule in `Underclass.Domain.Tests` and wiring (HTTP, Postgres, jobs, restarts) in `Underclass.Api.Tests`; the
  API suite leaves the numbers to the domain suite.
- Assert on what a player could observe: the `CommandResult`, `Snapshot()`, and `Fingerprint()` in the domain suite,
  HTTP responses in the API suite; `Town.State` stays out of assertions. Assert that a refused command changed nothing.
- Name a test as a snake_case sentence of the behaviour, keeping glossary capitals:
  `Crafted_Gear_lands_within_the_range_its_Material_implies`.
- When a test proves a spec story, cite it in the `<summary>`: "Story 36: …".
- Write an assertion message as a lowercase clause saying what had to hold: `"gold must trend upward, but averaged …"`.

### Domain suite

- Build a Town with `TestTown.With(clock, random, …)`: no Artisan, so accrual stays out of tests not about it; a cap of
  3; and two Explorers of its own, Elara (skill 10) and Bram (skill 14), not the Catalogue's three. Arm and send with
  `ArmWith` and `SendTo`, which assert each step.
- Move time on `TestTown.Clock()` with `clock.SetUtcNow(expedition.CompletesAt)` or `clock.Advance(…)`.
- Script rolls with `ScriptedRandom(doubles, ints)`. Doubles feed survival: `0.0` always survives, `1.0` always dies,
  an empty queue gives `0.5`. Ints feed Gear power and recruit skill, clamped into the range asked for; an empty queue
  gives the minimum.
- Assert on shape in `EconomyCurveTests`, and re-read it when it fails rather than forcing it green: re-tune
  `Catalogue`, or tell the user the loop stopped cohering. Print its curve with
  `dotnet test tests/Underclass.Domain.Tests --filter "FullyQualifiedName~EconomyCurveTests" --logger "console;verbosity=detailed"`.
- Iterate on this suite alone, without Docker:
  `dotnet test tests/Underclass.Domain.Tests --filter "FullyQualifiedName~DispatchTests"`.

### API suite

- Join every class to `[Collection(UnderclassApiCollection.Name)]`, take `UnderclassApi api` through the primary
  constructor, and open each test with `var host = await api.StartAsync();`: the one hardcoded Town makes the suite
  serial, and `StartAsync` empties its tables and sets Explorers to survive.
- Move the shared clock with `api.AdvanceTo(…)`, from a time the test read (`dispatched.Expedition.CompletesAt`). It
  starts in 2099, so no job fires mid-test, and only moves forward, so a test assumes no absolute date.
- Count jobs relative to before (`booked + 1`): job storage carries over between tests.
- Stage downtime with `api.ShutDownAsync()` and `api.BringUp()`, which keep the database and the clock.
- Drive the API through `UnderclassHost`'s helpers, with contract strings (`"Sword"`, `"SunkenVault"`) and
  `ExplorerStatuses`; expect a refusal with `ExpectRefusalAsync`.
- Decide deaths with `api.Random.ExplorersSurvive = false`: this suite's `ScriptedRandom` is a switch rather than the
  domain suite's queues, and its integer rolls return the minimum.
- Configure the host through environment variables set before it boots, as `InitializeAsync` sets `DbConnection`:
  `Program.cs` reads configuration before `Build()`, so `ConfigureAppConfiguration` arrives too late.

## Dependencies

- Run the same Postgres image in `docker-compose.yml` and the API suite's `PostgreSqlBuilder`: the suite proves the
  wiring against the image the stack runs.
- Keep the `Godot.NET.Sdk` version in `Underclass.Client.csproj` equal to the Godot editor version the client README
  names.
