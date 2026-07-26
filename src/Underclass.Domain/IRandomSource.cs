namespace Underclass.Domain;

/// <summary>
///    Every roll the game makes comes from here. Nothing in the domain touches <c>Random.Shared</c>, which
///    is what lets a test pin an Expedition's outcome — and what keeps the roll on the server, where a
///    Marketplace can eventually trust it.
/// </summary>
public interface IRandomSource
{
   /// <summary>A value in [0, 1). Used for survival rolls.</summary>
   double NextDouble();

   /// <summary>A value in [<paramref name="minInclusive" />, <paramref name="maxExclusive" />).</summary>
   int Next(int minInclusive, int maxExclusive);
}
