using System.Text.Json;
using mvdmio.Database.PgSQL;
using Underclass.Domain;

namespace Underclass.Data;

/// <summary>
///    Reads and writes the whole Town in one go.
/// </summary>
/// <remarks>
///    A Town is small — a handful of Explorers, a handful of Gear — and the domain treats it as one
///    consistent thing, so this rewrites every child row inside one transaction rather than tracking which
///    ones changed. That trades a little write amplification for having no change-tracking to get wrong, and
///    it is why <see cref="SaveAsync" /> is the only write path.
/// </remarks>
public sealed class TownRepository(DatabaseConnection db)
{
   /// <summary>
   ///    The one player's Town. The slice has no accounts, so identity is a constant rather than something
   ///    threaded through every call — see the Out of Scope section of the prototype spec.
   /// </summary>
   public static readonly Guid TownId = new("2f6c1a54-3c5f-4f2e-9c1d-000000000001");

   private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

   /// <summary>
   ///    Child tables in an order that can be emptied without tripping a foreign key. Rows are inserted back in
   ///    an order that satisfies the same constraints: Gear before the Explorers that carry it, Explorers before
   ///    the Expeditions they are out on.
   /// </summary>
   private static readonly string[] DeletionOrder = [
      "underclass.expedition_reports",
      "underclass.expeditions",
      "underclass.explorers",
      "underclass.gear",
      "underclass.materials"
   ];

   /// <summary>The stored Town, or null when this player has never been seen before.</summary>
   public async Task<TownState?> LoadAsync(CancellationToken ct = default)
   {
      var town = await db.Dapper.QuerySingleOrDefaultAsync<TownRow>(
         """
         SELECT gold           AS gold,
                artisan_count  AS artisanCount,
                explorer_cap   AS explorerCap,
                last_settled_at AS lastSettledAt
         FROM underclass.towns
         WHERE town_id = :townId
         """,
         TownParameter,
         ct: ct
      );

      if (town is null)
         return null;

      var materials = await db.Dapper.QueryAsync<MaterialRow>(
         """
         SELECT material AS material, amount AS amount
         FROM underclass.materials
         WHERE town_id = :townId
         """,
         TownParameter,
         ct: ct
      );

      var gear = await db.Dapper.QueryAsync<GearRow>(
         """
         SELECT gear_id AS gearId, base_type AS baseType, material AS material, power AS power
         FROM underclass.gear
         WHERE town_id = :townId
         """,
         TownParameter,
         ct: ct
      );

      var explorers = await db.Dapper.QueryAsync<ExplorerRow>(
         """
         SELECT explorer_id      AS explorerId,
                name             AS name,
                skill            AS skill,
                status           AS status,
                equipped_gear_id AS equippedGearId
         FROM underclass.explorers
         WHERE town_id = :townId
         ORDER BY name
         """,
         TownParameter,
         ct: ct
      );

      var expeditions = await db.Dapper.QueryAsync<ExpeditionRow>(
         """
         SELECT expedition_id   AS expeditionId,
                explorer_id     AS explorerId,
                gear_id         AS gearId,
                area            AS area,
                depth           AS depth,
                started_at      AS startedAt,
                completes_at    AS completesAt,
                survival_chance AS survivalChance
         FROM underclass.expeditions
         WHERE town_id = :townId
         ORDER BY completes_at
         """,
         TownParameter,
         ct: ct
      );

      var reports = await db.Dapper.QueryAsync<ReportRow>(
         """
         SELECT report_id     AS reportId,
                expedition_id AS expeditionId,
                explorer_id   AS explorerId,
                explorer_name AS explorerName,
                area          AS area,
                depth         AS depth,
                resolved_at   AS resolvedAt,
                survived      AS survived,
                gold          AS gold,
                materials     AS materials,
                lost_gear     AS lostGear
         FROM underclass.expedition_reports
         WHERE town_id = :townId
         ORDER BY resolved_at
         """,
         TownParameter,
         ct: ct
      );

      return new TownState {
         Gold = town.Gold,
         ArtisanCount = town.ArtisanCount,
         ExplorerCap = town.ExplorerCap,
         LastSettledAt = ToOffset(town.LastSettledAt),
         Materials = materials.ToDictionary(row => Parse<MaterialType>(row.Material), row => row.Amount),
         Gear = gear.Select(ToGear).ToList(),
         Explorers = explorers.Select(ToExplorer).ToList(),
         Expeditions = expeditions.Select(ToExpedition).ToList(),
         Reports = reports.Select(ToReport).ToList()
      };
   }

