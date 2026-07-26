namespace Underclass.Domain;

/// <summary>
///    Where an Explorer stands with respect to being dispatchable. <see cref="Dead" /> is terminal: a dead
///    Explorer is kept in the roster so the loss is acknowledged, and is permanently ineligible for
///    dispatch and equipping.
/// </summary>
public enum ExplorerStatus
{
   Idle,
   OnExpedition,
   Dead
}
