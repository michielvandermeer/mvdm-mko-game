# Project Underclass — Thin End-to-End Prototype
Status: ready-for-agent

## Problem Statement

There is no game yet — an empty repository and a design that exists only as a conversation.
The design's central claim is that a loop of *craft Gear → send an Explorer out → they come
back with Materials or don't come back at all → craft better Gear* is compelling enough to
build a live-service game around, with a player-to-player Marketplace on top.

That claim can't be evaluated from a document. Several parts of it are only credible if they
actually run end to end: an Expedition must resolve while the player isn't looking, an
Explorer's death must permanently destroy the Gear they were carrying, and every one of those
outcomes must be decided by the server rather than the client. Meanwhile the intended feel —
being a resident of your own Town, walking through it — is a claim about immersion that
nothing but a walkable scene can test.

What's needed first is the thinnest possible version of the whole thing, with none of the
systems fleshed out and none of the boundaries faked, so that the loop can be felt and then
iterated on.

## Solution

A minimal but genuinely end-to-end vertical slice of Project Underclass.

The player walks their Master Smith through a small isometric Town, enters the Workshop and
crafts a piece of Gear out of Materials they have on hand, gives it to one of their Explorers,
walks to the Expedition Board and sends that Explorer to an Area. The Expedition takes real
time. The player can close the game. When they come back, the Expedition has resolved: either
the Explorer returned with Loot and gold, or they died — permanently — and the Gear they were
carrying is gone with them. Obsolete Gear is Salvaged back into Materials, better Gear is
crafted, and the loop is closed.

Every boundary in that sentence is real. Real Godot client, real HTTP API, real Postgres, real
scheduled background resolution. What is *thin* is the content and the depth of each system:
one Gear slot, three Materials, two Areas, three Explorers, one Artisan, and crafting rules
simple enough to fit on an index card. The systems are deliberately shallow; the architecture
underneath them is not.

## User Stories

### Being in the Town

1. As a player, I want to walk my Master Smith around an isometric Town, so that I feel like a resident of the place rather than an operator of a menu.
2. As a player, I want my Master Smith to move along the isometric axes, so that movement reads as diagonal navigation of the Town rather than flat screen-space sliding.
3. As a player, I want buildings and my character to occlude each other correctly by depth, so that the Town reads as a coherent space.
4. As a player, I want to see the Workshop and the Expedition Board as distinct places I physically walk to, so that the Town's layout carries meaning instead of being decoration.
5. As a player, I want a visible prompt when I'm standing somewhere I can interact, so that I know the Town is responsive to where I am.
6. As a player, I want to see my current gold and Materials at a glance, so that I can decide what I'm able to craft before walking to the Workshop.

### Crafting

7. As a player, I want to craft Gear from Materials in the Workshop, so that I have something to equip an Explorer with.
8. As a player, I want my choice of Material to visibly determine the Gear's properties, so that I understand crafting as a decision rather than a button.
9. As a player, I want each crafted piece to vary somewhat even from the same inputs, so that a craft has a moment of anticipation.
10. As a player, I want crafting to consume my Materials, so that Materials are a real constraint and Loot has a purpose.
11. As a player, I want to be refused when I lack the Materials, with the reason stated, so that failure is legible rather than mysterious.
12. As a player, I want to see the Gear I've crafted collected somewhere, so that I can choose what to send out and what to Salvage.

### Explorers and dispatch

13. As a player, I want a roster of named Explorers, so that sending one out feels like sending a person rather than spending a resource.
14. As a player, I want to equip a specific piece of Gear to a specific Explorer, so that my crafting decisions attach to someone I recognise.
15. As a player, I want to choose which Area to send an Explorer to, so that I control the risk I'm taking.
16. As a player, I want to choose how deep the Expedition goes, so that risk and reward are a dial I set rather than a property of the Area alone.
17. As a player, I want an honest estimate of the Explorer's chance of returning before I confirm, so that a death is the result of a gamble I understood.
18. As a player, I want an Explorer who is out on an Expedition to be unavailable, so that Explorer capacity paces how fast I can progress.
19. As a player, I want to be prevented from sending an Explorer with no Gear equipped, or from sending a dead one, so that the game refuses incoherent states rather than resolving them strangely.
20. As a player, I want to recruit a new Explorer for gold, so that losing one is a setback rather than the end of my run.

### Time and returning

