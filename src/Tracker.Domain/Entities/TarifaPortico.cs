using Tracker.Domain.Common;

namespace Tracker.Domain.Entities
{
    public class TarifaPortico : BaseEntity
    {
        public Guid PorticoId { get; set; }
        public Portico Portico { get; set; } = default!;
        public int Categoria { get; set; }                    // 1,2,3
        public string Banda { get; set; } = default!;         // TBFP/TBP/TS
        public decimal? ValorFijo { get; set; }               // si la lámina trae valor directo
        public decimal? ValorPorKm { get; set; }              // si es por km
        public decimal? LongitudKmSnapshot { get; set; }      // snapshot para cálculo histórico
        public DateTime VigenteDesde { get; set; }
        public DateTime? VigenteHasta { get; set; }
    }
}
