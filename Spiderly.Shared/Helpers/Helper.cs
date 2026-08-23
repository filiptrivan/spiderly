using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Primitives;
using MimeDetective;
using Newtonsoft.Json.Linq;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;
using Spiderly.Shared.Exceptions;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Globalization;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;

namespace Spiderly.Shared.Helpers
{
    public static class Helper
    {
        public static void WriteToFile(string data, string path)
        {
            if (data != null)
            {
                StreamWriter sw = new StreamWriter(path, false);
                sw.WriteLine(data);
                sw.Close();
            }
        }

        public static string MakeFolder(string path, string name)
        {
            if (!Directory.Exists(path))
                throw new BusinessException($"Folder '{path}' not found.");

            string newFolderPath = Path.Combine(path, name);

            FolderOverrideCheck(newFolderPath);

            Directory.CreateDirectory(newFolderPath);

            return newFolderPath;
        }

        public static void FolderOverrideCheck(string path)
        {
            if (Directory.Exists(path))
            {
                throw new BusinessException($"Folder '{path}' already exists.");
            }
        }

        public static void FileOverrideCheck(string path)
        {
            if (File.Exists(path))
            {
                throw new BusinessException($"File '{path}' already exists.");
            }
        }

        public static bool AreDatesEqualToSeconds(DateTime? date1, DateTime? date2)
        {
            if (!date1.HasValue && !date2.HasValue) return true; // Both null are considered equal
            if (!date1.HasValue || !date2.HasValue) return false; // One is null, and the other is not

            // Truncate both dates to seconds
            var truncatedDate1 = date1.Value.AddTicks(-(date1.Value.Ticks % TimeSpan.TicksPerSecond));
            var truncatedDate2 = date2.Value.AddTicks(-(date2.Value.Ticks % TimeSpan.TicksPerSecond));

            return truncatedDate1 == truncatedDate2;
        }

        public static bool AreIdsDifferent<ID>(List<ID> ids1, List<ID> ids2) where ID : struct
        {
            return ids1.Except(ids2).Any() || ids2.Except(ids1).Any();
        }

        public static ID GetObjectIdFromFileName<ID>(string fileName) where ID : struct
        {
            List<string> parts = fileName.Split('-').ToList();

            if (parts.Count < 2)
                throw new SecurityViolationException($"Invalid file name format ({fileName}).");

            string idPart = parts[0];

            // Try to convert the string part to the specified struct type
            if (TypeDescriptor.GetConverter(typeof(ID)).IsValid(idPart))
                return (ID)TypeDescriptor.GetConverter(typeof(ID)).ConvertFromString(idPart)!; // Non-null: IsValid guarantees the conversion succeeds, and ID is a struct

            throw new InvalidCastException($"Cannot convert '{idPart}' to {typeof(ID)}. Id part can't be null, for new objects it should be 0.");
        }

        public static string GetFileExtensionFromFileName(string fileName)
        {
            List<string> parts = fileName.Split('.').ToList();

            if (parts.Count < 2) // It could be only 2, it's not the same validation as spliting with '-'
                throw new SecurityViolationException($"Invalid file name format ({fileName}).");

            return parts.Last(); // The file could be .abc.png
        }

        #region SQL Server

        public static string? CreateSqlServerConnectionString(string databaseName)
        {
            string? dockerConnectionString = TryDockerSqlServerConnection(databaseName);
            if (dockerConnectionString != null)
                return dockerConnectionString;

            return TryWindowsAuthSqlServerConnection(databaseName);
        }

        private static string? TryDockerSqlServerConnection(string databaseName)
        {
            string dataSource = "localhost,14330";
            Console.WriteLine($"  Trying Docker SQL Server at {dataSource}...");
            SqlConnectionStringBuilder connectionStringBuilder = BuildDockerSqlConnectionString(dataSource, connectTimeout: 3);

            if (TrySqlServerConnection(connectionStringBuilder.ConnectionString))
            {
                connectionStringBuilder.InitialCatalog = databaseName;
                connectionStringBuilder.ConnectTimeout = 15;
                return connectionStringBuilder.ConnectionString;
            }

            return null;
        }

