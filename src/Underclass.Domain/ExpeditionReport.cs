namespace Underclass.Domain;

/// <summary>
///    What happened on an Expedition, kept after it resolved so the player can read the outcome of their
///    crafting decisions whenever they next look. <see cref="Gold" /> and <see cref="Materials" /> are what
///    the Explorer was carrying: gained when they returned, forfeited when they did not.
/// </summary>
/// <param name="Id">Identity of this report.</param>
/// <param name="ExpeditionId">The Expedition it describes.</param>
/// <param name="ExplorerId">Who went.</param>
/// <param name="ExplorerName">Their name, kept here so a report reads on its own.</param>
/// <param name="Area">Where they went.</param>
/// <param name="Depth">How deep.</param>
/// <param name="ResolvedAt">When the Expedition came due — not when anyone happened to look.</param>
/// <param name="Survived">Whether they came back.</param>
/// <param name="Gold">The gold they were carrying.</param>
/// <param name="Materials">The Materials they were carrying.</param>
/// <param name="GearLost">The Gear destroyed with them, or null when they returned.</param>
public sealed record ExpeditionReport(
   ReportId Id,
   ExpeditionId ExpeditionId,
   ExplorerId ExplorerId,
   string ExplorerName,
   AreaId Area,
   int Depth,
   DateTimeOffset ResolvedAt,
   bool Survived,
   long Gold,
   IReadOnlyDictionary<MaterialType, int> Materials,
   GearState? GearLost);
