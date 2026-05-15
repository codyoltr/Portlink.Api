using System.IO.Compression;
using System.Text;
using Portlink.Api.Modules.Storage.Interfaces;
using Portlink.Api.Modules.Storage.Settings;
using Portlink.Api.Modules.Storage.Enums;

namespace Portlink.Api.Modules.Storage.Services;

public class FileValidationService : IFileValidationService
{
    private static readonly HashSet<string> DangerousExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        "svg", "html", "htm", "js", "mjs", "exe", "bat", "cmd", "sh", "php", "zip", "rar", "7z", "msi", "dll", "ps1"
    };

    private readonly StorageSettings _settings;
    private readonly Dictionary<string, string[]> _extensionMimeMap;

    public FileValidationService(StorageSettings settings)
    {
        _settings = settings;
        _extensionMimeMap = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["pdf"] = new[] { "application/pdf" },
            ["docx"] = new[] { "application/vnd.openxmlformats-officedocument.wordprocessingml.document" },
            ["jpg"] = new[] { "image/jpeg" },
            ["jpeg"] = new[] { "image/jpeg" },
            ["png"] = new[] { "image/png" },
            ["webp"] = new[] { "image/webp" },
            ["mp4"] = new[] { "video/mp4" },
            ["mov"] = new[] { "video/quicktime" },
            ["webm"] = new[] { "video/webm" }
        };
    }

    public async Task<ValidatedStorageFile> ValidateAsync(IFormFile file, StorageFileCategory expectedCategory, CancellationToken cancellationToken = default)
    {
        if (file == null)
        {
            throw new InvalidOperationException("Dosya gereklidir.");
        }

        if (file.Length <= 0)
        {
            throw new InvalidOperationException("Bos dosya yuklenemez.");
        }

        var originalFileName = Path.GetFileName(file.FileName ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(originalFileName))
        {
            throw new InvalidOperationException("Dosya adi gecersiz.");
        }

        var extension = Path.GetExtension(originalFileName).TrimStart('.').ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(extension))
        {
            throw new InvalidOperationException("Dosya uzantisi gereklidir.");
        }

        if (DangerousExtensions.Contains(extension))
        {
            throw new InvalidOperationException("Bu dosya turune izin verilmiyor.");
        }

        EnsureCategoryMatchesExistingFile(expectedCategory, extension, file.ContentType);

        var sizeLimitBytes = expectedCategory == StorageFileCategory.Video
            ? _settings.MaxVideoSizeMb * 1024L * 1024L
            : _settings.MaxDocumentSizeMb * 1024L * 1024L;

        if (file.Length > sizeLimitBytes)
        {
            throw new InvalidOperationException(
                expectedCategory == StorageFileCategory.Video
                    ? $"Video boyutu {_settings.MaxVideoSizeMb} MB sinirini asamaz."
                    : $"Belge veya gorsel boyutu {_settings.MaxDocumentSizeMb} MB sinirini asamaz.");
        }

        var safeFileName = SanitizeFileName(Path.GetFileNameWithoutExtension(originalFileName));
        var buffer = new MemoryStream();
        await using (var source = file.OpenReadStream())
        {
            await source.CopyToAsync(buffer, cancellationToken);
        }
        buffer.Position = 0;

        EnsureMagicNumber(extension, buffer);
        buffer.Position = 0;

        return new ValidatedStorageFile
        {
            OriginalFileName = originalFileName,
            SafeFileName = safeFileName,
            FileExtension = extension,
            MimeType = NormalizeMimeType(file.ContentType),
            SizeInBytes = file.Length,
            FileCategory = expectedCategory,
            Content = buffer
        };
    }

    public void EnsureCategoryMatchesExistingFile(StorageFileCategory category, string fileExtension, string mimeType)
    {
        var normalizedExtension = fileExtension.TrimStart('.').ToLowerInvariant();
        var normalizedMimeType = NormalizeMimeType(mimeType);

        if (!_extensionMimeMap.TryGetValue(normalizedExtension, out var allowedMimeTypes))
        {
            throw new InvalidOperationException("Desteklenmeyen dosya uzantisi.");
        }

        var expectedCategory = ResolveCategory(normalizedExtension);
        if (expectedCategory != category)
        {
            throw new InvalidOperationException("Dosya kategorisi dosya turu ile uyusmuyor.");
        }

        var allowedExtensionSet = category == StorageFileCategory.Video
            ? _settings.AllowedVideoExtensions
            : _settings.AllowedDocumentExtensions;
        var allowedMimeTypeSet = category == StorageFileCategory.Video
            ? _settings.AllowedVideoMimeTypes
            : _settings.AllowedDocumentMimeTypes;

        if (!allowedExtensionSet.Contains(normalizedExtension, StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Bu uzanti icin yukleme izni yok.");
        }

        if (!allowedMimeTypeSet.Contains(normalizedMimeType, StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Bu MIME tipi icin yukleme izni yok.");
        }

        if (!allowedMimeTypes.Contains(normalizedMimeType, StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Dosya uzantisi ile MIME tipi uyusmuyor.");
        }
    }

    private static StorageFileCategory ResolveCategory(string extension)
    {
        return extension switch
        {
            "jpg" or "jpeg" or "png" or "webp" => StorageFileCategory.Image,
            "mp4" or "mov" or "webm" => StorageFileCategory.Video,
            _ => StorageFileCategory.Document
        };
    }

    private static string SanitizeFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return "file";
        }

        var normalized = fileName.Trim().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);

        foreach (var ch in normalized)
        {
            if (char.IsLetterOrDigit(ch))
            {
                builder.Append(char.ToLowerInvariant(ch));
                continue;
            }

            if (char.IsWhiteSpace(ch) || ch == '-' || ch == '_' || ch == '.')
            {
                builder.Append('-');
            }
        }

        var safe = builder.ToString().Trim('-');
        while (safe.Contains("--", StringComparison.Ordinal))
        {
            safe = safe.Replace("--", "-", StringComparison.Ordinal);
        }

        if (string.IsNullOrWhiteSpace(safe))
        {
            safe = "file";
        }

        return safe.Length > 120 ? safe[..120] : safe;
    }

    private static string NormalizeMimeType(string? mimeType)
    {
        if (string.IsNullOrWhiteSpace(mimeType))
        {
            throw new InvalidOperationException("MIME tipi gereklidir.");
        }

        return mimeType.Trim().ToLowerInvariant();
    }

    private static void EnsureMagicNumber(string extension, MemoryStream content)
    {
        content.Position = 0;

        switch (extension)
        {
            case "pdf":
                EnsureStartsWith(content, Encoding.ASCII.GetBytes("%PDF"), "PDF imzasi gecersiz.");
                break;
            case "png":
                EnsureStartsWith(content, new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, "PNG imzasi gecersiz.");
                break;
            case "jpg":
            case "jpeg":
                EnsureStartsWith(content, new byte[] { 0xFF, 0xD8, 0xFF }, "JPEG imzasi gecersiz.");
                break;
            case "webp":
                EnsureWebp(content);
                break;
            case "docx":
                EnsureValidDocx(content);
                break;
            case "mp4":
                EnsureIsoBaseMediaFile(content, "mp4");
                break;
            case "mov":
                EnsureIsoBaseMediaFile(content, "mov");
                break;
            case "webm":
                EnsureStartsWith(content, new byte[] { 0x1A, 0x45, 0xDF, 0xA3 }, "WEBM imzasi gecersiz.");
                break;
            default:
                throw new InvalidOperationException("Desteklenmeyen dosya turu.");
        }
    }

    private static void EnsureStartsWith(Stream content, byte[] expected, string message)
    {
        var buffer = new byte[expected.Length];
        var read = content.Read(buffer, 0, buffer.Length);
        if (read != expected.Length || !buffer.SequenceEqual(expected))
        {
            throw new InvalidOperationException(message);
        }
    }

    private static void EnsureWebp(Stream content)
    {
        if (content.Length < 12)
        {
            throw new InvalidOperationException("WEBP imzasi gecersiz.");
        }

        var buffer = new byte[12];
        content.Position = 0;
        var read = content.Read(buffer, 0, buffer.Length);
        if (read != buffer.Length ||
            buffer[0] != (byte)'R' ||
            buffer[1] != (byte)'I' ||
            buffer[2] != (byte)'F' ||
            buffer[3] != (byte)'F' ||
            buffer[8] != (byte)'W' ||
            buffer[9] != (byte)'E' ||
            buffer[10] != (byte)'B' ||
            buffer[11] != (byte)'P')
        {
            throw new InvalidOperationException("WEBP imzasi gecersiz.");
        }
    }

    private static void EnsureValidDocx(MemoryStream content)
    {
        EnsureStartsWith(content, new byte[] { 0x50, 0x4B, 0x03, 0x04 }, "DOCX imzasi gecersiz.");
        content.Position = 0;

        using var archive = new ZipArchive(content, ZipArchiveMode.Read, leaveOpen: true);
        var hasContentTypes = archive.Entries.Any(e => e.FullName.Equals("[Content_Types].xml", StringComparison.OrdinalIgnoreCase));
        var hasWordDocument = archive.Entries.Any(e => e.FullName.StartsWith("word/", StringComparison.OrdinalIgnoreCase));

        if (!hasContentTypes || !hasWordDocument)
        {
            throw new InvalidOperationException("Dosya gecerli bir DOCX belgesi degil.");
        }
    }

    private static void EnsureIsoBaseMediaFile(MemoryStream content, string format)
    {
        content.Position = 0;
        var buffer = new byte[Math.Min(content.Length, 64)];
        var read = content.Read(buffer, 0, buffer.Length);
        if (read < 12)
        {
            throw new InvalidOperationException("Video dosyasi gecersiz.");
        }

        var hasFtyp = buffer[4] == (byte)'f' && buffer[5] == (byte)'t' && buffer[6] == (byte)'y' && buffer[7] == (byte)'p';
        if (!hasFtyp)
        {
            throw new InvalidOperationException("Video konteyner imzasi gecersiz.");
        }

        var brand = Encoding.ASCII.GetString(buffer, 8, 4);
        var isValid = format switch
        {
            "mov" => brand.Equals("qt  ", StringComparison.Ordinal),
            _ => brand is "isom" or "iso2" or "mp41" or "mp42" or "avc1" or "M4V "
        };

        if (!isValid)
        {
            throw new InvalidOperationException("Video dosya formati dogrulanamadi.");
        }
    }
}
