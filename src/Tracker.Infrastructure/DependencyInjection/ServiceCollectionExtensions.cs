using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Tracker.Domain.Abstractions;
using Tracker.Domain.Porticos;
using Tracker.Domain.Tarifas;
using Tracker.Domain.Transitos;
using Tracker.Infrastructure.Persistence;
using Tracker.Infrastructure.Repositories;

namespace Tracker.Infrastructure.DependencyInjection
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration cfg)
        {
            services.AddDbContext<TrackerDbContext>(opt =>
                opt.UseSqlServer(cfg.GetConnectionString("Sql"),
                    sql => sql.UseNetTopologySuite()));

            services.AddScoped<IPorticoRepository, PorticoRepository>();
            services.AddScoped<ITransitoRepository, TransitoRepository>();
            services.AddScoped<ITarifaPorticoRepository, TarifaPorticoRepository>();
            services.AddScoped<IUnitOfWork>(sp => (IUnitOfWork)sp.GetRequiredService<TrackerDbContext>());

            return services;
        }
    }
}
