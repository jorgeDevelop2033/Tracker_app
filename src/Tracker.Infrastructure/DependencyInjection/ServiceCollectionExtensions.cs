using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Tracker.Application.Services;
using Tracker.Domain.Abstractions;
using Tracker.Domain.Porticos;
using Tracker.Domain.Tarifas;
using Tracker.Domain.Transitos;
using Tracker.Domain.Vehiculos;
using Tracker.Domain.Viajes;
using Tracker.Infrastructure.Persistence;
using Tracker.Infrastructure.Repositories;
using Tracker.Worker.Infrastructure.Services;

namespace Tracker.Infrastructure.DependencyInjection
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration cfg)
        {
            //services.AddDbContext<TrackerDbContext>(opt =>
            //    opt.UseSqlServer(cfg.GetConnectionString("Sql"),
            //        sql => sql.UseNetTopologySuite()));

            services.AddScoped<IPorticoRepository, PorticoRepository>();
            services.AddScoped<ITransitoRepository, TransitoRepository>();
            
            services.AddScoped<ITarifaPorticoRepository, TarifaPorticoRepository>();
            services.AddScoped<IBandaHorarioRepository, BandaHorarioRepository>();
            services.AddScoped<IPorticoDetectionService, PorticoDetectionService>();

            // Vehículos y viajes
            services.AddScoped<IVehiculoRepository, VehiculoRepository>();
            services.AddScoped<IAsignacionDispositivoRepository, AsignacionDispositivoRepository>();
            services.AddScoped<IViajeRepository, ViajeRepository>();
            services.AddScoped<IViajeService, ViajeService>();

            // Calendario/festivos Chile para derivar el tipo de día y la banda.
            services.AddSingleton<IFestivosChile>(_ => new FestivosChile());
            services.AddSingleton<ICalendarioChile, CalendarioChile>();

            // Por defecto no hay escrituras diferidas: quien escribe directo a la
            // BD (la API) no tiene nada que volcar. El Worker sobreescribe este
            // registro con su BufferFixes, que sí acumula fixes en memoria.
            services.TryAddSingleton<IEscriturasPendientes, SinEscriturasPendientes>();

            // Umbrales del segmento recorrido, que usa PorticoDetectionService.
            services.Configure<OpcionesSegmento>(cfg.GetSection(OpcionesSegmento.SeccionConfig));

            // Sin memoria del fix anterior por defecto: la API resuelve el detector
            // por DI pero no ingiere fixes. El Worker lo reemplaza por la cache real.
            services.TryAddSingleton<IUltimaPosicion, SinUltimaPosicion>();

            //services.AddScoped<IUnitOfWork>(sp => (IUnitOfWork)sp.GetRequiredService<TrackerDbContext>());
             
            services.AddScoped<IUnitOfWork, EfUnitOfWork>();

            return services;
        }
    }
}
