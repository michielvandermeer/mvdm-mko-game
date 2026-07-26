namespace Underclass.Domain;

/// <summary>
///    One Expedition in progress. The survival chance is fixed at dispatch rather than recomputed at
///    resolution, which is what makes the estimate the player was shown before confirming the same number
///    the server rolls against — and what keeps the Gear that went along accounted for even though the
///    Explorer is the one carrying it.
/// </summary>
/// <param name="Id">Identity of this dispatch.</param>
/// <param name="ExplorerId">Who was sent.</param>
/// <param name="GearId">What they took. Destroyed with them if they do not come back.</param>
/// <param name="Area">Where they were sent.</param>
/// <param name="Depth">How deep they were sent.</param>
/// <param name="StartedAt">When they left.</param>
/// <param name="CompletesAt">When they are due. Resolution is not allowed before this.</param>
/// <param name="SurvivalChance">The chance they return, as shown to the player at dispatch.</param>
public sealed record ExpeditionState(
   ExpeditionId Id,
   ExplorerId ExplorerId,
   GearId GearId,
   AreaId Area,
   int Depth,
   DateTimeOffset StartedAt,
   DateTimeOffset CompletesAt,
   double SurvivalChance);
