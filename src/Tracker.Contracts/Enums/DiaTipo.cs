namespace Tracker.Contracts.Enums
{
    /// <summary>
    /// Tipo de día para resolver la banda tarifaria. Las ventanas TBP/TS de las
    /// concesiones se publican separadas para días laborales, sábado y
    /// domingo/festivo.
    /// </summary>
    public enum DiaTipo
    {
        Laboral = 0,        // lunes a viernes (hábiles)
        Sabado = 1,
        DomingoFestivo = 2  // domingo o festivo
    }
}
