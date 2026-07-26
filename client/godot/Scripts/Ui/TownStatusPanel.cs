using System.Linq;
using Godot;
using Underclass.Contracts;

namespace Underclass.Client.Ui;

/// <summary>
///    What the Town has, at a glance, without walking anywhere — story 6.
/// </summary>
/// <remarks>
///    Always on screen, because the decision it supports ("can I afford to craft?") is made on the way to the
///    Workshop rather than once you get there. Every number in it comes straight out of the last
///    <c>GET /api/town</c>; nothing here is derived.
/// </remarks>
public sealed partial class TownStatusPanel : PanelContainer, ITownView
{
   private VBoxContainer _body = null!;

   /// <inheritdoc />
   public override void _Ready()
   {
      CustomMinimumSize = new Vector2(280, 0);

      _body = Widgets.Panel(this);
   }

   /// <summary>Redraws from a snapshot. Called on every read, so it is built rather than patched.</summary>
   public void Accept(TownResponse town)
   {
      Widgets.Clear(_body);

      _body.AddChild(Widgets.Heading("Your Town"));
      _body.AddChild(Widgets.Line($"{town.Gold} gold", Widgets.Gold, size: 16));
      _body.AddChild(Widgets.Rule());

      _body.AddChild(Widgets.Line("Materials", Widgets.Quiet, size: 12));

      // Absent means none, so a Material the Town has run out of is still worth a line: "0 Iron" is what tells
      // the player why the Workshop refused them.
      if (town.Materials.Count == 0)
         _body.AddChild(Widgets.Line("nothing on hand", Widgets.Quiet));

      foreach (var (material, amount) in town.Materials.OrderBy(entry => entry.Key))
         _body.AddChild(Widgets.Line($"{amount} × {material}", amount > 0 ? Widgets.Ink : Widgets.Quiet));

      _body.AddChild(Widgets.Rule());

      var living = town.Explorers.Count(explorer => explorer.IsAlive());
      var lost = town.Explorers.Count - living;

      _body.AddChild(Widgets.Line($"{living} of {town.ExplorerCap} Explorers", Widgets.Quiet, size: 12));

      if (lost > 0)
         _body.AddChild(Widgets.Line($"{lost} lost for good", Widgets.Bad, size: 12));

      _body.AddChild(Widgets.Line($"{town.ArtisanCount} Artisan on wages", Widgets.Quiet, size: 12));
      _body.AddChild(Widgets.Line($"{town.Gear.Count} pieces of Gear", Widgets.Quiet, size: 12));
   }
}
