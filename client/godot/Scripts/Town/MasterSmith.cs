using Godot;

namespace Underclass.Client.Town;

/// <summary>
///    The player, resident in their own Town.
/// </summary>
/// <remarks>
///    Moves along the Town's axes rather than the screen's: the keys are read as a direction in grid space and
///    handed to <see cref="IsometricGrid.Along" />, so every step is a diagonal on screen and walking reads as
///    navigating the place. The body's origin is at his feet, which is what the parent's Y-sorting uses to decide
///    whether he is in front of a building or behind it.
/// </remarks>
public sealed partial class MasterSmith : CharacterBody2D
{
   /// <summary>Pixels a second. Brisk enough that crossing the Town is not a chore.</summary>
   private const float Speed = 300f;

   /// <summary>How far he stands from the ground, in the fiction of the projection.</summary>
   private const float BodyHeight = 62f;

   /// <inheritdoc />
   public override void _Ready()
   {
      AddChild(Shadow());
      AddChild(Body());
      AddChild(Head());
      AddChild(new CollisionShape2D { Shape = new CircleShape2D { Radius = 18f } });
      AddChild(Lens());
   }

   /// <inheritdoc />
   public override void _PhysicsProcess(double delta)
   {
      // Read in grid axes, not screen axes. "North-east" is a real direction in the Town; "up" is not.
      var intent = new Vector2(
         Input.GetAxis(Actions.WalkNorthWest, Actions.WalkSouthEast),
         Input.GetAxis(Actions.WalkNorthEast, Actions.WalkSouthWest)
      );

      Velocity = IsometricGrid.Along(intent) * Speed;

      MoveAndSlide();
   }

   /// <summary>A flattened diamond on the ground, which is what sells standing on an isometric plane.</summary>
   private static Polygon2D Shadow()
   {
      return new Polygon2D {
         Polygon = IsometricGrid.Diamond(scale: 0.28f),
         Color = new Color(0, 0, 0, 0.28f)
      };
   }

   private static Polygon2D Body()
   {
      return new Polygon2D {
         Polygon = [
            new Vector2(-13, 0),
            new Vector2(13, 0),
            new Vector2(10, -BodyHeight + 16),
            new Vector2(-10, -BodyHeight + 16)
         ],
         Color = new Color(0.62f, 0.24f, 0.20f)
      };
   }

   private static Polygon2D Head()
   {
      return new Polygon2D {
         Polygon = [
            new Vector2(-9, -BodyHeight + 16),
            new Vector2(9, -BodyHeight + 16),
            new Vector2(9, -BodyHeight),
            new Vector2(-9, -BodyHeight)
         ],
         Color = new Color(0.85f, 0.72f, 0.58f)
      };
   }

   /// <summary>
   ///    The camera rides along, smoothed, so the Town scrolls under the player instead of the player crossing a
   ///    fixed frame.
   /// </summary>
   private static Camera2D Lens()
   {
      return new Camera2D {
         PositionSmoothingEnabled = true,
         PositionSmoothingSpeed = 6f
      };
   }
}
