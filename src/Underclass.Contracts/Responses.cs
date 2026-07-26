namespace Underclass.Contracts;

/// <summary>The Gear that came off the anvil, and the Town it changed.</summary>
/// <param name="Gear">The piece that was crafted, so the client can point at what is new.</param>
/// <param name="Town">The Town after the craft.</param>
public sealed record CraftResponse(GearResponse Gear, TownResponse Town);

/// <summary>The Expedition that just left, and the Town it left behind.</summary>
/// <param name="Expedition">The dispatch, carrying the time it is due back.</param>
/// <param name="Town">The Town after the dispatch.</param>
public sealed record DispatchResponse(ExpeditionResponse Expedition, TownResponse Town);

/// <summary>
///    What a proposed Expedition would cost and return. Computed on the server from the same rule that
///    resolution rolls against, so the odds the player agrees to are the odds they get.
/// </summary>
/// <param name="ExplorerId">Who would go.</param>
/// <param name="Area">Where.</param>
/// <param name="Depth">How deep.</param>
/// <param name="SurvivalChance">The chance they come back, in [0, 1].</param>
/// <param name="Duration">How long they would be gone.</param>
/// <param name="Gold">The gold they would return with.</param>
/// <param name="Loot">The Materials they would return with.</param>
public sealed record PreviewResponse(
   Guid ExplorerId,
   string Area,
   int Depth,
   double SurvivalChance,
   TimeSpan Duration,
   long Gold,
   Dictionary<string, int> Loot);

/// <summary>
///    Why a command was refused. Returned with a 400 rather than an exception, so a refusal is something the
///    client can render as a sentence instead of a mystery.
/// </summary>
/// <param name="Reason">A stable code, for the client to branch on if it wants to.</param>
/// <param name="Message">The same thing in words, ready to show the player.</param>
public sealed record RejectionResponse(string Reason, string Message);
