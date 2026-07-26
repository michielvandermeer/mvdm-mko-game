namespace Underclass.Domain;

/// <summary>
///    The production <see cref="IRandomSource" />. Takes an optional seed so a run can be reproduced, and
///    locks around the underlying <see cref="Random" /> because the API resolves Expeditions from both
///    request threads and the scheduled job.
/// </summary>
public sealed class SeededRandomSource : IRandomSource
{
   private readonly Lock _lock = new();
   private readonly Random _random;

   /// <param name="seed">A seed to reproduce a run with, or null for an unpredictable sequence.</param>
   public SeededRandomSource(int? seed = null)
   {
      _random = seed is null ? new Random() : new Random(seed.Value);
   }

   /// <inheritdoc />
   public double NextDouble()
   {
      lock (_lock)
      {
         return _random.NextDouble();
      }
   }

   /// <inheritdoc />
   public int Next(int minInclusive, int maxExclusive)
   {
      lock (_lock)
      {
         return _random.Next(minInclusive, maxExclusive);
      }
   }
}
