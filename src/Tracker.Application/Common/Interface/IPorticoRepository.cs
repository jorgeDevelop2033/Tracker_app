using NetTopologySuite.Geometries;
using Tracker.Domain.Abstractions;
using Tracker.Domain.Entities;

namespace Tracker.Domain.Porticos
{
    public interface IPorticoRepository : IRepository<Portico>
    {
        Task<Portico?> GetByCodigoAsync(string codigo, CancellationToken ct = default);


        /// <summary>
        /// Porticos a una distancia (metros) de una posición.
        /// </summary>
        Task<IReadOnlyList<Portico>> GetNearAsync(Point position4326, double maxDistanceMeters, int take = 20, CancellationToken ct = default);


        /// <summary>
        /// Pórticos a una distancia (metros) de un TRAMO recorrido entre dos fixes.
        /// Es la variante correcta para detectar pasos: con muestreo cada 2 s a
        /// velocidad de autopista, ninguna muestra suelta tiene por qué caer dentro
        /// del radio, pero el segmento que las une sí pasa por encima del pórtico.
        /// </summary>
        Task<IReadOnlyList<Portico>> GetNearLineAsync(LineString tramo4326, double maxDistanceMeters, int take = 20, CancellationToken ct = default);


        /// <summary>
        /// Intersección con un corredor (línea) – devuelve candidatos ordenados por distancia.
        /// </summary>
        Task<IReadOnlyList<Portico>> IntersectsCorredorAsync(LineString corredor4326, int take = 50, CancellationToken ct = default);
    }
}
