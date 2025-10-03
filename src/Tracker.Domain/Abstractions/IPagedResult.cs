namespace Tracker.Domain.Abstractions
{
    public interface IPagedResult<T>
    {
        IReadOnlyList<T> Items { get; }
        int Total { get; }
        int PageNumber { get; }
        int PageSize { get; }
    }
    public record PagedResult<T>(IReadOnlyList<T> Items, int Total, int PageNumber, int PageSize) : IPagedResult<T>;
}
