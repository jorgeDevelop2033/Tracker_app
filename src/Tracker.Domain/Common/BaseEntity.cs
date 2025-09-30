namespace Tracker.Domain.Common
{
    public abstract class BaseEntity
    {
        public Guid Id { get; set; }            
        public byte[] RowVersion { get; set; } = Array.Empty<byte>();  
    }
}
