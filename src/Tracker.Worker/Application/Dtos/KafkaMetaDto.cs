using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tracker.Worker.Application.Dtos
{
    public sealed record KafkaMetaDto(
     string Topic,
     int Partition,
     long Offset,
     string? Key = null
 );
}
