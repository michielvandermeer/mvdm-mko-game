namespace Underclass.Domain;

/// <summary>
///    The whole game, behind one seam. Everything a player can do is a command here and everything they can
///    see comes out of <see cref="Snapshot" />; the API is a translation layer over this class and the tests
///    that decide whether the loop coheres drive it directly, with no infrastructure in the way.
/// </summary>
/// <remarks>
///    Time and chance arrive from outside. Nothing here reads a clock or a global random source, which is
///    what lets a test fast-forward an Expedition and pin its outcome — and what keeps every roll on the
///    server, where a Marketplace can eventually trust it.
/// </remarks>
public sealed class Town
{
   private readonly TimeProvider _clock;
   private readonly IRandomSource _random;
   private readonly TownState _state;

   private Town(TownState state, TimeProvider clock, IRandomSource random)
   {
      _state = state;
      _clock = clock;
      _random = random;
   }

   /// <summary>
   ///    True when this Town has moved since it was loaded — through a command, an Expedition resolving, or
   ///    accrual settling. Lets a caller skip persisting a read that changed nothing.
   /// </summary>
   public bool HasChanges { get; private set; }

   /// <summary>
   ///    The persisted form of this Town, for storage. Read <see cref="Snapshot" /> instead to show a Town to
   ///    a player: it settles first, and this does not.
   /// </summary>
   public TownState State => _state;

   /// <summary>Takes up an existing Town so commands can be issued against it.</summary>
   public static Town Load(TownState state, TimeProvider clock, IRandomSource random)
   {
      return new Town(state, clock, random);
   }

   /// <summary>The Town a new player starts with: some Copper, some gold, three Explorers and one Artisan.</summary>
   public static TownState NewTown(TimeProvider clock)
   {
      return new TownState {
         Gold = Catalogue.StartingGold,
         ArtisanCount = Catalogue.ArtisanCount,
         ExplorerCap = Catalogue.ExplorerCap,
         LastSettledAt = clock.GetUtcNow(),
         Materials = new Dictionary<MaterialType, int> {
            [MaterialType.Copper] = Catalogue.StartingCopper
         },
         Gear = [],
         Explorers = [
            ..Catalogue.StartingExplorers.Select(
               explorer => new ExplorerState(ExplorerId.New(), explorer.Name, explorer.Skill, ExplorerStatus.Idle, EquippedGear: null)
            )
         ],
         Expeditions = [],
         Reports = []
      };
   }

   /// <summary>
   ///    Brings the Town up to the present: settles Artisan output and wages for the elapsed time, then
   ///    resolves every Expedition that has come due. Idempotent, and the only path resolution takes — the
   ///    scheduled job exists to make resolution timely, not to make it happen.
   /// </summary>
   public void Settle()
   {
      SettleAccrual();
      ResolveDueExpeditions();
   }

   /// <summary>A settled read of the Town, as the player should see it.</summary>
   public TownSnapshot Snapshot()
   {
      Settle();

      return new TownSnapshot(
         _clock.GetUtcNow(),
         _state.Gold,
         _state.ArtisanCount,
         _state.ExplorerCap,
         new Dictionary<MaterialType, int>(_state.Materials),
         [.._state.Gear],
         [.._state.Explorers],
         [.._state.Expeditions.OrderBy(expedition => expedition.CompletesAt)],
         [.._state.Reports.OrderByDescending(report => report.ResolvedAt)]
      );
   }

   /// <summary>
   ///    Turns Materials into Gear. The Material sets the range the result can land in; a roll places it
   ///    inside that range, so the same inputs are worth a moment of anticipation.
   /// </summary>
   public CommandResult<GearId> CraftGear(GearBaseType baseType, MaterialType material)
   {
      Settle();

      if (MaterialCount(material) < Catalogue.CraftCost)
         return CommandResult<GearId>.Rejected(RejectionReason.InsufficientMaterials);

      var (min, max) = Catalogue.PowerRange(material);
      var gear = new GearState(GearId.New(), baseType, material, _random.Next(min, max + 1));

      AddMaterial(material, -Catalogue.CraftCost);
      _state.Gear.Add(gear);
      MarkChanged();

      return CommandResult<GearId>.Accepted(gear.Id);
   }

