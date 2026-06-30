namespace Tracker.Application.Services
{
    /// <summary>Determina si una fecha (local Chile) es festivo.</summary>
    public interface IFestivosChile
    {
        bool EsFestivo(DateOnly fecha);
    }

    /// <summary>
    /// Festivos de Chile. Cubre los de fecha fija de forma automática y permite
    /// inyectar los de fecha variable año a año (Semana Santa, etc.) vía
    /// <paramref name="festivosVariables"/>. Como el cálculo cae a TBFP cuando es
    /// domingo/festivo, un festivo no declarado solo implica cobrar tarifa de
    /// día hábil; no rompe nada, pero conviene mantener la lista al día.
    /// </summary>
    public sealed class FestivosChile : IFestivosChile
    {
        // (mes, día) de festivos chilenos de fecha fija.
        private static readonly HashSet<(int Mes, int Dia)> Fijos = new()
        {
            (1, 1),    // Año Nuevo
            (5, 1),    // Día del Trabajo
            (5, 21),   // Glorias Navales
            (6, 20),   // Día Nacional de los Pueblos Indígenas
            (6, 29),   // San Pedro y San Pablo
            (7, 16),   // Virgen del Carmen
            (8, 15),   // Asunción de la Virgen
            (9, 18),   // Independencia Nacional
            (9, 19),   // Glorias del Ejército
            (10, 12),  // Encuentro de Dos Mundos
            (10, 31),  // Iglesias Evangélicas y Protestantes
            (11, 1),   // Día de Todos los Santos
            (12, 8),   // Inmaculada Concepción
            (12, 25),  // Navidad
        };

        private readonly HashSet<DateOnly> _variables;

        public FestivosChile(IEnumerable<DateOnly>? festivosVariables = null)
            => _variables = festivosVariables is null ? new() : new(festivosVariables);

        public bool EsFestivo(DateOnly fecha)
            => Fijos.Contains((fecha.Month, fecha.Day)) || _variables.Contains(fecha);
    }
}
