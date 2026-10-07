# Adding a player action

Done when each step is made or ruled out; the neighbours at each place show the shape.

1. `Town`: the command, with its tuning numbers in `Catalogue`.
2. `RejectionReason` and its `Rejections.Describe` arm, for each new refusal.
3. `Underclass.Domain.Tests`: the rule and its refusals.
4. `Underclass.Contracts`: any new request or response record.
5. `UnderclassEndpoints.MapUnderclassApi`: the route, with new shapes mapped in `ContractMapper`. An action that books
   a job gets its own `TownService` method, as `DispatchAsync` does.
6. `Underclass.Api.Tests`: an `UnderclassHost` helper, and a test of the wiring, refusal included.
7. The client: a `GameApi` method and a `…Requested` panel event wired in `Main.WirePanels`; a new panel joins
   `Hud._views`, and a new key `Actions.Register`.