21. As a player, I want Expeditions to take real time, so that the world has a rhythm I check in on rather than a queue I grind.
22. As a player, I want an Expedition to resolve even if I close the game, so that my time is respected and the world continues without me.
23. As a player, I want to see how long is left on an Expedition in progress, so that I know when it's worth coming back.
24. As a player, I want to read what happened when an Explorer returns, so that the outcome of my crafting decisions is legible.
25. As a player, I want successful Expeditions to return Materials and gold, so that the loop feeds itself.
26. As a player, I want harder Areas and greater depth to return better Materials, so that pushing further is the way to progress.
27. As a player, I want my Artisan to accumulate Materials while I'm away, so that the Town is productive between my sessions.
28. As a player, I want my Artisan's wages deducted over that same time, so that gold drains continuously rather than only accumulating.

### Death and Salvage

29. As a player, I want a failed Expedition to kill the Explorer permanently, so that risk carries genuine weight.
30. As a player, I want the dead Explorer's equipped Gear destroyed with them, so that Gear leaves the economy and crafting stays necessary.
31. As a player, I want any Loot that Explorer had gathered lost as well, so that pushing deeper is a real gamble and not a free option.
32. As a player, I want a dead Explorer to remain visible in my roster as dead, so that the loss is acknowledged rather than silently erased.
33. As a player, I want to Salvage Gear I no longer need back into Materials, so that obsolete Gear has a floor value without an NPC vendor.
34. As a player, I want Salvage to return less than crafting consumed, so that Salvage is recovery rather than a loop I can farm.

### Integrity

35. As a player, I want all outcomes decided by the server, so that the Marketplace I'll eventually trade on is worth trusting.
36. As a player, I want my Town's state to survive a client restart and a server restart, so that progress is durable.
37. As a player, I want an Expedition that was due while the server was down to still resolve correctly, so that downtime doesn't strand my Explorers.

### Development

38. As a developer, I want the whole loop exercised through one domain seam with no infrastructure, so that balance can be iterated at unit-test speed.
39. As a developer, I want to control time and randomness in tests, so that Expedition outcomes are reproducible and fast-forwardable.
40. As a developer, I want to simulate hundreds of Expeditions in a test and read the resulting curve, so that I can judge the loop's economy numerically before judging it by feel.
41. As a developer, I want the full stack covered by an integration test against real Postgres, so that the API, job scheduling and persistence wiring is proven rather than assumed.
42. As a developer, I want the Godot client to hold no game rules, so that the client stays a view and server authority isn't quietly eroded.

## Implementation Decisions

### Modules

- **`Underclass.Domain`** — the simulation. Pure C#, zero infrastructure dependencies: no ASP.NET, no database, no Godot, no I/O. This constraint is load-bearing and should be enforced by keeping the project's references empty of infrastructure packages.
- **`Underclass.Data`** — Postgres persistence via `mvdmio.Database.PgSQL`. Owns schema as `.sql` files under `Schemas/`, picked up by the library's migration runner and embedded through its MSBuild props.
- **`Underclass.Api`** — ASP.NET Core. HTTP endpoints, DI composition, and Expedition resolution scheduling via `mvdmio.ASP.Jobs`.
- **`Underclass.Domain.Tests`**, **`Underclass.Api.Tests`** — see Testing Decisions.
- **`client/godot/`** — Godot 4.7 C# project. View and input only.

### Domain surface

The domain exposes a single command/query surface — one entry point that accepts commands and
returns the resulting Town state. Everything the player can do is a command on that surface:
craft Gear, equip Gear to an Explorer, dispatch an Expedition, resolve a due Expedition,
salvage Gear, recruit an Explorer. This single seam is what the domain tests drive.

`TimeProvider` (built into .NET 8+) is injected rather than reading the clock directly, and all
randomness comes from an injected, seedable source. Nothing in the domain calls
`DateTime.UtcNow` or `Random.Shared`. This is what makes both fast-forwarding and reproducible
outcomes possible, and it is also what keeps rolls on the server.

### Time model

No tick loop. Two mechanisms instead:

- **Discrete scheduled events.** On dispatch, a resolution job is scheduled for the Expedition's
  completion time via `IJobScheduler.PerformAtAsync`. Postgres-backed, so it survives restarts.
- **Lazy accrual.** Continuous quantities — Artisan output and wages — are not advanced by any
  loop. Each holds a `last_settled_at`, and elapsed time is settled on read.

