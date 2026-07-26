namespace Underclass.Domain;

/// <summary>
///    One Explorer in the roster. A dead Explorer is a row that stays: the status is terminal, the equipped
///    Gear is gone, and the name remains so the loss is acknowledged rather than silently erased.
/// </summary>
/// <param name="Id">Identity of this Explorer.</param>
/// <param name="Name">What the player calls them.</param>
/// <param name="Skill">How much they bring to an Expedition on their own, before Gear.</param>
/// <param name="Status">Whether they can be dispatched, are already out, or are dead.</param>
/// <param name="EquippedGear">The Gear they carry, or null when they carry none.</param>
public sealed record ExplorerState(ExplorerId Id, string Name, int Skill, ExplorerStatus Status, GearId? EquippedGear)
{
   /// <summary>True when this Explorer is neither out nor dead, and so can be given Gear or sent out.</summary>
   public bool IsAvailable => Status == ExplorerStatus.Idle;

   /// <summary>
   ///    Why this Explorer cannot be given Gear or sent out, or null when they can be.
   /// </summary>
   /// <remarks>
   ///    One lookup rather than a check per caller: equipping and dispatch both need to refuse an Explorer who is
   ///    out or dead, and both need to say which it was. Keeping the mapping here is what stops them from
   ///    disagreeing about either, and means a further status could only be added in one place.
   /// </remarks>
   public RejectionReason? UnavailableReason => Status switch {
      ExplorerStatus.Dead => RejectionReason.ExplorerDead,
      ExplorerStatus.OnExpedition => RejectionReason.ExplorerBusy,
      _ => null
   };

   /// <summary>True when this Explorer still counts against the Town's capacity.</summary>
   public bool IsAlive => Status != ExplorerStatus.Dead;
}
