using System.Text;
using Microsoft.Extensions.Time.Testing;
using Underclass.Domain;
using Xunit.Abstractions;

namespace Underclass.Domain.Tests;

/// <summary>
///    The test that answers "does the loop cohere". It plays a few hundred Expeditions with a plausible
///    player behind the wheel and asserts on the shape of the result — gold and Materials climbing, Explorers
///    still dying — rather than on any particular number. When the Catalogue is re-tuned this test is
///    expected to be re-read, not forced back to green.
/// </summary>
public sealed class EconomyCurveTests(ITestOutputHelper output)
{
   /// <summary>How many Expeditions the simulated player runs before we read the curve.</summary>
   private const int TargetExpeditions = 300;

   /// <summary>The worst odds the simulated player is willing to accept when a safer option exists.</summary>
   private const double AcceptableSurvival = 0.8;

   /// <summary>Stops a policy bug from turning a failing assertion into a hanging test.</summary>
   private const int MaxRounds = 20_000;

   [Fact]
   public void The_loop_feeds_itself_while_Explorers_still_die()
   {
      var run = Simulate(seed: 20260726);

      output.WriteLine(run.Curve);
      output.WriteLine($"expeditions={run.Expeditions} deaths={run.Deaths} ({run.DeathRate:P1}) elapsed={run.Elapsed.TotalHours:F1}h");
      output.WriteLine($"gold {Catalogue.StartingGold} -> {run.FinalGold}; best Gear crafted: power {run.BestGearPower}");

      Assert.True(run.Expeditions >= TargetExpeditions, $"the simulated player stalled after {run.Expeditions} Expeditions");

      Assert.True(
         run.LateGoldAverage > run.EarlyGoldAverage,
         $"gold must trend upward, but averaged {run.EarlyGoldAverage:F0} early and {run.LateGoldAverage:F0} late"
      );

      Assert.True(
         run.LateMaterialValueAverage > run.EarlyMaterialValueAverage,
         $"Materials must trend upward, but averaged {run.EarlyMaterialValueAverage:F0} early and {run.LateMaterialValueAverage:F0} late"
      );

      Assert.True(
         run.BestGearPower >= Catalogue.PowerRange(MaterialType.Steel).Min,
         $"the loop must reach Steel Gear, but the best crafted was power {run.BestGearPower}"
      );

      Assert.InRange(run.DeathRate, 0.02, 0.6);

      // The curve is only worth reading if the run actually spread itself over the content. Without this the
      // whole test could pass having sat at one Area and one depth for 300 Expeditions, and the shape it asserts
      // on would say nothing about the loop.
      Assert.Equal(Catalogue.Areas.Select(area => area.Id).OrderBy(area => area), run.AreasVisited.OrderBy(area => area));
      Assert.True(run.DepthsVisited.Count > 1, $"the run only ever went to depth {string.Join(", ", run.DepthsVisited)}");
   }

   /// <summary>
   ///    The other half of the claim the Quarry's tuning rests on, and the one comparing rates cannot make. Wages
   ///    stop draining at zero rather than accruing debt, so nothing in the numbers alone rules out a Town sitting
   ///    insolvent while Materials pile up forever — which is the comfortable plateau the design warns about,
   ///    reached from below. Only playing it out can tell.
   /// </summary>
   /// <remarks>
   ///    Expected to be re-read rather than forced green. If the Quarry is ever re-tuned so that a Town can live in
   ///    it, this is the test that should say so.
   /// </remarks>
   [Fact]
   public void A_Town_that_never_leaves_the_Quarry_cannot_keep_going()
   {
      var run = Simulate(seed: 20260726, AreaId.Quarry);

      output.WriteLine(run.Curve);
      output.WriteLine(
         $"quarry only: gold {Catalogue.StartingGold} -> {run.FinalGold} over {run.Elapsed.TotalHours:F1}h, "
         + $"{run.Expeditions} Expeditions, {run.Deaths} deaths, {run.LivingExplorers} Explorers left"
      );

      Assert.Equal([AreaId.Quarry], run.AreasVisited);

      Assert.True(
         run.FinalGold < Catalogue.StartingGold,
         $"the Quarry left the Town richer than it started, at {run.FinalGold} gold"
      );

      // The run ended because the Town could no longer field anyone, not because it ran out of Expeditions to try:
      // the roster was wiped out and there was no gold left to replace it. Staying shallow is a slow death rather
      // than a plateau — which is what makes pushing deeper compulsory.
      Assert.True(
         run.LivingExplorers == 0 && run.Expeditions < TargetExpeditions,
         $"a Quarry-only Town sustained {run.Expeditions} Expeditions and still has {run.LivingExplorers} Explorers on {run.FinalGold} gold"
      );
   }