        private static string? TryWindowsAuthSqlServerConnection(string databaseName)
        {
            List<string> dataSources = new List<string>
            {
                "localhost",
                @"localhost\SQLEXPRESS",
                @"(localdb)\MSSQLLocalDB"
            };

            foreach (string dataSource in dataSources)
            {
                Console.WriteLine($"  Trying Windows Auth SQL Server at {dataSource}...");
                SqlConnectionStringBuilder connectionStringBuilder = BuildWindowsAuthSqlConnectionString(dataSource, connectTimeout: 3);

                if (TrySqlServerConnection(connectionStringBuilder.ConnectionString))
                {
                    connectionStringBuilder.InitialCatalog = databaseName;
                    connectionStringBuilder.ConnectTimeout = 15;
                    return connectionStringBuilder.ConnectionString;
                }
            }

            return null;
        }

        private static bool TrySqlServerConnection(string connectionString)
        {
            try
            {
                using SqlConnection connection = new SqlConnection(connectionString);
                connection.Open();
                return true;
            }
            catch { }

            return false;
        }

        private static SqlConnectionStringBuilder BuildDockerSqlConnectionString(string dataSource, int connectTimeout = 15)
        {
            return new SqlConnectionStringBuilder
            {
                DataSource = dataSource,
                InitialCatalog = "master",
                UserID = "sa",
                Password = "SqlServer123",
                Encrypt = false,
                MultipleActiveResultSets = true,
                ConnectTimeout = connectTimeout,
                TrustServerCertificate = true
            };
        }

        private static SqlConnectionStringBuilder BuildWindowsAuthSqlConnectionString(string dataSource, int connectTimeout = 15)
        {
            return new SqlConnectionStringBuilder
            {
                DataSource = dataSource,
                InitialCatalog = "master",
                IntegratedSecurity = true,
                Encrypt = false,
                MultipleActiveResultSets = true,
                ConnectTimeout = connectTimeout,
            };
        }

        #endregion

        #region PostgreSQL

        public static string? CreatePostgreSQLConnectionString(string databaseName)
        {
            List<(string password, bool useIntegratedSecurity)> authMethods = new List<(string, bool)>
            {
                ("", true),
                ("", false),
                ("postgres", false),
                ("password", false),
                ("admin", false)
            };

            return TryPostgreSQLConnection(databaseName, authMethods);
        }

        public static string? CreatePostgreSQLConnectionString(string databaseName, string customPassword)
        {
            List<(string password, bool useIntegratedSecurity)> authMethods = new List<(string, bool)>
            {
                (customPassword, false)
            };

            return TryPostgreSQLConnection(databaseName, authMethods);
        }

        private static string? TryPostgreSQLConnection(string databaseName, List<(string password, bool useIntegratedSecurity)> authMethods)
        {
            List<(string host, int port)> dataSources = new List<(string, int)>
            {
                ("localhost", 54320),
                ("localhost", 5432),
                ("127.0.0.1", 5432)
            };

            foreach ((string host, int port) in dataSources)
            {
                foreach ((string password, bool useIntegratedSecurity) in authMethods)
                {
                    string authType = useIntegratedSecurity ? "Integrated Security" : (string.IsNullOrEmpty(password) ? "no password" : "password auth");
                    Console.WriteLine($"  Trying PostgreSQL at {host}:{port} with {authType}...");
                    string connectionString = BuildPostgresConnectionString(host, port, "postgres", password, useIntegratedSecurity);
                    if (TryConnectPostgres(connectionString))
                    {
                        return BuildPostgresConnectionString(host, port, databaseName, password, useIntegratedSecurity);
                    }
                }
            }

            return null;
        }

        private static string BuildPostgresConnectionString(string host, int port, string database, string password, bool useIntegratedSecurity)
        {
            if (useIntegratedSecurity && string.IsNullOrEmpty(password))
            {
                return $"Host={host};Port={port};Database={database};Username=postgres;Integrated Security=true;";
            }
            if (string.IsNullOrEmpty(password))
            {
                return $"Host={host};Port={port};Database={database};Username=postgres;";
            }
            return $"Host={host};Port={port};Database={database};Username=postgres;Password={password};";
        }

