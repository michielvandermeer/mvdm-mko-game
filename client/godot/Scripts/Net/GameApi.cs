using System;
using System.Text.Json;
using System.Threading.Tasks;
using Godot;
using Underclass.Contracts;

namespace Underclass.Client.Net;

/// <summary>
///    The only way this client learns anything or changes anything.
/// </summary>
/// <remarks>
///    <para>
///       Every method here is a command or a query on the server, and every one of them answers with the Town the
///       server now holds. Nothing in this class computes an outcome, rolls anything, or decides whether something
///       is allowed — a refusal arrives as a 400 carrying a sentence, and gets shown to the player as-is.
///    </para>
///    <para>
///       Requests go through Godot's own <see cref="HttpRequest" /> rather than <c>System.Net.Http</c>, because its
///       completion signal is delivered on the main thread. Touching the scene tree from a thread-pool
///       continuation is how a client like this acquires intermittent crashes, and a prototype cannot afford to
///       spend its debugging budget on that. <see cref="JsonSerializer" /> still does the parsing.
///    </para>
/// </remarks>
public sealed partial class GameApi : Node
{
   /// <summary>Matches the casing ASP.NET Core's minimal APIs serialise with.</summary>
   private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

   private static readonly string[] JsonHeaders = ["Content-Type: application/json"];

   /// <summary>
   ///    Where the API is. One hardcoded player on one machine — there is no authentication and nothing to
   ///    discover, so this is a constant rather than a setting.
   /// </summary>
   public string BaseUrl { get; set; } = "http://localhost:5268";

   /// <summary>The whole Town. Settles accrual and resolves due Expeditions as a side effect of being read.</summary>
   public Task<ApiResult<TownResponse>> ReadTownAsync()
   {
      return SendAsync<TownResponse>(HttpClient.Method.Get, "/api/town", body: null);
   }

   /// <summary>The Areas, priced per depth, so the risk dial can be shown without the client knowing the rules.</summary>
   public Task<ApiResult<AreaResponse[]>> ReadAreasAsync()
   {
      return SendAsync<AreaResponse[]>(HttpClient.Method.Get, "/api/areas", body: null);
   }

   /// <summary>What can be crafted, and what each Material would make of it.</summary>
   public Task<ApiResult<CraftingOptionsResponse>> ReadCraftingOptionsAsync()
   {
      return SendAsync<CraftingOptionsResponse>(HttpClient.Method.Get, "/api/crafting/options", body: null);
   }

   public Task<ApiResult<CraftResponse>> CraftAsync(string baseType, string material)
   {
      return SendAsync<CraftResponse>(HttpClient.Method.Post, "/api/crafting/craft", new CraftRequest(baseType, material));
   }

   public Task<ApiResult<TownResponse>> EquipAsync(Guid explorerId, Guid gearId)
   {
      return SendAsync<TownResponse>(HttpClient.Method.Post, $"/api/explorers/{explorerId}/equip", new EquipRequest(gearId));
   }

   public Task<ApiResult<TownResponse>> SalvageAsync(Guid gearId)
   {
      return SendAsync<TownResponse>(HttpClient.Method.Post, $"/api/gear/{gearId}/salvage", body: null);
   }

   public Task<ApiResult<TownResponse>> RecruitAsync()
   {
      return SendAsync<TownResponse>(HttpClient.Method.Post, "/api/explorers/recruit", body: null);
   }

   /// <summary>
   ///    The honest estimate for an Expedition that has not been sent. Asked for before every dispatch, so the odds
   ///    the player agrees to are the server's rather than something this client worked out.
   /// </summary>
   public Task<ApiResult<PreviewResponse>> PreviewAsync(Guid explorerId, string area, int depth)
   {
      return SendAsync<PreviewResponse>(HttpClient.Method.Post, "/api/expeditions/preview", new DispatchRequest(explorerId, area, depth));
   }

   public Task<ApiResult<DispatchResponse>> DispatchAsync(Guid explorerId, string area, int depth)
   {
      return SendAsync<DispatchResponse>(HttpClient.Method.Post, "/api/expeditions", new DispatchRequest(explorerId, area, depth));
   }

   private async Task<ApiResult<T>> SendAsync<T>(HttpClient.Method method, string path, object? body)
   {
      // One request node per call: an HttpRequest handles a single request at a time, and the panels can easily
      // ask for two things at once.
      var request = new HttpRequest();
      AddChild(request);

      try
      {
         var payload = body is null ? string.Empty : JsonSerializer.Serialize(body, Json);
         var dispatched = request.Request($"{BaseUrl}{path}", JsonHeaders, method, payload);

         if (dispatched != Error.Ok)
            return ApiResult<T>.Failed($"Could not send the request to {BaseUrl} ({dispatched}).");

         var completed = await ToSignal(request, HttpRequest.SignalName.RequestCompleted);

         return Interpret<T>(completed);
      }
      catch (JsonException exception)
      {
         return ApiResult<T>.Failed($"The Town's answer could not be read: {exception.Message}");
      }
      finally
      {
         request.QueueFree();
      }
   }

   /// <summary>
   ///    Turns the completion signal into either a value or a sentence. Nothing is retried and nothing is
   ///    reconnected — the slice puts resilience out of scope, so an unreachable API is reported and that is all.
   /// </summary>
   private static ApiResult<T> Interpret<T>(Variant[] completed)
   {
      var outcome = (HttpRequest.Result)(long)completed[0];
      var status = (long)completed[1];
      var body = completed[3].AsByteArray();

      if (outcome != HttpRequest.Result.Success)
         return ApiResult<T>.Failed($"The Town could not be reached ({outcome}). Is the API running?");

      if (status is >= 200 and < 300)
      {
         var value = JsonSerializer.Deserialize<T>(body, Json);

         return value is null
            ? ApiResult<T>.Failed("The Town answered with nothing at all.")
            : ApiResult<T>.Succeeded(value);
      }

      // A refused command is an ordinary answer and carries its own words. Anything else is a genuine fault, and
      // saying so plainly beats dressing it up as a game message.
      var rejection = TryReadRejection(body);

      return ApiResult<T>.Failed(rejection ?? $"The Town refused that ({status}).");
   }

   private static string? TryReadRejection(byte[] body)
   {
      try
      {
         return JsonSerializer.Deserialize<RejectionResponse>(body, Json)?.Message;
      }
      catch (JsonException)
      {
         return null;
      }
   }
}
