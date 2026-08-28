using Budgie.Shared.Abstractions;

namespace Budgie.Modules.Finances.Domain.Import;

public class ImportRun : Entity
{
    public Guid Id { get; internal set; }
    public Guid UserId { get; internal set; }
    public string FileHash { get; internal set; } = null!;
    public string? FileName { get; internal set; }
    public DateTime CreatedAt { get; internal set; }
    public string? CountsJson { get; internal set; }

    public ImportRun() { }

    public static ImportRun Create(Guid userId, string fileHash, string? fileName, string? countsJson)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("UserId cannot be empty.", nameof(userId));

        if (string.IsNullOrWhiteSpace(fileHash))
            throw new ArgumentException("FileHash cannot be empty.", nameof(fileHash));

        fileHash = fileHash.Trim().ToLowerInvariant();
        if (fileHash.Length != 64)
            throw new ArgumentException("FileHash must be SHA256 hex (64 chars).", nameof(fileHash));

        return new ImportRun
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            FileHash = fileHash,
            FileName = string.IsNullOrWhiteSpace(fileName) ? null : fileName.Trim(),
            CreatedAt = DateTime.UtcNow,
            CountsJson = string.IsNullOrWhiteSpace(countsJson) ? null : countsJson
        };
    }
}
