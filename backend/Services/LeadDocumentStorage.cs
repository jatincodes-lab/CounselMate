using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace EducationCrm.Api.Services;

public interface ILeadDocumentStorage
{
    bool IsConfigured { get; }
    Task<LeadDocumentUploadResult> UploadAsync(IFormFile file, LeadDocumentUploadContext context, CancellationToken cancellationToken);
    Task DeleteAsync(string publicId, string resourceType, string deliveryType, CancellationToken cancellationToken);
}

public sealed record LeadDocumentUploadContext(Guid TenantId, string LeadNumber, Guid DocumentTypeId);

public sealed record LeadDocumentUploadResult(
    string AssetId,
    string PublicId,
    string ResourceType,
    string DeliveryType,
    string? SecureUrl,
    long Bytes,
    string ContentType);

public sealed class CloudinaryLeadDocumentStorage(HttpClient httpClient, IConfiguration configuration) : ILeadDocumentStorage
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly CloudinarySettings settings = CloudinarySettings.FromConfiguration(configuration);

    public bool IsConfigured => settings.IsConfigured;

    public async Task<LeadDocumentUploadResult> UploadAsync(IFormFile file, LeadDocumentUploadContext context, CancellationToken cancellationToken)
    {
        if (!settings.IsConfigured)
        {
            throw new InvalidOperationException("Cloudinary document storage is not configured.");
        }

        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var folder = $"{settings.FolderRoot}/{context.TenantId:N}/leads/{SanitizePathSegment(context.LeadNumber)}/documents/{context.DocumentTypeId:N}";
        var publicId = Guid.NewGuid().ToString("N");
        var deliveryType = settings.DeliveryType;

        var signedValues = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["folder"] = folder,
            ["overwrite"] = "false",
            ["public_id"] = publicId,
            ["timestamp"] = timestamp,
            ["type"] = deliveryType
        };

        using var content = new MultipartFormDataContent();
        await using var stream = file.OpenReadStream();
        var fileContent = new StreamContent(stream);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType);
        content.Add(fileContent, "file", file.FileName);
        content.Add(new StringContent(settings.ApiKey), "api_key");
        content.Add(new StringContent(timestamp), "timestamp");
        content.Add(new StringContent(folder), "folder");
        content.Add(new StringContent(publicId), "public_id");
        content.Add(new StringContent(deliveryType), "type");
        content.Add(new StringContent("false"), "overwrite");
        content.Add(new StringContent(CreateApiSignature(signedValues, settings.ApiSecret)), "signature");

        using var response = await httpClient.PostAsync(
            $"https://api.cloudinary.com/v1_1/{settings.CloudName}/auto/upload",
            content,
            cancellationToken);

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Cloudinary upload failed with status {(int)response.StatusCode}: {GetCloudinaryError(body)}");
        }

        var upload = JsonSerializer.Deserialize<CloudinaryUploadResponse>(body, JsonOptions)
            ?? throw new InvalidOperationException("Cloudinary upload response was empty.");

        if (string.IsNullOrWhiteSpace(upload.PublicId) || string.IsNullOrWhiteSpace(upload.ResourceType))
        {
            throw new InvalidOperationException("Cloudinary upload response did not include an asset identifier.");
        }

        return new LeadDocumentUploadResult(
            upload.AssetId ?? string.Empty,
            upload.PublicId,
            upload.ResourceType,
            upload.Type ?? deliveryType,
            upload.SecureUrl,
            upload.Bytes == 0 ? file.Length : upload.Bytes,
            string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType);
    }

    public async Task DeleteAsync(string publicId, string resourceType, string deliveryType, CancellationToken cancellationToken)
    {
        if (!settings.IsConfigured || string.IsNullOrWhiteSpace(publicId) || string.IsNullOrWhiteSpace(resourceType))
        {
            return;
        }

        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var type = string.IsNullOrWhiteSpace(deliveryType) ? settings.DeliveryType : deliveryType;
        var signedValues = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["public_id"] = publicId,
            ["timestamp"] = timestamp,
            ["type"] = type
        };

        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["api_key"] = settings.ApiKey,
            ["timestamp"] = timestamp,
            ["public_id"] = publicId,
            ["type"] = type,
            ["signature"] = CreateApiSignature(signedValues, settings.ApiSecret)
        });

        using var response = await httpClient.PostAsync(
            $"https://api.cloudinary.com/v1_1/{settings.CloudName}/{resourceType}/destroy",
            content,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"Cloudinary delete failed with status {(int)response.StatusCode}: {GetCloudinaryError(body)}");
        }
    }

    private static string CreateApiSignature(SortedDictionary<string, string> values, string apiSecret)
    {
        var payload = string.Join("&", values.Where(item => !string.IsNullOrWhiteSpace(item.Value)).Select(item => $"{item.Key}={item.Value}"));
        var bytes = SHA1.HashData(Encoding.UTF8.GetBytes(payload + apiSecret));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string SanitizePathSegment(string value)
    {
        var normalized = Regex.Replace(value.Trim().ToLowerInvariant(), @"[^a-z0-9_-]+", "-");
        normalized = normalized.Trim('-');
        return string.IsNullOrWhiteSpace(normalized) ? "lead" : normalized;
    }

    private static string GetCloudinaryError(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("error", out var error) &&
                error.TryGetProperty("message", out var message))
            {
                return message.GetString() ?? "Unknown Cloudinary error.";
            }
        }
        catch
        {
            return "Unknown Cloudinary error.";
        }

        return "Unknown Cloudinary error.";
    }

    private sealed record CloudinaryUploadResponse(
        [property: JsonPropertyName("asset_id")] string? AssetId,
        [property: JsonPropertyName("public_id")] string PublicId,
        [property: JsonPropertyName("resource_type")] string ResourceType,
        [property: JsonPropertyName("type")] string? Type,
        [property: JsonPropertyName("secure_url")] string? SecureUrl,
        [property: JsonPropertyName("bytes")] long Bytes);

    private sealed record CloudinarySettings(
        string CloudName,
        string ApiKey,
        string ApiSecret,
        string FolderRoot,
        string DeliveryType)
    {
        public bool IsConfigured =>
            !string.IsNullOrWhiteSpace(CloudName) &&
            !string.IsNullOrWhiteSpace(ApiKey) &&
            !string.IsNullOrWhiteSpace(ApiSecret);

        public static CloudinarySettings FromConfiguration(IConfiguration configuration)
        {
            return new CloudinarySettings(
                configuration["Cloudinary:CloudName"] ?? Environment.GetEnvironmentVariable("CLOUDINARY_CLOUD_NAME") ?? string.Empty,
                configuration["Cloudinary:ApiKey"] ?? Environment.GetEnvironmentVariable("CLOUDINARY_API_KEY") ?? string.Empty,
                configuration["Cloudinary:ApiSecret"] ?? Environment.GetEnvironmentVariable("CLOUDINARY_API_SECRET") ?? string.Empty,
                NormalizeRoot(configuration["Cloudinary:FolderRoot"] ?? Environment.GetEnvironmentVariable("CLOUDINARY_FOLDER_ROOT") ?? "counselmate"),
                NormalizeDeliveryType(configuration["Cloudinary:DeliveryType"] ?? Environment.GetEnvironmentVariable("CLOUDINARY_DELIVERY_TYPE") ?? "upload"));
        }

        private static string NormalizeRoot(string value)
        {
            var normalized = Regex.Replace(value.Trim().ToLowerInvariant(), @"[^a-z0-9/_-]+", "-").Trim('/');
            return string.IsNullOrWhiteSpace(normalized) ? "counselmate" : normalized;
        }

        private static string NormalizeDeliveryType(string value)
        {
            return string.Equals(value, "authenticated", StringComparison.OrdinalIgnoreCase) ? "authenticated" : "upload";
        }
    }
}

public static class LeadDocumentFileRules
{
    public const long MaximumFileBytes = 10 * 1024 * 1024;

    public static readonly IReadOnlyDictionary<string, string[]> AllowedContentTypesByExtension = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = ["application/pdf"],
        [".jpg"] = ["image/jpeg"],
        [".jpeg"] = ["image/jpeg"],
        [".png"] = ["image/png"],
        [".doc"] = ["application/msword", "application/octet-stream"],
        [".docx"] = ["application/vnd.openxmlformats-officedocument.wordprocessingml.document", "application/zip", "application/octet-stream"]
    };

    public static IReadOnlyCollection<string> AllowedExtensions => AllowedContentTypesByExtension.Keys.ToArray();
}
