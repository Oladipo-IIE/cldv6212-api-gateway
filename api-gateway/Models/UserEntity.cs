using Azure;
using Azure.Data.Tables;

public class UserEntity : ITableEntity
{
    public string PartitionKey { get; set; } = default!;
    public string RowKey { get; set; } = default!;

    public string FullName { get; set; } = default!;
    public string PasswordHash { get; set; } = default!;

    public DateTime CreatedDate { get; set; }

    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }
}