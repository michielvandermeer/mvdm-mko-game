using Underclass.Contracts;

namespace Underclass.Api.Tests;

/// <summary>
///    The loop, driven the way the client drives it. These tests are not about whether the numbers are right —
///    the domain suite owns that — but about whether a decision made over HTTP reaches Postgres, comes back
///    settled, and reads the same on the way out as it did on the way in.
/// </summary>
[Collection(UnderclassApiCollection.Name)]
public sealed class LoopOverHttpTests(UnderclassApi api)
{
   [Fact]
   public async Task Craft_equip_dispatch_and_return_all_the_way_through_the_stack()
   {
      var host = await api.StartAsync();

      var start = await host.ReadTownAsync();
      var explorer = start.Explorers.First(candidate => candidate.IsIdle());

      var crafted = await host.CraftAsync("Sword", "Copper");
      Assert.Equal("Copper", crafted.Gear.Material);
      Assert.True(crafted.Gear.Power > 0);
      Assert.Contains(crafted.Gear.Id, crafted.Town.Gear.Select(gear => gear.Id));

      var equipped = await host.EquipAsync(explorer.Id, crafted.Gear.Id);
      Assert.Equal(crafted.Gear.Id, equipped.Explorers.Single(candidate => candidate.Id == explorer.Id).EquippedGearId);

      var dispatched = await host.DispatchAsync(explorer.Id, "Quarry", depth: 2);
      Assert.Equal(ExplorerStatuses.OnExpedition, dispatched.Town.Explorers.Single(candidate => candidate.Id == explorer.Id).Status);
      Assert.Equal(dispatched.Expedition.CompletesAt, Assert.Single(dispatched.Town.Expeditions).CompletesAt);

      api.AdvanceTo(dispatched.Expedition.CompletesAt);

      var returned = await host.ReadTownAsync();

      Assert.Empty(returned.Expeditions);
      Assert.Equal(ExplorerStatuses.Idle, returned.Explorers.Single(candidate => candidate.Id == explorer.Id).Status);

      var report = Assert.Single(returned.Reports);
      Assert.True(report.Survived);
      Assert.Equal(dispatched.Expedition.Id, report.ExpeditionId);
      Assert.Equal(explorer.Name, report.ExplorerName);
      Assert.Null(report.GearLost);

      // The Explorer came back with what they were carrying. Which Materials, and how much of each, is the
      // Area catalogue's business; that they landed in the Town's inventory is this test's.
      Assert.NotEmpty(report.Materials);

      foreach (var (material, amount) in report.Materials)
         Assert.True(returned.Materials.GetValueOrDefault(material) >= amount, $"the Town is holding no {material} the Expedition returned with");
   }

   /// <summary>
   ///    Story 17: the estimate the player agrees to has to be the one the server acts on. Preview and dispatch
   ///    are separate requests through separate code paths, so this is the only place that can catch them
   ///    disagreeing.
   /// </summary>
   [Fact]
   public async Task The_estimate_shown_before_dispatch_is_the_one_the_Expedition_is_resolved_against()
   {
      var host = await api.StartAsync();

      var explorer = (await host.ReadTownAsync()).Explorers.First(candidate => candidate.IsIdle());
      var crafted = await host.CraftAsync("Sword", "Copper");
      await host.EquipAsync(explorer.Id, crafted.Gear.Id);

      var preview = await host.PreviewAsync(explorer.Id, "SunkenVault", depth: 1);

      // Nothing was created by asking.
      Assert.Empty((await host.ReadTownAsync()).Expeditions);

      var dispatched = await host.DispatchAsync(explorer.Id, "SunkenVault", depth: 1);

      Assert.Equal(preview.SurvivalChance, dispatched.Expedition.SurvivalChance);
      Assert.Equal(preview.Duration, dispatched.Expedition.CompletesAt - dispatched.Expedition.StartedAt);

      api.AdvanceTo(dispatched.Expedition.CompletesAt);

      var report = Assert.Single((await host.ReadTownAsync()).Reports);

      Assert.Equal(preview.Gold, report.Gold);
      Assert.Equal(preview.Loot.OrderBy(entry => entry.Key), report.Materials.OrderBy(entry => entry.Key));
   }

