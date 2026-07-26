namespace Underclass.Contracts;

/// <summary>
///    One Area, with a row per depth so the client can show what each depth costs and returns without
///    knowing any of the rules that produce those numbers.
/// </summary>
/// <param name="Id">The Area's identifier, as used in dispatch and preview requests.</param>
/// <param name="Name">The name to show the player.</param>
/// <param name="Depths">What the Area is like at each depth it supports, shallowest first.</param>
public sealed record AreaResponse(string Id, string Name, List<AreaDepthResponse> Depths);

/// <summary>
///    What the Master Smith can make, and what each Material would make of it. The counterpart of
///    <see cref="AreaResponse" /> for the Workshop: it exists so a craft can be presented as a decision — this
///    much Material, for a result somewhere in this range — by a client that knows none of the rules behind
///    those numbers.
/// </summary>
/// <param name="BaseTypes">The kinds of Gear that can be crafted, as used in a craft request.</param>
/// <param name="Materials">What each Material costs and produces, in ascending tier.</param>
public sealed record CraftingOptionsResponse(List<string> BaseTypes, List<MaterialOptionResponse> Materials);

/// <param name="Material">The Material, as used in a craft request.</param>
/// <param name="Cost">How much of it one craft consumes.</param>
/// <param name="MinPower">The weakest Gear this Material can produce.</param>
/// <param name="MaxPower">The strongest. A craft lands somewhere between the two.</param>
public sealed record MaterialOptionResponse(string Material, int Cost, int MinPower, int MaxPower);

/// <param name="Depth">How deep this row describes.</param>
/// <param name="Duration">How long an Expedition to this depth takes.</param>
/// <param name="Threat">What the Explorer's skill and Gear are measured against.</param>
/// <param name="Gold">The gold a successful Expedition returns.</param>
/// <param name="Loot">The Materials a successful Expedition returns.</param>
public sealed record AreaDepthResponse(int Depth, TimeSpan Duration, int Threat, long Gold, Dictionary<string, int> Loot);
