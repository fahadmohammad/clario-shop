namespace ClarioShop.Infrastructure.Storage;

public sealed class FileStorageOptions
{
    public const string SectionName = "FileStorage";

    /// <summary>Folder the local-disk storage writes to; relative paths are resolved against the working directory.</summary>
    public string LocalRootPath { get; set; } = "App_Data/files";
}
