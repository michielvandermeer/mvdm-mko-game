namespace Underclass.Contracts;

/// <param name="BaseType">What to craft.</param>
/// <param name="Material">What to craft it from. Sets the range the result's power lands in.</param>
public sealed record CraftRequest(string BaseType, string Material);

/// <param name="GearId">The Gear to hand to this Explorer.</param>
public sealed record EquipRequest(Guid GearId);

/// <param name="ExplorerId">Who to send.</param>
/// <param name="Area">Where to send them.</param>
/// <param name="Depth">How deep to send them.</param>
public sealed record DispatchRequest(Guid ExplorerId, string Area, int Depth);
