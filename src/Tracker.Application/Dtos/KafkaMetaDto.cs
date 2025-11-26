namespace Tracker.Application.Dtos
{
    public sealed record KafkaMetaDto(
    string Topic,
    int Partition,
    long Offset,
    string? Key = null
);
}
