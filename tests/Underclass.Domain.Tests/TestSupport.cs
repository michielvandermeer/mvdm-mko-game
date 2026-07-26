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
}
