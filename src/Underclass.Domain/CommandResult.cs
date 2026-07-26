namespace Underclass.Domain;

/// <summary>
///    The outcome of a command that either happened or was refused for a stated reason. Commands do not
///    throw on refusal: a player asking for something the rules disallow is ordinary, and the reason is
///    part of the answer.
/// </summary>
public readonly record struct CommandResult
{
   private CommandResult(bool ok, RejectionReason? reason)
   {
      Ok = ok;
      Reason = reason;
   }

   /// <summary>True when the command was carried out.</summary>
   public bool Ok { get; }

   /// <summary>Why the command was refused, or null when it was carried out.</summary>
   public RejectionReason? Reason { get; }

   public static CommandResult Accepted() => new(ok: true, reason: null);

   public static CommandResult Rejected(RejectionReason reason) => new(ok: false, reason);
}

/// <summary>
///    The outcome of a command that produces something — the identity of the Gear that was crafted, the
///    Expedition that was dispatched. <see cref="Value" /> is meaningful only when <see cref="Ok" /> is true.
/// </summary>
/// <typeparam name="T">What the command produces when it succeeds.</typeparam>
public readonly record struct CommandResult<T>
{
   private CommandResult(bool ok, RejectionReason? reason, T value)
   {
      Ok = ok;
      Reason = reason;
      Value = value;
   }

   /// <summary>True when the command was carried out.</summary>
   public bool Ok { get; }

   /// <summary>Why the command was refused, or null when it was carried out.</summary>
   public RejectionReason? Reason { get; }

   /// <summary>What the command produced. Undefined when <see cref="Ok" /> is false.</summary>
   public T Value { get; }

   public static CommandResult<T> Accepted(T value) => new(ok: true, reason: null, value);

   public static CommandResult<T> Rejected(RejectionReason reason) => new(ok: false, reason, value: default!);
}
