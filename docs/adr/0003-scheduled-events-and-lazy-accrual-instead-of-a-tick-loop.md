# Scheduled events and lazy accrual, not a simulation tick loop

Expeditions run in real time and must resolve whether or not the player is present, which is
conventionally solved with a simulation loop advancing the world every N seconds. We rejected
that. Expeditions are scheduled as discrete jobs at their completion time via `mvdmio.ASP.Jobs`
(Postgres-backed, so they survive restarts), and genuinely continuous quantities — Artisan
output, wages — are never advanced by anything: each carries a `last_settled_at` and elapsed
time is settled when the value is read.

## Considered Options

- **Continuous tick loop.** The obvious approach, and the one a reader will assume we used. Rejected: it requires a heartbeat per player whether anything is happening or not, it scales with population rather than with activity, and offline catch-up becomes an explicit replay problem we would have to write and get right.

## Consequences

Anything requiring genuinely continuous interaction *between* systems becomes awkward — there is
no moment at which the world steps forward as a whole, so a rule like "wages compete with
Expedition upkeep for the same gold, minute by minute" has nowhere natural to live. Nothing in
the current design needs that; if something does later, this is the decision to revisit.

Expedition resolution is written to be **idempotent**, and reading Town state also resolves any
Expedition already past its completion time. The scheduled job is therefore an optimisation for
timeliness rather than the sole path to resolution: if the server was down when an Expedition
came due, the next read settles it correctly and identically. Any future resolution logic must
preserve that property — it is what makes downtime a non-event.
