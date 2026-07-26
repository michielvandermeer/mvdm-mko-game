using System;
using Godot;

namespace Underclass.Client.Town;

/// <summary>
///    The Town itself: ground, two buildings, and the Master Smith walking between them.
/// </summary>
/// <remarks>
///    <para>
///       Built from primitives, with no imported assets at all. The ground is a grid of
///       <see cref="Polygon2D" /> diamonds on their own layer below everything; the buildings and the Master Smith
///       share a Y-sorted layer above it, which is what makes them occlude each other by depth.
///    </para>
///    <para>
///       There is no game state here. The Workshop and the Expedition Board are places with names and a prompt —
///       what they do when used is the HUD's business, and what happens as a result is the server's.
///    </para>
/// </remarks>
public sealed partial class TownScene : Node2D
{
   /// <summary>How many tiles across the Town is, in each grid direction. Wider than a screen, so it can be walked.</summary>
   private const int Span = 15;

   /// <summary>
   ///    The grid row the two buildings sit on, and the lane between them.
   /// </summary>
   /// <remarks>
   ///    A row of constant grid row maps to a diagonal on screen, which is why the buildings share one: it puts them
   ///    at opposite ends of a visible diagonal lane, so crossing the Town is the diagonal navigation story 2 asks
   ///    for rather than a walk up and down a screen column.
   /// </remarks>
   private const int Lane = 0;

   private static readonly Color GroundLight = new(0.29f, 0.33f, 0.27f);
   private static readonly Color GroundDark = new(0.25f, 0.29f, 0.24f);
   private static readonly Color PathShade = new(0.38f, 0.35f, 0.28f);

   private Node2D _sorted = null!;

   /// <summary>Where the Master Smith crafts. Placed off to one side, so the Town has a layout worth crossing.</summary>
   public Building Workshop { get; private set; } = null!;

   /// <summary>Where Explorers are sent out from.</summary>
   public Building ExpeditionBoard { get; private set; } = null!;

   /// <summary>The player.</summary>
   public MasterSmith Smith { get; private set; } = null!;

   /// <summary>
   ///    Raised when the Master Smith walks into or out of somewhere he can use, carrying the place or null. The
   ///    HUD turns this into a prompt.
   /// </summary>
   public event Action<Building?>? StandingAtChanged;

   /// <inheritdoc />
   public override void _Ready()
   {
      AddChild(Ground());

      // One Y-sorted layer for everything that stands up off the ground. Godot sorts these children by their
      // position's Y, and every one of them has its origin on the ground, so depth ordering falls out of where
      // things are rather than out of a z-index anyone has to maintain.
      _sorted = new Node2D { YSortEnabled = true };
      AddChild(_sorted);

      Workshop = Place(
         new Building {
            Title = "The Workshop",
            Invitation = "craft Gear",
            Shade = new Color(0.55f, 0.40f, 0.22f)
         },
         column: -5,
         row: Lane
      );

      ExpeditionBoard = Place(
         new Building {
            Title = "The Expedition Board",
            Invitation = "send an Explorer out",
            Shade = new Color(0.28f, 0.40f, 0.52f)
         },
         column: 5,
         row: Lane
      );

      // Halfway along the lane, so both places are in sight and neither is where you already are.
      Smith = new MasterSmith { Position = IsometricGrid.ToWorld(0, Lane) };
      _sorted.AddChild(Smith);
   }

   private Building Place(Building building, int column, int row)
   {
      building.Position = IsometricGrid.ToWorld(column, row);
      building.SmithPresenceChanged += standing => StandingAtChanged?.Invoke(standing);

      _sorted.AddChild(building);

      return building;
   }

   /// <summary>
   ///    The ground, one diamond per cell, checkered so the grid is legible. Its own node below the sorted layer,
   ///    because tiles never need to be ordered against anything — they are what everything else stands on.
   /// </summary>
   private static Node2D Ground()
   {
      var ground = new Node2D { ZIndex = -1 };
      var diamond = IsometricGrid.Diamond();
      var half = Span / 2;

      for (var column = -half; column <= half; column++)
      {
         for (var row = -half; row <= half; row++)
         {
            ground.AddChild(
               new Polygon2D {
                  Polygon = diamond,
                  Position = IsometricGrid.ToWorld(column, row),
                  Color = ShadeFor(column, row)
               }
            );
         }
      }

      return ground;
   }

   /// <summary>
   ///    A checker, with a lighter lane along the row the two buildings share — so the Town's layout points the
   ///    player from one to the other instead of leaving them to wander.
   /// </summary>
   private static Color ShadeFor(int column, int row)
   {
      if (row == Lane)
         return PathShade;

      return (column + row) % 2 == 0 ? GroundLight : GroundDark;
   }
}