   /// <summary>
   ///    Writes the Town back. Child rows are replaced wholesale: emptied in an order no foreign key objects to,
   ///    then re-inserted in an order that satisfies the same constraints — Gear before the Explorers that carry it,
   ///    Explorers before the Expeditions they are out on.
   /// </summary>
   public async Task SaveAsync(TownState state, CancellationToken ct = default)
   {
      await db.InTransactionAsync(
         async () => {
            await db.Dapper.ExecuteAsync(
               """
               INSERT INTO underclass.towns (town_id, gold, artisan_count, explorer_cap, last_settled_at)
               VALUES (:townId, :gold, :artisanCount, :explorerCap, :lastSettledAt)
               ON CONFLICT (town_id) DO UPDATE
                  SET gold = EXCLUDED.gold,
                      artisan_count = EXCLUDED.artisan_count,
                      explorer_cap = EXCLUDED.explorer_cap,
                      last_settled_at = EXCLUDED.last_settled_at
               """,
               new Dictionary<string, object?> {
                  ["townId"] = TownId,
                  ["gold"] = state.Gold,
                  ["artisanCount"] = state.ArtisanCount,
                  ["explorerCap"] = state.ExplorerCap,
                  ["lastSettledAt"] = state.LastSettledAt.UtcDateTime
               },
               ct: ct
            );

            foreach (var table in DeletionOrder)
               await db.Dapper.ExecuteAsync($"DELETE FROM {table} WHERE town_id = :townId", TownParameter, ct: ct);

            await InsertAllAsync(
               """
               INSERT INTO underclass.gear (gear_id, town_id, base_type, material, power)
               VALUES (:gearId, :townId, :baseType, :material, :power)
               """,
               state.Gear,
               gear => new Dictionary<string, object?> {
                  ["gearId"] = gear.Id.Value,
                  ["baseType"] = gear.BaseType.ToString(),
                  ["material"] = gear.Material.ToString(),
                  ["power"] = gear.Power
               },
               ct
            );

            await InsertAllAsync(
               """
               INSERT INTO underclass.explorers (explorer_id, town_id, name, skill, status, equipped_gear_id)
               VALUES (:explorerId, :townId, :name, :skill, :status, :equippedGearId)
               """,
               state.Explorers,
               explorer => new Dictionary<string, object?> {
                  ["explorerId"] = explorer.Id.Value,
                  ["name"] = explorer.Name,
                  ["skill"] = explorer.Skill,
                  ["status"] = explorer.Status.ToString(),
                  ["equippedGearId"] = explorer.EquippedGear?.Value
               },
               ct
            );

            await InsertAllAsync(
               """
               INSERT INTO underclass.materials (town_id, material, amount)
               VALUES (:townId, :material, :amount)
               """,
               state.Materials,
               entry => new Dictionary<string, object?> {
                  ["material"] = entry.Key.ToString(),
                  ["amount"] = entry.Value
               },
               ct
            );

            await InsertAllAsync(
               """
               INSERT INTO underclass.expeditions (expedition_id, town_id, explorer_id, gear_id, area, depth, started_at, completes_at, survival_chance)
               VALUES (:expeditionId, :townId, :explorerId, :gearId, :area, :depth, :startedAt, :completesAt, :survivalChance)
               """,
               state.Expeditions,
               expedition => new Dictionary<string, object?> {
                  ["expeditionId"] = expedition.Id.Value,
                  ["explorerId"] = expedition.ExplorerId.Value,
                  ["gearId"] = expedition.GearId.Value,
                  ["area"] = expedition.Area.ToString(),
                  ["depth"] = expedition.Depth,
                  ["startedAt"] = expedition.StartedAt.UtcDateTime,
                  ["completesAt"] = expedition.CompletesAt.UtcDateTime,
                  ["survivalChance"] = expedition.SurvivalChance
               },
               ct
            );

            await InsertAllAsync(
               """
               INSERT INTO underclass.expedition_reports (report_id, town_id, expedition_id, explorer_id, explorer_name, area, depth, resolved_at, survived, gold, materials, lost_gear)
               VALUES (:reportId, :townId, :expeditionId, :explorerId, :explorerName, :area, :depth, :resolvedAt, :survived, :gold, CAST(:materials AS jsonb), CAST(:lostGear AS jsonb))
               """,
               state.Reports,
               report => new Dictionary<string, object?> {
                  ["reportId"] = report.Id.Value,
                  ["expeditionId"] = report.ExpeditionId.Value,
                  ["explorerId"] = report.ExplorerId.Value,
                  ["explorerName"] = report.ExplorerName,
                  ["area"] = report.Area.ToString(),
                  ["depth"] = report.Depth,
                  ["resolvedAt"] = report.ResolvedAt.UtcDateTime,
                  ["survived"] = report.Survived,
                  ["gold"] = report.Gold,
                  ["materials"] = SerializeMaterials(report.Materials),
                  ["lostGear"] = SerializeGear(report.GearLost)
               },
               ct
            );
         }
      );
   }

   /// <summary>
   ///    Inserts one row per item, adding the Town's identity to each — every child table has it, and none of the
   ///    callers should have to remember that.
   /// </summary>
   /// <remarks>
   ///    One round trip per row, in sequence. These all run inside <see cref="SaveAsync" />'s single transaction, and
   ///    a Town is a handful of Explorers and a handful of Gear, so the round trips cost less than the machinery for
   ///    batching them would.
   /// </remarks>
   private async Task InsertAllAsync<T>(string sql, IEnumerable<T> rows, Func<T, Dictionary<string, object?>> parameters, CancellationToken ct)
   {
      foreach (var row in rows)
      {
         var values = parameters(row);

         values["townId"] = TownId;

         await db.Dapper.ExecuteAsync(sql, values, ct: ct);
      }
   }

