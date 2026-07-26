using System;
using System.Linq;
using Godot;
using Underclass.Contracts;

namespace Underclass.Client.Ui;

/// <summary>
///    The Expedition Board: pick a person, arm them, set how far they go, and read the odds before you agree.
/// </summary>
/// <remarks>
///    <para>
///       Stories 13 to 20. The Areas and their depths arrive priced from <c>GET /api/areas</c>, and the chance of
///       coming back arrives from <c>POST /api/expeditions/preview</c> — this panel computes neither. That is the
///       point of story 17: a death has to be the outcome of a gamble the player understood, which means the number
///       they read came from the same place the roll will.
///    </para>
///    <para>
///       A dead Explorer stays on the roster, greyed and unselectable, so the loss is acknowledged rather than
///       quietly erased.
///    </para>
/// </remarks>
public sealed partial class ExpeditionBoardPanel : PanelContainer, ITownView
{
   private VBoxContainer _body = null!;
   private AreaResponse[] _areas = [];
   private TownResponse? _town;

   private Guid? _chosenExplorer;
   private Guid? _chosenGear;
   private string? _chosenArea;
   private int _chosenDepth = 1;

   private PreviewResponse? _preview;
   private string? _previewProblem;

   /// <summary>Raised when the player hands a piece of Gear to an Explorer.</summary>
   public event Action<Guid, Guid>? EquipRequested;

   /// <summary>Raised whenever the proposed Expedition changes and its odds need asking for again.</summary>
   public event Action<Guid, string, int>? PreviewRequested;

   /// <summary>Raised when the player commits: this Explorer, this Area, this depth.</summary>
   public event Action<Guid, string, int>? DispatchRequested;

   /// <summary>Raised when the player hires someone new.</summary>
   public event Action? RecruitRequested;

   /// <inheritdoc />
   public override void _Ready()
   {
      CustomMinimumSize = new Vector2(600, 0);

      _body = Widgets.Panel(this, padding: 18);
   }

   /// <summary>The Area catalogue. Content, fetched once.</summary>
   public void Accept(AreaResponse[] areas)
   {
      _areas = areas;
      _chosenArea ??= areas.FirstOrDefault()?.Id;

      Render();
   }

   /// <summary>Redraws against a new snapshot, keeping the player's selections where they are still valid.</summary>
   public void Accept(TownResponse town)
   {
      _town = town;

      // A selection can be invalidated by the world moving on — the chosen Explorer died while the panel was
      // open, the chosen Gear went into the ground with them.
      if (_chosenExplorer is not null && !town.Explorers.Any(explorer => explorer.Id == _chosenExplorer && explorer.IsIdle()))
         _chosenExplorer = null;

      if (_chosenGear is not null && town.Gear.All(gear => gear.Id != _chosenGear))
         _chosenGear = null;

      _chosenExplorer ??= town.Explorers.FirstOrDefault(explorer => explorer.IsIdle())?.Id;

      Render();
      AskForOdds();
   }

   /// <summary>The server's answer about a proposed Expedition, or its reason for refusing to estimate one.</summary>
   public void Accept(PreviewResponse? preview, string? problem)
   {
      _preview = preview;
      _previewProblem = problem;

      Render();
   }

   private void Render()
   {
      if (!IsNodeReady())
         return;

      Widgets.Clear(_body);

      _body.AddChild(Widgets.Heading("The Expedition Board"));

      if (_town is null)
      {
         _body.AddChild(Widgets.Line("waiting for the Town…", Widgets.Quiet));

         return;
      }

      RenderRoster(_town);
      _body.AddChild(Widgets.Rule());
      RenderArming(_town);
      _body.AddChild(Widgets.Rule());
      RenderDestination();
      _body.AddChild(Widgets.Rule());
      RenderOdds();
   }

   private void RenderRoster(TownResponse town)
   {
      _body.AddChild(Widgets.Line("Who goes", Widgets.Quiet, size: 12));

      foreach (var explorer in town.Explorers)
      {
         var gear = town.Gear.FirstOrDefault(piece => piece.Id == explorer.EquippedGearId);
         var carrying = gear is null ? "unarmed" : $"{gear.Material} {gear.BaseType} ({gear.Power})";

         var (note, colour) = explorer.Status switch {
            ExplorerStatuses.Dead => ("dead — permanently", Widgets.Bad),
            ExplorerStatuses.OnExpedition => ("out on an Expedition", Widgets.Quiet),
            _ => (carrying, Widgets.Ink)
         };

         var row = Widgets.Row(
            $"{explorer.Name} · skill {explorer.Skill} · {note}",
            chosen: explorer.Id == _chosenExplorer,
            enabled: explorer.IsIdle(),
            () => {
               _chosenExplorer = explorer.Id;
               Render();
               AskForOdds();
            }
         );

         row.AddThemeColorOverride("font_color", colour);
         row.AddThemeColorOverride("font_disabled_color", colour);

         _body.AddChild(row);
      }

      _body.AddChild(Widgets.Press("Recruit an Explorer", () => RecruitRequested?.Invoke()));
   }

