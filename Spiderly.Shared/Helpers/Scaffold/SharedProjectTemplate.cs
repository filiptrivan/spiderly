using Spiderly.Shared.Classes;
using static Spiderly.Shared.Helpers.Scaffold.TemplateComments;

namespace Spiderly.Shared.Helpers.Scaffold
{
    /// <summary>
    /// The <c>{AppName}.Shared</c> project: the backend's English strings, and the FluentValidation setup that
    /// translates property names in its messages.
    /// </summary>
    internal static class SharedProjectTemplate
    {
        internal static SpiderlyFolder Folder(string appName, string spiderlyVersion, bool isRunningFromNuget) => new()
        {
            Name = $"{appName}.Shared",
            ChildFolders =
            {
                new SpiderlyFolder
                {
                    Name = "FluentValidation",
                    Files =
                    {
                        new SpiderlyFile { Name = "TranslatePropertiesConfiguration.cs", Data = GetTranslatePropertiesConfigurationCsData(appName) },
                    }
                },
                new SpiderlyFolder
                {
                    Name = "Translations",
                    Files =
                    {
                        new SpiderlyFile { Name = "en.json", Data = GetSeedTranslationsJsonData() },
                    }
                }
            },
            Files =
            {
                new SpiderlyFile { Name = $"{appName}.Shared.csproj", Data = GetSharedCsProjData(spiderlyVersion, isRunningFromNuget) },
            }
        };

        private static string GetTranslatePropertiesConfigurationCsData(string appName)
        {
            return $$"""
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;

namespace {{appName}}.Shared.FluentValidation
{
    public class TranslatePropertiesConfiguration : IConfigureOptions<MvcOptions>
    {
        private readonly IStringLocalizer _localizer;

        public TranslatePropertiesConfiguration(IStringLocalizer localizer)
        {
            _localizer = localizer;
        }

        public void Configure(MvcOptions options)
        {
            ValidatorOptions.Global.DisplayNameResolver = (type, memberInfo, expression) =>
            {
                LocalizedString result = _localizer[memberInfo.Name];
                return result.ResourceNotFound ? null : result.Value;
            };
        }
    }
}

""";
        }

        private static string GetSeedTranslationsJsonData()
        {
            return """
{
  "And": "And",
  "AuthenticationEmailDoesNotExistException": "An account with the entered email address does not exist. Please check the email address or create a new account.",
  "AuthenticationIncorectPasswordException": "Incorrect password. Please try again or reset your password if you've forgotten it.",
  "BirthDate": "Birth Date",
  "Code": "Code",
  "ConcurrencyException": "This record has been modified or deleted by another user.",
  "CreatedAt": "Created At",
  "Description": "Description",
  "DisabledAccountException": "Your account is disabled, please contact the administrator.",
  "Discount": "Discount",
  "EmailAccountVerificationTitle": "Account verification",
  "EmailBody": "Email Body",
  "EmailSendError": "An error occurred while sending the email, our team has been informed and will fix it as soon as possible. Thank you for your patience.",
  "EntityDoesNotExistInDatabase": "The record you're looking for doesn't exist in database.",
  "EntityDoesNotExistInDatabaseForDeleteRequest": "Your deletion request couldn't be completed as the entity doesn't exist in our database. Maybe it's already deleted.",
  "ExpiredRefreshTokenException": "Your session has expired, please login again.",
  "ExpiredVerificationCodeException": "Your verification code has expired. Please request a new code to continue.",
  "ExternalProviderNotConfiguredException": "Signing in with this provider is currently unavailable. Please try another sign-in method.",
  "FileContainsActiveContent": "The file contains disallowed active content (scripts or event handlers).",
  "FileContentDoesNotMatchType": "File content does not match declared type '{0}'.",
  "FileIsEmpty": "File is empty.",
  "FileSizeExceeded": "File size must not exceed {0} MB.",
  "FileTypeNotAllowed": "File type '{0}' is not allowed.",
  "GlobalError": "An error occurred in the system, our team has been informed and will fix it as soon as possible. Thank you for your patience.",
  "Id": "Id",
  "ImageHeightMustBeExact": "Image height must be exactly {0}px (current: {1}px).",
  "ImageWidthMustBeExact": "Image width must be exactly {0}px (current: {1}px).",
  "IsDisabled": "Is Disabled",
  "LatestVerificationCodeException": "Please use the most recent verification code, as multiple codes were sent.",
  "LogoImage": "Logo Image",
  "ModifiedAt": "Modified At",
  "Name": "Name",
  "OnlyThirdPartyAccountButTriedToRegisterOrLoginException": "Your account already exists with third-party (eg. Google) authentication. If you want to set up a password as well, please use the 'Forgot password?' option to reset it or log in to your profile and add a password.",
  "OrderNumber": "Order Number",
  "PartnerDisplayName": "Partner",
  "PartnerId": "Partner",
  "PartnerList": "Partners",
  "Password": "Password",
  "Points": "Points",
  "PrimaryColor": "Primary Color",
  "ResetPasswordEmailDoesNotExistException": "An account with the entered email address does not exist. Please check the email address or create a new account.",
  "RoleList": "Roles",
  "SameEmailAlreadyExistsException": "An account with this email address already exists.",
  "Slug": "Slug",
  "Title": "Title",
  "TwoDifferentIpAddressesRefreshException": "You can't use the application with two different IP addresses at the same time, please login again.",
  "UnauthorizedAccessExceptionMessage": "You don't have the necessary rights to perform the operation.",
  "UserDisplayName": "User",
  "UserId": "User",
  "UserList": "Users",
  "ValidFrom": "Valid From",
  "ValidTo": "Valid To",
  "VerificationCodeDevelopmentMode": "Your verification code: {0}\n\n(Shown here because you're in development environment without emailing set up)",
  "Version": "Version"
}
""";
        }

        private static string GetSharedCsProjData(string version, bool isRunningFromNuget)
        {
            return $$"""
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
    <NoWarn>1591</NoWarn>
  </PropertyGroup>

  <ItemGroup>
{{XmlCommented($$"""
        <ProjectReference Include="..\..\..\spiderly\Spiderly.Shared\Spiderly.Shared.csproj" />
""", isRunningFromNuget)}}
  </ItemGroup>

  <ItemGroup>
  </ItemGroup>

	<ItemGroup>
{{XmlCommented($$"""
        <PackageReference Include="Spiderly.Shared" Version="{{version}}" />
""", !isRunningFromNuget)}}
	</ItemGroup>

    <ItemGroup>
	    <None Update="Translations\*.json" CopyToOutputDirectory="PreserveNewest" />
    </ItemGroup>

</Project>

""";
        }
    }
}
