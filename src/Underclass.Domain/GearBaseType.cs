namespace Underclass.Domain;

/// <summary>
///    The Gear the Master Smith can craft. The slice has one slot and therefore one base type; the enum
///    exists so that adding a second does not change the shape of a single command or payload.
/// </summary>
public enum GearBaseType
{
   Sword
}
