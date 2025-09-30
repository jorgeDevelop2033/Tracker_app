using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Tracker.Infrastructure.Persistence;

namespace Tracker.Infrastructure.DependencyInjection
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration cfg)
        {
            services.AddDbContext<TrackerDbContext>(opt =>
                opt.UseSqlServer(cfg.GetConnectionString("Sql"),
                    sql => sql.UseNetTopologySuite()));

            return services;
        }
    }
}
