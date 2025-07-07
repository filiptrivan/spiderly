using Microsoft.AspNetCore.Http;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Globalization;
using System.Resources;
using Spiderly.Shared.BaseEntities;
using System.Net.Mail;
using Serilog;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Hosting;
using Spiderly.Shared.Exceptions;
using System.ComponentModel;
using Microsoft.Data.SqlClient;
using System.Security.Cryptography;

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

        public static T ReadAssemblyConfiguration<T>(string jsonConfigurationFile)
        {
            string name = typeof(T).Assembly.GetName().Name;
            string propertyName = "AppSettings";
            string text = ReadConfigFile(jsonConfigurationFile);
            if (string.IsNullOrEmpty(text))
            {
                return default(T);
            }

            foreach (JProperty item in JObject.Parse(text)[propertyName]!.Children().OfType<JProperty>())
            {
                if (item.Name == name)
                {
                    return item.Value.ToObject<T>();
                }
            }

            return default(T);
        }

        private static string ReadConfigFile(string jsonConfigurationFile)
        {
            using StreamReader streamReader = new StreamReader(jsonConfigurationFile);
            return streamReader.ReadToEnd();
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

            throw new InvalidCastException($"Cannot convert '{idPart}' to {typeof(ID)}.");
        }

        public static string GetFileExtensionFromFileName(string fileName)
        {
            List<string> parts = fileName.Split('.').ToList();

            if (parts.Count < 2) // It could be only 2, it's not the same validation as spliting with '-'
                throw new HackerException($"Invalid file name format ({fileName}).");

            return parts.Last(); // The file could be .abc.png
        }

        #region SQL Server

        /// <summary>
        /// Attempts to find an available SQL Server connection string by checking common data sources.
        /// </summary>
        public static string GetAvailableSqlServerConnectionString(string databaseName)
        {
            List<string> dataSources = new List<string>
            {
                "localhost",
                @"localhost\SQLEXPRESS",
                @"(localdb)\MSSQLLocalDB"
            };

            foreach (string dataSource in dataSources)
            {
                SqlConnectionStringBuilder connectionStringBuilder = BuildConnectionString(dataSource);
                if (TryConnect(connectionStringBuilder))
                {
                    connectionStringBuilder.InitialCatalog = databaseName;
                    return connectionStringBuilder.ConnectionString;
                }
            }

            return null;
        }

        /// <summary>
        /// Constructs a SQL Server connection string for either the default or SQL Express instance.
        /// </summary>
        public static SqlConnectionStringBuilder BuildConnectionString(string dataSource)
        {
            SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder
            {
                DataSource = dataSource,
                InitialCatalog = "master",
                IntegratedSecurity = true,
                Encrypt = false,
                MultipleActiveResultSets = true,
            };

            return builder;
        }

        /// <summary>
        /// Tries to open a connection using the provided connection string.
        /// </summary>
        private static bool TryConnect(SqlConnectionStringBuilder connectionString)
        {
            try
            {
                connectionString.ConnectTimeout = 2;
                using SqlConnection connection = new SqlConnection(connectionString.ConnectionString);
                connection.Open();
                connectionString.ConnectTimeout = 15;
                return true;
            }
            catch (Exception) { }

            connectionString.ConnectTimeout = 15;
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

        public static void SendUnhandledExceptionEmails(string userEmail, long? userId, IWebHostEnvironment env, Exception unhandledEx)
        {
            Task.Run((Func<Task>)(async () =>
            {
                try
                {
                    using (SmtpClient smtpClient = GetSmtpClient())
                    using (MailMessage mailMessage = new MailMessage
                    {
                        From = new MailAddress((string)SettingsProvider.Current.EmailSender),
                        Subject = $"{SettingsProvider.Current.ApplicationName}: Unhandled Exception",
                        Body = $$"""
Currently authenticated user: {{userEmail}} (id: {{userId}}); <br>
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
                        "Unhandled Exception email is not sent; Currently authenticated user: {userEmail} (id: {userId});",
                        userEmail, userId
                    );
                }
            }));
        }

        public static SmtpClient GetSmtpClient()
        {
            return new SmtpClient(SettingsProvider.Current.SmtpHost, SettingsProvider.Current.SmtpPort)
            {
                Credentials = new NetworkCredential(SettingsProvider.Current.EmailSender, SettingsProvider.Current.EmailSenderPassword),
                EnableSsl = true
            };
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

        public static string GetCurrentUserEmail(HttpContext context)
        {
            return context.User.Claims.FirstOrDefault(x => x.Type == ClaimTypes.Email).Value;
        }

        public static string GetCurrentUserEmailOrDefault(HttpContext context)
        {
            return context.User.Claims.FirstOrDefault(x => x.Type == ClaimTypes.Email)?.Value;
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
    }
}
