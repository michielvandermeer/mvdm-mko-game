using Godot;

namespace Underclass.Client.Town;

/// <summary>
///    The only place that knows the Town is isometric.
/// </summary>
/// <remarks>
///    Everything else — the ground, the buildings, the Master Smith's movement — asks this for a position or a
///    direction. That is what makes the spec's requirement honest: swapping these primitives for a real tileset
///    means changing the numbers here and the shapes in <see cref="TownScene" />, and touching no logic at all.
/// </remarks>
public static class IsometricGrid
{
   /// <summary>How wide one tile's diamond is, corner to corner.</summary>
   public const float TileWidth = 128f;

   /// <summary>How tall it is. Half the width, which is the usual 2:1 isometric look.</summary>
   public const float TileHeight = 64f;

   /// <summary>
   ///    Where a grid cell sits on screen. Linear in the cell, which is what lets
   ///    <see cref="Along" /> reuse it for directions.
   /// </summary>
   public static Vector2 ToWorld(Vector2 cell)
   {
      return new Vector2((cell.X - cell.Y) * (TileWidth / 2f), (cell.X + cell.Y) * (TileHeight / 2f));
   }

   /// <inheritdoc cref="ToWorld(Godot.Vector2)" />
   public static Vector2 ToWorld(int column, int row)
   {
      return ToWorld(new Vector2(column, row));
   }

   /// <summary>
   ///    A unit direction on screen for a movement expressed in grid axes. Walking "one along X" is a diagonal on
   ///    screen, which is exactly the point: the Master Smith travels the Town's lanes rather than sliding along
   ///    the screen's own axes.
   /// </summary>
   public static Vector2 Along(Vector2 gridIntent)
   {
      return gridIntent == Vector2.Zero ? Vector2.Zero : ToWorld(gridIntent).Normalized();
   }

   /// <summary>The four corners of one tile's diamond, centred on the cell.</summary>
   public static Vector2[] Diamond(float scale = 1f)
   {
      var halfWidth = TileWidth / 2f * scale;
      var halfHeight = TileHeight / 2f * scale;

      return [
         new Vector2(0, -halfHeight),
         new Vector2(halfWidth, 0),
         new Vector2(0, halfHeight),
         new Vector2(-halfWidth, 0)
      ];
   }
}
