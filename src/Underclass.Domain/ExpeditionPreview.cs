namespace Underclass.Domain;

/// <summary>
///    What a proposed Expedition would cost and what it would return, computed for an Explorer who has not
///    been sent anywhere. The client never derives any of this: a death has to be the result of a gamble the
///    player understood, which means the number they saw came from the same place the roll does.
/// </summary>
/// <param name="ExplorerId">Who would go.</param>
/// <param name="Area">Where.</param>
/// <param name="Depth">How deep.</param>
/// <param name="SurvivalChance">The chance they come back, in [0, 1].</param>
/// <param name="Duration">How long they would be gone.</param>
/// <param name="Gold">The gold they would return with.</param>
/// <param name="Loot">The Materials they would return with.</param>
public sealed record ExpeditionPreview(
   ExplorerId ExplorerId,
   AreaId Area,
   int Depth,
   double SurvivalChance,
   TimeSpan Duration,
   long Gold,
   IReadOnlyDictionary<MaterialType, int> Loot);
