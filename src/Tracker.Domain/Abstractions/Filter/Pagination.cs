namespace Tracker.Domain.Abstractions.Filter
{
    public record Pagination(int PageNumber = 1, int PageSize = 50)
    {
        public int Skip => (PageNumber - 1) * PageSize;
    };
}