   /// <summary>
   ///    Gives a piece of Gear to an Explorer. Whatever they were carrying goes back into the Town's Gear;
   ///    Gear another Explorer is carrying cannot be taken from them.
   /// </summary>
   public CommandResult EquipGear(ExplorerId explorerId, GearId gearId)
   {
      Settle();

      var explorer = FindExplorer(explorerId);

      if (explorer is null)
         return CommandResult.Rejected(RejectionReason.ExplorerNotFound);

      if (explorer.UnavailableReason is { } unavailable)
         return CommandResult.Rejected(unavailable);

      if (FindGear(gearId) is null)
         return CommandResult.Rejected(RejectionReason.GearNotFound);

      if (IsEquippedByAnyoneElse(gearId, explorerId))
         return CommandResult.Rejected(RejectionReason.GearInUse);

      ReplaceExplorer(explorer with { EquippedGear = gearId });
      MarkChanged();

      return CommandResult.Accepted();
   }

   /// <summary>
   ///    Breaks Gear back down into Materials. Returns strictly less than crafting consumed, so obsolete Gear
   ///    has a floor value without Salvage becoming a loop worth farming.
   /// </summary>
   public CommandResult SalvageGear(GearId gearId)
   {
      Settle();

      var gear = FindGear(gearId);

      if (gear is null)
         return CommandResult.Rejected(RejectionReason.GearNotFound);

      if (_state.Explorers.Any(explorer => explorer.EquippedGear == gearId))
         return CommandResult.Rejected(RejectionReason.GearInUse);

      _state.Gear.Remove(gear);
      AddMaterial(gear.Material, Catalogue.SalvageYield);
      MarkChanged();

      return CommandResult.Accepted();
   }

   /// <summary>
   ///    What a proposed Expedition would return and how likely the Explorer is to come back from it.
   ///    Validated exactly as <see cref="Dispatch" /> validates, so an estimate the player is shown is always
   ///    an estimate of something they can actually do.
   /// </summary>
   public CommandResult<ExpeditionPreview> PreviewExpedition(ExplorerId explorerId, AreaId areaId, int depth)
   {
      Settle();

      var validated = ValidateProposal(explorerId, areaId, depth);

      if (!validated.Ok)
         return CommandResult<ExpeditionPreview>.Rejected(validated.Reason!.Value);

      var proposal = validated.Value;

      return CommandResult<ExpeditionPreview>.Accepted(
         new ExpeditionPreview(
            proposal.Explorer.Id,
            proposal.Area.Id,
            proposal.Depth,
            proposal.SurvivalChance,
            proposal.Duration,
            proposal.Gold,
            proposal.Loot
         )
      );
   }

   /// <summary>
   ///    Sends an Explorer to an Area. They become unavailable until the Expedition's completion time, and the
   ///    chance they come back is fixed here — the same number the player was shown before confirming.
   /// </summary>
   public CommandResult<ExpeditionState> Dispatch(ExplorerId explorerId, AreaId areaId, int depth)
   {
      Settle();

      var validated = ValidateProposal(explorerId, areaId, depth);

      if (!validated.Ok)
         return CommandResult<ExpeditionState>.Rejected(validated.Reason!.Value);

      var proposal = validated.Value;
      var now = _clock.GetUtcNow();

      var expedition = new ExpeditionState(
         ExpeditionId.New(),
         proposal.Explorer.Id,
         proposal.Gear.Id,
         proposal.Area.Id,
         proposal.Depth,
         now,
         now + proposal.Duration,
         proposal.SurvivalChance
      );

      _state.Expeditions.Add(expedition);
      ReplaceExplorer(proposal.Explorer with { Status = ExplorerStatus.OnExpedition });
      MarkChanged();

      return CommandResult<ExpeditionState>.Accepted(expedition);
   }

