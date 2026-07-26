using Godot;

namespace Underclass.Client;

/// <summary>
///    What the player can press, and what it is bound to.
/// </summary>
/// <remarks>
///    Registered in code rather than authored into project.godot, so the bindings sit next to the movement they
///    drive and are readable without opening the editor. The four walking actions are named after the Town's own
///    axes rather than after screen directions, because that is what they move along — see
///    <see cref="IsometricGrid" />.
/// </remarks>
public static class Actions
{
   /// <summary>Deeper into the Town, to the lower right of the screen.</summary>
   public const string WalkSouthEast = "walk_south_east";

   /// <summary>Back out of the Town, to the upper left.</summary>
   public const string WalkNorthWest = "walk_north_west";

   /// <summary>Across the Town, to the lower left.</summary>
   public const string WalkSouthWest = "walk_south_west";

   /// <summary>Across the Town, to the upper right.</summary>
   public const string WalkNorthEast = "walk_north_east";

   /// <summary>Use whatever the Master Smith is standing at.</summary>
   public const string Interact = "interact";

   /// <summary>Read the Expedition reports.</summary>
   public const string Reports = "reports";

   /// <summary>Put away whatever panel is open.</summary>
   public const string Close = "close";

   /// <summary>Ask the server for the Town again, without waiting for the next poll.</summary>
   public const string Refresh = "refresh";

   /// <summary>Binds every action. Safe to call more than once; existing bindings are replaced.</summary>
   public static void Register()
   {
      Bind(WalkNorthEast, Key.W, Key.Up);
      Bind(WalkSouthWest, Key.S, Key.Down);
      Bind(WalkNorthWest, Key.A, Key.Left);
      Bind(WalkSouthEast, Key.D, Key.Right);
      Bind(Interact, Key.E, Key.Enter);
      Bind(Reports, Key.R);
      Bind(Close, Key.Escape);
      Bind(Refresh, Key.F5);
   }

   private static void Bind(string action, params Key[] keys)
   {
      if (InputMap.HasAction(action))
         InputMap.EraseAction(action);

      InputMap.AddAction(action);

      foreach (var key in keys)
         InputMap.ActionAddEvent(action, new InputEventKey { PhysicalKeycode = key });
   }
}
