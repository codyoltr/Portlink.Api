using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Logging;
using Portlink.Api.Modules.Storage.Interfaces;
using Portlink.Api.Modules.Storage.Settings;

namespace Portlink.Api.Modules.Storage.Services;

public class S3StorageProvider : IS3StorageProvider
{
    private readonly StorageSettings _settings;
    private readonly ILogger<S3StorageProvider> _logger;
    private readonly IAmazonS3 _client;

    public S3StorageProvider(StorageSettings settings, ILogger<S3StorageProvider> logger)
    {
        _settings = settings;
        _logger = logger;
        _settings.Validate();

        var config = new AmazonS3Config
        {
            RegionEndpoint = RegionEndpoint.GetBySystemName(_settings.AwsRegion),
            ForcePathStyle = _settings.S3ForcePathStyle
        };

        if (!string.IsNullOrWhiteSpace(_settings.S3Endpoint))
        {
            config.ServiceURL = _settings.S3Endpoint;
        }

        _client = new AmazonS3Client(
            new BasicAWSCredentials(_settings.AwsAccessKeyId, _settings.AwsSecretAccessKey),
            config);
    }

    public async Task UploadAsync(string key, Stream content, string contentType, CancellationToken cancellationToken = default)
    {
        try
        {
            content.Position = 0;
            await _client.PutObjectAsync(new PutObjectRequest
            {
                BucketName = _settings.S3BucketName,
                Key = key,
                InputStream = content,
                ContentType = contentType,
                AutoCloseStream = false
            }, cancellationToken);
        }
        catch (AmazonS3Exception ex)
        {
            _logger.LogError(ex, "S3 yukleme hatasi. Key: {Key}", key);
            throw new InvalidOperationException("Dosya depolama servisine yuklenemedi.");
        }
    }

    public async Task<StorageProviderDownloadResult> DownloadAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _client.GetObjectAsync(new GetObjectRequest
            {
                BucketName = _settings.S3BucketName,
                Key = key
            }, cancellationToken);

            return new StorageProviderDownloadResult
            {
                Stream = response.ResponseStream,
                ContentType = response.Headers.ContentType ?? "application/octet-stream",
                ContentLength = response.Headers.ContentLength
            };
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            throw new KeyNotFoundException("Dosya depolama alaninda bulunamadi.");
        }
        catch (AmazonS3Exception ex)
        {
            _logger.LogError(ex, "S3 indirme hatasi. Key: {Key}", key);
            throw new InvalidOperationException("Dosya depolama servisinden okunamadi.");
        }
    }

    public async Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            await _client.DeleteObjectAsync(new DeleteObjectRequest
            {
                BucketName = _settings.S3BucketName,
                Key = key
            }, cancellationToken);
        }
        catch (AmazonS3Exception ex)
        {
            _logger.LogError(ex, "S3 silme hatasi. Key: {Key}", key);
            throw new InvalidOperationException("Dosya depolama alanindan silinemedi.");
        }
    }

    public string GeneratePresignedDownloadUrl(string key, string downloadFileName, TimeSpan expiresIn)
    {
        try
        {
            return _client.GetPreSignedURL(new GetPreSignedUrlRequest
            {
                BucketName = _settings.S3BucketName,
                Key = key,
                Expires = DateTime.UtcNow.Add(expiresIn),
                ResponseHeaderOverrides = new ResponseHeaderOverrides
                {
                    ContentDisposition = $"attachment; filename=\"{downloadFileName}\""
                }
            });
        }
        catch (AmazonS3Exception ex)
        {
            _logger.LogError(ex, "Presigned URL olusturma hatasi. Key: {Key}", key);
            throw new InvalidOperationException("Gecici erisim baglantisi olusturulamadi.");
        }
    }

    public string GeneratePresignedViewUrl(string key, TimeSpan expiresIn)
    {
        try
        {
            return _client.GetPreSignedURL(new GetPreSignedUrlRequest
            {
                BucketName = _settings.S3BucketName,
                Key = key,
                Expires = DateTime.UtcNow.Add(expiresIn)
            });
        }
        catch (AmazonS3Exception ex)
        {
            _logger.LogError(ex, "Presigned view URL olusturma hatasi. Key: {Key}", key);
            throw new InvalidOperationException("Gecici gorunum baglantisi olusturulamadi.");
        }
    }
}
