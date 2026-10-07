# Coding Standards

## Done When

- `dotnet build Underclass.slnx` and `dotnet test Underclass.slnx` pass with Docker running (Testcontainers),
  warning-free except NU1903 on Testcontainers' SSH.NET.
- A `client/godot/` change goes to the user to walk through by hand, per the client README's _Playing it_: the build
  compiles the client, and nothing runs it.

## Style

- Name domain concepts with [`CONTEXT.md`](CONTEXT.md)'s terms, capitalised as it writes them, in identifiers, tests,
  comments and player-facing strings: its _Avoid_ lists name the synonyms that creep in. Rename a concept it lacks, or
  flag it to the user as a glossary gap.
- Spell in British English, in prose and identifiers (`Catalogue`, `serialise`, `Centred`); framework names keep
  their own (`Color`, `JsonSerializer`).
- Comment the why — the reason for a number, the invariant a line protects, the story it serves: the code says what.
- Indent 3 spaces and format by hand, touching only the lines you change: with no `.editorconfig`, `dotnet format`
  reindents whole files to 4. Lay out XML docs as `src/` does; `ScriptedRandom` in `TestSupport.cs` is the outlier.

## Decisions

Working against an ADR is the user's call: name it and ask.

- Keep every rule, roll, outcome and estimate in `Underclass.Domain`, with time and chance only from the injected
  `TimeProvider` and `IRandomSource`, and send the client each number it shows: **server authority**,
  [ADR 0002](docs/adr/0002-server-authoritative-from-day-one.md).
- Settle a continuous quantity on read from its `last_settled_at`, idempotent and blind to when anyone looks, and book
  a discrete event as a job through `IJobScheduler.PerformAtAsync`: **event-driven time**,
  [ADR 0003](docs/adr/0003-scheduled-events-and-lazy-accrual-instead-of-a-tick-loop.md).
- Take Gear out of the economy only with the death of the Explorer carrying it: **permadeath sink**,
  [ADR 0001](docs/adr/0001-permadeath-is-the-only-gear-sink.md).
- Re-read the `EconomyCurveTests` curve after adding or reordering a roll: every later roll of its seeded run shifts.
  When it goes red, re-tune `Catalogue` or tell the user the loop stopped cohering.

## Layers

- Before adding a player action → [`.agents/refs/player-action.md`](.agents/refs/player-action.md).
- Call `MarkChanged()` with every mutation in `Town`: `TownService` saves only a Town whose `HasChanges` is set, the
  domain suite never persists, and accrual settling in the same request often sets the flag anyway.
- Word each `RejectionReason` as a whole sentence in its own arm of `Rejections.Describe`: the client shows it as-is,
  and the `_` arm says only "That cannot be done."
- Give a contract record its helpers through an extension class, as `ExplorerStatuses` does: a computed property
  would serialise into the payload.

## Persistence

- Change the schema with a new migration: `schema.sql` is applied only to an empty database, so an edit there alone
  misses every existing one (the compose volume), and the API suite's fresh database hides the gap.
- Name the migration class `_<yyyyMMddHHmm>_<Name>`, an `IDbMigration` in `Underclass.Data`: the runner reads the
  stamp from the name and runs only stamps past the last one applied.
- Add a table holding part of a Town to `TownRepository.DeletionOrder` and to `TruncateTown` in
  `tests/Underclass.Api.Tests/UnderclassApi.cs`: a save empties the Town's rows through the one, and each API test
  through the other.
- Raise `schema.sql`'s `-- Migration version:` header to the last migration folded into it: a fresh database
  otherwise runs that migration again.

## Lockstep

Change each group in one commit: no build checks them.

- a port or launch command, and every file naming it, the client README's _Running it_ and `GameApi.BaseUrl` among
  them;
- `Actions.Register` and the client README's key table;
- the `Godot.NET.Sdk` version in `Underclass.Client.csproj` and the Godot editor version the client README names;
- the Postgres image in `docker-compose.yml` and the API suite's `PostgreSqlBuilder`.
