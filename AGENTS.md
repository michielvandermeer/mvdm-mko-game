# Project Underclass

A live-service crafting-and-Expedition game, built so far as a thin end-to-end slice: a Godot client, an ASP.NET Core
API, and Postgres.

- Finish each change green on `dotnet build Underclass.slnx` and `dotnet test Underclass.slnx`. The test run needs
  Docker: `Underclass.Api.Tests` starts its own Postgres through Testcontainers, whose SSH.NET dependency raises the
  NU1903 warnings; finish with no others.
- Finish a change under `client/godot/` by telling the user it needs walking through by hand (the client README's
  _Playing it_): the build compiles the client, and no automated test runs it.

## Context

- Before writing code, tests or docs → [`CODING_STANDARDS.md`](CODING_STANDARDS.md).
- Before naming or discussing a domain concept → [`CONTEXT.md`](CONTEXT.md).
- Before adding a player action → [`.agents/refs/player-action.md`](.agents/refs/player-action.md).
- Before changing the schema or what a Town stores → [`.agents/refs/schema-change.md`](.agents/refs/schema-change.md).
- Before changing where rules, rolls or time live, how Gear leaves the economy, or how an Expedition resolves →
  [`docs/adr/`](docs/adr/). Work against an ADR is the user's call: name the ADR and ask.
- Before changing or launching the Godot client → [`client/godot/README.md`](client/godot/README.md).
- Before running Postgres or the API by hand → [`.agents/refs/stack.md`](.agents/refs/stack.md).
- Before acting on a cited story ("Story 17") or the spec's _Out of Scope_ → the deleted prototype spec:
  `git show 8e7199e^:.agents/specs/project-underclass-prototype.md`.