        private static bool TryConnectPostgres(string connectionString)
        {
            try
            {
                string connectionStringWithTimeout = connectionString + "Timeout=3;";
                using (Npgsql.NpgsqlConnection connection = new Npgsql.NpgsqlConnection(connectionStringWithTimeout))
                {
                    connection.Open();
                    return true;
                }
            }
            catch { }

            return false;
        }

        #endregion

        #region Security

        #region JWT

        /// <summary>
        /// Reads the access token from the Authorization: Bearer header, or <c>null</c> when the request carries none.
        /// </summary>
        public static string? GetAccessTokenFromHeader(HttpContext context)
        {
            string? authHeader = context.Request.Headers["Authorization"].FirstOrDefault();
            if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                string token = authHeader.Substring("Bearer ".Length).Trim();
                if (!string.IsNullOrEmpty(token))
                    return token;
            }

            return null;
        }

        /// <summary>
        /// Whether the request carries a bearer token, without materializing it. A presence check on a global
        /// path must not copy the token: <see cref="GetAccessTokenFromHeader"/> allocates a fresh string holding
        /// the whole JWT (600-1200 chars) only for the caller to compare it to null.
        /// </summary>
        public static bool HasBearerToken(HttpContext context)
        {
            StringValues authHeader = context.Request.Headers.Authorization;
            if (authHeader.Count == 0)
                return false;

            string? value = authHeader[0];

            return value != null
                && value.AsSpan().StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                && value.AsSpan("Bearer ".Length).IsWhiteSpace() == false;
        }

        /// <summary>
        /// Reads the access token from the cookie, or <c>null</c> when the cookie is absent or blank.
        /// </summary>
        public static string? GetAccessTokenFromCookie(HttpContext context, string accessTokenKey)
        {
            if (context.Request.Cookies.TryGetValue(accessTokenKey, out string? cookieToken) &&
                !string.IsNullOrWhiteSpace(cookieToken))
            {
                return cookieToken;
            }

            return null;
        }

