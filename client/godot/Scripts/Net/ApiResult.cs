namespace Underclass.Client.Net;

/// <summary>
///    What came back from the server: either the thing that was asked for, or a sentence explaining why not.
/// </summary>
/// <remarks>
///    A refused command and an unreachable server both land here as a <see cref="Problem" />, because the client
///    does the same thing with either — shows it to the player. It is deliberately not an exception: being told
///    "there is not enough Copper" is an ordinary part of playing, not a fault.
/// </remarks>
/// <typeparam name="T">What the call produces when it works.</typeparam>
public readonly record struct ApiResult<T>
{
   private ApiResult(T? value, string? problem)
   {
      Value = value;
      Problem = problem;
   }

   /// <summary>The answer, or null when there is a <see cref="Problem" />.</summary>
   public T? Value { get; }

   /// <summary>What to tell the player, or null when the call worked.</summary>
   public string? Problem { get; }

   /// <summary>True when <see cref="Value" /> is there to be used.</summary>
   public bool Ok => Problem is null && Value is not null;

   public static ApiResult<T> Succeeded(T value) => new(value, problem: null);

   public static ApiResult<T> Failed(string problem) => new(value: default, problem);
}
