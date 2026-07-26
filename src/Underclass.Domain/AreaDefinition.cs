namespace Underclass.Domain;

/// <summary>
///    One Area, and how it behaves at each depth. Duration, threat and reward are all functions of depth
///    rather than fixed numbers, because depth is the dial the player sets: it is the only place in the
///    slice where the player chooses how much risk to take on for how much return.
/// </summary>
/// <param name="Id">Which Area this is.</param>
/// <param name="Name">The name the player reads.</param>
/// <param name="DurationPerDepth">How long one depth level takes. Total duration is linear in depth.</param>
/// <param name="BaseThreat">The threat an Expedition faces at depth 1.</param>
/// <param name="ThreatPerDepth">How much threat each further depth level adds.</param>
/// <param name="GoldUnit">Gold at depth 1. Gold is quadratic in depth, so reward outruns threat.</param>
/// <param name="BulkLoot">The Material this Area yields at every depth.</param>
/// <param name="BulkPerDepth">How much <paramref name="BulkLoot" /> one depth level yields.</param>
/// <param name="RareLoot">The Material this Area only yields below the first depth level.</param>
/// <param name="RarePerDepth">How much <paramref name="RareLoot" /> each depth level past the first yields.</param>
public sealed record AreaDefinition(
   AreaId Id,
   string Name,
   TimeSpan DurationPerDepth,
   int BaseThreat,
   int ThreatPerDepth,
   long GoldUnit,
   MaterialType BulkLoot,
   int BulkPerDepth,
   MaterialType RareLoot,
   int RarePerDepth)
{
   /// <summary>How long an Expedition to this Area at this depth takes.</summary>
   public TimeSpan DurationAt(int depth) => DurationPerDepth * depth;

   /// <summary>What the Explorer's strength is measured against at this depth.</summary>
   public int ThreatAt(int depth) => BaseThreat + (ThreatPerDepth * (depth - 1));

   /// <summary>
   ///    The gold a successful Expedition to this depth returns. Quadratic in depth while threat is linear,
   ///    which is what makes pushing deeper the way to progress rather than an optional flourish.
   /// </summary>
   public long GoldAt(int depth) => GoldUnit * depth * depth;

   /// <summary>
   ///    The Materials a successful Expedition to this depth returns. Deterministic: the gamble in this slice
   ///    is whether the Explorer comes back at all, not what they are carrying.
   /// </summary>
   public IReadOnlyDictionary<MaterialType, int> LootAt(int depth)
   {
      var loot = new Dictionary<MaterialType, int> {
         [BulkLoot] = BulkPerDepth * depth
      };

      var rare = RarePerDepth * (depth - 1);

      if (rare > 0)
         loot[RareLoot] = loot.GetValueOrDefault(RareLoot) + rare;

      return loot;
   }
}
