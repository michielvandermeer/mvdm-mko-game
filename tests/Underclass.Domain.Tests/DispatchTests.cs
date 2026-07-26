using Underclass.Domain;

namespace Underclass.Domain.Tests;

public sealed class DispatchTests
{
   [Fact]
   public void Dispatch_makes_the_Explorer_unavailable_and_records_a_completion_time_from_the_Area()
   {
      var clock = TestTown.Clock();
      var town = TestTown.With(clock, new ScriptedRandom(), materials: [(MaterialType.Copper, 10)]);
      town.ArmWith(TestTown.Elara, MaterialType.Copper);

      var expedition = town.SendTo(TestTown.Elara, AreaId.Quarry, depth: 1);

      var snapshot = town.Snapshot();
      Assert.Equal(ExplorerStatus.OnExpedition, snapshot.Explorers.Single(e => e.Id == TestTown.Elara).Status);
      Assert.Equal(TestTown.Start + Catalogue.Area(AreaId.Quarry)!.DurationAt(1), expedition.CompletesAt);
      Assert.Equal(expedition.Id, snapshot.Expeditions.Single().Id);
   }

   [Fact]
   public void A_deeper_Expedition_takes_longer_than_a_shallow_one()
   {
      var clock = TestTown.Clock();
      var town = TestTown.With(clock, new ScriptedRandom(), materials: [(MaterialType.Copper, 20)]);
      town.ArmWith(TestTown.Elara, MaterialType.Copper);
      town.ArmWith(TestTown.Bram, MaterialType.Copper);

      var shallow = town.SendTo(TestTown.Elara, AreaId.Quarry, depth: 1);
      var deep = town.SendTo(TestTown.Bram, AreaId.Quarry, depth: 3);

      Assert.True(deep.CompletesAt > shallow.CompletesAt);
   }

   [Fact]
   public void An_Explorer_who_is_already_out_cannot_be_sent_again()
   {
      var clock = TestTown.Clock();
      var town = TestTown.With(clock, new ScriptedRandom(), materials: [(MaterialType.Copper, 10)]);
      town.ArmWith(TestTown.Elara, MaterialType.Copper);
      town.SendTo(TestTown.Elara, AreaId.Quarry, depth: 1);

      var result = town.Dispatch(TestTown.Elara, AreaId.Quarry, depth: 1);

      Assert.False(result.Ok);
      Assert.Equal(RejectionReason.ExplorerBusy, result.Reason);
      Assert.Single(town.Snapshot().Expeditions);
   }

   [Fact]
   public void An_Explorer_carrying_no_Gear_cannot_be_sent()
   {
      var clock = TestTown.Clock();
      var town = TestTown.With(clock, new ScriptedRandom(), materials: [(MaterialType.Copper, 10)]);

      var result = town.Dispatch(TestTown.Elara, AreaId.Quarry, depth: 1);

      Assert.False(result.Ok);
      Assert.Equal(RejectionReason.NoGearEquipped, result.Reason);
      Assert.Empty(town.Snapshot().Expeditions);
   }

   [Fact]
   public void An_Explorer_who_is_out_cannot_have_their_Gear_swapped()
   {
      var clock = TestTown.Clock();
      var town = TestTown.With(clock, new ScriptedRandom(), materials: [(MaterialType.Copper, 20)]);
      town.ArmWith(TestTown.Elara, MaterialType.Copper);
      town.SendTo(TestTown.Elara, AreaId.Quarry, depth: 1);
      var spare = town.CraftGear(GearBaseType.Sword, MaterialType.Copper).Value;

      var result = town.EquipGear(TestTown.Elara, spare);

      Assert.False(result.Ok);
      Assert.Equal(RejectionReason.ExplorerBusy, result.Reason);
   }

   [Theory]
   [InlineData(0)]
   [InlineData(-1)]
   [InlineData(Catalogue.MaxDepth + 1)]
   public void A_depth_the_Area_does_not_support_is_rejected(int depth)
   {
      var clock = TestTown.Clock();
      var town = TestTown.With(clock, new ScriptedRandom(), materials: [(MaterialType.Copper, 10)]);
      town.ArmWith(TestTown.Elara, MaterialType.Copper);

      var result = town.Dispatch(TestTown.Elara, AreaId.Quarry, depth);

      Assert.False(result.Ok);
      Assert.Equal(RejectionReason.InvalidDepth, result.Reason);
   }

