using mvdmio.ASP.Jobs;

namespace Underclass.Api.Jobs;

/// <summary>
///    Wakes up when an Expedition is due and reads the Town, which is what resolves it. Scheduled at dispatch
///    through Postgres-backed job storage, so a restart does not lose the appointment.
/// </summary>
/// <remarks>
///    The job does not resolve the Expedition itself, and does not need its identifier to do its work: reading
///    a Town settles everything that has come due. That makes the job idempotent, makes a duplicate run
///    harmless, and means a job that is lost outright only delays the news until the player next looks.
/// </remarks>
public sealed class ResolveExpeditionJob(TownService towns, ILogger<ResolveExpeditionJob> logger) : Job<ResolveExpeditionJob.Parameters>
{
   /// <inheritdoc />
   public override async Task ExecuteAsync(Parameters parameters, CancellationToken cancellationToken)
   {
      logger.LogInformation("Resolving Expedition {ExpeditionId}", parameters.ExpeditionId);

      await towns.ReadAsync(cancellationToken);
   }

   /// <summary>
   ///    Which Expedition this job was booked for. Carried for the log rather than for the logic — see the
   ///    remarks on the job itself.
   /// </summary>
   public sealed class Parameters
   {
      public Guid ExpeditionId { get; set; }
   }
}
