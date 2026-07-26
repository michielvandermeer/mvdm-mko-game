namespace Underclass.Domain;

/// <summary>
///    A settled read of a Town, taken at <see cref="ServerTime" />. Everything the player can see comes from
///    one of these, and it carries the server's clock so remaining durations are rendered against server
///    time rather than whatever the client's clock says.
/// </summary>
/// <param name="ServerTime">The moment this read was taken, according to the server.</param>
/// <param name="Gold">The Town's gold.</param>
/// <param name="ArtisanCount">How many Artisans are producing and being paid.</param>
/// <param name="ExplorerCap">How many living Explorers the Town can house.</param>
/// <param name="Materials">What the Town has on hand.</param>
/// <param name="Gear">Every piece of Gear the Town holds.</param>
/// <param name="Explorers">The roster, living and dead.</param>
/// <param name="Expeditions">Expeditions still out.</param>
/// <param name="Reports">Reports of resolved Expeditions, newest first.</param>
public sealed record TownSnapshot(
   DateTimeOffset ServerTime,
   long Gold,
   int ArtisanCount,
   int ExplorerCap,
   IReadOnlyDictionary<MaterialType, int> Materials,
   IReadOnlyList<GearState> Gear,
   IReadOnlyList<ExplorerState> Explorers,
   IReadOnlyList<ExpeditionState> Expeditions,
   IReadOnlyList<ExpeditionReport> Reports);
