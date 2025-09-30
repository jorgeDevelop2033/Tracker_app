using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tracker.Contracts.Enums
{
    /// <summary>
    /// Bandas tarifarias típicas de concesiones urbanas.
    /// </summary>
    public enum Banda
    {
        TBFP = 0,  // Fuera de Punta
        TBP = 1,  // Punta
        TS = 2   // Saturación
    }
}