   [Fact]
   public void Farming_the_safe_Area_does_not_pay_the_Town_wages()
   {
      // The economy's one drain is optional: an Explorer who never risks anything never loses Gear. The
      // Quarry is therefore tuned to lose money against a single Artisan's wage, so that staying shallow is
      // a slow death rather than a comfortable plateau.
      var quarry = Catalogue.Area(AreaId.Quarry)!;
      var wagePerMinute = (double)Catalogue.ArtisanWagePerInterval / Catalogue.AccrualInterval.TotalMinutes;

      for (var depth = 1; depth <= Catalogue.MaxDepth; depth++)
      {
         var goldPerMinute = quarry.GoldAt(depth) / quarry.DurationAt(depth).TotalMinutes;

         Assert.True(
            goldPerMinute < wagePerMinute,
            $"the Quarry at depth {depth} pays {goldPerMinute:F2} gold a minute against a {wagePerMinute:F2} wage"
         );
      }
   }

   [Fact]
   public void The_dangerous_Area_pays_better_per_minute_than_the_safe_one()
   {
      var quarry = Catalogue.Area(AreaId.Quarry)!;
      var vault = Catalogue.Area(AreaId.SunkenVault)!;

      var quarryRate = quarry.GoldAt(Catalogue.MaxDepth) / quarry.DurationAt(Catalogue.MaxDepth).TotalMinutes;
      var vaultRate = vault.GoldAt(Catalogue.MaxDepth) / vault.DurationAt(Catalogue.MaxDepth).TotalMinutes;

      Assert.True(vaultRate > quarryRate, $"the Vault pays {vaultRate:F2} a minute against the Quarry's {quarryRate:F2}");
   }

   /// <param name="seed">Fixes the run's rolls, so a curve that is read once can be read again.</param>
   /// <param name="areas">
   ///    Where the simulated player is willing to go. Empty means the whole catalogue; naming one Area is how a test
   ///    asks what a player who never pushes deeper ends up with.
   /// </param>
   private static SimulationRun Simulate(int seed, params AreaId[] areas)
   {
      var allowed = areas.Length == 0 ? Catalogue.Areas.Select(area => area.Id).ToHashSet() : areas.ToHashSet();

      var clock = new FakeTimeProvider(TestTown.Start);
      var town = Town.Load(Town.NewTown(clock), clock, new SeededRandomSource(seed));

      var samples = new List<(TimeSpan At, long Gold, int MaterialValue)>();
      var curve = new StringBuilder("  hours   gold   materials   expeditions   deaths");
      var visited = new HashSet<(AreaId Area, int Depth)>();
      var expeditions = 0;
      var bestGearPower = 0;

      for (var round = 0; round < MaxRounds && expeditions < TargetExpeditions; round++)
      {
         var snapshot = town.Snapshot();

         samples.Add((snapshot.ServerTime - TestTown.Start, snapshot.Gold, MaterialValue(snapshot.Materials)));

         if (samples.Count % 100 == 1)
         {
            curve.AppendLine().Append(
               $"{(snapshot.ServerTime - TestTown.Start).TotalHours,7:F1}{snapshot.Gold,7}{MaterialValue(snapshot.Materials),12}{expeditions,14}{snapshot.Reports.Count(report => !report.Survived),9}"
            );
         }

         if (snapshot.Explorers.All(explorer => !explorer.IsAlive) && snapshot.Gold < Catalogue.RecruitCost)
            break;

         bestGearPower = Math.Max(bestGearPower, EquipEveryone(town, snapshot));
         expeditions += SendEveryoneOut(town, town.Snapshot(), allowed, visited);
         Recruit(town, town.Snapshot());
         Advance(clock, town.Snapshot());
      }

      var final = town.Snapshot();
      var fifth = Math.Max(1, samples.Count / 5);

      return new SimulationRun(
         expeditions,
         final.Reports.Count(report => !report.Survived),
         bestGearPower,
         final.Gold,
         final.Explorers.Count(explorer => explorer.IsAlive),
         final.ServerTime - TestTown.Start,
         visited.Select(been => been.Area).Distinct().ToList(),
         visited.Select(been => been.Depth).Distinct().OrderBy(depth => depth).ToList(),
         samples.Take(fifth).Average(sample => (double)sample.Gold),
         samples.TakeLast(fifth).Average(sample => (double)sample.Gold),
         samples.Take(fifth).Average(sample => (double)sample.MaterialValue),
         samples.TakeLast(fifth).Average(sample => (double)sample.MaterialValue),
         curve.ToString()
      );
   }

