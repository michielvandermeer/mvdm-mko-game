namespace Underclass.Contracts;

/// <summary>
///    Everything the client renders, in one payload. Every command returns one of these too, so the client
///    never has to work out what a command changed — it re-renders from the answer.
/// </summary>
/// <remarks>
///    Enumerated values travel as their names rather than as numbers or as shared enum types: the client is a
///    view, and a wire format that reads plainly in a log is worth more here than compile-time coupling to the
///    domain. Nothing in this assembly references <c>Underclass.Domain</c>, which is what stops game rules
///    from reaching the client through the back door.
/// </remarks>
/// <param name="ServerTime">
///    The server's clock at the moment of the read. Remaining durations are rendered against this rather than
///    against the client's clock.
/// </param>
/// <param name="Gold">The Town's gold.</param>
/// <param name="ArtisanCount">How many Artisans are producing and drawing wages.</param>
/// <param name="ExplorerCap">How many living Explorers the Town can house.</param>
/// <param name="Materials">Material name to amount on hand.</param>
/// <param name="Gear">Every piece of Gear the Town holds.</param>
/// <param name="Explorers">The roster, living and dead.</param>
/// <param name="Expeditions">Expeditions still out, soonest due first.</param>
/// <param name="Reports">Reports of resolved Expeditions, newest first.</param>
public sealed record TownResponse(
   DateTimeOffset ServerTime,
   long Gold,
   int ArtisanCount,
   int ExplorerCap,
   Dictionary<string, int> Materials,
   List<GearResponse> Gear,
   List<ExplorerResponse> Explorers,
   List<ExpeditionResponse> Expeditions,
   List<ReportResponse> Reports);

/// <param name="Id">Identity of this piece of Gear.</param>
/// <param name="BaseType">What it is.</param>
/// <param name="Material">What it was crafted from.</param>
/// <param name="Power">The stat that is added to its Explorer's skill.</param>
public sealed record GearResponse(Guid Id, string BaseType, string Material, int Power);

/// <param name="Id">Identity of this Explorer.</param>
/// <param name="Name">What the player calls them.</param>
/// <param name="Skill">What they bring before Gear.</param>
/// <param name="Status">Idle, OnExpedition or Dead.</param>
/// <param name="EquippedGearId">The Gear they carry, or null.</param>
public sealed record ExplorerResponse(Guid Id, string Name, int Skill, string Status, Guid? EquippedGearId);

/// <param name="Id">Identity of this Expedition.</param>
/// <param name="ExplorerId">Who is out.</param>
/// <param name="GearId">What they took.</param>
/// <param name="Area">Which Area they went to.</param>
/// <param name="Depth">How deep.</param>
/// <param name="StartedAt">When they left.</param>
/// <param name="CompletesAt">When they are due back.</param>
/// <param name="SurvivalChance">The chance they return, as it was at dispatch.</param>
public sealed record ExpeditionResponse(
   Guid Id,
   Guid ExplorerId,
   Guid GearId,
   string Area,
   int Depth,
   DateTimeOffset StartedAt,
   DateTimeOffset CompletesAt,
   double SurvivalChance);

/// <param name="Id">Identity of this report.</param>
/// <param name="ExpeditionId">The Expedition it describes.</param>
/// <param name="ExplorerId">Who went.</param>
/// <param name="ExplorerName">Their name.</param>
/// <param name="Area">Where they went.</param>
/// <param name="Depth">How deep.</param>
/// <param name="ResolvedAt">When the Expedition came due.</param>
/// <param name="Survived">Whether they came back.</param>
/// <param name="Gold">The gold they were carrying — gained if they returned, forfeited if not.</param>
/// <param name="Materials">The Materials they were carrying, on the same terms.</param>
/// <param name="GearLost">The Gear destroyed with them, or null when they returned.</param>
public sealed record ReportResponse(
   Guid Id,
   Guid ExpeditionId,
   Guid ExplorerId,
   string ExplorerName,
   string Area,
   int Depth,
   DateTimeOffset ResolvedAt,
   bool Survived,
   long Gold,
   Dictionary<string, int> Materials,
   GearResponse? GearLost);