   /// <summary>Story 14: a specific piece of Gear, to a specific person you recognise.</summary>
   private void RenderArming(TownResponse town)
   {
      _body.AddChild(Widgets.Line("What they carry", Widgets.Quiet, size: 12));

      var spare = town.Gear.Where(gear => town.Explorers.All(explorer => explorer.EquippedGearId != gear.Id)).ToArray();

      if (spare.Length == 0)
      {
         _body.AddChild(Widgets.Line("No spare Gear. Craft something in the Workshop.", Widgets.Quiet));

         return;
      }

      foreach (var gear in spare.OrderByDescending(piece => piece.Power))
      {
         _body.AddChild(
            Widgets.Row(
               $"{gear.Material} {gear.BaseType} · power {gear.Power}",
               chosen: gear.Id == _chosenGear,
               enabled: true,
               () => {
                  _chosenGear = gear.Id;
                  Render();
               }
            )
         );
      }

      var chosenName = town.Explorers.FirstOrDefault(explorer => explorer.Id == _chosenExplorer)?.Name;

      var give = Widgets.Press(
         chosenName is null ? "Give it to…" : $"Give it to {chosenName}",
         () => {
            if (_chosenExplorer is not null && _chosenGear is not null)
               EquipRequested?.Invoke(_chosenExplorer.Value, _chosenGear.Value);
         }
      );

      give.Disabled = _chosenExplorer is null || _chosenGear is null;

      _body.AddChild(give);
   }

   /// <summary>Stories 15 and 16: the Area, and how deep into it — the dial the player actually sets.</summary>
   private void RenderDestination()
   {
      _body.AddChild(Widgets.Line("Where, and how deep", Widgets.Quiet, size: 12));

      foreach (var area in _areas)
      {
         _body.AddChild(
            Widgets.Row(
               area.Name,
               chosen: area.Id == _chosenArea,
               enabled: true,
               () => {
                  _chosenArea = area.Id;
                  _chosenDepth = Math.Min(_chosenDepth, area.Depths.Count);
                  Render();
                  AskForOdds();
               }
            )
         );

         if (area.Id != _chosenArea)
            continue;

         var depths = new HBoxContainer();

         foreach (var depth in area.Depths)
         {
            var loot = string.Join(", ", depth.Loot.OrderBy(entry => entry.Key).Select(entry => $"{entry.Value} {entry.Key}"));

            var button = Widgets.Press(
               $"depth {depth.Depth}",
               () => {
                  _chosenDepth = depth.Depth;
                  Render();
                  AskForOdds();
               }
            );

            button.TooltipText = $"{Widgets.Length(depth.Duration)} · threat {depth.Threat} · {depth.Gold} gold · {loot}";
            button.Disabled = depth.Depth == _chosenDepth;

            depths.AddChild(button);
         }

         _body.AddChild(depths);

         var chosen = area.Depths.FirstOrDefault(row => row.Depth == _chosenDepth);

         if (chosen is not null)
         {
            var loot = string.Join(", ", chosen.Loot.OrderBy(entry => entry.Key).Select(entry => $"{entry.Value} {entry.Key}"));

            _body.AddChild(Widgets.Line($"    {Widgets.Length(chosen.Duration)} away · threat {chosen.Threat} · brings back {chosen.Gold} gold and {loot}", Widgets.Quiet, size: 12));
         }
      }
   }

   /// <summary>Story 17: the honest estimate, and the only place the player is asked to commit.</summary>
   private void RenderOdds()
   {
      if (_previewProblem is not null)
      {
         _body.AddChild(Widgets.Line(_previewProblem, Widgets.Bad, size: 13));

         return;
      }

      if (_preview is null)
      {
         _body.AddChild(Widgets.Line("Pick someone who can go, and the Board will work out their chances.", Widgets.Quiet));

         return;
      }

      var loot = string.Join(", ", _preview.Loot.OrderBy(entry => entry.Key).Select(entry => $"{entry.Value} {entry.Key}"));
      var odds = _preview.SurvivalChance;

      _body.AddChild(
         Widgets.Line(
            $"{odds:P0} chance of coming back",
            odds switch {
               >= 0.8 => Widgets.Good,
               >= 0.5 => Widgets.Gold,
               _ => Widgets.Bad
            },
            size: 17
         )
      );

      _body.AddChild(Widgets.Line($"Gone {Widgets.Length(_preview.Duration)}. If they return: {_preview.Gold} gold and {loot}.", Widgets.Quiet, size: 12));
      _body.AddChild(Widgets.Line("If they do not, the Gear they carried is destroyed with them.", Widgets.Bad, size: 12));

      _body.AddChild(
         Widgets.Press(
            "Send them out",
            () => {
               if (_chosenExplorer is not null && _chosenArea is not null)
                  DispatchRequested?.Invoke(_chosenExplorer.Value, _chosenArea, _chosenDepth);
            }
         )
      );
   }

   /// <summary>Called when the Board is opened, so its odds are current rather than whatever they were last time.</summary>
   public void Reopened()
   {
      Render();
      AskForOdds();
   }

   /// <summary>
   ///    Asks the server what the currently proposed Expedition would look like. Called whenever the proposal
   ///    changes, because an estimate for something other than what is on screen is worse than none.
   /// </summary>
   private void AskForOdds()
   {
      // Nobody is reading the Board, so nobody needs its odds. Without this the poll would ask for an estimate
      // every few seconds for a proposal that is not on screen.
      if (!Visible)
         return;

      if (_chosenExplorer is null || _chosenArea is null)
      {
         Accept(preview: null, problem: null);

         return;
      }

      PreviewRequested?.Invoke(_chosenExplorer.Value, _chosenArea, _chosenDepth);
   }
}
