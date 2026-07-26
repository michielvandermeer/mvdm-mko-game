namespace Underclass.Domain;

/// <summary>
///    Why a command was refused. Every rejection carries one of these rather than throwing, so the API can
///    hand the client a reason code to render as text instead of a failure the player has to guess at.
/// </summary>
public enum RejectionReason
{
   /// <summary>Crafting needs more of the chosen Material than the Town has.</summary>
   InsufficientMaterials,

   /// <summary>Recruiting needs more gold than the Town has.</summary>
   InsufficientGold,

   /// <summary>No Gear with that identity exists in the Town.</summary>
   GearNotFound,

   /// <summary>The Gear is equipped to an Explorer, so it cannot be salvaged or given to someone else.</summary>
   GearInUse,

   /// <summary>No Explorer with that identity is in the roster.</summary>
   ExplorerNotFound,

   /// <summary>The Explorer is out on an Expedition.</summary>
   ExplorerBusy,

   /// <summary>The Explorer is dead. Permanently.</summary>
   ExplorerDead,

   /// <summary>The Explorer carries no Gear, and an Expedition without Gear is not a gamble but a certainty.</summary>
   NoGearEquipped,

   /// <summary>The requested depth is outside the range the Area supports.</summary>
   InvalidDepth,

   /// <summary>No such Area exists in the catalogue.</summary>
   UnknownArea,

   /// <summary>The Town already houses as many living Explorers as it can.</summary>
   ExplorerCapReached
}
