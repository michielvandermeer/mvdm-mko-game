# Adding a player action

A player action crosses every layer. Work down the list: each step names where the addition goes, and the neighbours
there show its shape. The action is done when every step is made or ruled out.

1. `Town`: the command. Any tuning number it needs goes in `Catalogue`.
2. `RejectionReason`: each new refusal, worded in `Rejections.Describe`.
3. `Underclass.Domain.Tests`: the rule, on a Town from `TestTown` in `TestSupport.cs`.
4. `Underclass.Contracts`: a `<Thing>Request` when the action takes a body. The action answers with `TownResponse`, or
   with a `<Thing>Response` pairing what it made with the Town, as `CraftResponse` and `DispatchResponse` do.
5. `UnderclassEndpoints.MapUnderclassApi`: the route. Parse enum names with `Rejections.TryParseName`, run the command
   through `TownService.ExecuteAsync`, and map new shapes in `ContractMapper`. An action that books a job gets its own
   `TownService` method, as `DispatchAsync` does.
6. `Underclass.Api.Tests`: a helper on `UnderclassHost`, and a test of the wiring, refusal included.
7. The client: a `GameApi` method, and a `…Requested` event on the panel, wired in `Main.WirePanels`. A new panel
   implements `ITownView` and joins `Hud._views`; a new key goes in `Actions.Register`.
