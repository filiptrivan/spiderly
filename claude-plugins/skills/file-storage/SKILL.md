---
name: file-storage
description: Configure file storage providers and blob upload behavior in Spiderly. Use when setting up storage for entity blob properties, choosing a built-in adapter, writing a custom adapter, or troubleshooting file upload issues.
---

# File Storage

## How storage is selected

Per blob property. Decorate a string property with a `StorageAttribute` subclass; the source generator emits upload/delete code that resolves the matching `IFileManager` adapter from DI for that property only. There is no global storage registration — every blob property declares which adapter it goes through.

## Built-in adapters

Spiderly ships three built-in adapters and matching attributes:

| Adapter | Attribute | Best for | Returns |
| --- | --- | --- | --- |
| `DiskStorageService` | `[DiskStorage]` | Local development | File key |
| `S3PublicStorageService` | `[S3PublicStorage]` | CDN-served images, public assets | Full public URL |
| `S3PrivateStorageService` | `[S3PrivateStorage]` | Private documents, signed-URL access | S3 key |

All three implement `Spiderly.Shared.Interfaces.IFileManager`.

## Entity property declaration

```csharp
public class Brand : BusinessObject<int>
{
    [S3PublicStorage]
    [AcceptedFileTypes("image/*")]
    [MaxFileSize(2_000_000)]
    [StringLength(1000, MinimumLength = 1)]
    public string LogoUrl { get; set; }
}

public class WarrantyRegistration : BusinessObject<long>
{
    [S3PrivateStorage]
    [AcceptedFileTypes("image/jpeg", "image/png", "application/pdf")]
    [MaxFileSize(10_000_000)]
    [StringLength(1000, MinimumLength = 1)]
    public string ReceiptImageUrl { get; set; }
}
```

`[StorageAttribute]` subclasses replace the legacy `[BlobName]` marker — presence of any subclass is what marks a string property as a blob.

| Attribute | Level | Purpose |
| --- | --- | --- |
| `[DiskStorage]` / `[S3PublicStorage]` / `[S3PrivateStorage]` | Property | Routes uploads through the matching adapter |
| `[AcceptedFileTypes("mime/type", ...)]` | Property | **Required on every blob property** — MIME-type whitelist. Build error `SPIDERLY014` if missing. No default. |
| `[MaxFileSize(N)]` | Property | Max bytes (default: 20MB) |
| `[ImageWidth(N)]` / `[ImageHeight(N)]` | Property | Validate exact image dimensions |

## DI registration

Spiderly's source generator emits `_deps.ServiceProvider.GetRequiredService<TConcrete>()` per blob property, so each adapter you use must be discoverable by its concrete type.

