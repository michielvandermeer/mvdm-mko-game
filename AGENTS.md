# Project Underclass

A live-service crafting-and-Expedition game, built so far as a thin end-to-end slice: a Godot client, an ASP.NET Core
API, and Postgres.

- Finish each change green on `dotnet build Underclass.slnx` and `dotnet test Underclass.slnx`. The test run needs
  Docker: `Underclass.Api.Tests` starts its own Postgres through Testcontainers. The NU1903 warnings for SSH.NET arrive
  through Testcontainers and predate your change; finish with no other warnings.
- Finish a change under `client/godot/` by telling the user it needs walking through by hand (the client README's
  _Playing it_): `dotnet build` compiles the client, and no automated test runs it.

## Projects

- `src/Underclass.Domain`: the simulation, behind the one seam `Town`.
- `src/Underclass.Data`: loads and saves a whole Town in Postgres; the schema is `Schemas/schema.sql`.
- `src/Underclass.Api`: REST over `Town`'s commands, and the scheduled job that resolves an Expedition when it comes
  due.
- `src/Underclass.Contracts`: the wire shapes, shared with the client.
- `client/godot`: the Godot client.
- `tests/Underclass.Domain.Tests`: the rules, driven through `Town`.
- `tests/Underclass.Api.Tests`: the wiring, over HTTP against a real Postgres.

## Context

- Before writing code, tests, or docs → [`CODING_STANDARDS.md`](CODING_STANDARDS.md).
- Before naming, describing, or discussing a domain concept → the glossary, [`CONTEXT.md`](CONTEXT.md).
- Before changing where rules, rolls, or time live, how Gear leaves the economy, or how an Expedition resolves →
  [`docs/adr/`](docs/adr/). Work that contradicts an ADR is the user's call: name the ADR and ask.
- Before changing or launching the Godot client → [`client/godot/README.md`](client/godot/README.md).
- Before running Postgres or the API by hand → [`.agents/refs/stack.md`](.agents/refs/stack.md).
- Before acting on a story number ("story 17") or the spec's _Out of Scope_, both cited in comments → the prototype
  spec, deleted from the tree: `git show 8e7199e^:.agents/specs/project-underclass-prototype.md`.
