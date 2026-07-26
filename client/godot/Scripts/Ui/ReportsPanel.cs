using System.Linq;
using Godot;
using Underclass.Contracts;

namespace Underclass.Client.Ui;

/// <summary>
///    What happened while you were away — story 24.
/// </summary>
/// <remarks>
///    A report reads on its own: it carries the Explorer's name, where they went, and what they were carrying,
///    because by the time it is read the Expedition is gone and so, sometimes, is the Explorer. A loss is stated
///    in terms of what it cost — the Gear destroyed, the Loot forfeited — since that is what makes the gamble
///    legible in hindsight.
/// </remarks>
public sealed partial class ReportsPanel : PanelContainer, ITownView
{
   private VBoxContainer _body = null!;
   private TownResponse? _town;

   /// <inheritdoc />
   public override void _Ready()
   {
      CustomMinimumSize = new Vector2(560, 0);

      _body = Widgets.Panel(this, padding: 18);
   }

   /// <summary>Redraws against a new snapshot.</summary>
   public void Accept(TownResponse town)
   {
      _town = town;

      Render();
   }

   private void Render()
   {
      if (!IsNodeReady())
         return;

      Widgets.Clear(_body);

      _body.AddChild(Widgets.Heading("Expedition reports"));

      if (_town is null || _town.Reports.Count == 0)
      {
         _body.AddChild(Widgets.Line("Nothing has come back yet.", Widgets.Quiet));

         return;
      }

      var rows = Widgets.Scrolling(_body, height: 380);

      // Newest first, as the server ordered them.
      foreach (var report in _town.Reports)
      {
         var loot = string.Join(", ", report.Materials.OrderBy(entry => entry.Key).Select(entry => $"{entry.Value} {entry.Key}"));

         rows.AddChild(
            Widgets.Line(
               report.Survived
                  ? $"{report.ExplorerName} came back from {report.Area}, depth {report.Depth}."
                  : $"{report.ExplorerName} did not come back from {report.Area}, depth {report.Depth}.",
               report.Survived ? Widgets.Good : Widgets.Bad,
               size: 14
            )
         );

         if (report.Survived)
         {
            rows.AddChild(Widgets.Line($"    Brought back {report.Gold} gold and {loot}.", Widgets.Quiet, size: 12));
         }
         else
         {
            rows.AddChild(Widgets.Line($"    The {report.Gold} gold and {loot} they had gathered went with them.", Widgets.Quiet, size: 12));

            if (report.GearLost is not null)
               rows.AddChild(Widgets.Line($"    Their {report.GearLost.Material} {report.GearLost.BaseType} (power {report.GearLost.Power}) is gone for good.", Widgets.Bad, size: 12));
         }

         rows.AddChild(Widgets.Line($"    Due back {report.ResolvedAt:yyyy-MM-dd HH:mm} UTC.", Widgets.Quiet, size: 11));
         rows.AddChild(Widgets.Rule());
      }
   }
}
