using Microsoft.IdentityModel.Tokens;
using OfficeOpenXml.FormulaParsing.LexicalAnalysis;
using Spiderly.Shared.Exceptions;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Spiderly.Shared.Helpers
{
    public sealed class SpiderlyLicenseManager
    {
        public static string CreateToken(DateTime expiresAt, string privateKeyBase64)
        {
            ECDsa ecdsa = ECDsa.Create();
            ecdsa.ImportECPrivateKey(Convert.FromBase64String(privateKeyBase64), out _);

            ECDsaSecurityKey securityKey = new ECDsaSecurityKey(ecdsa);
            SigningCredentials credentials = new SigningCredentials(securityKey, SecurityAlgorithms.EcdsaSha256);

            JwtSecurityToken token = new JwtSecurityToken(
                audience: "FullAccess",
                expires: expiresAt,
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        public static void VerifyToken()
        {
            string token = SettingsProvider.Current.SpiderlySecretLicenseToken;
            string publicKeyBase64 = SettingsProvider.Current.SpiderlyPublicLicenseKey;

            if (token == null)
                throw new ArgumentNullException(nameof(token), "The Spiderly license token was not provided. For more information, visit https://www.spiderly.dev.");

            try
            {
                ECDsa ecdsa = ECDsa.Create();
                ecdsa.ImportSubjectPublicKeyInfo(Convert.FromBase64String(publicKeyBase64), out _);

                TokenValidationParameters validationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = false,
                    ValidateAudience = true,
                    ValidAudience = "FullAccess",
                    ValidateLifetime = true,
                    IssuerSigningKey = new ECDsaSecurityKey(ecdsa),
                    ValidateIssuerSigningKey = true,
                    ClockSkew = TimeSpan.Zero
                };

                new JwtSecurityTokenHandler().ValidateToken(token, validationParameters, out SecurityToken _);
            }
            catch (SecurityTokenExpiredException ex)
            {
                throw new InvalidOperationException("The Spiderly license token has expired. Please renew your license at https://www.spiderly.dev/pricing.", ex);
            }
            catch (SecurityTokenInvalidSignatureException ex)
            {
                throw new InvalidOperationException("Invalid license token signature. Please verify your license at https://www.spiderly.dev.", ex);
            }
            catch (SecurityTokenInvalidAudienceException ex)
            {
                throw new InvalidOperationException("The Spiderly license token is not valid for the feature that you tried to use. Please contact support at https://www.spiderly.dev.", ex);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"The Spiderly license token validation failed: {ex.Message}. For support, visit https://www.spiderly.dev.", ex);
            }
        }
    }
}
