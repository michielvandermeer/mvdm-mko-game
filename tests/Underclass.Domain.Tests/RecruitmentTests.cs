using Underclass.Domain;

namespace Underclass.Domain.Tests;

public sealed class RecruitmentTests
{
   [Fact]
   public void Recruiting_costs_gold_and_adds_a_named_Explorer_to_the_roster()
   {
      var clock = TestTown.Clock();
      var town = TestTown.With(clock, new ScriptedRandom(), gold: Catalogue.RecruitCost, explorerCap: 3);

      var result = town.RecruitExplorer();

      Assert.True(result.Ok, $"recruit was rejected: {result.Reason}");

      var snapshot = town.Snapshot();
      var recruit = snapshot.Explorers.Single(explorer => explorer.Id == result.Value);
      Assert.Equal(0, snapshot.Gold);
      Assert.Equal(3, snapshot.Explorers.Count);
      Assert.False(string.IsNullOrWhiteSpace(recruit.Name));
      Assert.Equal(ExplorerStatus.Idle, recruit.Status);
      Assert.Null(recruit.EquippedGear);
      Assert.InRange(recruit.Skill, Catalogue.RecruitSkillRange.Min, Catalogue.RecruitSkillRange.Max);
   }

   [Fact]
   public void Recruiting_without_the_gold_is_rejected_and_changes_nothing()
   {
      var clock = TestTown.Clock();
      var town = TestTown.With(clock, new ScriptedRandom(), gold: Catalogue.RecruitCost - 1, explorerCap: 3);

      var result = town.RecruitExplorer();

      Assert.False(result.Ok);
      Assert.Equal(RejectionReason.InsufficientGold, result.Reason);

      var snapshot = town.Snapshot();
      Assert.Equal(Catalogue.RecruitCost - 1, snapshot.Gold);
      Assert.Equal(2, snapshot.Explorers.Count);
   }

   [Fact]
   public void A_Town_at_capacity_cannot_take_another_Explorer()
   {
      var clock = TestTown.Clock();
      var town = TestTown.With(clock, new ScriptedRandom(), gold: 10_000, explorerCap: 2);

      var result = town.RecruitExplorer();

      Assert.False(result.Ok);
      Assert.Equal(RejectionReason.ExplorerCapReached, result.Reason);
      Assert.Equal(10_000, town.Snapshot().Gold);
   }

   [Fact]
   public void A_death_frees_the_capacity_the_Explorer_was_taking_up()
   {
      var clock = TestTown.Clock();
      var town = TestTown.With(clock, new ScriptedRandom([1.0]), gold: 10_000, explorerCap: 2, materials: [(MaterialType.Copper, 10)]);
      town.ArmWith(TestTown.Bram, MaterialType.Copper);
      var doomed = town.SendTo(TestTown.Bram, AreaId.SunkenVault, depth: 3);

      clock.SetUtcNow(doomed.CompletesAt);

      var result = town.RecruitExplorer();

      Assert.True(result.Ok, $"recruit was rejected: {result.Reason}");
      Assert.Equal(3, town.Snapshot().Explorers.Count);
      Assert.Contains(town.Snapshot().Explorers, explorer => explorer.Id == TestTown.Bram && explorer.Status == ExplorerStatus.Dead);
   }
}
