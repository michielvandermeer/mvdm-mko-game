using Underclass.Domain;

namespace Underclass.Domain.Tests;

public sealed class ResolutionTests
{
   /// <summary>A survival roll of 0 is below every possible chance, so the Explorer always comes back.</summary>
   private const double AlwaysSurvives = 0.0;

   /// <summary>A survival roll of 1 is above every possible chance, so the Explorer never comes back.</summary>
   private const double AlwaysDies = 1.0;

   [Fact]
   public void An_Expedition_does_not_resolve_before_its_completion_time()
   {
      var clock = TestTown.Clock();
      var town = TestTown.With(clock, new ScriptedRandom([AlwaysSurvives]), materials: [(MaterialType.Copper, 10)]);
      town.ArmWith(TestTown.Elara, MaterialType.Copper);
      var expedition = town.SendTo(TestTown.Elara, AreaId.Quarry, depth: 1);

      clock.Advance(expedition.CompletesAt - clock.GetUtcNow() - TimeSpan.FromSeconds(1));

      var snapshot = town.Snapshot();
      Assert.Single(snapshot.Expeditions);
      Assert.Empty(snapshot.Reports);
      Assert.Equal(ExplorerStatus.OnExpedition, snapshot.Explorers.Single(e => e.Id == TestTown.Elara).Status);
   }

   [Fact]
   public void An_Expedition_resolves_once_time_is_advanced_past_it()
   {
      var clock = TestTown.Clock();
      var town = TestTown.With(clock, new ScriptedRandom([AlwaysSurvives]), materials: [(MaterialType.Copper, 10)]);
      town.ArmWith(TestTown.Elara, MaterialType.Copper);
      var expedition = town.SendTo(TestTown.Elara, AreaId.Quarry, depth: 1);

      clock.SetUtcNow(expedition.CompletesAt);

      var snapshot = town.Snapshot();
      Assert.Empty(snapshot.Expeditions);
      Assert.Equal(ExplorerStatus.Idle, snapshot.Explorers.Single(e => e.Id == TestTown.Elara).Status);
      Assert.True(snapshot.Reports.Single().Survived);
   }

   [Fact]
   public void An_Expedition_that_came_due_unobserved_resolves_as_of_the_moment_it_was_due()
   {
      var clock = TestTown.Clock();
      var town = TestTown.With(clock, new ScriptedRandom([AlwaysSurvives]), materials: [(MaterialType.Copper, 10)]);
      town.ArmWith(TestTown.Elara, MaterialType.Copper);
      var expedition = town.SendTo(TestTown.Elara, AreaId.Quarry, depth: 1);

      clock.Advance(TimeSpan.FromDays(3));

      Assert.Equal(expedition.CompletesAt, town.Snapshot().Reports.Single().ResolvedAt);
   }

   [Fact]
   public void Resolving_twice_produces_the_same_state_as_resolving_once()
   {
      var clock = TestTown.Clock();
      var town = TestTown.With(clock, new ScriptedRandom([AlwaysSurvives]), materials: [(MaterialType.Copper, 10)]);
      town.ArmWith(TestTown.Elara, MaterialType.Copper);
      var expedition = town.SendTo(TestTown.Elara, AreaId.Quarry, depth: 1);
      clock.SetUtcNow(expedition.CompletesAt);

      var afterFirstSettle = town.Snapshot().Fingerprint();
      town.Settle();
      town.Settle();

      Assert.Equal(afterFirstSettle, town.Snapshot().Fingerprint());
   }

   [Fact]
   public void A_successful_Expedition_returns_the_Areas_Materials_and_gold()
   {
      var clock = TestTown.Clock();
      var town = TestTown.With(clock, new ScriptedRandom([AlwaysSurvives]), gold: 100, materials: [(MaterialType.Iron, 10)]);
      town.ArmWith(TestTown.Bram, MaterialType.Iron);
      var expedition = town.SendTo(TestTown.Bram, AreaId.SunkenVault, depth: 2);
      var area = Catalogue.Area(AreaId.SunkenVault)!;
      var before = town.Snapshot();

      clock.SetUtcNow(expedition.CompletesAt);

      var after = town.Snapshot();
      Assert.Equal(before.Gold + area.GoldAt(2), after.Gold);

      foreach (var (material, amount) in area.LootAt(2))
         Assert.Equal(before.Materials.GetValueOrDefault(material) + amount, after.Materials[material]);

      var report = after.Reports.Single();
      Assert.True(report.Survived);
      Assert.Equal(area.GoldAt(2), report.Gold);
      Assert.Null(report.GearLost);
   }

   [Fact]
   public void A_failed_Expedition_kills_the_Explorer_destroys_their_Gear_and_forfeits_the_Loot()
   {
      var clock = TestTown.Clock();
      var town = TestTown.With(clock, new ScriptedRandom([AlwaysDies]), gold: 100, materials: [(MaterialType.Iron, 10)]);
      var gearId = town.ArmWith(TestTown.Bram, MaterialType.Iron);
      var expedition = town.SendTo(TestTown.Bram, AreaId.SunkenVault, depth: 2);
      var before = town.Snapshot();

      clock.SetUtcNow(expedition.CompletesAt);

      var after = town.Snapshot();
      var bram = after.Explorers.Single(e => e.Id == TestTown.Bram);
      Assert.Equal(ExplorerStatus.Dead, bram.Status);
      Assert.Null(bram.EquippedGear);
      Assert.DoesNotContain(after.Gear, gear => gear.Id == gearId);
      Assert.Equal(before.Gold, after.Gold);
      Assert.Equal(before.Materials.GetValueOrDefault(MaterialType.Iron), after.Materials.GetValueOrDefault(MaterialType.Iron));
      Assert.Equal(before.Materials.GetValueOrDefault(MaterialType.Steel), after.Materials.GetValueOrDefault(MaterialType.Steel));
   }

