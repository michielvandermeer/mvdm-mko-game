using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using mvdmio.Database.PgSQL;
using mvdmio.Database.PgSQL.Migrations;

namespace Underclass.Data;

/// <summary>Composition for the persistence layer.</summary>
public static class ServiceCollectionExtensions
{
   /// <summary>Registers the database connection and the Town repository against the given database.</summary>
   public static IServiceCollection AddUnderclassData(this IServiceCollection services, string connectionString)
   {
      services.AddDatabase();

      services.AddScoped<DatabaseConnection>(provider => provider.GetRequiredService<DatabaseConnectionFactory>().BuildConnection(connectionString));
      services.AddScoped<TownRepository>();

      return services;
   }

   /// <summary>
   ///    Brings the database up to the embedded schema. Safe to call from several instances starting at once —
   ///    the runner serialises the run with a Postgres advisory lock — and safe to call again on a database that
   ///    is already current.
   /// </summary>
   public static async Task MigrateUnderclassDatabaseAsync(this IServiceProvider services, CancellationToken ct = default)
   {
      using var scope = services.CreateScope();

      var db = scope.ServiceProvider.GetRequiredService<DatabaseConnection>();
      var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
      var migrator = new DatabaseMigrator(db, loggerFactory, typeof(UnderclassDataAssemblyHook).Assembly);

      await migrator.MigrateDatabaseToLatestAsync(ct);
   }
}