   /// <summary>
   ///    Hires another Explorer for gold, so that losing one is a setback rather than the end of the run. Dead
   ///    Explorers stay in the roster but stop counting against the Town's capacity.
   /// </summary>
   public CommandResult<ExplorerId> RecruitExplorer()
   {
      Settle();

      if (_state.Explorers.Count(explorer => explorer.IsAlive) >= _state.ExplorerCap)
         return CommandResult<ExplorerId>.Rejected(RejectionReason.ExplorerCapReached);

      if (_state.Gold < Catalogue.RecruitCost)
         return CommandResult<ExplorerId>.Rejected(RejectionReason.InsufficientGold);

      var (minSkill, maxSkill) = Catalogue.RecruitSkillRange;

      var recruit = new ExplorerState(
         ExplorerId.New(),
         Catalogue.RecruitNames[_state.Explorers.Count % Catalogue.RecruitNames.Count],
         _random.Next(minSkill, maxSkill + 1),
         ExplorerStatus.Idle,
         EquippedGear: null
      );

      _state.Gold -= Catalogue.RecruitCost;
      _state.Explorers.Add(recruit);
      MarkChanged();

      return CommandResult<ExplorerId>.Accepted(recruit.Id);
   }

   /// <summary>
   ///    The one place dispatch and preview agree on what makes a proposed Expedition legal, and what it is
   ///    made of once it is.
   /// </summary>
   private CommandResult<Proposal> ValidateProposal(ExplorerId explorerId, AreaId areaId, int depth)
   {
      var area = Catalogue.Area(areaId);

      if (area is null)
         return CommandResult<Proposal>.Rejected(RejectionReason.UnknownArea);

      if (depth < 1 || depth > Catalogue.MaxDepth)
         return CommandResult<Proposal>.Rejected(RejectionReason.InvalidDepth);

      var explorer = FindExplorer(explorerId);

      if (explorer is null)
         return CommandResult<Proposal>.Rejected(RejectionReason.ExplorerNotFound);

      if (explorer.UnavailableReason is { } unavailable)
         return CommandResult<Proposal>.Rejected(unavailable);

      if (explorer.EquippedGear is null)
         return CommandResult<Proposal>.Rejected(RejectionReason.NoGearEquipped);

      var gear = FindGear(explorer.EquippedGear.Value);

      if (gear is null)
         return CommandResult<Proposal>.Rejected(RejectionReason.NoGearEquipped);

      return CommandResult<Proposal>.Accepted(new Proposal(explorer, gear, area, depth));
   }

   /// <summary>
   ///    Advances Artisan output and wages over the elapsed time. Whole intervals only: the remainder stays
   ///    unsettled so that settling twice in a row is a no-op and no fraction of a Material is ever lost.
   /// </summary>
   private void SettleAccrual()
   {
      var elapsed = _clock.GetUtcNow() - _state.LastSettledAt;

      if (elapsed < Catalogue.AccrualInterval)
         return;

      var intervals = elapsed.Ticks / Catalogue.AccrualInterval.Ticks;

      _state.LastSettledAt += Catalogue.AccrualInterval * intervals;

      var artisanIntervals = intervals * _state.ArtisanCount;

      if (artisanIntervals > 0)
      {
         AddMaterial(Catalogue.ArtisanOutput, (int)(artisanIntervals * Catalogue.ArtisanOutputPerInterval));
         _state.Gold = Math.Max(0, _state.Gold - (artisanIntervals * Catalogue.ArtisanWagePerInterval));
      }

      MarkChanged();
   }

   /// <summary>
   ///    Resolves every Expedition whose completion time has passed, oldest first, so that a player returning
   ///    after a long absence gets the same outcomes in the same order as one who watched.
   /// </summary>
   private void ResolveDueExpeditions()
   {
      var now = _clock.GetUtcNow();

      while (true)
      {
         var due = _state.Expeditions.Where(expedition => expedition.CompletesAt <= now).OrderBy(expedition => expedition.CompletesAt).FirstOrDefault();

         if (due is null)
            return;

         Resolve(due);
      }
   }

