namespace Underclass.Domain;

/// <summary>
///    Everything about a Town that has to survive being switched off. This is the shape the database stores
///    and the shape <see cref="Town" /> mutates; it holds no rules of its own.
/// </summary>
public sealed class TownState
{
   /// <summary>The Town's gold. Never negative: wages stop draining at zero rather than accruing debt.</summary>
   public required long Gold { get; set; }

   /// <summary>How many Artisans the Town employs, and therefore how fast it accrues and drains.</summary>
   public required int ArtisanCount { get; set; }

   /// <summary>How many living Explorers the Town can house.</summary>
   public required int ExplorerCap { get; set; }

   /// <summary>
   ///    How far continuous accrual has been settled. Advanced in whole <see cref="Catalogue.AccrualInterval" />
   ///    steps, so the unsettled remainder is carried to the next read rather than dropped.
   /// </summary>
   public required DateTimeOffset LastSettledAt { get; set; }

   /// <summary>What the Town has on hand to craft with. Absent means none.</summary>
   public required Dictionary<MaterialType, int> Materials { get; init; }

   /// <summary>Every piece of Gear the Town holds, equipped or not.</summary>
   public required List<GearState> Gear { get; init; }

   /// <summary>The roster, living and dead.</summary>
   public required List<ExplorerState> Explorers { get; init; }

   /// <summary>Expeditions currently out. An Expedition leaves this list when it resolves.</summary>
   public required List<ExpeditionState> Expeditions { get; init; }

   /// <summary>Reports of Expeditions that have resolved, oldest first.</summary>
   public required List<ExpeditionReport> Reports { get; init; }
}
