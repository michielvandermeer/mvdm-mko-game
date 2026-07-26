using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Underclass.Contracts;

namespace Underclass.Client.Ui;

/// <summary>
///    The Workshop: turn Materials into Gear, and turn Gear you have outgrown back into Materials.
/// </summary>
/// <remarks>
///    <para>
///       Stories 7 to 12 and 33. The Material's cost and the range it produces are shown before the craft, so the
///       choice is legible — and both come from <c>GET /api/crafting/options</c> rather than from anything this
///       panel knows. Where inside that range a piece lands is the server's roll, which is what gives a craft its
///       moment.
///    </para>
///    <para>
///       Nothing here decides whether a craft is possible. The button is greyed when the Town plainly lacks the
///       Material, as an affordance, but the answer that counts is the server's refusal and its wording.
///    </para>
/// </remarks>
public sealed partial class WorkshopPanel : PanelContainer, ITownView
{
   private VBoxContainer _body = null!;
   private CraftingOptionsResponse? _options;
   private TownResponse? _town;
   private string? _chosenMaterial;

   /// <summary>Raised when the player commits to a craft: what to make, and what to make it from.</summary>
   public event Action<string, string>? CraftRequested;

   /// <summary>Raised when the player breaks a piece of Gear down.</summary>
   public event Action<Guid>? SalvageRequested;

   /// <inheritdoc />
   public override void _Ready()
   {
      CustomMinimumSize = new Vector2(520, 0);

      _body = Widgets.Panel(this, padding: 18);
   }

   /// <summary>The catalogue of what can be crafted. Fetched once; it is content, and it does not move.</summary>
   public void Accept(CraftingOptionsResponse options)
   {
      _options = options;
      _chosenMaterial ??= options.Materials.FirstOrDefault()?.Material;

      Render();
   }

   /// <summary>Redraws against a new snapshot of the Town.</summary>
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

      _body.AddChild(Widgets.Heading("The Workshop"));

      if (_options is null || _town is null)
      {
         _body.AddChild(Widgets.Line("waiting for the Town…", Widgets.Quiet));

         return;
      }

      RenderCrafting(_options, _town);
      _body.AddChild(Widgets.Rule());
      RenderGear(_town);
   }

   private void RenderCrafting(CraftingOptionsResponse options, TownResponse town)
   {
      _body.AddChild(Widgets.Line("Choose a Material. It sets what the Gear can be; where inside that range a piece lands is settled when you Craft it.", Widgets.Quiet, size: 12));

      var affordable = false;

      foreach (var material in options.Materials)
      {
         var onHand = town.Materials.GetValueOrDefault(material.Material);
         var enough = onHand >= material.Cost;

         if (material.Material == _chosenMaterial)
            affordable = enough;

         _body.AddChild(
            Widgets.Row(
               $"{material.Material} — costs {material.Cost}, you have {onHand} · power {material.MinPower}–{material.MaxPower}",
               chosen: material.Material == _chosenMaterial,
               enabled: true,
               () => {
                  _chosenMaterial = material.Material;
                  Render();
               }
            )
         );

         if (!enough && material.Material == _chosenMaterial)
            _body.AddChild(Widgets.Line($"    not enough {material.Material} for a craft", Widgets.Bad, size: 12));
      }

      // One base type in the slice, so there is nothing to choose here yet. Taking it from the server anyway is
      // what keeps a second one from needing a client change.
      var baseType = options.BaseTypes.FirstOrDefault() ?? "Sword";

      var craft = Widgets.Press(
         $"Craft a {baseType} from {_chosenMaterial ?? "…"}",
         () => {
            if (_chosenMaterial is not null)
               CraftRequested?.Invoke(baseType, _chosenMaterial);
         }
      );

      // Greyed on what the server said a craft costs against what it said is on hand — an affordance, not a
      // rule. Nothing is decided here: the answer that counts is still the server's refusal and its wording.
      craft.Disabled = _chosenMaterial is null || !affordable;

      _body.AddChild(craft);
   }

   /// <summary>Story 12: everything crafted, gathered in one place, so it can be chosen from or broken down.</summary>
   private void RenderGear(TownResponse town)
   {
      _body.AddChild(Widgets.Line($"Gear in the Town ({town.Gear.Count})", Widgets.Quiet, size: 12));

      if (town.Gear.Count == 0)
      {
         _body.AddChild(Widgets.Line("Nothing on the racks yet.", Widgets.Quiet));

         return;
      }

      var rows = Widgets.Scrolling(_body, height: 200);

      foreach (var gear in town.Gear.OrderByDescending(piece => piece.Power))
      {
         var carrier = town.Explorers.FirstOrDefault(explorer => explorer.EquippedGearId == gear.Id);
         var held = carrier is null ? "on the rack" : $"carried by {carrier.Name}";

         var row = new HBoxContainer();

         row.AddChild(Widgets.Line($"{gear.Material} {gear.BaseType} · power {gear.Power} · {held}", size: 13));

         var spacer = new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill };
         row.AddChild(spacer);

         // Salvage is offered for Gear nobody is carrying. The server refuses the rest, and says so.
         var salvage = Widgets.Press("Salvage", () => SalvageRequested?.Invoke(gear.Id));
         salvage.Disabled = carrier is not null;
         row.AddChild(salvage);

         rows.AddChild(row);
      }

      _body.AddChild(Widgets.Line("Salvage returns less than a craft consumed — it is a floor, not a living.", Widgets.Quiet, size: 11));
   }
}