   /// <summary>
   ///    Arms every idle Explorer as well as the Town can: crafts from the best Material on hand when that
   ///    would outclass what is lying around, hands out the strongest piece, and salvages the rest. Returns the
   ///    power of the best piece crafted this round.
   /// </summary>
   private static int EquipEveryone(Town town, TownSnapshot snapshot)
   {
      var bestPower = 0;
      var spare = snapshot.Gear.Where(gear => snapshot.Explorers.All(explorer => explorer.EquippedGear != gear.Id)).ToList();

      foreach (var explorer in snapshot.Explorers.Where(explorer => explorer.IsAvailable).OrderByDescending(explorer => explorer.Skill))
      {
         var carried = explorer.EquippedGear is null ? null : snapshot.Gear.Single(gear => gear.Id == explorer.EquippedGear);
         var onHand = Math.Max(carried?.Power ?? 0, spare.Count == 0 ? 0 : spare.Max(gear => gear.Power));
         var material = BestCraftableMaterial(town.Snapshot().Materials);

         // Only craft when the Material would beat everything the Town already has; otherwise the Materials
         // are better kept for the tier above.
         if (material is not null && Catalogue.PowerRange(material.Value).Min > onHand)
         {
            var craft = town.CraftGear(GearBaseType.Sword, material.Value);

            if (craft.Ok)
            {
               var crafted = town.Snapshot().Gear.Single(gear => gear.Id == craft.Value);
               spare.Add(crafted);
               bestPower = Math.Max(bestPower, crafted.Power);
            }
         }

         var best = spare.OrderByDescending(gear => gear.Power).FirstOrDefault();

         if (best is null || best.Power <= (carried?.Power ?? 0))
            continue;

         if (!town.EquipGear(explorer.Id, best.Id).Ok)
            continue;

         spare.Remove(best);

         if (carried is not null)
            spare.Add(carried);
      }

      // Obsolete Gear is worth more as Materials than as a pile: keep one spare per living Explorer.
      var keep = snapshot.Explorers.Count(explorer => explorer.IsAlive);

      foreach (var obsolete in spare.OrderBy(gear => gear.Power).Take(Math.Max(0, spare.Count - keep)))
         town.SalvageGear(obsolete.Id);

      return bestPower;
   }