Expedition resolution is **idempotent**, and reading Town state also resolves any Expedition
whose completion time has passed. The scheduled job is therefore an optimisation for
timeliness, not the only path to resolution; if the server was down when an Expedition came
due, the next read settles it correctly. This directly satisfies story 37.

### Content and shape of the slice

Content is defined as a static catalogue in the domain, not in the database — there is no
content pipeline in scope, and two Areas do not justify one.

- Three Material types, tiered.
- One Gear slot. Gear has one meaningful stat plus a quality roll.
- Two Areas of differing difficulty, each with a duration, a survival difficulty, and a Loot table.
- Three starting Explorers, each with a name and a skill value. Recruitment adds more, for gold.
- One Artisan, producing the lowest-tier Material on accrual and charging a wage on the same clock.

Crafting: the chosen Material deterministically sets the stat *range*; a roll places the result
within it. This is the minimum that honours the settled "author the shape, gamble the magnitude"
direction without committing to the crafting internals, which are explicitly deferred.

Resolution: survival is computed from Explorer skill and equipped Gear stat against Area
difficulty and chosen depth. Success yields Materials and gold scaled by Area and depth.
Failure kills the Explorer, destroys their equipped Gear, and forfeits the run's Loot. A dead
Explorer is retained in the roster carrying a dead status rather than deleted, and is
permanently ineligible for dispatch or equipping.

### API contract

A REST surface over the domain commands, returning the Town state snapshot as the common
response shape so the client can re-render from one payload:

- `GET /api/town` — full snapshot: gold, Materials, Gear, Explorer roster, Expeditions in progress, unread reports. Settles accruals and due Expeditions as a side effect. Carries the server's current time, so the client renders remaining durations against server time rather than trusting the local clock.
- `GET /api/areas` — the Area catalogue with difficulty and duration.
- `POST /api/crafting/craft` — Gear base type + Material choice.
- `POST /api/explorers/recruit`
- `POST /api/explorers/{id}/equip` — Gear to equip.
- `POST /api/expeditions` — Explorer, Area, depth. Returns the Expedition with its completion time.
- `POST /api/expeditions/preview` — the honest survival estimate for a *proposed* Explorer + Area + depth. Takes the same inputs as dispatch, creates nothing, and is computed server-side so the client never models risk itself.
- `POST /api/gear/{id}/salvage`

Rejections (insufficient Materials, Explorer busy or dead, no Gear equipped) return a
structured error with a reason code the client can render as text — story 11 and 19.

There is no authentication and a single hardcoded player. The client points at `localhost`.

### Persistence

Postgres in Docker, schema authored as SQL under `Underclass.Data/Schemas/`, migrations run at
API startup through the `mvdmio.Database.PgSQL` runner with its advisory-lock serialisation.
Tables cover the player/Town, Explorers, Gear, Material inventory, and Expeditions. Areas,
Material definitions and Gear base types are code, not rows.

### Client

Zero art assets. The isometric Town is built from Godot primitives: `Polygon2D` diamonds
arranged in a Y-sorted grid for ground, taller polygons for the Workshop and Expedition Board,
and a `CharacterBody2D` for the Master Smith moving along the isometric axes. `Area2D` triggers
drive interaction prompts. Swapping in a real tileset later must not touch logic.

UI is a small set of panels — Town status, crafting, dispatch, Expedition report — rendered from
the `GET /api/town` snapshot. An HTTP client wrapper with `System.Text.Json` is the only path to
state. The client computes no outcomes, rolls nothing, and holds no rules; it renders what the
server says and sends commands.

### Toolchain

