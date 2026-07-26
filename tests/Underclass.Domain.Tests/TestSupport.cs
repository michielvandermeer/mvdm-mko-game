using Microsoft.Extensions.Time.Testing;
using Underclass.Domain;

namespace Underclass.Domain.Tests;

/// <summary>
/// An <see cref="IRandomSource"/> whose results are dictated by the test. Survival rolls come
/// from the double queue, Gear power rolls from the int queue; the two never share a sequence,
/// so a test can pin one without knowing how many times the other was consumed.
/// </summary>
internal sealed class ScriptedRandom : IRandomSource
{
   private readonly Queue<double> _doubles;
   private readonly Queue<int> _ints;

   public ScriptedRandom(double[]? doubles = null, int[]? ints = null)
   {
      _doubles = new Queue<double>(doubles ?? []);
      _ints = new Queue<int>(ints ?? []);
   }

   public double NextDouble() => _doubles.Count > 0 ? _doubles.Dequeue() : 0.5;

   public int Next(int minInclusive, int maxExclusive)
      => _ints.Count > 0 ? Math.Clamp(_ints.Dequeue(), minInclusive, maxExclusive - 1) : minInclusive;
}

internal static class TestTown
{
   public static readonly DateTimeOffset Start = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

   public static FakeTimeProvider Clock() => new(Start);

   /// <summary>A Town with no Artisan, so accrual never perturbs a test that isn't about accrual.</summary>
   public static Town With(
      FakeTimeProvider clock,
      IRandomSource random,
      long gold = 1_000,
      int artisans = 0,
      int explorerCap = 3,
      params (MaterialType Material, int Amount)[] materials)
   {
      var state = new TownState {
         Gold = gold,
         ArtisanCount = artisans,
         ExplorerCap = explorerCap,
         LastSettledAt = clock.GetUtcNow(),
         Materials = materials.ToDictionary(m => m.Material, m => m.Amount),
         Gear = [],
         Explorers = [
            new ExplorerState(new ExplorerId(Guid.Parse("00000000-0000-0000-0000-000000000001")), "Elara", 10, ExplorerStatus.Idle, null),
            new ExplorerState(new ExplorerId(Guid.Parse("00000000-0000-0000-0000-000000000002")), "Bram", 14, ExplorerStatus.Idle, null)
         ],
         Expeditions = [],
         Reports = []
      };

      return Town.Load(state, clock, random);
   }

   public static ExplorerId Elara => new(Guid.Parse("00000000-0000-0000-0000-000000000001"));
   public static ExplorerId Bram => new(Guid.Parse("00000000-0000-0000-0000-000000000002"));

   /// <summary>Crafts a Sword from the given Material and equips it, asserting each step succeeded.</summary>
   public static GearId ArmWith(this Town town, ExplorerId explorer, MaterialType material)
   {
      var craft = town.CraftGear(GearBaseType.Sword, material);
      Assert.True(craft.Ok, $"craft was rejected: {craft.Reason}");

      var equip = town.EquipGear(explorer, craft.Value);
      Assert.True(equip.Ok, $"equip was rejected: {equip.Reason}");

      return craft.Value;
   }

   /// <summary>Sends an Explorer out, asserting the dispatch succeeded.</summary>
   public static ExpeditionState SendTo(this Town town, ExplorerId explorer, AreaId area, int depth)
   {
      var dispatch = town.Dispatch(explorer, area, depth);
      Assert.True(dispatch.Ok, $"dispatch was rejected: {dispatch.Reason}");

      return dispatch.Value;
   }

   /// <summary>
   ///    Everything about a Town a player could observe, flattened into one comparable string. Lets a test
   ///    assert that two reads describe the same Town without asserting on how the Town is built — the
   ///    server's clock is left out, since it moves whether or not the Town does.
   /// </summary>
   public static string Fingerprint(this TownSnapshot snapshot)
   {
      var materials = snapshot.Materials.OrderBy(entry => entry.Key).Select(entry => $"{entry.Key}={entry.Value}");
      var gear = snapshot.Gear.OrderBy(g => g.Id.Value).Select(g => $"{g.Id}:{g.Material}:{g.Power}");
      var explorers = snapshot.Explorers.Select(e => $"{e.Name}:{e.Status}:{e.EquippedGear}");
      var expeditions = snapshot.Expeditions.Select(e => $"{e.Id}:{e.Area}:{e.Depth}:{e.CompletesAt:O}");
      var reports = snapshot.Reports.Select(r => $"{r.Id}:{r.Survived}:{r.Gold}:{r.ResolvedAt:O}");

      return string.Join(
         " | ",
         $"gold={snapshot.Gold}",
         string.Join(",", materials),
         string.Join(",", gear),
         string.Join(",", explorers),
         string.Join(",", expeditions),
         string.Join(",", reports)
      );
   }
}
