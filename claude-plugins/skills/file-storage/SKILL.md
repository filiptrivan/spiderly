---
name: file-storage
description: Configure file storage providers and blob upload behavior in Spiderly. Use when setting up S3, Cloudinary, Azure Blob, or disk storage, choosing entity blob attributes, customizing upload hooks (validation, optimization), or troubleshooting file upload issues.
---

# File Storage

## Provider Selection

| Provider | Class | Best For | Returns |
|---|---|---|---|
| Disk | `DiskStorageService` | Local dev | File key |
| S3 Public | `S3PublicStorageService` | CDN-served images | Full public URL |
| S3 Private | `S3StorageService` | Authenticated files | S3 key |
| Azure Blob | `BlobStorageService` | Azure environments | Blob name |
| Cloudinary | `CloudinaryStorageService` | Image transformations | Public ID |

All providers implement `IFileManager`.

## Entity Attributes

### Attribute Combinations

```csharp
// S3 Public (CDN images) — most common
[BlobName]
[S3PublicUrl]
[AcceptedFileTypes("image/*")]
[MaxFileSize(2_000_000)]
[StringLength(1000, MinimumLength = 1)]
public string MainImage { get; set; }

// S3 Private (authenticated access)
[BlobName]
[S3Url]
[AcceptedFileTypes("application/pdf")]
[StringLength(1000, MinimumLength = 1)]
public string PrivateDocument { get; set; }

// Cloudinary
[BlobName]
[CloudinaryPublicId]
[AcceptedFileTypes("image/*")]
[StringLength(200, MinimumLength = 1)]
public string Thumbnail { get; set; }

// Disk / Azure Blob (no URL attribute)
[BlobName]
[AcceptedFileTypes("image/*")]
[MaxFileSize(5_000_000)]
[StringLength(1000, MinimumLength = 1)]
public string ProfilePicture { get; set; }
```

### Attribute Reference

| Attribute | Level | Purpose |
|---|---|---|
| `[BlobName]` | Property | Marks as file reference (required for all uploads) |
| `[S3PublicUrl]` | Property | Uses `S3PublicStorageService`, stores full CDN URL |
| `[S3Url]` | Property | Uses `S3StorageService`, stores S3 key |
| `[CloudinaryPublicId]` | Property | Uses `CloudinaryStorageService`, stores public ID |
| `[AcceptedFileTypes("image/*")]` | Property | MIME type restriction (default: `image/*`) |
| `[MaxFileSize(N)]` | Property | Max bytes (default: 20MB) |
| `[ImageWidth(N)]` | Property | Validate exact image width |
| `[ImageHeight(N)]` | Property | Validate exact image height |

`[BlobName]` maps to an existing `string` column — no migration needed when adding it.

## DI Registration

### CompositionRoot.cs (LightInject)

```csharp
// Disk (default for local dev)
registry.Register<IFileManager, DiskStorageService>();

// S3 Public — register BOTH IFileManager + named service
registry.Register<IFileManager, S3PublicStorageService>();
registry.Register<S3PublicStorageService>();

// S3 Private
registry.Register<IFileManager, S3StorageService>();

// Azure Blob
registry.Register<IFileManager, BlobStorageService>();

// Cloudinary
registry.Register<IFileManager, CloudinaryStorageService>();
registry.Register<CloudinaryStorageService>();
```

### S3 Client Registration (Startup.cs)

```csharp
services.AddSingleton<IAmazonS3>(sp =>
{
    AmazonS3Config s3Config = new AmazonS3Config
    {
        ServiceURL = SettingsProvider.Current.S3ServiceUrl,
        ForcePathStyle = true,
        AuthenticationRegion = "auto",
    };

    return new AmazonS3Client(
        new BasicAWSCredentials(
            SettingsProvider.Current.S3AccessKey,
            SettingsProvider.Current.S3SecretKey
        ),
        s3Config
    );
});
```

### Azure Blob Client Registration

```csharp
// In Startup.cs — call the Spiderly extension method:
services.SpiderlyAddAzureClients();
```

Reads `BlobStorageConnectionString` and `BlobStorageContainerName` from `SettingsProvider`.

## Configuration (appsettings.json)