   [Fact]
   public void The_estimate_shown_before_confirming_is_the_chance_the_Expedition_is_rolled_against()
   {
      var clock = TestTown.Clock();
      var town = TestTown.With(clock, new ScriptedRandom(), materials: [(MaterialType.Iron, 10)]);
      town.ArmWith(TestTown.Elara, MaterialType.Iron);

      var preview = town.PreviewExpedition(TestTown.Elara, AreaId.SunkenVault, depth: 2);
      var expedition = town.SendTo(TestTown.Elara, AreaId.SunkenVault, depth: 2);

      Assert.True(preview.Ok);
      Assert.Equal(preview.Value.SurvivalChance, expedition.SurvivalChance);
      Assert.Equal(preview.Value.Duration, expedition.CompletesAt - expedition.StartedAt);
   }

   [Fact]
   public void A_preview_creates_nothing()
   {
      var clock = TestTown.Clock();
      var town = TestTown.With(clock, new ScriptedRandom(), materials: [(MaterialType.Iron, 10)]);
      town.ArmWith(TestTown.Elara, MaterialType.Iron);

      town.PreviewExpedition(TestTown.Elara, AreaId.SunkenVault, depth: 3);

      var snapshot = town.Snapshot();
      Assert.Empty(snapshot.Expeditions);
      Assert.Equal(ExplorerStatus.Idle, snapshot.Explorers.Single(e => e.Id == TestTown.Elara).Status);
   }

   [Fact]
   public void Pushing_deeper_lowers_the_chance_of_returning_and_raises_the_reward()
   {
      var clock = TestTown.Clock();
      var town = TestTown.With(clock, new ScriptedRandom(), materials: [(MaterialType.Iron, 10)]);
      town.ArmWith(TestTown.Elara, MaterialType.Iron);

      var shallow = town.PreviewExpedition(TestTown.Elara, AreaId.SunkenVault, depth: 1).Value;
      var deep = town.PreviewExpedition(TestTown.Elara, AreaId.SunkenVault, depth: 3).Value;

      Assert.True(deep.SurvivalChance < shallow.SurvivalChance, "greater depth must be more dangerous");
      Assert.True(deep.Gold > shallow.Gold, "greater depth must pay more gold");
      Assert.True(TotalLoot(deep) > TotalLoot(shallow), "greater depth must return more Materials");
      Assert.Contains(MaterialType.Steel, deep.Loot.Keys);
   }

   [Fact]
   public void The_harder_Area_is_more_dangerous_and_returns_a_better_Material()
   {
      var clock = TestTown.Clock();
      var town = TestTown.With(clock, new ScriptedRandom(), materials: [(MaterialType.Iron, 10)]);
      town.ArmWith(TestTown.Elara, MaterialType.Iron);

      var safe = town.PreviewExpedition(TestTown.Elara, AreaId.Quarry, depth: 1).Value;
      var dangerous = town.PreviewExpedition(TestTown.Elara, AreaId.SunkenVault, depth: 1).Value;

      Assert.True(dangerous.SurvivalChance < safe.SurvivalChance);
      Assert.Equal(MaterialType.Copper, safe.Loot.Keys.Max());
      Assert.Equal(MaterialType.Iron, dangerous.Loot.Keys.Max());
   }

   [Fact]
   public void Better_Gear_makes_the_same_Expedition_more_survivable()
   {
      var clock = TestTown.Clock();
      var town = TestTown.With(clock, new ScriptedRandom(), materials: [(MaterialType.Copper, 10), (MaterialType.Steel, 10)]);
      town.ArmWith(TestTown.Elara, MaterialType.Copper);
      town.ArmWith(TestTown.Bram, MaterialType.Steel);

      var poorlyArmed = town.PreviewExpedition(TestTown.Elara, AreaId.SunkenVault, depth: 2).Value;
      var wellArmed = town.PreviewExpedition(TestTown.Bram, AreaId.SunkenVault, depth: 2).Value;

      Assert.True(wellArmed.SurvivalChance > poorlyArmed.SurvivalChance);
   }

   private static int TotalLoot(ExpeditionPreview preview)
   {
      return preview.Loot.Values.Sum();
   }
}
