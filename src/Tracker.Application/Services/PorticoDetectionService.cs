// Tracker.Worker.Infrastructure/Services/PorticoDetectionService.cs
using Microsoft.Extensions.Options;
using NetTopologySuite;
using NetTopologySuite.Geometries;
using Tracker.Domain.Porticos;                 // IPorticoRepository
using Tracker.Domain.Transitos;               // ITransitoRepository
using Tracker.Domain.Tarifas;                 // ITarifaPorticoRepository
using Tracker.Domain.Entities;                // Transito, Portico, TarifaPortico
using Tracker.Domain.Abstractions;            // IUnitOfWork (ajusta si vive en otro ns)
using Tracker.Domain.Abstractions.Filter;
using Tracker.Application.Dtos;
using Tracker.Application.Services;
using Tracker.Contracts.Enums;
using Tracker.Domain.Vehiculos;                // IVehiculoRepository, IAsignacionDispositivoRepository
using Tracker.Domain.Viajes;                   // IViajeRepository

namespace Tracker.Worker.Infrastructure.Services
{
    public sealed class PorticoDetectionService : IPorticoDetectionService
    {
        private readonly IPorticoRepository _porticos;
        private readonly ITransitoRepository _transitos;
        private readonly ITarifaPorticoRepository _tarifas;
        private readonly IBandaHorarioRepository _bandas;
        private readonly ICalendarioChile _calendario;
        private readonly IAsignacionDispositivoRepository _asignaciones;
        private readonly IVehiculoRepository _vehiculos;
        private readonly IViajeRepository _viajes;
        private readonly IUltimaPosicion _ultimaPosicion;
        private readonly OpcionesSegmento _segmento;
        private readonly IUnitOfWork _uow; // si no usas UoW, reemplaza por save en capa superior
        private readonly GeometryFactory _gf = NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);

        // Como no existen en Portico, uso constantes locales
        private const double RADIO_M = 50.0;          // radio de captura
        private const double TOL_ANGULO = 45.0;       // tolerancia de heading
        private static readonly TimeSpan VENTANA = TimeSpan.FromSeconds(90); // de-bounce

        public PorticoDetectionService(
            IPorticoRepository porticos,
            ITransitoRepository transitos,
            ITarifaPorticoRepository tarifas,
            IBandaHorarioRepository bandas,
            ICalendarioChile calendario,
            IAsignacionDispositivoRepository asignaciones,
            IVehiculoRepository vehiculos,
            IViajeRepository viajes,
            IUltimaPosicion ultimaPosicion,
            IOptions<OpcionesSegmento> segmento,
            IUnitOfWork uow)
        {
            _porticos = porticos;
            _transitos = transitos;
            _tarifas = tarifas;
            _bandas = bandas;
            _calendario = calendario;
            _asignaciones = asignaciones;
            _vehiculos = vehiculos;
            _viajes = viajes;
            _ultimaPosicion = ultimaPosicion;
            _segmento = segmento.Value;
            _uow = uow;
        }

