namespace Underclass.Domain;

/// <summary>
///    Every number in the game, in one place. Content is code rather than rows: the slice has three
///    Materials and two Areas, which does not justify a content pipeline, and keeping it here means the
///    economy can be re-tuned by editing one file and re-reading the curve test.
/// </summary>
public static class Catalogue
{
   /// <summary>How much of the chosen Material one craft consumes.</summary>
   public const int CraftCost = 3;

   /// <summary>
   ///    How much Salvage returns. Strictly less than <see cref="CraftCost" />, so Salvage is a floor value
   ///    for obsolete Gear rather than a loop that can be farmed.
   /// </summary>
   public const int SalvageYield = 1;

   /// <summary>What a new Explorer costs, so that losing one is a setback rather than the end of the run.</summary>
   public const int RecruitCost = 250;

   /// <summary>The deepest an Expedition can go.</summary>
   public const int MaxDepth = 3;

   /// <summary>Gold a fresh Town starts with — roughly two recruits, or eight hours of one Artisan's wages.</summary>
   public const int StartingGold = 500;

   /// <summary>How much Copper a fresh Town starts with: four Copper crafts.</summary>
   public const int StartingCopper = 12;

   /// <summary>How many living Explorers a Town can house.</summary>
   public const int ExplorerCap = 4;

   /// <summary>How many Artisans a Town has. Exactly one, purely to give lazy accrual something to accrue.</summary>
   public const int ArtisanCount = 1;

   /// <summary>How much Copper one Artisan produces per <see cref="AccrualInterval" />.</summary>
   public const int ArtisanOutputPerInterval = 1;

   /// <summary>What one Artisan is paid per <see cref="AccrualInterval" /> — one gold a minute.</summary>
   public const int ArtisanWagePerInterval = 5;

   /// <summary>The Material an Artisan produces: the lowest tier, and only that.</summary>
   public const MaterialType ArtisanOutput = MaterialType.Copper;

   /// <summary>
   ///    The granularity of lazy accrual. Artisan output and wages are not advanced by any loop; elapsed time
   ///    is settled in whole intervals on read, and the remainder is carried rather than dropped.
   /// </summary>
   public static readonly TimeSpan AccrualInterval = TimeSpan.FromMinutes(5);

   /// <summary>The Explorers a fresh Town starts with, and how skilled each of them is.</summary>
   public static readonly IReadOnlyList<(string Name, int Skill)> StartingExplorers = [
      ("Elara", 10),
      ("Bram", 12),
      ("Corin", 14)
   ];

   /// <summary>Names for recruits, taken in order so that recruitment consumes no roll of its own.</summary>
   public static readonly IReadOnlyList<string> RecruitNames = [
      "Halvard", "Ysolde", "Perrin", "Maeve", "Odo", "Sabine"
   ];

   /// <summary>The skill a recruit turns out to have, rolled inside this inclusive range.</summary>
   public static readonly (int Min, int Max) RecruitSkillRange = (8, 15);

   /// <summary>
   ///    The two Areas of the slice. The Quarry is survivable and poor; the Sunken Vault is the only source
   ///    of Iron and Steel and it kills under-equipped Explorers.
   /// </summary>
   public static readonly IReadOnlyList<AreaDefinition> Areas = [
      new(
         AreaId.Quarry,
         "The Quarry",
         DurationPerDepth: TimeSpan.FromMinutes(5),
         // Shallow enough that a starting Explorer in Copper Gear can reach the depth that trickles Iron. The
         // Quarry is the on-ramp: it will arm an Explorer for the Vault, and it will never do more than that.
         BaseThreat: 8,
         ThreatPerDepth: 4,
         // Deliberately below an Artisan's wage at every depth. A player who only ever runs the Quarry is
         // slowly going broke, which is what keeps the safe Area from being a comfortable plateau — see
         // docs/adr/0001-permadeath-is-the-only-gear-sink.md.
         GoldUnit: 1,
         BulkLoot: MaterialType.Copper,
         BulkPerDepth: 2,
         RareLoot: MaterialType.Iron,
         RarePerDepth: 1
      ),
      new(
         AreaId.SunkenVault,
         "The Sunken Vault",
         DurationPerDepth: TimeSpan.FromMinutes(15),
         // Reachable in Copper Gear by the Town's strongest Explorer at roughly seven-in-ten, which is what
         // makes the first Iron a gamble the player takes rather than a milestone they grind to. Depth climbs
         // gently enough that each Gear tier opens the next depth rather than two tiers of grinding.
         BaseThreat: 18,
         ThreatPerDepth: 6,
         // The Town's only real source of gold, and the only one that outruns the Artisan's wage.
         GoldUnit: 40,
         BulkLoot: MaterialType.Iron,
         BulkPerDepth: 3,
         RareLoot: MaterialType.Steel,
         RarePerDepth: 1
      )
   ];

   /// <summary>
   ///    The power range a piece of Gear crafted from this Material can land in. The ranges do not overlap:
   ///    the Material the Master Smith chooses authors the shape of the result, and the roll only gambles the
   ///    magnitude within it.
   /// </summary>
   public static (int Min, int Max) PowerRange(MaterialType material)
   {
      return material switch {
         MaterialType.Copper => (5, 9),
         MaterialType.Iron => (12, 18),
         MaterialType.Steel => (22, 30),
         _ => throw new ArgumentOutOfRangeException(nameof(material), material, "No power range is defined for this Material.")
      };
   }

   /// <summary>The Area with this identity, or null when the catalogue holds no such Area.</summary>
   public static AreaDefinition? Area(AreaId id)
   {
      return Areas.FirstOrDefault(area => area.Id == id);
   }

   /// <summary>
   ///    The chance an Explorer of this skill, carrying Gear of this power, returns from this Area at this
   ///    depth. An even gamble when strength exactly matches threat, sliding to near-certainty or near-doom
   ///    over a margin of about ten either side, and never quite either — the safest Expedition can still
   ///    kill, and the deepest can still be survived.
   /// </summary>
   public static double SurvivalChance(int explorerSkill, int gearPower, AreaDefinition area, int depth)
   {
      var margin = explorerSkill + gearPower - area.ThreatAt(depth);

      return Math.Clamp(0.5 + (0.04 * margin), 0.05, 0.95);
   }
}