`DiskStorageService` is pre-registered by the `spiderly init` template in `AppServiceExtensions.AddAppServices` (it's the dev default and has no external dependencies). When you opt in to S3, register the adapters you reference there:

```csharp
// In your AppServiceExtensions.cs
services.AddSingleton<S3PublicStorageService>();
services.AddSingleton<S3PrivateStorageService>();
```

The storage services are stateless wrappers around external clients (the `IAmazonS3` instance for S3, the local filesystem for Disk) — `Singleton` avoids per-resolve constructor work like the `Directory.CreateDirectory` call in `DiskStorageService`.

The S3 client itself is registered separately (one `IAmazonS3` shared by both S3 adapters):

```csharp
services.AddSingleton<IAmazonS3>(sp =>
{
    IConfiguration configuration = sp.GetRequiredService<IConfiguration>();
    AmazonS3Config s3Config = new AmazonS3Config
    {
        ServiceURL = configuration.GetValue<string>($"{Spiderly.Shared.Settings.ConfigurationSection}:S3ServiceUrl"),
        ForcePathStyle = true,
        AuthenticationRegion = "auto",
    };

    return new AmazonS3Client(
        new BasicAWSCredentials(
            configuration.GetValue<string>($"{Spiderly.Shared.Settings.ConfigurationSection}:S3AccessKey"),
            configuration.GetValue<string>($"{Spiderly.Shared.Settings.ConfigurationSection}:S3SecretKey")
        ),
        s3Config
    );
});
```

## Configuration (`appsettings.json`)

```json
{
  "AppSettings": {
    "Spiderly.Shared": {
      "S3BucketName": "my-bucket",
      "S3PublicEndpoint": "https://cdn.example.com"
    }
  }
}
```

S3 credentials (`S3AccessKey`, `S3SecretKey`, `S3ServiceUrl`) are app-specific settings, not in `Spiderly.Shared`.

## Writing a custom storage adapter

Spiderly ships only the three adapters above. Other backends (Cloudinary, Azure Blob, Backblaze, on-prem MinIO, …) are written by the consumer:

1. Implement `IFileManager`:

   ```csharp
   public class MyCustomStorageService : IFileManager
   {
       public Task<string> UploadFileAsync(...) { /* impl */ }
       public Task DeleteNonActiveBlobs(...)    { /* impl */ }
       public Task<string> GetFileDataAsync(string key) { /* impl */ }
       public Task<string> MoveBlobToEntityPathAsync(...) { /* impl */ }
       public Task DeleteNonActiveEditorImages(...) { /* impl */ }
   }
   ```

2. Subclass `StorageAttribute`, passing your service type to the base constructor:

   ```csharp
   public sealed class MyCustomStorageAttribute : StorageAttribute
   {
       public MyCustomStorageAttribute() : base(typeof(MyCustomStorageService)) { }
   }
   ```

3. Register the service in DI:

   ```csharp
   services.AddTransient<MyCustomStorageService>();
   ```

4. Use the attribute on entity properties:

   ```csharp
   [MyCustomStorage]
   [AcceptedFileTypes("image/*")]
   [StringLength(1000, MinimumLength = 1)]
   public string Photo { get; set; }
   ```

The source generator detects custom storage attributes by the convention "attribute name ends with `Storage`" — so `MyCustomStorageAttribute` is treated as a blob marker automatically. The generator's auto-resolution of the field name is currently hard-coded for the three built-ins; for custom adapters, the generated code emits a marker comment indicating it can't dispatch — you'll need to inject your custom service directly into hand-written upload paths instead of relying on Spiderly's auto-CRUD endpoints. (Open issue: extend the source generator to read `StorageAttribute.ServiceType` from the subclass's base initializer to support fully-automatic custom-adapter routing.)

## Upload flow

Generated methods per blob property:

```
1. Upload{Property}For{Entity}(IFormFile file)         ← Controller endpoint
2.   → OnBefore{Property}BlobFor{Entity}UploadIsAuthorized(file, id)
3.   → OnBefore{Property}BlobFor{Entity}IsUploaded(stream, file, id)
4.       → For image/* content types:
5.           → ValidateImageFor{Property}Of{Entity}(stream, file, id)
6.           → OptimizeImageFor{Property}Of{Entity}(stream, file, id)
7.   → storageService.UploadFileAsync(...)            ← resolved per [*Storage] attribute
8.   → Returns file key/URL (semantics depend on the adapter)
```

On entity save (Update/Insert):

```
→ storageService.DeleteNonActiveBlobs(activeKey, entityName, propertyName, entityId)
```

## Upload hooks

Override in your entity service class:

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

Throws `SecurityViolationException` if dimensions don't match exactly.

## Cleanup methods

### `DeleteNonActiveBlobs`

Called automatically during entity save. Deletes all previously uploaded files for a property except the current active one. Uses file naming prefix to find old files.

### `DeleteNonActiveEditorImages`

For rich text `[Editor]` properties paired with `[S3PublicStorage]`. Extracts `<img>` URLs from HTML, deletes uploaded images that are no longer referenced.

```csharp
List<string> activeUrls = Helper.ExtractImageUrlsFromHtml(dto.HtmlDescription);
await _s3PublicStorageService.DeleteNonActiveEditorImages(
    activeUrls, nameof(Brand), nameof(Brand.HtmlDescription) + "Image", id.ToString());
```

Only implemented for `S3PublicStorageService`. Other built-in providers throw `NotImplementedException`.

## File naming convention

All providers generate: `{objectId}-{objectType}-{objectProperty}-{GUID}.{extension}`

S3 providers add folder structure: `{objectType}/{objectProperty}/{objectId}/{filename}`

This prefix-based naming enables `DeleteNonActiveBlobs` to find and clean up old files without database tracking.