        public async Task<TransitoDetectadoDto?> DetectarYGuardarAsync(GpsEventDto evt, KafkaMetaDto meta, CancellationToken ct)
        {
            // 1) Punto GPS (lon, lat) SRID 4326
            var punto = _gf.CreatePoint(new Coordinate(evt.Lon, evt.Lat));
            var ts = evt.Utc;

            // 2) Candidatos. Se busca contra el TRAYECTO recorrido desde el fix
            //    anterior, no contra el punto suelto: a 100 km/h con muestreo de
            //    5 s el vehículo avanza ~139 m entre fixes, más que el diámetro de
            //    la ventana de 50 m, así que un pórtico puede quedar entre dos
            //    puntos sin que ninguno caiga dentro y el peaje no se cobra.
            var previa = _ultimaPosicion.Obtener(evt.DeviceId);
            var segmento = ConstruirSegmento(previa, evt);

            var candidatos = segmento is not null
                ? await _porticos.GetNearSegmentAsync(segmento, RADIO_M, take: 5, ct: ct)
                : await _porticos.GetNearAsync(punto, RADIO_M, take: 5, ct: ct);

            // Se registra siempre, haya o no candidatos: el próximo fix necesita
            // este como origen de su segmento.
            _ultimaPosicion.Registrar(evt.DeviceId, evt.Lat, evt.Lon, ts);

            if (candidatos.Count == 0)
                return null;

            // 3) Recorre candidatos y valida el sentido de circulación.
            foreach (var portico in candidatos)
            {
                // Rumbo real del vehículo: el del trayecto recorrido si lo hay, y
                // si no el que reporta el dispositivo. Preferir el del segmento
                // evita depender de que el móvil informe HeadingDeg, que es
                // opcional en el proto y con el vehículo lento suele ser ruido.
                var rumbo = RumboDelSegmento(previa, evt) ?? evt.HeadingDeg;

                // Nota: hoy este filtro casi nunca se aplica, porque Corredor está
                // a NULL en los 187 pórticos del seed (el catálogo OSM sólo trae
                // Lat/Lon). Hasta que se pueble, el sentido no se puede discriminar
                // y dos pórticos opuestos a menos de 50 m son indistinguibles.
                if (rumbo is double r && portico.Corredor is not null)
                {
                    var bearing = BearingFromLine(portico.Corredor);
                    var diff = AngularDiff(r, bearing);
                    if (diff > TOL_ANGULO) continue;
                }

                // 4) De-bounce temporal (usa tu repo: GetByPorticoAsync)
                var desde = ts - VENTANA;
                var hasta = ts + VENTANA;

                // PageSize=1 para hacer existencia O(1)
                var page = new Pagination(1, 1);
                var recientes = await _transitos.GetByPorticoAsync(portico.Id, desde, hasta, page, ct);
                if (recientes.Total > 0)
                    continue;

                // 5) Atribución: ¿de qué vehículo era este device EN ESE INSTANTE?
                //    Se resuelve por la asignación vigente al momento del paso,
                //    no por la asignación de hoy: así reasignar el teléfono a otro
                //    auto no reescribe los cobros pasados.
                Guid? vehiculoId = null;
                var categoria = VehicleCategory.C1;   // fallback si el device no está asignado

                // DeviceId es no-nulable en el DTO; si llega vacío desde Protobuf,
                // las consultas simplemente no encuentran nada y el tránsito se
                // registra sin atribuir.
                var asignacion = await _asignaciones.GetVigenteAsync(evt.DeviceId, ts, ct);

                if (asignacion is not null)
                {
                    vehiculoId = asignacion.VehiculoId;
                    var vehiculo = await _vehiculos.GetByIdAsync(asignacion.VehiculoId, ct);
                    if (vehiculo is not null)
                        categoria = vehiculo.Categoria;   // la categoría real manda sobre el C1 fijo
                }

                // Viaje en curso del device, si lo hay. Un tránsito fuera de viaje
                // se registra igual: el peaje existe aunque el conductor no haya
                // apretado "Iniciar".
                var viaje = await _viajes.GetEnCursoPorDeviceAsync(evt.DeviceId, ct);

                // 6) Resolver banda según la hora local Chile del tránsito y la
                //    grilla horaria del pórtico; luego tarifa vigente y precio.
                var diaTipo = _calendario.DiaTipoDe(ts);
                var local = _calendario.ToLocal(ts);
                var horaLocal = TimeOnly.FromDateTime(local);
                var banda = await _bandas.ResolverBandaAsync(portico.Id, diaTipo, horaLocal, ct);

                var tarifa = await _tarifas.GetVigenteAsync(portico.Id, categoria, banda, ts, ct);
                var precio = CalcularPrecio(tarifa, portico.LongitudKm);

                // 7) Guardar el tránsito con toda la evidencia del cobro
                var transito = new Transito
                {
                    Id = Guid.NewGuid(),
                    PorticoId = portico.Id,
                    DeviceId = evt.DeviceId,
                    VehiculoId = vehiculoId,
                    ViajeId = viaje?.Id,
                    Utc = ts,
                    Posicion = punto,

                    // Fecha/hora local persistidas: el reporte diario agrupa por
                    // el día de Chile, no por el día UTC.
                    FechaLocal = DateOnly.FromDateTime(local),
                    HoraLocal = horaLocal,
                    DiaTipo = diaTipo,

                    Categoria = categoria,
                    Banda = banda,

                    // Trazabilidad del monto: qué tarifa se aplicó.
                    TarifaPorticoId = tarifa?.Id,

                    // Snapshots del catálogo, congelados al momento del paso.
                    AutopistaSnapshot = portico.Autopista,
                    PorticoCodigoSnapshot = portico.Codigo,
                    SentidoSnapshot = portico.Sentido,

                    EstadoConciliacion = EstadoConciliacion.Pendiente,

                    // opcionales pero útiles si existen en tu entidad
                    ExactitudM = (evt.AccuracyM ?? 0),   // double
                    Fuente = "gps",                  // string (topic/fuente)
                    PrecioCalculado = precio
                };

                await _transitos.AddAsync(transito, ct);

                // Totales del viaje al vuelo, para que la lista de viajes no tenga
                // que agregar sobre los tránsitos en cada request. Al cerrar el
                // viaje se recalculan contra la BD por si esto quedó corto.
                if (viaje is not null)
                {
                    viaje.CantidadTransitos += 1;
                    viaje.TotalGasto += precio;
                }

                await _uow.SaveChangesAsync(ct); // si no usas UoW, mueve el commit donde corresponda

                // Registramos el primer match válido y devolvemos el resultado
                // para notificarlo en vivo al dashboard.
                return new TransitoDetectadoDto(
                    DeviceId: evt.DeviceId,
                    PorticoId: portico.Id,
                    PorticoCodigo: portico.Codigo,
                    Autopista: portico.Autopista,
                    Precio: precio,
                    Lat: evt.Lat,
                    Lon: evt.Lon,
                    Utc: ts);
            }

            return null; // ningún candidato pasó los filtros
        }

