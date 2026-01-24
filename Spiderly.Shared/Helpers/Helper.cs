using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json.Linq;
using Serilog;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;
using Spiderly.Shared.Exceptions;
using Spiderly.Shared.Resources;
using System.ComponentModel;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Mail;
using System.Net.Sockets;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

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

        #region Emailing

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

        public static void SendUnhandledExceptionEmails(long? userId, IWebHostEnvironment env, Exception unhandledEx)
        {
            Task.Run(async () =>
            {
                try
                {
                    using (SmtpClient smtpClient = GetSmtpClient())
                    using (MailMessage mailMessage = new MailMessage
                    {
                        From = new MailAddress(SettingsProvider.Current.EmailSender),
                        Subject = $"{SettingsProvider.Current.ApplicationName}: Unhandled Exception",
                        Body = $$"""
Currently authenticated user id: {{userId}}); <br>
{{unhandledEx}}
""",
                        BodyEncoding = Encoding.UTF8, // Without this, the email is not sent, and don't throw the exception
                        IsBodyHtml = true,
                    })
                    {
                        foreach (string recipient in SettingsProvider.Current.UnhandledExceptionRecipients)
                            mailMessage.To.Add(new MailAddress(recipient));

                        if (env.IsDevelopment() == false)
                            await smtpClient.SendMailAsync(mailMessage);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(
                        ex,
                        "Unhandled Exception email is not sent; Currently authenticated user id: {userId});",
                        userId
                    );
                }
            });
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

        public static bool IsJwtTokenValid(string accessToken)
        {
            try
            {
                byte[] secretKey = Encoding.UTF8.GetBytes(SettingsProvider.Current.JwtKey);
                JwtSecurityTokenHandler tokenHandler = new JwtSecurityTokenHandler();

                tokenHandler.ValidateToken(accessToken, new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = SettingsProvider.Current.JwtIssuer,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(secretKey),
                    ValidAudience = SettingsProvider.Current.JwtAudience,
                    ValidateAudience = true, // Checking if the audience is the valid one (localhost:7260)
                    ValidateLifetime = true, // If the token has expired, it will not be valid
                    ClockSkew = TimeSpan.FromMinutes(SettingsProvider.Current.ClockSkewMinutes),
                }, out SecurityToken validatedToken);

                //JwtSecurityToken jwtToken = validatedToken as JwtSecurityToken;
                //Optionally, check claims from token...
                //var userId = jwtToken.Claims.First(x => x.Type == ClaimTypes.NameIdentifier).Value;

                return true;
            }
            catch
            {
                return false;
            }
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
            string ipAddress = GetRemoteHostIpAddressUsingXForwardedFor(httpContext)?.ToString();

            if (string.IsNullOrEmpty(ipAddress))
                ipAddress = httpContext.Connection.RemoteIpAddress?.ToString();

            if (string.IsNullOrEmpty(ipAddress))
                ipAddress = GetRemoteHostIpAddressUsingXRealIp(httpContext)?.ToString();

            return ipAddress;
        }

        private static IPAddress GetRemoteHostIpAddressUsingXForwardedFor(HttpContext httpContext)
        {
            IPAddress remoteIpAddress = null;
            string forwardedFor = httpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault();

            if (string.IsNullOrEmpty(forwardedFor) == false)
            {
                List<string> ipList = forwardedFor
                    .Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => s.Trim())
                    .ToList();

                foreach (string ip in ipList)
                {
                    if (IPAddress.TryParse(ip, out var address) &&
                       (address.AddressFamily is AddressFamily.InterNetwork or AddressFamily.InterNetworkV6))
                    {
                        remoteIpAddress = address;
                        break;
                    }
                }
            }

            return remoteIpAddress;
        }

        private static IPAddress GetRemoteHostIpAddressUsingXRealIp(HttpContext httpContext)
        {
            bool xRealIpExists = httpContext.Request.Headers.TryGetValue("X-Real-IP", out var xRealIp);

            if (xRealIpExists)
            {
                if (!IPAddress.TryParse(xRealIp, out IPAddress address))
                    return null;

                bool isValidIP = address.AddressFamily is AddressFamily.InterNetwork or AddressFamily.InterNetworkV6;

                if (isValidIP)
                    return address;
            }

            return null;
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

        #endregion
    }
}
