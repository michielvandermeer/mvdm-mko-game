using Underclass.Contracts;

namespace Underclass.Api.Tests;

/// <summary>
///    What has to hold when the server is not running. Stories 36 and 37 are claims about durability, and they
///    are the two claims a domain test cannot make: there is no database and no host in that suite to take away.
/// </summary>
[Collection(UnderclassApiCollection.Name)]
public sealed class DurabilityTests(UnderclassApi api)
{
   /// <summary>Story 36: the Town is in Postgres, not in the process that happened to write it.</summary>
   [Fact]
   public async Task A_Town_survives_the_API_being_restarted()
   {
      var before = await api.StartAsync();

      var explorerId = (await before.ReadTownAsync()).Explorers.First(candidate => candidate.IsIdle()).Id;
      var crafted = (await before.CraftAsync("Sword", "Copper")).Gear;

      await before.EquipAsync(explorerId, crafted.Id);

      var dispatched = (await before.DispatchAsync(explorerId, "SunkenVault", depth: 2)).Expedition;

      await api.ShutDownAsync();

      var town = await api.BringUp().ReadTownAsync();

      var expedition = Assert.Single(town.Expeditions);
      Assert.Equal(dispatched.Id, expedition.Id);
      Assert.Equal(dispatched.CompletesAt, expedition.CompletesAt);
      Assert.Equal(dispatched.SurvivalChance, expedition.SurvivalChance);
      Assert.Equal(dispatched.Depth, expedition.Depth);
      Assert.Equal("SunkenVault", expedition.Area);

      var gear = Assert.Single(town.Gear);
      Assert.Equal(crafted.Id, gear.Id);
      Assert.Equal(crafted.Power, gear.Power);
      Assert.Equal(crafted.Material, gear.Material);

      var explorer = town.Explorers.Single(candidate => candidate.Id == explorerId);
      Assert.Equal(ExplorerStatuses.OnExpedition, explorer.Status);
      Assert.Equal(crafted.Id, explorer.EquippedGearId);
   }

   /// <summary>
   ///    Story 37, staged as literally as the slice allows: the Expedition comes due while there is no API
   ///    running at all, so no scheduled job can have resolved it. The next read has to, and it has to attribute
   ///    the outcome to the moment the Expedition was due rather than to the moment someone looked.
   /// </summary>
   [Fact]
   public async Task An_Expedition_that_came_due_while_the_API_was_down_resolves_on_the_next_read()
   {
      var before = await api.StartAsync();

      var explorerId = (await before.ReadTownAsync()).Explorers.First(candidate => candidate.IsIdle()).Id;
      var crafted = await before.CraftAsync("Sword", "Copper");

      await before.EquipAsync(explorerId, crafted.Gear.Id);

      var dispatched = (await before.DispatchAsync(explorerId, "Quarry", depth: 1)).Expedition;

      await api.ShutDownAsync();

      // Nothing is serving requests and nothing is running jobs. The Expedition comes due here, and then some.
      api.AdvanceTo(dispatched.CompletesAt + TimeSpan.FromHours(3));

      var town = await api.BringUp().ReadTownAsync();

      Assert.Empty(town.Expeditions);
      Assert.Equal(ExplorerStatuses.Idle, town.Explorers.Single(candidate => candidate.Id == explorerId).Status);

      var report = Assert.Single(town.Reports);
      Assert.Equal(dispatched.Id, report.ExpeditionId);
      Assert.Equal(dispatched.CompletesAt, report.ResolvedAt);
   }

   /// <summary>
   ///    Resolving is idempotent, so reading twice must not pay out twice. The read is the resolution path, which
   ///    makes this the one place a double payout could hide.
   /// </summary>
   [Fact]
   public async Task Reading_a_Town_twice_does_not_resolve_an_Expedition_twice()
   {
      var host = await api.StartAsync();

      var explorerId = (await host.ReadTownAsync()).Explorers.First(candidate => candidate.IsIdle()).Id;
      var crafted = await host.CraftAsync("Sword", "Copper");
      await host.EquipAsync(explorerId, crafted.Gear.Id);

      var dispatched = (await host.DispatchAsync(explorerId, "SunkenVault", depth: 1)).Expedition;

      api.AdvanceTo(dispatched.CompletesAt);

      var first = await host.ReadTownAsync();
      var second = await host.ReadTownAsync();

      Assert.Equal(first.Gold, second.Gold);
      Assert.Equal(first.Materials.OrderBy(entry => entry.Key), second.Materials.OrderBy(entry => entry.Key));
      Assert.Single(second.Reports);
   }

   /// <summary>
   ///    The job is what makes resolution timely rather than what makes it happen, but it still has to be booked
   ///    — otherwise a player who never opens the game is never told anything. Asserting it is scheduled proves
   ///    the appointment was written without waiting for it to come due.
   /// </summary>
   [Fact]
   public async Task Dispatching_books_a_resolution_job_for_the_Expedition()
   {
      var host = await api.StartAsync();

      var explorerId = (await host.ReadTownAsync()).Explorers.First(candidate => candidate.IsIdle()).Id;
      var crafted = await host.CraftAsync("Sword", "Copper");
      await host.EquipAsync(explorerId, crafted.Gear.Id);

      var booked = await host.PendingResolutionJobsAsync();

      await host.DispatchAsync(explorerId, "Quarry", depth: 3);

      Assert.Equal(booked + 1, await host.PendingResolutionJobsAsync());
   }
}