   private static Dictionary<string, object?> TownParameter => new() { ["townId"] = TownId };

   private static GearState ToGear(GearRow row)
   {
      return new GearState(new GearId(row.GearId), Parse<GearBaseType>(row.BaseType), Parse<MaterialType>(row.Material), row.Power);
   }

   private static ExplorerState ToExplorer(ExplorerRow row)
   {
      return new ExplorerState(
         new ExplorerId(row.ExplorerId),
         row.Name,
         row.Skill,
         Parse<ExplorerStatus>(row.Status),
         row.EquippedGearId is null ? null : new GearId(row.EquippedGearId.Value)
      );
   }

   private static ExpeditionState ToExpedition(ExpeditionRow row)
   {
      return new ExpeditionState(
         new ExpeditionId(row.ExpeditionId),
         new ExplorerId(row.ExplorerId),
         new GearId(row.GearId),
         Parse<AreaId>(row.Area),
         row.Depth,
         ToOffset(row.StartedAt),
         ToOffset(row.CompletesAt),
         row.SurvivalChance
      );
   }

   private static ExpeditionReport ToReport(ReportRow row)
   {
      return new ExpeditionReport(
         new ReportId(row.ReportId),
         new ExpeditionId(row.ExpeditionId),
         new ExplorerId(row.ExplorerId),
         row.ExplorerName,
         Parse<AreaId>(row.Area),
         row.Depth,
         ToOffset(row.ResolvedAt),
         row.Survived,
         row.Gold,
         DeserializeMaterials(row.Materials),
         DeserializeGear(row.LostGear)
      );
   }

   /// <summary>
   ///    Npgsql reads and writes <c>timestamptz</c> as a UTC <see cref="DateTime" />; the domain works in
   ///    <see cref="DateTimeOffset" />. The instant is the same either way, so the conversion is total.
   /// </summary>
   private static DateTimeOffset ToOffset(DateTime stored)
   {
      return new DateTimeOffset(DateTime.SpecifyKind(stored, DateTimeKind.Utc));
   }

   /// <summary>
   ///    Enums are stored as their names rather than their numbers, so a renumbered enum cannot silently
   ///    reinterpret existing rows and the tables stay readable by hand.
   /// </summary>
   private static T Parse<T>(string stored) where T : struct, Enum
   {
      return Enum.Parse<T>(stored);
   }

   private static string SerializeMaterials(IReadOnlyDictionary<MaterialType, int> materials)
   {
      return JsonSerializer.Serialize(materials.ToDictionary(entry => entry.Key.ToString(), entry => entry.Value), JsonOptions);
   }

   private static IReadOnlyDictionary<MaterialType, int> DeserializeMaterials(string stored)
   {
      var materials = JsonSerializer.Deserialize<Dictionary<string, int>>(stored, JsonOptions) ?? new Dictionary<string, int>();

      return materials.ToDictionary(entry => Parse<MaterialType>(entry.Key), entry => entry.Value);
   }

   private static string? SerializeGear(GearState? gear)
   {
      return gear is null ? null : JsonSerializer.Serialize(new LostGearJson(gear.Id.Value, gear.BaseType.ToString(), gear.Material.ToString(), gear.Power), JsonOptions);
   }

   private static GearState? DeserializeGear(string? stored)
   {
      if (stored is null)
         return null;

      var gear = JsonSerializer.Deserialize<LostGearJson>(stored, JsonOptions);

      return gear is null ? null : new GearState(new GearId(gear.Id), Parse<GearBaseType>(gear.BaseType), Parse<MaterialType>(gear.Material), gear.Power);
   }

   private sealed record TownRow(long Gold, int ArtisanCount, int ExplorerCap, DateTime LastSettledAt);

   private sealed record MaterialRow(string Material, int Amount);

   private sealed record GearRow(Guid GearId, string BaseType, string Material, int Power);

   private sealed record ExplorerRow(Guid ExplorerId, string Name, int Skill, string Status, Guid? EquippedGearId);

   private sealed record ExpeditionRow(
      Guid ExpeditionId,
      Guid ExplorerId,
      Guid GearId,
      string Area,
      int Depth,
      DateTime StartedAt,
      DateTime CompletesAt,
      double SurvivalChance);

   private sealed record ReportRow(
      Guid ReportId,
      Guid ExpeditionId,
      Guid ExplorerId,
      string ExplorerName,
      string Area,
      int Depth,
      DateTime ResolvedAt,
      bool Survived,
      long Gold,
      string Materials,
      string? LostGear);

   private sealed record LostGearJson(Guid Id, string BaseType, string Material, int Power);
}