   [Fact]
   public void A_failed_Expeditions_report_says_what_was_lost()
   {
      var clock = TestTown.Clock();
      var town = TestTown.With(clock, new ScriptedRandom([AlwaysDies]), materials: [(MaterialType.Iron, 10)]);
      var gearId = town.ArmWith(TestTown.Bram, MaterialType.Iron);
      var expedition = town.SendTo(TestTown.Bram, AreaId.SunkenVault, depth: 2);
      var area = Catalogue.Area(AreaId.SunkenVault)!;

      clock.SetUtcNow(expedition.CompletesAt);

      var report = town.Snapshot().Reports.Single();
      Assert.False(report.Survived);
      Assert.Equal("Bram", report.ExplorerName);
      Assert.Equal(gearId, report.GearLost!.Id);
      Assert.Equal(area.GoldAt(2), report.Gold);
      Assert.Equal(area.LootAt(2), report.Materials);
   }

   [Fact]
   public void A_dead_Explorer_stays_in_the_roster_and_can_never_be_used_again()
   {
      var clock = TestTown.Clock();
      var town = TestTown.With(clock, new ScriptedRandom([AlwaysDies]), materials: [(MaterialType.Iron, 20)]);
      town.ArmWith(TestTown.Bram, MaterialType.Iron);
      var expedition = town.SendTo(TestTown.Bram, AreaId.SunkenVault, depth: 2);
      clock.SetUtcNow(expedition.CompletesAt);
      town.Settle();

      var replacementGear = town.CraftGear(GearBaseType.Sword, MaterialType.Iron).Value;
      var equip = town.EquipGear(TestTown.Bram, replacementGear);
      var dispatch = town.Dispatch(TestTown.Bram, AreaId.Quarry, depth: 1);
      var preview = town.PreviewExpedition(TestTown.Bram, AreaId.Quarry, depth: 1);

      Assert.Contains(town.Snapshot().Explorers, e => e.Id == TestTown.Bram && e.Status == ExplorerStatus.Dead);
      Assert.Equal(RejectionReason.ExplorerDead, equip.Reason);
      Assert.Equal(RejectionReason.ExplorerDead, dispatch.Reason);
      Assert.Equal(RejectionReason.ExplorerDead, preview.Reason);
   }

   [Fact]
   public void Several_Expeditions_that_came_due_together_all_resolve()
   {
      var clock = TestTown.Clock();
      var town = TestTown.With(clock, new ScriptedRandom([AlwaysSurvives, AlwaysSurvives]), materials: [(MaterialType.Copper, 20)]);
      town.ArmWith(TestTown.Elara, MaterialType.Copper);
      town.ArmWith(TestTown.Bram, MaterialType.Copper);
      town.SendTo(TestTown.Elara, AreaId.Quarry, depth: 1);
      town.SendTo(TestTown.Bram, AreaId.Quarry, depth: 2);

      clock.Advance(TimeSpan.FromHours(1));

      var snapshot = town.Snapshot();
      Assert.Empty(snapshot.Expeditions);
      Assert.Equal(2, snapshot.Reports.Count);
      Assert.All(snapshot.Explorers, explorer => Assert.Equal(ExplorerStatus.Idle, explorer.Status));
   }

   [Fact]
   public void Reports_are_read_newest_first()
   {
      var clock = TestTown.Clock();
      var town = TestTown.With(clock, new ScriptedRandom([AlwaysSurvives, AlwaysSurvives]), materials: [(MaterialType.Copper, 20)]);
      town.ArmWith(TestTown.Elara, MaterialType.Copper);
      town.ArmWith(TestTown.Bram, MaterialType.Copper);
      var first = town.SendTo(TestTown.Elara, AreaId.Quarry, depth: 1);
      var second = town.SendTo(TestTown.Bram, AreaId.Quarry, depth: 3);

      clock.Advance(TimeSpan.FromHours(1));

      var reports = town.Snapshot().Reports;
      Assert.Equal(second.Id, reports.First().ExpeditionId);
      Assert.Equal(first.Id, reports.Last().ExpeditionId);
   }

   [Fact]
   public void Gear_that_came_back_can_be_sent_out_again()
   {
      var clock = TestTown.Clock();
      var town = TestTown.With(clock, new ScriptedRandom([AlwaysSurvives]), materials: [(MaterialType.Copper, 10)]);
      var gearId = town.ArmWith(TestTown.Elara, MaterialType.Copper);
      var expedition = town.SendTo(TestTown.Elara, AreaId.Quarry, depth: 1);
      clock.SetUtcNow(expedition.CompletesAt);
      town.Settle();

      var again = town.Dispatch(TestTown.Elara, AreaId.Quarry, depth: 1);

      Assert.True(again.Ok, $"dispatch was rejected: {again.Reason}");
      Assert.Equal(gearId, again.Value.GearId);
   }
}