        /// <summary>
        /// Generates a cryptographically secure JWT secret key as a Base64 string. <br/><br/>
        /// The strength depends on the byte size (default 64 bytes = 512 bits ~ 88 characters).
        /// </summary>
        /// <param name="byteSize">Number of random bytes (default: 64)</param>
        /// <returns>Base64-encoded secret key</returns>
        public static string GenerateJwtSecretKey(int byteSize = 64)
        {
            if (byteSize < 1)
                throw new ArgumentException("Byte size must be at least 1.", nameof(byteSize));

            byte[] randomBytes = new byte[byteSize];
            using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(randomBytes);
            }
            return Convert.ToBase64String(randomBytes);
        }

        #endregion

        #region IP Address

        public static string? GetIPAddress(HttpContext httpContext)
        {
            return httpContext.Connection.RemoteIpAddress?.ToString();
        }

        /// <summary>
        /// Fallback used in place of a client IP that could not be determined. A single shared bucket, so a
        /// deployment where the IP is always null (Kestrel on a Unix socket behind nginx, fixed by
        /// <c>UseForwardedHeaders</c>) throttles visibly instead of hiding the misconfiguration.
        /// </summary>
        public const string UnknownIPAddress = "unknown";

        /// <summary>
        /// The client IP, or <see cref="UnknownIPAddress"/> when there isn't one. Use this wherever the IP
        /// becomes a rate-limit partition key: <see cref="GetIPAddress"/> is null on non-socket transports
        /// (in-memory test servers, Unix-domain sockets), and the limiter keys its partitions in a
        /// dictionary — a null key throws rather than degrading.
        /// </summary>
        public static string GetIPAddressOrUnknown(HttpContext httpContext)
        {
            return GetIPAddress(httpContext) ?? UnknownIPAddress;
        }

        /// <summary>
        /// The authenticated API-key principal's id, or <c>null</c> when the request is anonymous or
        /// carries a non-machine principal. Reads the validated principal stamped by the authentication
        /// middleware — never the raw API-key header, whose unvalidated value must not be trusted as a
        /// partition/identity key. Only meaningful in middleware that runs after <c>UseAuthentication</c>.
        /// </summary>
        public static string? GetAuthenticatedApiKeyId(HttpContext httpContext)
        {
            ClaimsPrincipal user = httpContext.User;
            if (user?.Identity?.IsAuthenticated != true)
                return null;
            if (user.FindFirstValue(Authorization.PrincipalClaims.PrincipalKind) != Authorization.PrincipalKinds.ApiKey)
                return null;
            return user.FindFirstValue(ClaimTypes.NameIdentifier);
        }

        #endregion

        #endregion

        #region Image Processing

        /// <summary>
        /// Compresses an image, then exports it as WebP.
        /// </summary>
        /// <param name="originalImageStream">Original image stream</param>
        /// <param name="quality">Compression quality (1-100)</param>
        /// <param name="newImageSize">New image size (optional)</param>
        /// <returns>Optimized image byte[]</returns>
        public static async Task<byte[]> OptimizeImage(
            Stream originalImageStream,
            Size? newImageSize = null,
            int quality = 85
        )
        {
            (byte[] bytes, _, _) = await OptimizeImageWithDimensions(originalImageStream, newImageSize, quality);
            return bytes;
        }

        /// <summary>
        /// Compresses an image, exports it as WebP, and returns the dimensions of the encoded result.
        /// Use this overload when callers need the final width/height — for example, the Spiderly editor
        /// upload endpoints write the dimensions onto the inserted &lt;img&gt; tag to prevent storefront
        /// layout shift.
        /// </summary>
        /// <param name="originalImageStream">Original image stream</param>
        /// <param name="newImageSize">New image size (optional)</param>
        /// <param name="quality">Compression quality (1-100)</param>
        /// <returns>Optimized image bytes plus the post-resize width and height in pixels</returns>
        public static async Task<(byte[] Bytes, int Width, int Height)> OptimizeImageWithDimensions(
            Stream originalImageStream,
            Size? newImageSize = null,
            int quality = 85
        )
        {
            using (Image image = await Image.LoadAsync(originalImageStream))
            {
                // Don't separate Image resizing and optimizing, we always want resizing first, then optimizing.
                if (newImageSize != null)
                {
                    image.Mutate(ctx => ctx
                        .Resize(new ResizeOptions
                        {
                            Mode = ResizeMode.Max, // Fit within the bounds of maxWidth and maxHeight
                            Size = newImageSize.Value
                        })
                    );
                }

                int width = image.Width;
                int height = image.Height;

                using (MemoryStream outputStream = new())
                {
                    WebpEncoder encoder = new WebpEncoder
                    {
                        Quality = quality,
                        FileFormat = WebpFileFormatType.Lossy
                    };

                    await image.SaveAsWebpAsync(outputStream, encoder);

                    return (outputStream.ToArray(), width, height);
                }
            }
        }

        public static async Task ValidateImageDimensions(
            Stream imageStream,
            int width = 0,
            int height = 0,
            IStringLocalizer? localizer = null
        )
        {
            ImageInfo imageInfo = await Image.IdentifyAsync(imageStream);
            int actualWidth = imageInfo.Width;
            int actualHeight = imageInfo.Height;

            if (width > 0 && actualWidth != width)
                throw new BusinessException(localizer?["ImageWidthMustBeExact", width, actualWidth]
                    ?? $"Image width must be exactly {width}px (current: {actualWidth}px).");

            if (height > 0 && actualHeight != height)
                throw new BusinessException(localizer?["ImageHeightMustBeExact", height, actualHeight]
                    ?? $"Image height must be exactly {height}px (current: {actualHeight}px).");
        }

        public static void ValidateFileSize(long fileSize, int maxFileSize, IStringLocalizer? localizer = null)
        {
            if (maxFileSize > 0 && fileSize > maxFileSize)
                throw new BusinessException(localizer?["FileSizeExceeded", maxFileSize / 1_000_000]
                    ?? $"File size must not exceed {maxFileSize / 1_000_000} MB.");
        }

        /// <summary>
        /// SVG is special-cased throughout upload validation: it's the one image type with no magic
        /// bytes (validated structurally as XML instead) and the one ImageSharp cannot decode
        /// (skips optimization). See <see cref="Attributes.Entity.AcceptedFileTypesAttribute"/>.
        /// </summary>
        private const string SvgMimeType = "image/svg+xml";

        /// <summary>
        /// XXE-safe reader settings for sniffing uploaded SVG content: DTDs ignored (vector editors
        /// commonly emit a DOCTYPE line) and external resolution disabled. <c>XmlReader.Create</c>
        /// clones the settings, so the shared instance is thread-safe.
        /// </summary>
        private static readonly XmlReaderSettings SvgReaderSettings = new()
        {
            DtdProcessing = DtdProcessing.Ignore,
            XmlResolver = null,
            CloseInput = false,
        };

        /// <summary>
        /// True for content types the generated <c>OnBefore*IsUploaded</c> hook may route through
        /// ImageSharp validation/optimization — raster <c>image/*</c> types only. SVG is an image
        /// content type but a vector text format ImageSharp cannot decode; it must pass through raw
        /// (its safety is enforced by <see cref="ValidateFileSignature"/>'s XML content validation).
        /// </summary>
        public static bool IsOptimizableImage(string contentType)
        {
            return contentType != null
                && contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)
                && !contentType.Equals(SvgMimeType, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Returns <paramref name="fileName"/> with its extension corrected to match what the
        /// bytes actually are. Blob keys and Content-Type are derived from the file name, but the
        /// optimize hooks may transcode the content (rasters → WebP by default, anything a
        /// consumer override produces) — this keeps the stored key honest without trusting what
        /// the hook was expected to do. Detection is by magic bytes; when the content is
        /// undetectable, or the current extension is already valid for the detected type
        /// (<c>.jpg</c> for JPEG must not churn to <c>.jpeg</c>), the name is returned unchanged.
        /// </summary>
        public static string AlignExtensionWithContent(string fileName, byte[] content)
        {
            // Inspect a bounded HEAD of the content, not the whole array: Mime-Detective's stream
            // reader buffers up to its 10 MB MaxFileSize regardless of input size, so inspecting a
            // 300 KB image cost a 10 MB LOH allocation (measured 2,3 ms; 50 ms for a video) to read
            // bytes that all sit in the first few hundred. 64 KB clears every signature offset in
            // the default pack (the largest, ISO, is at 32769). A format whose signature sits past
            // the window is simply not detected, and an undetected type keeps the original name.
            var results = FileSignatures.Inspector.Inspect(content, 0, Math.Min(content.Length, 65536));

            ImmutableArray<string> detectedExtensions = results
                .Select(r => r.Definition.File.Extensions)
                .FirstOrDefault(e => !e.IsDefaultOrEmpty);

            if (detectedExtensions.IsDefaultOrEmpty)
                return fileName;

            string currentExtension = GetFileExtensionFromFileName(fileName);

            if (detectedExtensions.Contains(currentExtension, StringComparer.OrdinalIgnoreCase))
                return fileName;

            return Path.ChangeExtension(fileName, detectedExtensions[0]);
        }

        /// <summary>
        /// Validates that the content-type header declared by the client is in the allowed list
        /// AND that the stream content matches the declared type. Both checks are required —
        /// client-supplied Content-Type is trivially spoofable.
        /// <para>Allowed entries may be exact MIME types (<c>"image/png"</c>) or type wildcards
        /// (<c>"image/*"</c>), matching any declared type with that prefix.</para>
        /// <para>Binary types are matched by magic bytes via Mime-Detective. <c>image/svg+xml</c> is a
        /// text format with no magic bytes, so it is validated structurally instead: the document must
        /// parse as XML with an <c>&lt;svg&gt;</c> root and carry no active content (script elements,
        /// event-handler attributes, <c>javascript:</c> hrefs, <c>foreignObject</c>).</para>
        /// <para>Failures throw <see cref="BusinessException"/> — a user-correctable mistake (wrong file
        /// picked, renamed extension), not a security violation: the whitelist is already public in the
        /// UI's accept attribute, and the localized messages are written to be shown to the user.</para>
        /// Resets <paramref name="content"/> to position 0 before and after inspection.
        /// </summary>
        public static Task ValidateFileSignature(
            Stream content,
            string declaredContentType,
            IReadOnlyCollection<string> allowedMimeTypes,
            IStringLocalizer? localizer = null)
        {
            if (allowedMimeTypes == null || allowedMimeTypes.Count == 0)
                return Task.CompletedTask;

            if (string.IsNullOrEmpty(declaredContentType) ||
                !allowedMimeTypes.Any(t => MatchesMimeType(t, declaredContentType)))
            {
                throw new BusinessException(localizer?["FileTypeNotAllowed", declaredContentType ?? ""]
                    ?? $"File type '{declaredContentType}' is not allowed.");
            }

            if (content.Length == 0)
                throw new BusinessException(localizer?["FileIsEmpty"] ?? "File is empty.");

            if (declaredContentType.Equals(SvgMimeType, StringComparison.OrdinalIgnoreCase))
            {
                ValidateSvgContent(content, localizer);
                return Task.CompletedTask;
            }

            content.Position = 0;
            var results = FileSignatures.Inspector.Inspect(content);
            content.Position = 0;

            bool matches = results.Any(r =>
                string.Equals(r.Definition.File.MimeType, declaredContentType, StringComparison.OrdinalIgnoreCase));

            if (!matches)
                throw new BusinessException(localizer?["FileContentDoesNotMatchType", declaredContentType]
                    ?? $"File content does not match declared type '{declaredContentType}'.");

            return Task.CompletedTask;
        }

        /// <summary>
        /// Matches a declared content type against a whitelist entry: exact match, or prefix match
        /// for type wildcards (<c>"image/*"</c> matches <c>"image/png"</c>). Extension entries
        /// (<c>".svg"</c> — no '/') never reach this method; the source generator strips them.
        /// </summary>
        private static bool MatchesMimeType(string allowed, string declared)
        {
            if (allowed.EndsWith("/*"))
                return declared.StartsWith(allowed[..^1], StringComparison.OrdinalIgnoreCase);

            return allowed.Equals(declared, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Structural validation for SVG uploads (no magic bytes to inspect): the stream must parse as
        /// XML with an <c>&lt;svg&gt;</c> root, and must not contain active content — script elements,
        /// event-handler (<c>on*</c>) attributes, <c>javascript:</c> hrefs, or <c>foreignObject</c>.
        /// SVG is an active format (a script inside executes when the file is opened directly), so a
        /// framework-level allowance has to be safe-by-default even when uploads are admin-gated.
        /// DTDs are ignored and external resolution is disabled (XXE-safe) while still accepting the
        /// DOCTYPE line vector editors commonly emit.
        /// </summary>
        private static void ValidateSvgContent(Stream content, IStringLocalizer? localizer)
        {
            content.Position = 0;

            bool rootSeen = false;

            try
            {
                using (XmlReader reader = XmlReader.Create(content, SvgReaderSettings))
                {
                    while (reader.Read())
                    {
                        if (reader.NodeType != XmlNodeType.Element)
                            continue;

                        if (!rootSeen)
                        {
                            if (!reader.LocalName.Equals("svg", StringComparison.OrdinalIgnoreCase))
                                throw NotAnSvg(localizer);

                            rootSeen = true;
                        }

                        if (reader.LocalName.Equals("script", StringComparison.OrdinalIgnoreCase) ||
                            reader.LocalName.Equals("foreignObject", StringComparison.OrdinalIgnoreCase))
                        {
                            throw ActiveSvgContent(localizer);
                        }

                        if (reader.HasAttributes)
                        {
                            for (int i = 0; i < reader.AttributeCount; i++)
                            {
                                reader.MoveToAttribute(i);

                                if (reader.LocalName.StartsWith("on", StringComparison.OrdinalIgnoreCase))
                                    throw ActiveSvgContent(localizer);

                                if (reader.LocalName.Equals("href", StringComparison.OrdinalIgnoreCase) &&
                                    reader.Value.TrimStart().StartsWith("javascript:", StringComparison.OrdinalIgnoreCase))
                                {
                                    throw ActiveSvgContent(localizer);
                                }
                            }

                            reader.MoveToElement();
                        }
                    }
                }
            }
            catch (XmlException)
            {
                throw NotAnSvg(localizer);
            }
            finally
            {
                content.Position = 0;
            }

            if (!rootSeen)
                throw NotAnSvg(localizer);
        }

        private static BusinessException NotAnSvg(IStringLocalizer? localizer) =>
            new(localizer?["FileContentDoesNotMatchType", SvgMimeType]
                ?? $"File content does not match declared type '{SvgMimeType}'.");

        private static BusinessException ActiveSvgContent(IStringLocalizer? localizer) =>
            new(localizer?["FileContainsActiveContent"]
                ?? "The file contains disallowed active content (scripts or event handlers).");

        /// <summary>
        /// Best-effort intrinsic pixel size of an SVG stream, used for CLS-preventing width/height
        /// attributes on editor-inserted images: root <c>&lt;svg&gt;</c> width/height (unitless or px),
        /// falling back to the viewBox size. Returns (0, 0) when the size can't be determined
        /// (percentage/other units without a viewBox, not an SVG) — callers must omit the attributes
        /// in that case, never write 0. Resets the stream position before and after reading.
        /// </summary>
        public static (int Width, int Height) GetSvgDimensions(Stream content)
        {
            try
            {
                content.Position = 0;

                using (XmlReader reader = XmlReader.Create(content, SvgReaderSettings))
                {
                    while (reader.Read())
                    {
                        if (reader.NodeType != XmlNodeType.Element)
                            continue;

                        if (!reader.LocalName.Equals("svg", StringComparison.OrdinalIgnoreCase))
                            return (0, 0);

                        if (TryParseSvgLength(reader.GetAttribute("width"), out int width) &&
                            TryParseSvgLength(reader.GetAttribute("height"), out int height))
                        {
                            return (width, height);
                        }

                        string? viewBox = reader.GetAttribute("viewBox");
                        string[]? parts = viewBox?.Split([' ', ','], StringSplitOptions.RemoveEmptyEntries);

                        if (parts?.Length == 4 &&
                            double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out double viewBoxWidth) &&
                            double.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out double viewBoxHeight) &&
                            viewBoxWidth > 0 && viewBoxHeight > 0)
                        {
                            return ((int)Math.Round(viewBoxWidth), (int)Math.Round(viewBoxHeight));
                        }

                        return (0, 0);
                    }
                }
            }
            catch (XmlException)
            {
                // not parseable XML — no dimensions to report
            }
            finally
            {
                content.Position = 0;
            }

            return (0, 0);
        }

        /// <summary>
        /// Parses an SVG length attribute into whole pixels. Only unitless and <c>px</c> values count —
        /// percentages and other units have no fixed pixel meaning, so they fail and the caller falls
        /// back to the viewBox.
        /// </summary>
        private static bool TryParseSvgLength(string? value, out int pixels)
        {
            pixels = 0;

            if (string.IsNullOrWhiteSpace(value))
                return false;

            value = value.Trim();

            if (value.EndsWith("px", StringComparison.OrdinalIgnoreCase))
                value = value[..^2];

            if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double result) || result <= 0)
                return false;

            pixels = (int)Math.Round(result);
            return true;
        }

        public static async Task<byte[]> ReadAllBytesAsync(Stream stream)
        {
            stream.Seek(0, SeekOrigin.Begin);

            byte[] bytes;

            using (MemoryStream memoryStream = new())
            {
                await stream.CopyToAsync(memoryStream);
                bytes = memoryStream.ToArray();
            }

            return bytes;
        }

        public static List<string> ExtractImageUrlsFromHtml(string htmlContent)
        {
            if (string.IsNullOrEmpty(htmlContent))
                return new List<string>();

            List<string> urls = new();
            Regex regex = new Regex(@"<img[^>]+src=""([^""]+)""", RegexOptions.IgnoreCase);

            foreach (Match match in regex.Matches(htmlContent))
            {
                if (match.Groups.Count > 1)
                    urls.Add(match.Groups[1].Value);
            }

            return urls;
        }

        #endregion
    }
}
