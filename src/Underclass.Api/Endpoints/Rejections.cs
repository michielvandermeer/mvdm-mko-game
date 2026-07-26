using Underclass.Contracts;
using Underclass.Domain;

namespace Underclass.Api.Endpoints;

/// <summary>
///    Turns a refusal into a 400 the client can read out loud. A refused command is an ordinary answer, not a
///    failure, so nothing here throws and nothing here returns a 500.
/// </summary>
internal static class Rejections
{
   /// <summary>The code returned when a request names something the game has never heard of.</summary>
   public const string MalformedRequestCode = "MalformedRequest";

   public static IResult ToResult(RejectionReason reason)
   {
      return Results.BadRequest(new RejectionResponse(reason.ToString(), Describe(reason)));
   }

   public static IResult Malformed(string what, string? value)
   {
      return Results.BadRequest(new RejectionResponse(MalformedRequestCode, $"'{value}' is not a {what} this game knows about."));
   }

   /// <summary>
   ///    Parses a name from a request into a domain enum. Rejects numeric input and names outside the enum, so
   ///    a malformed request can never reach the domain as an undefined value.
   /// </summary>
   /// <remarks>
   ///    Matching against the declared names rather than handing the string to <see cref="Enum.TryParse{T}(string,bool,out T)" />
   ///    on its own is the whole point: that method also accepts a number, which would let a request smuggle in an
   ///    ordinal — and an ordinal is a coincidence of how the enum happens to be written down, not something a
   ///    client is entitled to depend on.
   /// </remarks>
   public static bool TryParseName<T>(string? value, out T parsed) where T : struct, Enum
   {
      parsed = default;

      if (value is null || !Enum.GetNames<T>().Contains(value, StringComparer.OrdinalIgnoreCase))
         return false;

      return Enum.TryParse(value, ignoreCase: true, out parsed);
   }

   private static string Describe(RejectionReason reason)
   {
      return reason switch {
         RejectionReason.InsufficientMaterials => $"There is not enough of that Material — a craft needs {Catalogue.CraftCost}.",
         RejectionReason.InsufficientGold => $"There is not enough gold — recruiting costs {Catalogue.RecruitCost}.",
         RejectionReason.GearNotFound => "That Gear is not in the Town.",
         RejectionReason.GearInUse => "An Explorer is carrying that Gear.",
         RejectionReason.ExplorerNotFound => "That Explorer is not in the roster.",
         RejectionReason.ExplorerBusy => "That Explorer is out on an Expedition.",
         RejectionReason.ExplorerDead => "That Explorer is dead.",
         RejectionReason.NoGearEquipped => "That Explorer is carrying no Gear.",
         RejectionReason.InvalidDepth => $"Depth must be between 1 and {Catalogue.MaxDepth}.",
         RejectionReason.UnknownArea => "There is no such Area.",
         RejectionReason.ExplorerCapReached => "The Town has no room for another Explorer.",
         _ => "That cannot be done."
      };
   }
}