```json
{
  "AppSettings": {
    "Spiderly.Shared": {
      "S3BucketName": "my-bucket",
      "S3PublicEndpoint": "https://cdn.example.com",

      "BlobStorageConnectionString": "DefaultEndpointsProtocol=...",
      "BlobStorageContainerName": "files",
      "BlobStorageUrl": "https://myaccount.blob.core.windows.net/files",

      "CloudinaryCloudName": "my-cloud",
      "CloudinaryApiKey": "123456",
      "CloudinaryApiSecret": "secret"
    }
  }
}
```

S3 credentials (`S3AccessKey`, `S3SecretKey`, `S3ServiceUrl`) are app-specific settings, not in `Spiderly.Shared`.

## Upload Flow

Generated methods per blob property:

```
1. Upload{Property}For{Entity}(IFormFile file)         ← Controller endpoint
2.   → OnBefore{Property}BlobFor{Entity}UploadIsAuthorized(file, id)
3.   → OnBefore{Property}BlobFor{Entity}IsUploaded(stream, file, id)
4.       → For image/* content types:
5.           → ValidateImageFor{Property}Of{Entity}(stream, file, id)
6.           → OptimizeImageFor{Property}Of{Entity}(stream, file, id)
7.   → storageService.UploadFileAsync(...)
8.   → Returns file key/URL
```

On entity save (Update/Insert):
```
→ storageService.DeleteNonActiveBlobs(activeKey, entityName, propertyName, entityId)
```

## Upload Hooks

Override in `BusinessService`:

```csharp
// Authorization hook — run before upload
public override async Task OnBeforeMainImageBlobForProductUploadIsAuthorized(
    IFormFile file, long id)
{
    // Custom authorization logic
}

// Full preprocessing hook — runs for ALL file types
public override async Task<byte[]> OnBeforeMainImageBlobForProductIsUploaded(
    Stream stream, IFormFile file, long id)
{
    // For images: validate then optimize
    if (file.ContentType.StartsWith("image/"))
    {
        await ValidateImageForMainImageOfProduct(stream, file, id);
        stream.Position = 0;
        return await OptimizeImageForMainImageOfProduct(stream, file, id);
    }
    return await Helper.ReadAllBytesAsync(stream);
}

// Image validation — check dimensions, format, etc.
public override async Task ValidateImageForMainImageOfProduct(
    Stream stream, IFormFile file, long id)
{
    await Helper.ValidateImageDimensions(stream, width: 800, height: 600);
}

// Image optimization — resize, compress, convert format
public override async Task<byte[]> OptimizeImageForMainImageOfProduct(
    Stream stream, IFormFile file, long id)
{
    return await Helper.OptimizeImage(stream, new Size(800, 600), quality: 80);
}
```

### Helper.OptimizeImage

```csharp
public static async Task<byte[]> OptimizeImage(
    Stream originalImageStream,
    Size? newImageSize = null,  // null = keep original size
    int quality = 85            // WebP quality
)
```

- Converts to **WebP lossy** format (via SixLabors.ImageSharp)
- Resizes with `ResizeMode.Max` (fit within bounds, not crop)
- Default quality: 85

### Helper.ValidateImageDimensions

```csharp
public static async Task ValidateImageDimensions(
    Stream imageStream,
    int width = 0,   // 0 = skip width check
    int height = 0   // 0 = skip height check
)
```

Throws `HackerException` if dimensions don't match exactly.

## Cleanup Methods

### DeleteNonActiveBlobs

Called automatically during entity save. Deletes all previously uploaded files for a property except the current active one. Uses file naming prefix to find old files.

### DeleteNonActiveEditorImages

For rich text `[Editor]` properties. Extracts `<img>` URLs from HTML, deletes uploaded images that are no longer referenced.

```csharp
List<string> activeUrls = Helper.ExtractImageUrlsFromHtml(dto.HtmlDescription);
await _s3PublicStorageService.DeleteNonActiveEditorImages(
    activeUrls, nameof(Brand), nameof(Brand.HtmlDescription) + "Image", id.ToString());
```

Only implemented for `S3PublicStorageService`. Other providers throw `NotImplementedException`.

## File Naming Convention

All providers generate: `{objectId}-{objectType}-{objectProperty}-{GUID}.{extension}`

S3 providers add folder structure: `{objectType}/{objectProperty}/{objectId}/{filename}`

This prefix-based naming enables `DeleteNonActiveBlobs` to find and clean up old files without database tracking.
