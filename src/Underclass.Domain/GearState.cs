namespace Underclass.Domain;

/// <summary>
///    A crafted piece of Gear. Its power was rolled once, at the moment of crafting, inside the range its
///    Material implied — nothing changes it afterwards.
/// </summary>
/// <param name="Id">Identity of this piece.</param>
/// <param name="BaseType">What was crafted.</param>
/// <param name="Material">What it was crafted from. Determines the range <paramref name="Power" /> fell in.</param>
/// <param name="Power">The one stat that matters, added to its Explorer's skill when facing an Area.</param>
public sealed record GearState(GearId Id, GearBaseType BaseType, MaterialType Material, int Power);
