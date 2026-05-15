using System.Globalization;

namespace Portlink.Api.Modules.Storage.Settings;

public class StorageSettings
{
    public string AwsAccessKeyId { get; init; } = string.Empty;
    public string AwsSecretAccessKey { get; init; } = string.Empty;
    public string AwsRegion { get; init; } = string.Empty;
    public string S3BucketName { get; init; } = string.Empty;
    public string? S3Endpoint { get; init; }
    public bool S3ForcePathStyle { get; init; }
    public int PresignedUrlExpiresInSeconds { get; init; } = 300;
    public int MaxDocumentSizeMb { get; init; } = 25;
    public int MaxVideoSizeMb { get; init; } = 100;
    public List<string> AllowedDocumentExtensions { get; init; } = new();
    public List<string> AllowedVideoExtensions { get; init; } = new();
    public List<string> AllowedDocumentMimeTypes { get; init; } = new();
    public List<string> AllowedVideoMimeTypes { get; init; } = new();

    public static StorageSettings FromConfiguration(IConfiguration configuration)
    {
        var section = configuration.GetSection("Storage");

        return new StorageSettings
        {
            AwsAccessKeyId = ReadString(configuration, section, "AwsAccessKeyId", "AWS_ACCESS_KEY_ID"),
            AwsSecretAccessKey = ReadString(configuration, section, "AwsSecretAccessKey", "AWS_SECRET_ACCESS_KEY"),
            AwsRegion = ReadString(configuration, section, "AwsRegion", "AWS_REGION"),
            S3BucketName = ReadString(configuration, section, "S3BucketName", "AWS_S3_BUCKET_NAME"),
            S3Endpoint = ReadNullableString(configuration, section, "S3Endpoint", "AWS_S3_ENDPOINT"),
            S3ForcePathStyle = ReadBool(configuration, section, "S3ForcePathStyle", "AWS_S3_FORCE_PATH_STYLE"),
            PresignedUrlExpiresInSeconds = ReadInt(configuration, section, "PresignedUrlExpiresInSeconds", "AWS_S3_PRESIGNED_URL_EXPIRES_IN_SECONDS", 300),
            MaxDocumentSizeMb = ReadInt(configuration, section, "MaxDocumentSizeMb", "STORAGE_MAX_DOCUMENT_SIZE_MB", 25),
            MaxVideoSizeMb = ReadInt(configuration, section, "MaxVideoSizeMb", "STORAGE_MAX_VIDEO_SIZE_MB", 100),
            AllowedDocumentExtensions = ReadList(configuration, section, "AllowedDocumentExtensions", "STORAGE_ALLOWED_DOCUMENT_EXTENSIONS", new[] { "pdf", "docx", "jpg", "jpeg", "png" }),
            AllowedVideoExtensions = ReadList(configuration, section, "AllowedVideoExtensions", "STORAGE_ALLOWED_VIDEO_EXTENSIONS", new[] { "mp4", "mov", "webm" }),
            AllowedDocumentMimeTypes = ReadList(configuration, section, "AllowedDocumentMimeTypes", "STORAGE_ALLOWED_DOCUMENT_MIME_TYPES", new[]
            {
                "application/pdf",
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                "image/jpeg",
                "image/png"
            }),
            AllowedVideoMimeTypes = ReadList(configuration, section, "AllowedVideoMimeTypes", "STORAGE_ALLOWED_VIDEO_MIME_TYPES", new[]
            {
                "video/mp4",
                "video/quicktime",
                "video/webm"
            })
        };
    }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(AwsAccessKeyId) ||
            string.IsNullOrWhiteSpace(AwsSecretAccessKey) ||
            string.IsNullOrWhiteSpace(AwsRegion) ||
            string.IsNullOrWhiteSpace(S3BucketName))
        {
            throw new InvalidOperationException("Storage ayarlari eksik. AWS ve bucket bilgileri yapilandirilmalidir.");
        }

        if (PresignedUrlExpiresInSeconds <= 0)
        {
            throw new InvalidOperationException("Presigned URL suresi sifirdan buyuk olmalidir.");
        }
    }

    private static string ReadString(IConfiguration configuration, IConfigurationSection section, string sectionKey, string envKey)
        => ReadNullableString(configuration, section, sectionKey, envKey) ?? string.Empty;

    private static string? ReadNullableString(IConfiguration configuration, IConfigurationSection section, string sectionKey, string envKey)
    {
        var sectionValue = section[sectionKey];
        if (!string.IsNullOrWhiteSpace(sectionValue))
        {
            return sectionValue.Trim();
        }

        var envValue = configuration[envKey];
        return string.IsNullOrWhiteSpace(envValue) ? null : envValue.Trim();
    }

    private static bool ReadBool(IConfiguration configuration, IConfigurationSection section, string sectionKey, string envKey)
    {
        var value = ReadNullableString(configuration, section, sectionKey, envKey);
        return bool.TryParse(value, out var parsed) && parsed;
    }

    private static int ReadInt(IConfiguration configuration, IConfigurationSection section, string sectionKey, string envKey, int defaultValue)
    {
        var value = ReadNullableString(configuration, section, sectionKey, envKey);
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) && parsed > 0
            ? parsed
            : defaultValue;
    }

    private static List<string> ReadList(IConfiguration configuration, IConfigurationSection section, string sectionKey, string envKey, IEnumerable<string> defaultValues)
    {
        var configuredValues = section.GetSection(sectionKey).Get<string[]>();
        if (configuredValues is { Length: > 0 })
        {
            return configuredValues
                .Select(NormalizeListValue)
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        var envValue = configuration[envKey];
        if (!string.IsNullOrWhiteSpace(envValue))
        {
            return envValue
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(NormalizeListValue)
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        return defaultValues
            .Select(NormalizeListValue)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string NormalizeListValue(string value) => value.Trim().ToLowerInvariant();
}
