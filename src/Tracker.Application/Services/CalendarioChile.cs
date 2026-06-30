using Tracker.Contracts.Enums;

namespace Tracker.Application.Services
{
    /// <summary>
    /// Convierte instantes UTC a hora local de Chile y determina el
    /// <see cref="DiaTipo"/> (laboral / sábado / domingo-festivo) que usan las
    /// concesiones para resolver la banda tarifaria.
    /// </summary>
    public interface ICalendarioChile
    {
        /// <summary>Hora local de Chile correspondiente al instante UTC.</summary>
        DateTime ToLocal(DateTime utc);

        /// <summary>Tipo de día (en hora local Chile) para el instante UTC dado.</summary>
        DiaTipo DiaTipoDe(DateTime utc);
    }

    public sealed class CalendarioChile : ICalendarioChile
    {
        // En Linux/.NET el id IANA es "America/Santiago"; en Windows "Pacific SA Standard Time".
        private static readonly TimeZoneInfo Tz = ResolveTz();

        private readonly IFestivosChile _festivos;

        public CalendarioChile(IFestivosChile festivos) => _festivos = festivos;

        public DateTime ToLocal(DateTime utc)
        {
            // Aseguramos Kind=Utc para una conversión correcta.
            var u = DateTime.SpecifyKind(utc, DateTimeKind.Utc);
            return TimeZoneInfo.ConvertTimeFromUtc(u, Tz);
        }

        public DiaTipo DiaTipoDe(DateTime utc)
        {
            var local = ToLocal(utc);

            if (local.DayOfWeek == DayOfWeek.Sunday || _festivos.EsFestivo(DateOnly.FromDateTime(local)))
                return DiaTipo.DomingoFestivo;

            if (local.DayOfWeek == DayOfWeek.Saturday)
                return DiaTipo.Sabado;

            return DiaTipo.Laboral;
        }

        private static TimeZoneInfo ResolveTz()
        {
            foreach (var id in new[] { "America/Santiago", "Pacific SA Standard Time" })
            {
                try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
                catch (TimeZoneNotFoundException) { }
                catch (InvalidTimeZoneException) { }
            }
            // Fallback: UTC-4 (sin horario de verano). Mejor esto que reventar.
            return TimeZoneInfo.CreateCustomTimeZone("CL-Fallback", TimeSpan.FromHours(-4), "Chile (fallback)", "Chile");
        }
    }
}