Godot **4.7.1, .NET build** (the plain build ships without C# support). .NET SDK pinned via
`global.json`, since Godot 4.x documents .NET 8+ as its requirement and a newer SDK's
compatibility with Godot's build tooling should be verified on first build rather than assumed.

## Testing Decisions

**What makes a good test here.** Tests assert on externally observable behaviour: the Town
state returned after a command, the outcome of a resolved Expedition, the error returned by a
rejected command. They do not assert on internal method calls, private state, or the sequence
of steps taken to reach a result. A test that would break when the crafting formula is
refactored but the observable results are unchanged is a bad test — and given that crafting
internals are explicitly deferred and *will* change, this matters more than usual here.

**Prior art.** None — this is a greenfield repository. This spec establishes the pattern.

**`Underclass.Domain.Tests`** — the primary suite, driving the single domain command surface
with a fake `TimeProvider` and a seeded random source. No database, no HTTP. Coverage:

- Crafting consumes the right Materials and produces Gear whose stats fall in the range the chosen Material implies.
- Crafting with insufficient Materials is rejected, and inventory is unchanged.
- Dispatch marks the Explorer unavailable and records a completion time derived from the Area.
- Dispatching a busy Explorer, a dead Explorer, or an unequipped Explorer is rejected.
- An Expedition does not resolve before its completion time, and does resolve once time is advanced past it.
- Resolving twice produces the same state as resolving once (idempotence).
- A successful Expedition adds the expected Materials and gold; a failed one kills the Explorer, destroys the equipped Gear, and forfeits the run's Loot.
- A dead Explorer remains in the roster as dead and cannot be dispatched or equipped.
- Salvage returns Materials strictly less than crafting consumed.
- Accrual settles Artisan output and wages proportional to elapsed time, and settling twice in a row is a no-op.
- **An economy curve test**: hundreds of Expeditions across both Areas and a range of depths, asserting on aggregate shape — that gold and Materials trend upward while deaths occur at a non-trivial rate. This is the test that answers "does the loop cohere", and it is expected to be re-tuned rather than kept green by force.

**`Underclass.Api.Tests`** — thin integration coverage through HTTP, via `WebApplicationFactory`
against a real Postgres in Docker with migrations applied per run. This seam exists only to
prove the wiring the domain seam cannot see:

- The full loop over HTTP: craft → equip → dispatch → advance past completion → read Town → assert the outcome landed.
- State survives an API restart.
- An Expedition due while no job ran is resolved by the next `GET /api/town`.
- A rejected command returns a structured reason rather than a 500.

**Godot client** — no automated tests. Verified by walking the Town and completing the loop by hand.

## Out of Scope

- **Marketplace** and **leaderboards**. Settled as design direction, absent from this slice. Server authority is in place specifically so they can be added without a rewrite.
- **Authentication, accounts, multiple players.** One hardcoded player.
- **Crafting internals** — affix pools, relic components, process choices, rerolling. Deliberately deferred; the slice uses the simplest rule that preserves the shape.
- **Area tier gating** — what unlocks higher Areas. Deferred.
- **Whether Loot may include finished Gear.** Deferred; the slice returns Materials and gold only, which is the reversible choice.
- **Encounter logs and mid-Expedition decision points.** The end goal for Expeditions; this slice delivers a plain report.
- **Artisans as a system** — hiring, assignment, multiple Artisans, production chains. Exactly one Artisan exists, purely to give lazy accrual something to accrue.
- **Explorer skill growth**, injury, recovery mechanics beyond permadeath.
- **Multiple Gear slots**, Gear comparison UI, inventory management beyond a list.
- **Art, animation, audio, localization**, and the `mvdmio.TranslationTools.Client` integration.
- **Monetization**, onboarding, tutorialisation, save-slot management.
- **Godot export and packaging.** Runs from the editor.
- **Client-side resilience.** No retry, reconnect, or offline UX; if the API is unreachable the client surfaces the failure and does nothing clever.
- **Concurrency.** One player, one client, no guarding against simultaneous conflicting commands or double dispatch from two sessions.
- **Town building and upgrades** as a gold sink. The design calls for them; the slice has recruitment and wages as its only sinks.

## Further Notes

**Two prerequisites are unmet on the development machine** and need installing before anything
builds: the .NET SDK (nothing .NET is present), and the Godot 4.7.1 **.NET** build. Both need
elevated permissions or a manual download.

**The economy has one drain, and it is optional.** Gear leaves the game only when an Explorer
dies, and death is something the player chooses to risk. A player who farms the safe Area
indefinitely never loses Gear, never needs to craft again, and — once the Marketplace exists —
contributes nothing to it. Safe Areas must therefore be tuned to be genuinely unprofitable, with
reward scaling faster than risk, hard enough that pushing deeper is compulsory rather than
optional. The economy curve test above is where this gets watched. This is the single largest
balance risk in the design and it was accepted knowingly.

**Deliberately deferred, and worth revisiting in roughly this order**: whether Loot may include
finished Gear (cheap to keep as "never", expensive to walk back once it's in), what gates
higher-tier Areas, and then crafting depth.

**Candidate ADRs** for decisions that are expensive to reverse and will read as arbitrary
without their reasoning: permadeath as the sole Gear sink; server authority from day one; and
scheduled events with lazy accrual instead of a tick loop.