   /// <summary>
   ///    Stories 29 to 32, and the one outcome the slice exists to prove is real: a death that costs the player
   ///    the Gear. Read back from a second request, so the loss is what Postgres holds rather than what one
   ///    response happened to say.
   /// </summary>
   [Fact]
   public async Task A_death_destroys_the_Gear_and_leaves_the_Explorer_in_the_roster()
   {
      var host = await api.StartAsync();

      api.Random.ExplorersSurvive = false;

      var explorer = (await host.ReadTownAsync()).Explorers.First(candidate => candidate.IsIdle());
      var crafted = await host.CraftAsync("Sword", "Copper");
      await host.EquipAsync(explorer.Id, crafted.Gear.Id);

      var dispatched = await host.DispatchAsync(explorer.Id, "SunkenVault", depth: 3);

      api.AdvanceTo(dispatched.Expedition.CompletesAt);
      await host.ReadTownAsync();

      var afterwards = await host.ReadTownAsync();

      var dead = afterwards.Explorers.Single(candidate => candidate.Id == explorer.Id);
      Assert.Equal(ExplorerStatuses.Dead, dead.Status);
      Assert.Null(dead.EquippedGearId);
      Assert.Equal(explorer.Name, dead.Name);

      Assert.DoesNotContain(crafted.Gear.Id, afterwards.Gear.Select(gear => gear.Id));

      var report = Assert.Single(afterwards.Reports);
      Assert.False(report.Survived);
      Assert.NotNull(report.GearLost);
      Assert.Equal(crafted.Gear.Id, report.GearLost.Id);
      Assert.Equal(crafted.Gear.Power, report.GearLost.Power);

      // Permanently ineligible, not merely unavailable.
      var refusal = await host.ExpectRefusalAsync("/api/expeditions", new DispatchRequest(explorer.Id, "Quarry", Depth: 1));
      Assert.Equal("ExplorerDead", refusal.Reason);
   }

   /// <summary>
   ///    The client sets the risk dial without knowing any of the rules behind it, so the catalogue has to arrive
   ///    already priced per depth.
   /// </summary>
   [Fact]
   public async Task The_Area_catalogue_is_priced_per_depth_for_a_client_that_holds_no_rules()
   {
      var host = await api.StartAsync();

      var areas = await host.ReadAreasAsync();

      Assert.Equal(new[] { "Quarry", "SunkenVault" }, areas.Select(area => area.Id).ToArray());

      foreach (var area in areas)
      {
         Assert.False(string.IsNullOrWhiteSpace(area.Name));
         Assert.NotEmpty(area.Depths);
         Assert.Equal(Enumerable.Range(1, area.Depths.Count), area.Depths.Select(depth => depth.Depth));

         foreach (var depth in area.Depths)
         {
            Assert.True(depth.Duration > TimeSpan.Zero);
            Assert.True(depth.Threat > 0);
            Assert.NotEmpty(depth.Loot);
         }
      }
   }

   /// <summary>
   ///    Story 8: the Material has to be a visible decision, which means the Workshop needs the cost and the range
   ///    each Material implies — and has to be given them, since a client that derived them would be holding rules.
   /// </summary>
   [Fact]
   public async Task Crafting_options_carry_the_cost_and_the_range_each_Material_implies()
   {
      var host = await api.StartAsync();

      var options = await host.ReadCraftingOptionsAsync();

      Assert.Equal(new[] { "Sword" }, options.BaseTypes.ToArray());
      Assert.Equal(new[] { "Copper", "Iron", "Steel" }, options.Materials.Select(material => material.Material).ToArray());

      foreach (var material in options.Materials)
      {
         Assert.True(material.Cost > 0);
         Assert.True(material.MinPower <= material.MaxPower);
      }

      // A better Material has to be strictly better, or the choice is decoration.
      var ranges = options.Materials.Select(material => (material.MinPower, material.MaxPower)).ToArray();

      for (var tier = 1; tier < ranges.Length; tier++)
         Assert.True(ranges[tier].MinPower > ranges[tier - 1].MaxPower, $"{options.Materials[tier].Material} overlaps the tier below it");

      // And what the server actually rolls has to land inside the range it published.
      var copper = options.Materials.Single(material => material.Material == "Copper");
      var crafted = await host.CraftAsync("Sword", "Copper");

      Assert.InRange(crafted.Gear.Power, copper.MinPower, copper.MaxPower);
   }

   /// <summary>Stories 33 and 34, over HTTP: Salvage puts Material back, and less than a craft took out.</summary>
   [Fact]
   public async Task Salvaging_Gear_returns_less_Material_than_crafting_it_consumed()
   {
      var host = await api.StartAsync();

      var before = await host.ReadTownAsync();
      var crafted = await host.CraftAsync("Sword", "Copper");
      var afterCraft = crafted.Town.Materials["Copper"];

      var afterSalvage = (await host.SalvageAsync(crafted.Gear.Id)).Materials["Copper"];

      Assert.True(afterCraft < before.Materials["Copper"], "crafting did not consume any Copper");
      Assert.True(afterSalvage > afterCraft, "Salvage returned no Copper at all");
      Assert.True(afterSalvage < before.Materials["Copper"], "Salvage returned everything the craft consumed");
   }
}
