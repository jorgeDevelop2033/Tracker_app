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
        /// Pórticos a menos de <paramref name="maxDistanceMeters"/> del trayecto
        /// recorrido entre dos fixes consecutivos.
        ///
        /// <para>
        /// Es la versión robusta de <see cref="GetNearAsync"/>: buscar por el punto
        /// suelto exige que algún fix caiga dentro del radio, y a 100 km/h con
        /// muestreo de 5 s el vehículo avanza ~139 m entre fixes — más que el
        /// diámetro de la ventana de 50 m. El pórtico queda entre dos puntos, no se
        /// detecta, y el peaje no se cobra. Preguntando por el segmento, el paso se
        /// detecta aunque ningún fix individual haya caído cerca.
        /// </para>
        /// </summary>
        Task<IReadOnlyList<Portico>> GetNearSegmentAsync(LineString segmento4326, double maxDistanceMeters, int take = 20, CancellationToken ct = default);


        /// <summary>
        /// Intersección con un corredor (línea) – devuelve candidatos ordenados por distancia.
        /// </summary>
        Task<IReadOnlyList<Portico>> IntersectsCorredorAsync(LineString corredor4326, int take = 50, CancellationToken ct = default);
    }
}
