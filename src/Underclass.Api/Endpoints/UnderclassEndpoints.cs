using Underclass.Contracts;
using Underclass.Domain;

namespace Underclass.Api.Endpoints;

/// <summary>
///    The HTTP surface: a thin translation of the domain's command seam. Every command answers with the Town
///    it produced, so the client re-renders from one payload instead of patching its own copy of the world.
/// </summary>
internal static class UnderclassEndpoints
{
   public static void MapUnderclassApi(this WebApplication app)
   {
      var api = app.MapGroup("/api");

      api.MapGet("/town", ReadTownAsync);
      api.MapGet("/areas", () => Results.Ok(ContractMapper.Areas()));
      api.MapGet("/crafting/options", () => Results.Ok(ContractMapper.CraftingOptions()));
      api.MapPost("/crafting/craft", CraftAsync);
      api.MapPost("/explorers/recruit", RecruitAsync);
      api.MapPost("/explorers/{explorerId:guid}/equip", EquipAsync);
      api.MapPost("/expeditions", DispatchAsync);
      api.MapPost("/expeditions/preview", PreviewAsync);
      api.MapPost("/gear/{gearId:guid}/salvage", SalvageAsync);
   }

   /// <summary>
   ///    The whole Town. Settles Artisan accrual and resolves any Expedition that has come due, which is why
   ///    this is the path that makes downtime harmless.
   /// </summary>
   private static async Task<IResult> ReadTownAsync(TownService towns, CancellationToken ct)
   {
      var snapshot = await towns.ReadAsync(ct);

      return Results.Ok(snapshot.ToContract());
   }

   private static async Task<IResult> CraftAsync(CraftRequest request, TownService towns, CancellationToken ct)
   {
      if (!Rejections.TryParseName<GearBaseType>(request.BaseType, out var baseType))
         return Rejections.Malformed("kind of Gear", request.BaseType);

      if (!Rejections.TryParseName<MaterialType>(request.Material, out var material))
         return Rejections.Malformed("Material", request.Material);

      var (result, town) = await towns.ExecuteAsync(underclass => underclass.CraftGear(baseType, material), ct);

      if (!result.Ok)
         return Rejections.ToResult(result.Reason!.Value);

      var crafted = town.Gear.Single(gear => gear.Id == result.Value);

      return Results.Ok(new CraftResponse(crafted.ToContract(), town.ToContract()));
   }

   private static async Task<IResult> RecruitAsync(TownService towns, CancellationToken ct)
   {
      var (result, town) = await towns.ExecuteAsync(underclass => underclass.RecruitExplorer(), ct);

      return result.Ok ? Results.Ok(town.ToContract()) : Rejections.ToResult(result.Reason!.Value);
   }

   private static async Task<IResult> EquipAsync(Guid explorerId, EquipRequest request, TownService towns, CancellationToken ct)
   {
      var (result, town) = await towns.ExecuteAsync(underclass => underclass.EquipGear(new ExplorerId(explorerId), new GearId(request.GearId)), ct);

      return result.Ok ? Results.Ok(town.ToContract()) : Rejections.ToResult(result.Reason!.Value);
   }

   private static async Task<IResult> SalvageAsync(Guid gearId, TownService towns, CancellationToken ct)
   {
      var (result, town) = await towns.ExecuteAsync(underclass => underclass.SalvageGear(new GearId(gearId)), ct);

      return result.Ok ? Results.Ok(town.ToContract()) : Rejections.ToResult(result.Reason!.Value);
   }

   private static async Task<IResult> DispatchAsync(DispatchRequest request, TownService towns, CancellationToken ct)
   {
      if (!Rejections.TryParseName<AreaId>(request.Area, out var area))
         return Rejections.Malformed("Area", request.Area);

      var (result, town) = await towns.DispatchAsync(new ExplorerId(request.ExplorerId), area, request.Depth, ct);

      if (!result.Ok)
         return Rejections.ToResult(result.Reason!.Value);

      return Results.Ok(new DispatchResponse(result.Value.ToContract(), town.ToContract()));
   }

   /// <summary>
   ///    The honest estimate for a proposed Expedition. Creates nothing, and is computed here rather than in the
   ///    client so that the odds the player agrees to are the odds the server rolls against.
   /// </summary>
   private static async Task<IResult> PreviewAsync(DispatchRequest request, TownService towns, CancellationToken ct)
   {
      if (!Rejections.TryParseName<AreaId>(request.Area, out var area))
         return Rejections.Malformed("Area", request.Area);

      var (result, _) = await towns.ExecuteAsync(underclass => underclass.PreviewExpedition(new ExplorerId(request.ExplorerId), area, request.Depth), ct);

      return result.Ok ? Results.Ok(result.Value.ToContract()) : Rejections.ToResult(result.Reason!.Value);
   }
}
