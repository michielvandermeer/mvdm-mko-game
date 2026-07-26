# The Godot client

A view and an input device, and nothing else. Every number on screen came out of the API, every outcome was
decided by the server, and no rule about what is allowed lives in this project. When a command is refused, what
the player reads is the server's own sentence.

## Running it

The client needs the API, and the API needs Postgres.

```bash
# 1. Postgres, from the repository root
docker compose up -d

# 2. The API. Migrations run at startup; the port matters, the client looks for 5268.
dotnet run --project src/Underclass.Api --urls http://localhost:5268

# 3. The client
godot-mono --path client/godot            # or open the project in the Godot 4.7.1 .NET editor and press F5
```

The first `GET /api/town` creates the Town a new player starts with, so there is nothing to seed.

## Playing it

| Key            | What it does                                              |
| -------------- | --------------------------------------------------------- |
| `WASD`, arrows | Walk. Each key follows one of the Town's isometric lanes. |
| `E`            | Use whatever you are standing at.                         |
| `R`            | Read the Expedition reports.                              |
| `Esc`          | Put away whatever is open.                                |
| `F5`           | Ask the Town for its state again.                         |

The loop: walk up the lane to **The Workshop** and craft a Sword out of Copper. Walk down the lane to **The
Expedition Board**, give the Sword to an Explorer, pick an Area and a depth, read the chance they come back, and
send them out. The Quarry is survivable and pays badly. The Sunken Vault is where Iron and Steel come from and it
kills Explorers who are not equipped for it.

Then close the game. The Expedition resolves without you — there is a scheduled job for it, and reading the Town
settles anything the job missed. Come back and press `R`.

## How it is put together

- `Scripts/Main.cs` — the composition root. Wires panels to requests; holds the only knowledge of all three of
  the Town, the HUD and the server.
- `Scripts/Net/` — the one path to state. Godot's `HttpRequest` (its completion signal arrives on the main
  thread) with `System.Text.Json` doing the parsing.
- `Scripts/Town/` — the isometric Town, built from `Polygon2D` primitives with no imported assets.
  `IsometricGrid` is the only place that knows the projection, which is what makes swapping in a real tileset a
  change to shapes rather than to logic.
- `Scripts/Ui/` — the panels. Each one renders a snapshot and raises what the player asked for; none of them
  decide anything.

There are no automated tests here, by design — the server suites cover the rules and the wiring, and this project
is verified by walking the Town and completing the loop.
