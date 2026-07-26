# The simulation is server-authoritative from day one

The game's multiplayer core is a player-to-player Marketplace plus leaderboards, which makes
item provenance and progress claims worth attacking from the moment the first stranger can see
them. We decided that the server owns the simulation immediately — all outcomes, all rolls, all
state transitions — and the client is strictly a view that sends commands and renders what it is
told. This is settled now, while the prototype is a single hardcoded player and none of it is
strictly necessary yet.

## Consequences

Retrofitting authority onto a client-simulated game is a rewrite rather than a refactor, which
is the whole reason for deciding it this early: every rule written on the wrong side of the
boundary would have to move. Accepting it now costs us a genuine offline mode — permanently.
There will never be a version of this game that runs without a server, and that is understood
and accepted, not an oversight.

Practically, this means the client never rolls dice, never computes an Expedition outcome, and
never derives a risk estimate — even a displayed survival chance comes from the server, because
a client that can model risk locally is a client that can be modified to model it favourably.
It also means the client cannot be trusted with time: durations render against a server-supplied
clock rather than the local one.
