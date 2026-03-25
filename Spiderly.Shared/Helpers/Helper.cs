using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;
using Spiderly.Shared.Exceptions;
using Spiderly.Shared.Resources;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Net.Mail;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

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
                throw new HackerException($"Invalid file name format ({fileName}).");

            string idPart = parts[0];

            // Try to convert the string part to the specified struct type
            if (TypeDescriptor.GetConverter(typeof(ID)).IsValid(idPart))
                return (ID)TypeDescriptor.GetConverter(typeof(ID)).ConvertFromString(idPart);

            throw new InvalidCastException($"Cannot convert '{idPart}' to {typeof(ID)}. Id part can't be null, for new objects it should be 0.");
        }

        public static string GetFileExtensionFromFileName(string fileName)
        {
            List<string> parts = fileName.Split('.').ToList();

            if (parts.Count < 2) // It could be only 2, it's not the same validation as spliting with '-'
                throw new HackerException($"Invalid file name format ({fileName}).");

            return parts.Last(); // The file could be .abc.png
        }

        #region SQL Server

        public static string CreateSqlServerConnectionString(string databaseName)
        {
            string dockerConnectionString = TryDockerSqlServerConnection(databaseName);
            if (dockerConnectionString != null)
                return dockerConnectionString;

            return TryWindowsAuthSqlServerConnection(databaseName);
        }

        private static string TryDockerSqlServerConnection(string databaseName)
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

        private static string TryWindowsAuthSqlServerConnection(string databaseName)
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

        public static string CreatePostgreSQLConnectionString(string databaseName)
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

        public static string CreatePostgreSQLConnectionString(string databaseName, string customPassword)
        {
            List<(string password, bool useIntegratedSecurity)> authMethods = new List<(string, bool)>
            {
                (customPassword, false)
            };

            return TryPostgreSQLConnection(databaseName, authMethods);
        }

        private static string TryPostgreSQLConnection(string databaseName, List<(string password, bool useIntegratedSecurity)> authMethods)
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

        #region Notifications

        private static readonly HttpClient _telegramHttpClient = new();
        private static readonly ConcurrentDictionary<string, DateTimeOffset> _rateLimitCache = new();

        public static async Task SendTelegramNotificationAsync(long? userId, string exceptionString, ILogger logger)
        {
            try
            {
                Settings settings = SettingsProvider.Current;
                string truncated = exceptionString.Length > 2000 ? exceptionString[..2000] : exceptionString;
                string text = $$$"""
[{{{settings.ApplicationName}}}] Unhandled Exception
User ID: {{{userId}}}
{{{truncated}}}
""";

                string url = $"https://api.telegram.org/bot{settings.TelegramBotToken}/sendMessage";
                await _telegramHttpClient.PostAsJsonAsync(url, new { chat_id = settings.TelegramChatId, text });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unhandled Exception Telegram notification is not sent; Currently authenticated user id: {userId});", userId);
            }
        }

        public static bool ShouldSendNotification(Exception ex)
        {
            if (SettingsProvider.Current.NotificationRateLimitMinutes <= 0)
                return true;

            string key = $"{ex.GetType().FullName}:{ex.Message.GetHashCode()}";
            DateTimeOffset now = DateTimeOffset.UtcNow;
            DateTimeOffset threshold = now.AddMinutes(-SettingsProvider.Current.NotificationRateLimitMinutes);

            foreach (string staleKey in _rateLimitCache.Keys)
            {
                if (_rateLimitCache.TryGetValue(staleKey, out DateTimeOffset ts) && ts < threshold)
                    _rateLimitCache.TryRemove(staleKey, out _);
            }

            if (_rateLimitCache.TryGetValue(key, out DateTimeOffset lastSent) && lastSent >= threshold)
                return false;

            _rateLimitCache[key] = now;
            return true;
        }

        public static async Task SendEmailAsync(string recipient, string subject, string body)
        {
            using (SmtpClient smtpClient = GetSmtpClient())
            using (MailMessage mailMessage = new MailMessage(SettingsProvider.Current.EmailSender, recipient)
            {
                Subject = subject,
                Body = body,
                BodyEncoding = Encoding.UTF8, // Without this, the email is not sent, and don't throw the exception
                IsBodyHtml = true,
            })
            {
                await smtpClient.SendMailAsync(mailMessage);
            }
        }

        public static SmtpClient GetSmtpClient()
        {
            return new SmtpClient(SettingsProvider.Current.SmtpHost, SettingsProvider.Current.SmtpPort)
            {
                Credentials = new NetworkCredential(SettingsProvider.Current.EmailSender, SettingsProvider.Current.EmailSenderPassword),
                EnableSsl = true
            };
        }

        public static bool IsEmailingConfigured()
        {
            Settings settings = SettingsProvider.Current;
            return !string.IsNullOrWhiteSpace(settings.EmailSender) &&
                   !string.IsNullOrWhiteSpace(settings.EmailSenderPassword) &&
                   !string.IsNullOrWhiteSpace(settings.SmtpHost) &&
                   settings.SmtpPort > 0;
        }

        public static bool IsTelegramConfigured()
        {
            Settings settings = SettingsProvider.Current;
            return !string.IsNullOrWhiteSpace(settings.TelegramBotToken) &&
                   !string.IsNullOrWhiteSpace(settings.TelegramChatId);
        }

        #endregion

        #region Security

        #region User

        public static bool IsUserLoggedIn(HttpContext context)
        {
            return context?.User?.Identity?.IsAuthenticated ?? false;
        }

        public static long GetCurrentUserId(HttpContext context)
        {
            return long.Parse(context.User.Claims.FirstOrDefault(x => x.Type == ClaimTypes.PrimarySid).Value);
        }

        public static long? GetCurrentUserIdOrDefault(HttpContext context)
        {
            if (IsUserLoggedIn(context))
                return long.Parse(context.User.Claims.FirstOrDefault(x => x.Type == ClaimTypes.PrimarySid).Value);

            return null;
        }

        #endregion

        #region JWT

        /// <summary>
        /// Reads the access token from the Authorization: Bearer header.
        /// </summary>
        public static string GetAccessTokenFromHeader(HttpContext context)
        {
            string authHeader = context.Request.Headers["Authorization"].FirstOrDefault();
            if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                string token = authHeader.Substring("Bearer ".Length).Trim();
                if (!string.IsNullOrEmpty(token))
                    return token;
            }

            return null;
        }

        /// <summary>
        /// Reads the access token from the cookie.
        /// </summary>
        public static string GetAccessTokenFromCookie(HttpContext context)
        {
            if (context.Request.Cookies.TryGetValue(SettingsProvider.Current.AccessTokenKey, out string cookieToken) &&
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

        public static string GetIPAddress(HttpContext httpContext)
        {
            return httpContext.Connection.RemoteIpAddress?.ToString();
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

                using (MemoryStream outputStream = new())
                {
                    WebpEncoder encoder = new WebpEncoder
                    {
                        Quality = quality,
                        FileFormat = WebpFileFormatType.Lossy
                    };

                    await image.SaveAsWebpAsync(outputStream, encoder);

                    return outputStream.ToArray();
                }
            }
        }

        public static async Task ValidateImageDimensions(
            Stream imageStream,
            int width = 0,
            int height = 0
        )
        {
            ImageInfo imageInfo = await Image.IdentifyAsync(imageStream);
            int actualWidth = imageInfo.Width;
            int actualHeight = imageInfo.Height;

            if (width > 0 && actualWidth != width)
                throw new HackerException(string.Format(SharedTerms.ImageWidthMustBeExact, width, actualWidth));

            if (height > 0 && actualHeight != height)
                throw new HackerException(string.Format(SharedTerms.ImageHeightMustBeExact, height, actualHeight));
        }

        public static void ValidateFileSize(long fileSize, int maxFileSize)
        {
            if (maxFileSize > 0 && fileSize > maxFileSize)
                throw new HackerException(string.Format(SharedTerms.FileSizeExceeded, maxFileSize / 1_000_000));
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
