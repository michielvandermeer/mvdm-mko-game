using Underclass.Contracts;

namespace Underclass.Api.Tests;

/// <summary>
///    Stories 11 and 19: a refused command has to come back as a reason the client can read out loud. The domain
///    suite proves the rules refuse; this proves the refusal survives the trip through HTTP as a 400 with a code
///    rather than arriving as a 500 the player has to guess at.
/// </summary>
[Collection(UnderclassApiCollection.Name)]
public sealed class RejectionTests(UnderclassApi api)
{
   [Fact]
   public async Task Crafting_without_the_Materials_is_refused_with_a_reason()
   {
      var host = await api.StartAsync();

      // Copper is the only Material a new Town holds, so a Steel Sword is out of reach on the first day.
      var refusal = await host.ExpectRefusalAsync("/api/crafting/craft", new CraftRequest("Sword", "Steel"));

      Assert.Equal("InsufficientMaterials", refusal.Reason);

      // Story 11 also asks that the failure changed nothing.
      var town = await host.ReadTownAsync();
      Assert.Empty(town.Gear);
      Assert.Equal(0, town.Materials.GetValueOrDefault("Steel"));
   }

   [Fact]
   public async Task Dispatching_an_Explorer_carrying_no_Gear_is_refused_with_a_reason()
   {
      var host = await api.StartAsync();

      var explorer = (await host.ReadTownAsync()).Explorers.First(candidate => candidate.IsIdle());

      var refusal = await host.ExpectRefusalAsync("/api/expeditions", new DispatchRequest(explorer.Id, "Quarry", Depth: 1));

      Assert.Equal("NoGearEquipped", refusal.Reason);
      Assert.Empty((await host.ReadTownAsync()).Expeditions);
   }

   [Fact]
   public async Task Dispatching_an_Explorer_who_is_already_out_is_refused_with_a_reason()
   {
      var host = await api.StartAsync();

      var explorer = (await host.ReadTownAsync()).Explorers.First(candidate => candidate.IsIdle());
      var crafted = await host.CraftAsync("Sword", "Copper");
      await host.EquipAsync(explorer.Id, crafted.Gear.Id);

      await host.DispatchAsync(explorer.Id, "Quarry", depth: 1);

      var refusal = await host.ExpectRefusalAsync("/api/expeditions", new DispatchRequest(explorer.Id, "Quarry", Depth: 1));

      Assert.Equal("ExplorerBusy", refusal.Reason);
      Assert.Single((await host.ReadTownAsync()).Expeditions);
   }

   [Fact]
   public async Task Salvaging_Gear_an_Explorer_is_carrying_is_refused_with_a_reason()
   {
      var host = await api.StartAsync();

      var explorer = (await host.ReadTownAsync()).Explorers.First(candidate => candidate.IsIdle());
      var crafted = await host.CraftAsync("Sword", "Copper");
      await host.EquipAsync(explorer.Id, crafted.Gear.Id);

      var refusal = await host.ExpectRefusalAsync($"/api/gear/{crafted.Gear.Id}/salvage", content: null);

      Assert.Equal("GearInUse", refusal.Reason);
      Assert.Single((await host.ReadTownAsync()).Gear);
   }

   [Fact]
   public async Task A_depth_the_Area_does_not_support_is_refused_with_a_reason()
   {
      var host = await api.StartAsync();

      var explorer = (await host.ReadTownAsync()).Explorers.First(candidate => candidate.IsIdle());
      var crafted = await host.CraftAsync("Sword", "Copper");
      await host.EquipAsync(explorer.Id, crafted.Gear.Id);

      var refusal = await host.ExpectRefusalAsync("/api/expeditions", new DispatchRequest(explorer.Id, "Quarry", Depth: 99));

      Assert.Equal("InvalidDepth", refusal.Reason);
   }

   /// <summary>
   ///    A name the game has never heard of has to be refused at the edge. Parsing it into the domain as some
   ///    undefined enum value is how a request that is merely wrong turns into behaviour nobody chose.
   /// </summary>
   [Fact]
   public async Task A_Material_the_game_does_not_have_is_refused_as_a_malformed_request()
   {
      var host = await api.StartAsync();

      var refusal = await host.ExpectRefusalAsync("/api/crafting/craft", new CraftRequest("Sword", "Mithril"));

      Assert.Equal("MalformedRequest", refusal.Reason);
      Assert.Contains("Mithril", refusal.Message);
   }

   /// <summary>Enums travel as names, so a number that happens to be a valid ordinal must not be accepted either.</summary>
   [Fact]
   public async Task A_Material_given_as_a_number_is_refused_as_a_malformed_request()
   {
      var host = await api.StartAsync();

      var refusal = await host.ExpectRefusalAsync("/api/crafting/craft", new CraftRequest("Sword", "0"));

      Assert.Equal("MalformedRequest", refusal.Reason);
   }

   [Fact]
   public async Task An_Explorer_the_Town_has_never_had_is_refused_with_a_reason()
   {
      var host = await api.StartAsync();

      var refusal = await host.ExpectRefusalAsync("/api/expeditions", new DispatchRequest(Guid.NewGuid(), "Quarry", Depth: 1));

      Assert.Equal("ExplorerNotFound", refusal.Reason);
   }

   /// <summary>
   ///    Recruiting is the Town's only gold sink besides wages, and it is the one command that can be refused for
   ///    running out of room rather than out of money.
   /// </summary>
   [Fact]
   public async Task Recruiting_past_the_Town_s_capacity_is_refused_with_a_reason()
   {
      var host = await api.StartAsync();

      var town = await host.ReadTownAsync();

      while (town.Explorers.Count(explorer => explorer.IsAlive()) < town.ExplorerCap)
         town = await host.RecruitAsync();

      var refusal = await host.ExpectRefusalAsync("/api/explorers/recruit", content: null);

      Assert.Equal("ExplorerCapReached", refusal.Reason);
   }
}