        // --- cálculo de tarifa ---

        /// <summary>
        /// Precio del tránsito según la tarifa vigente:
        ///  - si trae <see cref="TarifaPortico.ValorFijo"/>, ése es el precio;
        ///  - si trae <see cref="TarifaPortico.ValorPorKm"/>, se multiplica por la
        ///    longitud (snapshot de la tarifa o, en su defecto, la del pórtico);
        ///  - si no hay tarifa vigente, 0 (el tránsito se registra igual).
        /// </summary>
        private static decimal CalcularPrecio(TarifaPortico? tarifa, decimal? longitudPortico)
        {
            if (tarifa is null) return 0m;

            if (tarifa.ValorFijo is decimal fijo)
                return fijo;

            if (tarifa.ValorPorKm is decimal porKm)
            {
                var km = tarifa.LongitudKmSnapshot ?? longitudPortico ?? 0m;
                return porKm * km;
            }

            return 0m;
        }

        // --- trayecto recorrido entre dos fixes ---

        /// <summary>
        /// Une el fix previo con el actual. Devuelve null si no hay previo o si
        /// unirlos sería inventar un trayecto.
        /// </summary>
        private LineString? ConstruirSegmento(PosicionPrevia? previa, GpsEventDto evt)
        {
            if (!_segmento.Habilitado || previa is not PosicionPrevia p)
                return null;

            var segundos = (evt.Utc - p.Utc).TotalSeconds;

            // Fix atrasado o repetido: el segmento iría hacia atrás en el tiempo.
            if (segundos <= 0) return null;

            // Hueco largo — túnel, pérdida de señal, app cerrada. La recta entre
            // ambos puntos no es por donde fue el vehículo y podría cruzar pórticos
            // por los que nunca pasó, generando cobros falsos. Se prefiere no
            // detectar a cobrar de más.
            if (segundos > _segmento.MaxSegundosEntreFixes) return null;

            var metros = DistanciaM(p.Lat, p.Lon, evt.Lat, evt.Lon);

            // Sin movimiento apreciable no hay trayecto que mirar; el punto basta.
            if (metros < 1) return null;

            // Salto imposible en poco tiempo: multipath, no desplazamiento real.
            if (metros > _segmento.MaxLongitudM) return null;

            return _gf.CreateLineString(new[]
            {
                new Coordinate(p.Lon, p.Lat),
                new Coordinate(evt.Lon, evt.Lat)
            });
        }

        /// <summary>Rumbo real del vehículo entre el fix previo y el actual.</summary>
        private double? RumboDelSegmento(PosicionPrevia? previa, GpsEventDto evt)
        {
            if (previa is not PosicionPrevia p) return null;
            if (DistanciaM(p.Lat, p.Lon, evt.Lat, evt.Lon) < 5) return null; // ruido parado
            return Bearing(p.Lat, p.Lon, evt.Lat, evt.Lon);
        }

        /// <summary>Distancia haversine en metros.</summary>
        private static double DistanciaM(double lat1, double lon1, double lat2, double lon2)
        {
            const double R = 6_371_000;
            var dLat = Deg2Rad(lat2 - lat1);
            var dLon = Deg2Rad(lon2 - lon1);
            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos(Deg2Rad(lat1)) * Math.Cos(Deg2Rad(lat2)) *
                    Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            return R * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        }

        // --- utilidades de heading ---

        private static double BearingFromLine(LineString ls)
        {
            if (ls.NumPoints < 2) return 0;
            var a = ls.GetCoordinateN(0);
            var b = ls.GetCoordinateN(1);
            return Bearing(a.Y, a.X, b.Y, b.X); // (latA, lonA, latB, lonB)
        }

        private static double Bearing(double lat1, double lon1, double lat2, double lon2)
        {
            double dLon = Deg2Rad(lon2 - lon1);
            lat1 = Deg2Rad(lat1); lat2 = Deg2Rad(lat2);
            double y = Math.Sin(dLon) * Math.Cos(lat2);
            double x = Math.Cos(lat1) * Math.Cos(lat2) * Math.Cos(dLon) - Math.Sin(lat1) * Math.Sin(lat2);
            double brng = Math.Atan2(y, x);
            return (Rad2Deg(brng) + 360.0) % 360.0;
        }

        private static double AngularDiff(double a, double b)
        {
            double diff = Math.Abs(a - b) % 360.0;
            return diff > 180.0 ? 360.0 - diff : diff;
        }

        private static double Deg2Rad(double d) => d * Math.PI / 180.0;
        private static double Rad2Deg(double r) => r * 180.0 / Math.PI;
    }
}
