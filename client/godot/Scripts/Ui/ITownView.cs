using Underclass.Contracts;

namespace Underclass.Client.Ui;

/// <summary>
///    Something that draws a Town.
/// </summary>
/// <remarks>
///    Every panel renders from a whole snapshot rather than patching its own copy of the world, so every panel wants
///    exactly this one method — and the composition root can hand a new Town to all of them without naming any of
///    them. Adding a panel is then a line in <see cref="Hud" />, not an edit in two places.
/// </remarks>
public interface ITownView
{
   /// <summary>Redraws against a Town that has just been read.</summary>
   void Accept(TownResponse town);
}
