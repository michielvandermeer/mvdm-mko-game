using Underclass.Contracts;
using Underclass.Domain;

namespace Underclass.Api.Endpoints;

/// <summary>
///    Turns domain state into wire contracts. One direction only: requests are parsed at the endpoint, and
///    nothing here decides anything the domain has not already decided.
/// </summary>
internal static class ContractMapper
{
   public static TownResponse ToContract(this TownSnapshot snapshot)
   {
      return new TownResponse(
         snapshot.ServerTime,
         snapshot.Gold,
         snapshot.ArtisanCount,
         snapshot.ExplorerCap,
         ToContract(snapshot.Materials),
         snapshot.Gear.Select(ToContract).ToList(),
         snapshot.Explorers.Select(ToContract).ToList(),
         snapshot.Expeditions.Select(ToContract).ToList(),
         snapshot.Reports.Select(ToContract).ToList()
      );
   }

   public static GearResponse ToContract(this GearState gear)
   {
      return new GearResponse(gear.Id.Value, gear.BaseType.ToString(), gear.Material.ToString(), gear.Power);
   }

   public static ExplorerResponse ToContract(this ExplorerState explorer)
   {
      return new ExplorerResponse(explorer.Id.Value, explorer.Name, explorer.Skill, explorer.Status.ToString(), explorer.EquippedGear?.Value);
   }

   public static ExpeditionResponse ToContract(this ExpeditionState expedition)
   {
      return new ExpeditionResponse(
         expedition.Id.Value,
         expedition.ExplorerId.Value,
         expedition.GearId.Value,
         expedition.Area.ToString(),
         expedition.Depth,
         expedition.StartedAt,
         expedition.CompletesAt,
         expedition.SurvivalChance
      );
   }

   public static ReportResponse ToContract(this ExpeditionReport report)
   {
      return new ReportResponse(
         report.Id.Value,
         report.ExpeditionId.Value,
         report.ExplorerId.Value,
         report.ExplorerName,
         report.Area.ToString(),
         report.Depth,
         report.ResolvedAt,
         report.Survived,
         report.Gold,
         ToContract(report.Materials),
         report.GearLost?.ToContract()
      );
   }

   public static PreviewResponse ToContract(this ExpeditionPreview preview)
   {
      return new PreviewResponse(
         preview.ExplorerId.Value,
         preview.Area.ToString(),
         preview.Depth,
         preview.SurvivalChance,
         preview.Duration,
         preview.Gold,
         ToContract(preview.Loot)
      );
   }

   /// <summary>
   ///    What can be crafted and what each Material would make of it, so the Workshop can show the choice rather
   ///    than just a button. The range is published; where inside it a craft lands is still the server's roll.
   /// </summary>
   public static CraftingOptionsResponse CraftingOptions()
   {
      return new CraftingOptionsResponse(
         Enum.GetNames<GearBaseType>().ToList(),
         Enum.GetValues<MaterialType>()
            .Select(
               material => {
                  var (min, max) = Catalogue.PowerRange(material);

                  return new MaterialOptionResponse(material.ToString(), Catalogue.CraftCost, min, max);
               }
            )
            .ToList()
      );
   }

   /// <summary>The whole Area catalogue, expanded per depth so the client can price a decision without rules.</summary>
   public static List<AreaResponse> Areas()
   {
      return Catalogue.Areas
         .Select(
            area => new AreaResponse(
               area.Id.ToString(),
               area.Name,
               Enumerable.Range(1, Catalogue.MaxDepth)
                  .Select(depth => new AreaDepthResponse(depth, area.DurationAt(depth), area.ThreatAt(depth), area.GoldAt(depth), ToContract(area.LootAt(depth))))
                  .ToList()
            )
         )
         .ToList();
   }

   private static Dictionary<string, int> ToContract(IReadOnlyDictionary<MaterialType, int> materials)
   {
      return materials.ToDictionary(entry => entry.Key.ToString(), entry => entry.Value);
   }
}
