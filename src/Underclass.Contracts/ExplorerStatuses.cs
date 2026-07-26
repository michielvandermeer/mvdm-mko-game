namespace Underclass.Contracts;

/// <summary>
///    The values <see cref="ExplorerResponse.Status" /> can take, and what each of them means for what the player
///    can do with that Explorer.
/// </summary>
/// <remarks>
///    Status travels as a name rather than as a shared enum — see the remarks on <see cref="TownResponse" /> — which
///    leaves every reader comparing against a bare string. These predicates are where that comparison lives, so
///    "is this Explorer dead" is asked once and asked by name. They are extension methods rather than properties on
///    the record on purpose: a computed property would be serialised back out as part of the payload.
/// </remarks>
public static class ExplorerStatuses
{
   /// <summary>Available: can be given Gear and sent out.</summary>
   public const string Idle = "Idle";

   /// <summary>Out on an Expedition, and unavailable until it resolves.</summary>
   public const string OnExpedition = "OnExpedition";

   /// <summary>Dead. Terminal — kept in the roster so the loss is acknowledged, never dispatchable again.</summary>
   public const string Dead = "Dead";

   /// <summary>True when this Explorer can be given Gear or sent out.</summary>
   public static bool IsIdle(this ExplorerResponse explorer) => explorer.Status == Idle;

   /// <summary>True when this Explorer is away on an Expedition.</summary>
   public static bool IsOnExpedition(this ExplorerResponse explorer) => explorer.Status == OnExpedition;

   /// <summary>True when this Explorer is dead, and so permanently ineligible.</summary>
   public static bool IsDead(this ExplorerResponse explorer) => explorer.Status == Dead;

   /// <summary>True while this Explorer still counts against the Town's capacity.</summary>
   public static bool IsAlive(this ExplorerResponse explorer) => !explorer.IsDead();
}
