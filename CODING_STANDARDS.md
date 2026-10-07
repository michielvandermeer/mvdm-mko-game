# Coding Standards

## Language

- Write the glossary's terms capitalised as [`CONTEXT.md`](CONTEXT.md) writes them, in identifiers, test names,
  comments and player-facing strings alike. Treat a concept it lacks as invented language to rename, or as a glossary
  gap to flag to the user.
- Write British English in prose and identifiers (`Catalogue`, `serialise`, `Centred`); framework names keep their own
  spelling (`Color`, `JsonSerializer`).

## Formatting

- Indent with 3 spaces and format by hand, touching only the lines you change: with no `.editorconfig`, `dotnet format`
  reindents whole files to 4 spaces.

## Comments and docs

- Spend comments on why: the reason for a number, the invariant a line protects, the story it serves. Cite stories by
  number ("Story 17"), ADRs by path, and spec sections by name.
- Document a type or member whose name leaves out what it guarantees or why, and give every parameter of a public
  positional record a `<param>`; self-describing ones (`Accepted`, `ToContract`, row records, test classes) go bare.
- Lay out XML docs as `src/` does; `ScriptedRandom` in `TestSupport.cs` is an outlier, not a second style.

## Lockstep

Change each of these together, in one commit:

- a port or launch command, and every file naming it, the client README's _Running it_, `.agents/refs/stack.md` and
  the client's `GameApi.BaseUrl` among them;
- `Actions.Register` and the client README's key table;
- the `Godot.NET.Sdk` version in `Underclass.Client.csproj` and the Godot editor version the client README names;
- the Postgres image in `docker-compose.yml` and the API suite's `PostgreSqlBuilder`: the suite proves the wiring
  against the image the stack runs.

## Server authority

- Keep every rule, roll, outcome and estimate in `Underclass.Domain`, on the server, and send the client each number it
  shows, as `GET /api/crafting/options` was added for the Workshop:
  [ADR 0002](docs/adr/0002-server-authoritative-from-day-one.md).
- Read time only from the injected `TimeProvider` and chance only from the injected `IRandomSource`: that is what lets
  a test fast-forward an Expedition and pin its outcome.
- Grey a client control only from server-supplied numbers, as an affordance; the server's refusal stays the answer.
  Render a duration against `TownResponse.ServerTime` plus the local time elapsed since that snapshot arrived.

## Time

- Settle a continuous quantity on read, from a stored `last_settled_at`, and book a discrete event as a job through
  `IJobScheduler.PerformAtAsync`, as `TownService.DispatchAsync` does:
  [ADR 0003](docs/adr/0003-scheduled-events-and-lazy-accrual-instead-of-a-tick-loop.md).
- Keep settling idempotent and blind to when anyone looks: a player who looks late, or often, gets what one who watched
  gets, and a job that never runs only delays the news.
- Reach the Town through `TownService`, for reads as for commands: a read settles the Town, and `TownService` saves what
  settling changed.

## Economy

- Take Gear out of the economy only through the death of the Explorer carrying it, keeping the dead Explorer's row with
  status `Dead`: [ADR 0001](docs/adr/0001-permadeath-is-the-only-gear-sink.md).
- Read a red `EconomyCurveTests` as news about the loop: re-tune `Catalogue`, or tell the user the loop stopped
  cohering. Print its curve with
  `dotnet test tests/Underclass.Domain.Tests --filter "FullyQualifiedName~EconomyCurveTests" --logger "console;verbosity=detailed"`.
- Re-read that curve after adding or reordering a roll: every later roll of a seeded run shifts.

## Domain

- Call `MarkChanged()` with every mutation in `Town`: `TownService` saves only a Town whose `HasChanges` is set, the
  domain suite never persists, and accrual settling in the same request hides a forgotten call intermittently.
- Build `Underclass.Domain` on the BCL alone, keeping ASP.NET, the database, Godot and I/O outside it: that is what lets
  the domain suite play the whole loop at unit speed.

## Refusals

- Refuse a player's request with `CommandResult.Rejected(RejectionReason.X)`, and throw only for a broken invariant
  (`InvalidOperationException` naming the ids involved) or an enum value with no definition
  (`ArgumentOutOfRangeException`): a refusal is an ordinary answer, which the API sends as a 400 `RejectionResponse`.
- Word each `RejectionReason` as a whole sentence in its own arm of `Rejections.Describe`: the client shows that
  sentence as-is, and the `_` arm tells the player only "That cannot be done."

## Persistence

- Change the schema with a new migration: `schema.sql` is applied only to an empty database, so an edit there alone
  misses every existing one (the compose volume), and the API suite's fresh database hides the gap.

## Contracts

- Keep `Underclass.Contracts` self-contained: positional records of plain data, with readers' predicates in extension
  classes like `ExplorerStatuses`. The Godot client compiles this project, so a project reference would carry rules to
  it, and a computed property would serialise into the payload.

## Tests

- Cover a rule in `Underclass.Domain.Tests` and wiring (HTTP, Postgres, jobs, restarts) in `Underclass.Api.Tests`,
  which leaves the numbers to the domain suite.
- Assert on what a player could observe — the `CommandResult`, `Snapshot()` and `Fingerprint()` in the domain suite,
  HTTP responses in the API suite — and that a refused command changed nothing.
