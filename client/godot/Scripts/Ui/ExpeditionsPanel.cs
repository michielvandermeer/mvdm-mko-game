using System;
using System.Linq;
using Godot;
using Underclass.Contracts;

namespace Underclass.Client.Ui;

/// <summary>
///    Who is out, and how long until they are due — story 23.
/// </summary>
/// <remarks>
///    The countdown is measured against the server's clock, not this machine's. The snapshot carries the server's
///    time; the client only ever uses its own clock to measure how long ago that snapshot arrived. A player whose
///    system clock is wrong therefore sees the right remaining time, and cannot make an Expedition look due by
///    changing it.
/// </remarks>
public sealed partial class ExpeditionsPanel : PanelContainer, ITownView
{
   private VBoxContainer _body = null!;
   private TownResponse? _town;

   /// <summary>How far the server's clock has been carried forward since the snapshot arrived.</summary>
   private double _secondsSinceRead;

   private double _secondsSinceRedraw;

   /// <inheritdoc />
   public override void _Ready()
   {
      CustomMinimumSize = new Vector2(320, 0);

      _body = Widgets.Panel(this);

      Render();
   }

   /// <inheritdoc />
   public override void _Process(double delta)
   {
      if (_town is null)
         return;

      _secondsSinceRead += delta;
      _secondsSinceRedraw += delta;

      // Only a countdown moves between reads, so this redraws once a second rather than once a frame.
      if (_secondsSinceRedraw < 1)
         return;

      _secondsSinceRedraw = 0;

      Render();
   }

   /// <summary>Takes a new snapshot, and restarts the local measurement of time since it was read.</summary>
   public void Accept(TownResponse town)
   {
      _town = town;
      _secondsSinceRead = 0;
      _secondsSinceRedraw = 0;

      Render();
   }

   /// <summary>The server's clock as it should read now: what it said, plus how long ago it said it.</summary>
   private DateTimeOffset ServerNow => (_town?.ServerTime ?? DateTimeOffset.UnixEpoch).AddSeconds(_secondsSinceRead);

   private void Render()
   {
      Widgets.Clear(_body);

      _body.AddChild(Widgets.Heading("Out there"));

      if (_town is null)
      {
         _body.AddChild(Widgets.Line("waiting for the Town…", Widgets.Quiet));

         return;
      }

      if (_town.Expeditions.Count == 0)
      {
         _body.AddChild(Widgets.Line("Nobody is on an Expedition.", Widgets.Quiet));
         _body.AddChild(Widgets.Line("Walk to the Expedition Board to send someone.", Widgets.Quiet, size: 12));

         return;
      }

      foreach (var expedition in _town.Expeditions)
      {
         var explorer = _town.Explorers.FirstOrDefault(candidate => candidate.Id == expedition.ExplorerId);
         var name = explorer?.Name ?? "Someone";

         _body.AddChild(Widgets.Line($"{name} — {expedition.Area}, depth {expedition.Depth}", size: 13));
         _body.AddChild(
            Widgets.Line(
               $"    back in {Widgets.Remaining(expedition.CompletesAt - ServerNow)} · {expedition.SurvivalChance:P0} to return",
               Widgets.Quiet,
               size: 12
            )
         );
      }
   }
}
