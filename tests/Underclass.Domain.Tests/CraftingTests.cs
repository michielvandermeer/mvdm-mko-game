using Underclass.Domain;

namespace Underclass.Domain.Tests;

public sealed class CraftingTests
{
   [Fact]
   public void Crafting_consumes_the_materials_it_used()
   {
      var clock = TestTown.Clock();
      var town = TestTown.With(clock, new ScriptedRandom(), materials: [(MaterialType.Copper, 10)]);

      var result = town.CraftGear(GearBaseType.Sword, MaterialType.Copper);

      Assert.True(result.Ok);
      Assert.Equal(10 - Catalogue.CraftCost, town.Snapshot().Materials[MaterialType.Copper]);
   }

   [Fact]
   public void Crafted_Gear_lands_within_the_range_its_Material_implies()
   {
      var clock = TestTown.Clock();
      var town = TestTown.With(clock, new ScriptedRandom(), materials: [(MaterialType.Iron, 10)]);

      var gearId = town.CraftGear(GearBaseType.Sword, MaterialType.Iron).Value;

      var (min, max) = Catalogue.PowerRange(MaterialType.Iron);
      var gear = town.Snapshot().Gear.Single(g => g.Id == gearId);
      Assert.InRange(gear.Power, min, max);
      Assert.Equal(MaterialType.Iron, gear.Material);
   }

   [Fact]
   public void A_better_Material_cannot_produce_worse_Gear_than_a_lesser_one_at_its_best()
   {
      // The deterministic half of crafting: Material choice sets the range, so the tiers must not
      // overlap — otherwise "author the shape" is a lie and only the roll matters.
      var (copperMin, copperMax) = Catalogue.PowerRange(MaterialType.Copper);
      var (ironMin, ironMax) = Catalogue.PowerRange(MaterialType.Iron);
      var (steelMin, _) = Catalogue.PowerRange(MaterialType.Steel);

      Assert.True(ironMin > copperMax);
      Assert.True(steelMin > ironMax);
   }

   [Fact]
   public void Crafting_without_the_Materials_is_rejected_and_changes_nothing()
   {
      var clock = TestTown.Clock();
      var town = TestTown.With(clock, new ScriptedRandom(), materials: [(MaterialType.Copper, 1)]);

      var result = town.CraftGear(GearBaseType.Sword, MaterialType.Copper);

      Assert.False(result.Ok);
      Assert.Equal(RejectionReason.InsufficientMaterials, result.Reason);

      var snapshot = town.Snapshot();
      Assert.Equal(1, snapshot.Materials[MaterialType.Copper]);
      Assert.Empty(snapshot.Gear);
   }

   [Fact]
   public void The_quality_roll_varies_the_result_from_identical_inputs()
   {
      var clock = TestTown.Clock();
      var town = TestTown.With(
         clock,
         new ScriptedRandom(ints: [Catalogue.PowerRange(MaterialType.Iron).Min, Catalogue.PowerRange(MaterialType.Iron).Max]),
         materials: [(MaterialType.Iron, 20)]);

      var weak = town.CraftGear(GearBaseType.Sword, MaterialType.Iron).Value;
      var strong = town.CraftGear(GearBaseType.Sword, MaterialType.Iron).Value;

      var gear = town.Snapshot().Gear;
      Assert.True(gear.Single(g => g.Id == strong).Power > gear.Single(g => g.Id == weak).Power);
   }

   [Fact]
   public void Salvage_returns_Materials_but_strictly_less_than_crafting_consumed()
   {
      var clock = TestTown.Clock();
      var town = TestTown.With(clock, new ScriptedRandom(), materials: [(MaterialType.Iron, 10)]);
      var gearId = town.CraftGear(GearBaseType.Sword, MaterialType.Iron).Value;

      var afterCraft = town.Snapshot().Materials[MaterialType.Iron];
      var result = town.SalvageGear(gearId);
      var afterSalvage = town.Snapshot().Materials[MaterialType.Iron];

      Assert.True(result.Ok);
      Assert.True(afterSalvage > afterCraft, "Salvage must return something");
      Assert.True(afterSalvage < 10, "Salvage must not fully refund the craft, or it is a free loop");
      Assert.DoesNotContain(town.Snapshot().Gear, g => g.Id == gearId);
   }

   [Fact]
   public void Gear_equipped_to_an_Explorer_cannot_be_salvaged_out_from_under_them()
   {
      var clock = TestTown.Clock();
      var town = TestTown.With(clock, new ScriptedRandom(), materials: [(MaterialType.Iron, 10)]);
      var gearId = town.ArmWith(TestTown.Elara, MaterialType.Iron);

      var result = town.SalvageGear(gearId);

      Assert.False(result.Ok);
      Assert.Equal(RejectionReason.GearInUse, result.Reason);
   }
}
