using Underclass.Domain;

namespace Underclass.Domain.Tests;

public sealed class AccrualTests
{
   [Fact]
   public void The_Artisan_produces_Material_and_draws_wages_over_elapsed_time()
   {
      var clock = TestTown.Clock();
      var town = TestTown.With(clock, new ScriptedRandom(), gold: 1_000, artisans: 1);

      clock.Advance(Catalogue.AccrualInterval * 4);

      var snapshot = town.Snapshot();
      Assert.Equal(4 * Catalogue.ArtisanOutputPerInterval, snapshot.Materials[Catalogue.ArtisanOutput]);
      Assert.Equal(1_000 - (4 * Catalogue.ArtisanWagePerInterval), snapshot.Gold);
   }

   [Fact]
   public void Twice_the_elapsed_time_accrues_twice_as_much()
   {
      var clock = TestTown.Clock();
      var town = TestTown.With(clock, new ScriptedRandom(), gold: 1_000, artisans: 1);

      clock.Advance(Catalogue.AccrualInterval * 2);
      var halfway = town.Snapshot();
      clock.Advance(Catalogue.AccrualInterval * 2);
      var full = town.Snapshot();

      Assert.Equal(2 * halfway.Materials[Catalogue.ArtisanOutput], full.Materials[Catalogue.ArtisanOutput]);
      Assert.Equal(2 * (1_000 - halfway.Gold), 1_000 - full.Gold);
   }

   [Fact]
   public void Settling_twice_in_a_row_changes_nothing()
   {
      var clock = TestTown.Clock();
      var town = TestTown.With(clock, new ScriptedRandom(), gold: 1_000, artisans: 1);
      clock.Advance(Catalogue.AccrualInterval * 3);

      var settled = town.Snapshot().Fingerprint();
      town.Settle();
      town.Settle();

      Assert.Equal(settled, town.Snapshot().Fingerprint());
   }

   [Fact]
   public void Time_short_of_a_whole_interval_is_carried_rather_than_dropped()
   {
      var clock = TestTown.Clock();
      var town = TestTown.With(clock, new ScriptedRandom(), gold: 1_000, artisans: 1);

      // One interval and a bit, read; then the rest of a second interval, read again. The leftover from the
      // first read has to survive it, or a player who checks in often would earn less than one who does not.
      clock.Advance(Catalogue.AccrualInterval + TimeSpan.FromMinutes(2));
      var afterFirstRead = town.Snapshot();
      clock.Advance(Catalogue.AccrualInterval - TimeSpan.FromMinutes(2));
      var afterSecondRead = town.Snapshot();

      Assert.Equal(1, afterFirstRead.Materials[Catalogue.ArtisanOutput]);
      Assert.Equal(2, afterSecondRead.Materials[Catalogue.ArtisanOutput]);
   }

   [Fact]
   public void A_Town_with_no_Artisan_neither_produces_nor_pays()
   {
      var clock = TestTown.Clock();
      var town = TestTown.With(clock, new ScriptedRandom(), gold: 1_000, artisans: 0);

      clock.Advance(TimeSpan.FromDays(1));

      var snapshot = town.Snapshot();
      Assert.Equal(1_000, snapshot.Gold);
      Assert.Equal(0, snapshot.Materials.GetValueOrDefault(Catalogue.ArtisanOutput));
   }

   [Fact]
   public void Wages_stop_at_nothing_rather_than_running_into_debt()
   {
      var clock = TestTown.Clock();
      var town = TestTown.With(clock, new ScriptedRandom(), gold: Catalogue.ArtisanWagePerInterval, artisans: 1);

      clock.Advance(Catalogue.AccrualInterval * 10);

      Assert.Equal(0, town.Snapshot().Gold);
   }

   [Fact]
   public void Accrual_keeps_running_while_an_Expedition_is_out()
   {
      var clock = TestTown.Clock();
      var town = TestTown.With(clock, new ScriptedRandom([0.0]), gold: 1_000, artisans: 1, materials: [(MaterialType.Copper, 10)]);
      town.ArmWith(TestTown.Elara, MaterialType.Copper);
      var expedition = town.SendTo(TestTown.Elara, AreaId.Quarry, depth: 1);
      var copperAfterCrafting = town.Snapshot().Materials[MaterialType.Copper];

      clock.SetUtcNow(expedition.CompletesAt);

      // Five minutes out: one interval of Artisan Copper, plus the Quarry's Loot.
      var expectedLoot = Catalogue.Area(AreaId.Quarry)!.LootAt(1)[MaterialType.Copper];
      Assert.Equal(copperAfterCrafting + Catalogue.ArtisanOutputPerInterval + expectedLoot, town.Snapshot().Materials[MaterialType.Copper]);
   }
}
