using Godot;

namespace Underclass.Client.Town;

/// <summary>
///    A place in the Town the Master Smith walks to and uses.
/// </summary>
/// <remarks>
///    <para>
///       Drawn as an isometric box out of three <see cref="Polygon2D" /> faces: a top diamond and the two sides
///       that face the camera. Its origin is at the centre of its footprint, on the ground, so the Y-sorting its
///       parent does puts the Master Smith in front of it when he is nearer the camera and behind it when he is
///       not — which is all story 3 asks for.
///    </para>
///    <para>
///       The footprint is solid, so the Town has shape you have to walk around. A wider
///       <see cref="Area2D" /> around it is what raises the prompt: standing next to the door is enough, and
///       nothing needs to be aimed at.
///    </para>
/// </remarks>
public sealed partial class Building : Node2D
{
   /// <summary>How tall the box stands above its footprint.</summary>
   private const float Height = 150f;

   /// <summary>How close the Master Smith has to be for the prompt to appear.</summary>
   private const float ReachRadius = 110f;

   private bool _smithIsHere;

   /// <summary>What the player calls this place.</summary>
   public required string Title { get; init; }

   /// <summary>What the prompt offers when the Master Smith is standing here — "Craft Gear", say.</summary>
   public required string Invitation { get; init; }

   /// <summary>The face colour. The two Areas of the slice have no art, so a building is told apart by its hue.</summary>
   public required Color Shade { get; init; }

   /// <summary>Raised when the Master Smith arrives or leaves, so the HUD can offer or withdraw the prompt.</summary>
   public event System.Action<Building?>? SmithPresenceChanged;

   /// <inheritdoc />
   public override void _Ready()
   {
      var footprint = IsometricGrid.Diamond(scale: 1.6f);

      AddChild(Face(TopFace(footprint), Shade.Lightened(0.25f)));
      AddChild(Face(SideFace(footprint, from: 3, to: 2), Shade.Darkened(0.35f)));
      AddChild(Face(SideFace(footprint, from: 2, to: 1), Shade.Darkened(0.15f)));

      AddChild(Nameplate());
      AddChild(Footing(footprint));
      AddChild(Reach());
   }

   /// <summary>True while the Master Smith is standing close enough to use this place.</summary>
   public bool SmithIsHere => _smithIsHere;

   /// <summary>The lid of the box: the footprint diamond, lifted.</summary>
   private static Vector2[] TopFace(Vector2[] footprint)
   {
      var top = new Vector2[footprint.Length];

      for (var corner = 0; corner < footprint.Length; corner++)
         top[corner] = footprint[corner] + new Vector2(0, -Height);

      return top;
   }

   /// <summary>
   ///    One wall, from one footprint corner to the next: along the ground, then up, then back along the lifted
   ///    edge.
   /// </summary>
   private static Vector2[] SideFace(Vector2[] footprint, int from, int to)
   {
      var lift = new Vector2(0, -Height);

      return [footprint[from], footprint[to], footprint[to] + lift, footprint[from] + lift];
   }

   private static Polygon2D Face(Vector2[] points, Color colour)
   {
      return new Polygon2D {
         Polygon = points,
         Color = colour
      };
   }

   private Label Nameplate()
   {
      var label = new Label {
         Text = Title,
         // Centred over the roof. Godot lays a Label out from its top-left, so the box is placed rather than
         // anchored: there is no container involved out here in the world.
         Position = new Vector2(-90, -Height - 56),
         CustomMinimumSize = new Vector2(180, 0),
         HorizontalAlignment = HorizontalAlignment.Center
      };

      label.AddThemeColorOverride("font_color", new Color(0.94f, 0.93f, 0.88f));
      label.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.75f));
      label.AddThemeConstantOverride("outline_size", 6);

      return label;
   }

   /// <summary>The solid part, so the building cannot be walked through.</summary>
   private static StaticBody2D Footing(Vector2[] footprint)
   {
      var body = new StaticBody2D();

      body.AddChild(new CollisionPolygon2D { Polygon = footprint });

      return body;
   }

   /// <summary>The area that notices the Master Smith standing here.</summary>
   private Area2D Reach()
   {
      var reach = new Area2D();

      reach.AddChild(new CollisionShape2D { Shape = new CircleShape2D { Radius = ReachRadius } });

      reach.BodyEntered += body => {
         if (body is MasterSmith)
            SetSmithPresence(present: true);
      };

      reach.BodyExited += body => {
         if (body is MasterSmith)
            SetSmithPresence(present: false);
      };

      return reach;
   }

   private void SetSmithPresence(bool present)
   {
      if (_smithIsHere == present)
         return;

      _smithIsHere = present;

      SmithPresenceChanged?.Invoke(present ? this : null);
   }
}