   /// <summary>
   ///    Rolls one Expedition's outcome. On success the Explorer returns with the Area's Loot and gold; on
   ///    failure they die, the Gear they carried is destroyed with them, and the run's Loot is forfeited. The
   ///    report records what they were carrying either way, so a loss is legible rather than just absent.
   /// </summary>
   private void Resolve(ExpeditionState expedition)
   {
      var explorer = FindExplorer(expedition.ExplorerId)
                     ?? throw new InvalidOperationException($"Expedition {expedition.Id} refers to Explorer {expedition.ExplorerId}, who is not in the roster.");

      var area = Catalogue.Area(expedition.Area)
                 ?? throw new InvalidOperationException($"Expedition {expedition.Id} refers to Area {expedition.Area}, which is not in the catalogue.");

      var carriedGold = area.GoldAt(expedition.Depth);
      var carriedLoot = area.LootAt(expedition.Depth);
      var survived = _random.NextDouble() < expedition.SurvivalChance;
      var lostGear = survived ? null : FindGear(expedition.GearId);

      _state.Expeditions.Remove(expedition);

      if (survived)
      {
         _state.Gold += carriedGold;

         foreach (var (material, amount) in carriedLoot)
            AddMaterial(material, amount);

         ReplaceExplorer(explorer with { Status = ExplorerStatus.Idle });
      }
      else
      {
         if (lostGear is not null)
            _state.Gear.Remove(lostGear);

         ReplaceExplorer(explorer with { Status = ExplorerStatus.Dead, EquippedGear = null });
      }

      _state.Reports.Add(
         new ExpeditionReport(
            ReportId.New(),
            expedition.Id,
            explorer.Id,
            explorer.Name,
            expedition.Area,
            expedition.Depth,
            expedition.CompletesAt,
            survived,
            carriedGold,
            carriedLoot,
            lostGear
         )
      );

      MarkChanged();
   }

   private ExplorerState? FindExplorer(ExplorerId explorerId)
   {
      return _state.Explorers.FirstOrDefault(explorer => explorer.Id == explorerId);
   }

   private GearState? FindGear(GearId gearId)
   {
      return _state.Gear.FirstOrDefault(gear => gear.Id == gearId);
   }

   private bool IsEquippedByAnyoneElse(GearId gearId, ExplorerId explorerId)
   {
      return _state.Explorers.Any(explorer => explorer.EquippedGear == gearId && explorer.Id != explorerId);
   }

   private void ReplaceExplorer(ExplorerState explorer)
   {
      var index = _state.Explorers.FindIndex(candidate => candidate.Id == explorer.Id);

      _state.Explorers[index] = explorer;
   }

   private int MaterialCount(MaterialType material)
   {
      return _state.Materials.GetValueOrDefault(material);
   }

   private void AddMaterial(MaterialType material, int amount)
   {
      _state.Materials[material] = MaterialCount(material) + amount;
   }

   private void MarkChanged()
   {
      HasChanges = true;
   }

   /// <summary>
   ///    A proposed Expedition that has been checked and found legal: who would go, what they are carrying, where
   ///    to, and how deep.
   /// </summary>
   /// <remarks>
   ///    What such an Expedition would be worth and how likely the Explorer is to come back are derived here, and
   ///    that is the point of the type existing. <see cref="PreviewExpedition" /> shows those numbers to the player
   ///    and <see cref="Dispatch" /> fixes them onto the Expedition the server will roll against; reading both off
   ///    one object makes them the same expression rather than two copies of it that could drift apart. Story 17 —
   ///    a death being the result of a gamble the player understood — rests on exactly that.
   /// </remarks>
   private sealed record Proposal(ExplorerState Explorer, GearState Gear, AreaDefinition Area, int Depth)
   {
      /// <summary>The chance the Explorer comes back.</summary>
      public double SurvivalChance => Catalogue.SurvivalChance(Explorer.Skill, Gear.Power, Area, Depth);

      /// <summary>How long they would be gone.</summary>
      public TimeSpan Duration => Area.DurationAt(Depth);

      /// <summary>The gold they would return with.</summary>
      public long Gold => Area.GoldAt(Depth);

      /// <summary>The Materials they would return with.</summary>
      public IReadOnlyDictionary<MaterialType, int> Loot => Area.LootAt(Depth);
   }
}
