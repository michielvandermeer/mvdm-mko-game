using mvdmio.ASP.Jobs;
using Underclass.Api.Jobs;
using Underclass.Data;
using Underclass.Domain;

namespace Underclass.Api;

/// <summary>
///    Runs commands against the stored Town: load it, ask the domain, store what changed, hand back what the
///    player should now see. This is the only place the domain and the database meet, and the only place that
///    knows a Town has to be created before it can be read.
/// </summary>
public sealed class TownService(TownRepository repository, TimeProvider clock, IRandomSource random, IJobScheduler jobs)
{
   /// <summary>
   ///    Reads the Town, settling accruals and resolving anything that came due while nobody was looking. A
   ///    read can therefore change the world — which is exactly why an Expedition resolves correctly after the
   ///    server has been down.
   /// </summary>
   public Task<TownSnapshot> ReadAsync(CancellationToken ct = default)
   {
      return WithTownAsync(town => town.Snapshot(), ct);
   }

   /// <summary>Runs one command and returns both its outcome and the Town it produced.</summary>
   public Task<(CommandResult Result, TownSnapshot Town)> ExecuteAsync(Func<Town, CommandResult> command, CancellationToken ct = default)
   {
      return WithTownAsync(town => (command(town), town.Snapshot()), ct);
   }

   /// <inheritdoc cref="ExecuteAsync(System.Func{Underclass.Domain.Town,Underclass.Domain.CommandResult},System.Threading.CancellationToken)" />
   public Task<(CommandResult<T> Result, TownSnapshot Town)> ExecuteAsync<T>(Func<Town, CommandResult<T>> command, CancellationToken ct = default)
   {
      return WithTownAsync(town => (command(town), town.Snapshot()), ct);
   }

   /// <summary>
   ///    Sends an Explorer out and books the job that will resolve the Expedition when it comes due. The job is
   ///    only there for timeliness: resolution also happens on the next read, so a job that never runs delays
   ///    the news rather than losing it.
   /// </summary>
   public async Task<(CommandResult<ExpeditionState> Result, TownSnapshot Town)> DispatchAsync(ExplorerId explorerId, AreaId area, int depth, CancellationToken ct = default)
   {
      var (result, snapshot) = await ExecuteAsync(town => town.Dispatch(explorerId, area, depth), ct);

      if (result.Ok)
      {
         await jobs.PerformAtAsync<ResolveExpeditionJob, ResolveExpeditionJob.Parameters>(
            result.Value.CompletesAt.UtcDateTime,
            new ResolveExpeditionJob.Parameters { ExpeditionId = result.Value.Id.Value },
            ct
         );
      }

      return (result, snapshot);
   }

   /// <summary>
   ///    The one sequence every path through this class takes: take the Town up, do something with it, and write it
   ///    back if it moved.
   /// </summary>
   /// <remarks>
   ///    A read goes through here too, and is saved just like a command, because settling the Town is a change in
   ///    its own right — as is a command that was refused, since the settling still happened. Where a caller wants
   ///    both an outcome and a snapshot, the tuple it returns is what orders the two: C# evaluates tuple elements
   ///    left to right, so the command runs and only then is the Town read.
   /// </remarks>
   private async Task<TResult> WithTownAsync<TResult>(Func<Town, TResult> work, CancellationToken ct)
   {
      var town = await LoadAsync(ct);
      var result = work(town);

      await SaveIfChangedAsync(town, ct);

      return result;
   }

   private async Task<Town> LoadAsync(CancellationToken ct)
   {
      var stored = await repository.LoadAsync(ct);

      if (stored is not null)
         return Town.Load(stored, clock, random);

      // First visit. The starting Town is written straight away so that everything after this point can
      // assume the player exists.
      var created = Town.NewTown(clock);
      await repository.SaveAsync(created, ct);

      return Town.Load(created, clock, random);
   }

   private Task SaveIfChangedAsync(Town town, CancellationToken ct)
   {
      return town.HasChanges ? repository.SaveAsync(town.State, ct) : Task.CompletedTask;
   }
}