   /// <summary>
   ///    Sends every idle armed Explorer to the best-paying Expedition they are likely to survive, recording where
   ///    they went so a test can assert the run covered the content rather than sitting in one corner of it.
   /// </summary>
   private static int SendEveryoneOut(Town town, TownSnapshot snapshot, IReadOnlySet<AreaId> allowed, ISet<(AreaId Area, int Depth)> visited)
   {
      var dispatched = 0;

      foreach (var explorer in snapshot.Explorers.Where(explorer => explorer.IsAvailable && explorer.EquippedGear is not null))
      {
         var choice = BestExpedition(town, explorer.Id, allowed);

         if (choice is null)
            continue;

         if (!town.Dispatch(explorer.Id, choice.Value.Area, choice.Value.Depth).Ok)
            continue;

         visited.Add(choice.Value);
         dispatched++;
      }

      return dispatched;
   }

   private static (AreaId Area, int Depth)? BestExpedition(Town town, ExplorerId explorerId, IReadOnlySet<AreaId> allowed)
   {
      var options = new List<(AreaId Area, int Depth, double Value, double Survival)>();

      foreach (var area in Catalogue.Areas.Where(area => allowed.Contains(area.Id)))
      {
         for (var depth = 1; depth <= Catalogue.MaxDepth; depth++)
         {
            var preview = town.PreviewExpedition(explorerId, area.Id, depth);

            if (!preview.Ok)
               continue;

            var worth = preview.Value.Gold + MaterialValue(preview.Value.Loot);
            options.Add((area.Id, depth, worth / preview.Value.Duration.TotalMinutes, preview.Value.SurvivalChance));
         }
      }

      if (options.Count == 0)
         return null;

      var survivable = options.Where(option => option.Survival >= AcceptableSurvival).ToList();

      // Take the best-paying Expedition this Explorer is likely to walk away from; when nothing clears the
      // bar — a weak Explorer in poor Gear — take the safest thing on offer rather than the richest.
      var chosen = survivable.Count > 0
         ? survivable.OrderByDescending(option => option.Value).First()
         : options.OrderByDescending(option => option.Survival).First();

      return (chosen.Area, chosen.Depth);
   }

   private static void Recruit(Town town, TownSnapshot snapshot)
   {
      // Replace losses, but never spend the last of the Town's gold on it.
      if (snapshot.Explorers.Count(explorer => explorer.IsAlive) < snapshot.ExplorerCap && snapshot.Gold > Catalogue.RecruitCost * 2)
         town.RecruitExplorer();
   }

   private static void Advance(FakeTimeProvider clock, TownSnapshot snapshot)
   {
      var nextDue = snapshot.Expeditions.Select(expedition => expedition.CompletesAt).DefaultIfEmpty().Min();

      if (nextDue > clock.GetUtcNow())
         clock.SetUtcNow(nextDue);
      else
         clock.Advance(Catalogue.AccrualInterval);
   }

   private static MaterialType? BestCraftableMaterial(IReadOnlyDictionary<MaterialType, int> materials)
   {
      return materials
         .Where(entry => entry.Value >= Catalogue.CraftCost)
         .OrderByDescending(entry => entry.Key)
         .Select(entry => (MaterialType?)entry.Key)
         .FirstOrDefault();
   }

   /// <summary>
   ///    A rough worth for a pile of Materials, so that "Materials trend upward" means the Town is climbing
   ///    tiers rather than merely hoarding Copper.
   /// </summary>
   private static int MaterialValue(IReadOnlyDictionary<MaterialType, int> materials)
   {
      return materials.Sum(
         entry => entry.Value * (entry.Key switch {
            MaterialType.Copper => 1,
            MaterialType.Iron => 10,
            MaterialType.Steel => 40,
            _ => 0
         })
      );
   }

   private sealed record SimulationRun(
      int Expeditions,
      int Deaths,
      int BestGearPower,
      long FinalGold,
      int LivingExplorers,
      TimeSpan Elapsed,
      IReadOnlyList<AreaId> AreasVisited,
      IReadOnlyList<int> DepthsVisited,
      double EarlyGoldAverage,
      double LateGoldAverage,
      double EarlyMaterialValueAverage,
      double LateMaterialValueAverage,
      string Curve)
   {
      public double DeathRate => Expeditions == 0 ? 0 : (double)Deaths / Expeditions;
   }
}
