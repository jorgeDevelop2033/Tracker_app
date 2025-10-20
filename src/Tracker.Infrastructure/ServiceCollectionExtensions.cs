using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations.Internal;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Tracker.Domain.Abstractions;
using Tracker.Infrastructure.Persistence; 

namespace Tracker.Infrastructure
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddTrackerInfrastructure(
            this IServiceCollection services, IConfiguration cfg)
        {
             
           var cs = cfg.GetConnectionString("TrackerDb")
                         ?? "Server=localhost,1433;Database=TrackerDb;User Id=sa;Password=09mayo@84;TrustServerCertificate=True;";
                
             
            services.AddDbContext<TrackerDbContext>(opt =>
            {
                opt.UseSqlServer(cs, sql =>
                {
                    sql.UseNetTopologySuite();

                });
            });

            // Repos
            services.AddScoped<IGpsFixRepository, Tracker.Infrastructure.Repositories.GpsFixRepository>();
            // agrega otros repos aquí

            return services;
        }
    }
}


 