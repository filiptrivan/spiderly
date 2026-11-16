using Spiderly.Shared.Classes;
using static Spiderly.Shared.Helpers.Scaffold.TemplateComments;

namespace Spiderly.Shared.Helpers.Scaffold
{
    /// <summary>
    /// The <c>src/assets/</c> folder: the app's English strings, the PrimeNG theme and styles, and the images.
    /// </summary>
    internal static class AssetsTemplate
    {
        internal static SpiderlyFolder Folder(bool isRunningFromNuget) => new()
        {
            Name = "assets",
            ChildFolders =
            {
                new SpiderlyFolder
                {
                    Name = "i18n",
                    Files =
                    {
                        new SpiderlyFile { Name = "en.json", Data = GetTranslocoEnJsonCode() },
                    }
                },
                ImagesTemplate.Folder(),
            },
            Files =
            {
                new SpiderlyFile { Name = "primeng-theme.ts", Data = GetPrimeNGThemeTsData() },
                new SpiderlyFile { Name = "styles.scss", Data = GetStylesScssData(isRunningFromNuget) },
                new SpiderlyFile { Name = "tailwind.css", Data = GetTailwindCssData(isRunningFromNuget) },
            }
        };

        private static string GetPrimeNGThemeTsData()
        {
            return $$"""
import Aura from '@primeng/themes/aura';
import { definePreset } from '@primeng/themes';

export const ThemePreset = definePreset(Aura, {
  semantic: {
    surface: {
      0: '#e5e7eb',
    },
    primary: {
      50: '{zinc.50}',
      100: '{zinc.100}',
      200: '{zinc.200}',
      300: '{zinc.300}',
      400: '{zinc.400}',
      500: '{zinc.500}',
      600: '{zinc.600}',
      700: '{zinc.700}',
      800: '{zinc.800}',
      900: '{zinc.900}',
      950: '{zinc.950}',
      color: '{zinc.950}',          
      contrastColor: '{surface.0}',
      hoverColor: '{zinc.800}',
      activeColor: '{zinc.700}',
    },
  },
  components: {
    panel: {
      colorScheme: {
        dark: {
          root: {
            header: {
              background: '{surface.900}',
            },
            background: '{surface.800}',
          },
        },
      },
    },
  },
});
""";
        }

        private static string GetStylesScssData(bool isRunningFromNuget)
        {
            return $$"""
//#region PrimeNG

@use "../../node_modules/primeicons/primeicons.css";
@use "../../node_modules/ngx-spinner/animations/ball-clip-rotate-multiple.css";

//#endregion

//#region Spiderly

{{SlashCommented("""
@use "../../../../spiderly/Angular/projects/spiderly/src/lib/styles/styles.scss";
""", isRunningFromNuget)}}

{{SlashCommented("""
@use "../../node_modules/spiderly/styles/styles/styles.scss";
""", !isRunningFromNuget)}}

//#endregion
""";
        }

        private static string GetTailwindCssData(bool isRunningFromNuget)
        {
            return $$"""
@layer primeng;
@import "tailwindcss/theme";
/* Bridges PrimeNG's runtime theme variables (--p-surface-*, --p-primary-* from the Aura preset) to
   Tailwind color tokens, so utilities like `bg-primary-50` / `border-surface-200` resolve to the
   active theme instead of being dead classes. MUST stay before `tailwindcss/utilities`: it registers
   the tokens via `@theme inline`, and Tailwind generates utilities at the utilities import, so tokens
   added after that point are silently ignored. The tokens are unlayered and the enabled utilities land
   in the `utilities` layer, so don't move this into `@layer primeng`. Note: Tailwind v4 defaults a bare
   `border` (no color modifier) to `currentColor` — always name a color. */
@import "tailwindcss-primeui";
@import "tailwindcss/utilities";

{{SlashCommented("""
@source "../../../../spiderly/Angular/projects/spiderly/src/lib";
""", isRunningFromNuget)}}

{{SlashCommented("""
@source "../../node_modules/spiderly";
""", !isRunningFromNuget)}}
""";
        }

        private static string GetTranslocoEnJsonCode()
        {
            return $$$"""
{
  "TierList": "Loyalty Tiers",
  "Partner": "Partner",
  "UserList": "Users",
  "SuperRoles": "Super roles",
  "IsDisabled": "Account is disabled",
  "PartnerRoleList": "Roles",
  "Save": "Save",
  "AddNewTier": "Add new loyalty tier",
  "AddNewBusinessSystemTier": "Add new discount product group",
  "SegmentationList": "Segmentations",
  "AddNewSegmentationItem": "Add new segmentation item",
  "RoleList": "Roles",
  "Permissions": "Permissions",
  "Settings": "Settings",
  "PartnerList": "Partners",
  "CookiePolicy": "cookie policy",
  "AgreeAndJoin": "Agree and Join",
  "ForgotPassword": "Forgot password?",
  "RememberYourPassword": "Remembered your password?",
  "ResetPassword": "Reset password",
  "PleaseConfirmToProceed": "Please confirm to proceed.",
  "Cancel": "Cancel",
  "Confirm": "Confirm",
  "Clear": "Clear",
  "Select": "Select",
  "SyncDiscountProductGroups": "Sync discount product groups",
  "Loading": "Loading",
  "ProductsForYouTitle": "Products for You",
  "Currency": "$",
  "PartnerIntermediateStepTitle": "You can change partners at any time",
  "PartnerIntermediateStepDescription": "Choose the partner whose loyalty program you want to visit.",
  "Actions": "Actions",
  "Details": "Details",
  "Points": "Points",
  "Tier": "Loyalty tier",
  "Segmentation": "Segmentation",
  "CreatedAt": "Created at",
  "FirstTimeFieldFillTooltipText": "Fill in the field for the first time and earn extra points!",
  "Delete": "Delete",
  "Gender": "Gender",
  "Name": "Name",
  "PointsForTheFirstTimeFill": "Points for first-time fill",
  "Title": "Title",
  "SuccessfulAttempt": "Your attempt has been processed.",
  "MarkAsRead": "Mark as read",
  "MarkAsUnread": "Mark as unread",
  "Email": "Email",
  "Slug": "URL path",
  "Profile": "Profile",
  "Home": "Home",
  "SuperAdministration": "Super Administration",
  "Administration": "Administration",
  "YouHaveSuccessfullyChangedYourPassword": "You have successfully changed your password.",
  "Revert": "Revert to state",
  "AreYouSure": "Are you sure?",
  "SuccessfulSyncToastDescription": "Successfully updated data.",
  "NotEmpty": "Field cannot be empty.",
  "Length": "Field must have a minimum of {{min}} and a maximum of {{max}} characters.",
  "MaxLength": "Field must have a maximum of {{max}} characters.",
  "SingleLength": "Field must have {{length}} characters.",
  "NumberRangeMin": "Field value must be greater than or equal to {{min}}.",
  "NumberRangeMinNumberRangeMax": "Field value must be between {{min}} and {{max}}.",
  "PrecisionScale": "Field value must have a total of {{precision}} digits, and the number of digits after the decimal point must not be greater than {{scale}}.",
  "NumberRangeMinPrecisionScale": "Field value must be greater than or equal to {{min}}, must have a total of {{precision}} digits, and the number of digits after the decimal point must not be greater than {{scale}}.",
  "NotEmptyLength": "Field cannot be empty and must have a minimum of {{min}} and a maximum of {{max}} characters.",
  "NotEmptyMaxLength": "Field cannot be empty and must have a maximum of {{max}} characters.",
  "NotEmptySingleLength": "Field cannot be empty and must have {{length}} characters.",
  "NotEmptyNumberRangeMin": "Field cannot be empty and value must be greater than or equal to {{min}}.",
  "NotEmptyNumberRangeMinNumberRangeMax": "Field cannot be empty and value must be between {{min}} and {{max}}.",
  "NotEmptyPrecisionScale": "Field cannot be empty, value must have a total of {{precision}} digits, and the number of digits after the decimal point must not be greater than {{scale}}.",
  "NotEmptyNumberRangeMinPrecisionScale": "Field cannot be empty, value must be greater than or equal to {{min}}, must have a total of {{precision}} digits, and the number of digits after the decimal point must not be greater than {{scale}}.",
  "NotEmptyLengthEmailAddress": "Field cannot be empty, must have a minimum of {{min}} and a maximum of {{max}} characters, and must be a valid email address.",
  "FileSizeExceeded": "File size must not exceed {{ 0 }} MB.",
  "IdToken": "/",
  "Browser": "/",
  "NewPassword": "New password",
  "ExpiresAt": "Expires at",
  "UserEmail": "Email",
  "AccessToken": "/",
  "Token": "/",
  "Password": "Password",
  "RefreshToken": "/",
  "IpAddress": "IP address",
  "BusinessSystemUpdatePointsScheduledTaskList": "Points updates",
  "SuccessfullyDoneBusinessSystemUpdatePointsScheduledTaskList": "Points updates successfully performed",
  "UpdatePoints": "Update points",
  "AutomaticUpdatePoints": "Automatic points update",
  "File": "File",
  "ManualUpdatePoints": "Manual points update",
  "ExcelUpdatePoints": "Excel points update",
  "ManualUpdatePointsFromDate": "Manual points update from date",
  "ManualUpdatePointsToDate": "Manual points update to date",
  "TransactionsTo": "Transactions to",
  "TransactionsFrom": "Transactions from",
  "IsManuallyStarted": "Manually started",
  "TransactionList": "Transactions",
  "TokenString": "/",
  "Status": "Status",
  "Message": "Message",
  "SelectedPermissionIds": "/",
  "SelectedUserIds": "/",
  "RoleDTO": "/",
  "VerificationCode": "Verification code",
  "NameLatin": "Name (Latin)",
  "Description": "Description",
  "DescriptionLatin": "Description (Latin)",
  "Code": "Code",
  "Id": "ID",
  "Version": "Version",
  "ModifiedAt": "Modified at",
  "Roles": "Roles",
  "Users": "Users",
  "ExternalProvider": "/",
  "ForgotPasswordVerificationToken": "/",
  "JwtAuthResult": "/",
  "AuthResult": "/",
  "LoginVerificationToken": "/",
  "RefreshTokenRequest": "/",
  "RoleSaveBody": "/",
  "VerificationTokenRequest": "/",
  "Permission": "Permission",
  "Role": "Role",
  "RoleUser": "/",
  "Checked": "Checked",
  "PointsMultiplier": "Points multiplier",
  "TableFilter": "Table filter",
  "SelectedIds": "Selected",
  "UnselectedIds": "Unselected",
  "IsAllSelected": "All selected",
  "TransactionCode": "Transaction code",
  "Discount": "Discount",
  "PartnerRoleDTO": "Role",
  "SelectedPartnerUserIds": "/",
  "UserDTO": "/",
  "SelectedRoleIds": "/",
  "PartnerUserDTO": "/",
  "SelectedPartnerRoleIds": "/",
  "SelectedSegmentationItemIds": "/",
  "Price": "Price",
  "Category": "Category",
  "LinkToWebsite": "Link to website",
  "SegmentationDTO": "/",
  "SegmentationItemsDTO": "Segmentation items",
  "EmailBody": "Email content",
  "PartnerProfile": "Partner profile",
  "SuccessfulSaveAndRefreshThePageToastDescription": "Successfully saved. To see partner changes, please refresh the page.",
  "AddNewDiscountProductGroup": "Add new discount product group",
  "LogoImageData": "/",
  "StartUpdatePointsScheduledTask": "Start automatic points update",
  "PauseUpdatePointsScheduledTask": "Pause automatic points update",
  "LogoImage": "Logo",
  "PrimaryColor": "Primary color",
  "PointsForTheFirstTimeGenderFill": "Points for first-time gender fill",
  "PointsForTheFirstTimeBirthDateFill": "Points for first-time birth date fill",
  "ProductsRecommendationEndpoint": "Product recommendation path",
  "HasFilledGenderForTheFirstTime": "Gender filled for the first time",
  "HasFilledBirthDateForTheFirstTime": "Birth date filled for the first time",
  "CheckedSegmentationItems": "Checked segmentation items",
  "OrderNumber": "Order number",
  "ValidFrom": "Valid from",
  "ValidTo": "Valid to",
  "Guid": "GUID",
  "Product": "Product",
  "Transaction": "Transaction",
  "NumberOfFailedAttemptsInARow": "Number of failed attempts in a row",
  "BirthDate": "Birth Date",
  "PartnerUser": "User",
  "SegmentationItem": "Segmentation item",
  "User": "User",
  "Brand": "Brand",
  "MergedPartnerUser": "User",
  "PartnerRoleSaveBody": "/",
  "PartnerUserSaveBody": "/",
  "QrCode": "QR Code",
  "SegmentationSaveBody": "/",
  "UserSaveBody": "/",
  "PartnerPermission": "Permission",
  "PartnerRole": "Role",
  "TransactionProduct": "Transaction product",
  "TransactionStatus": "Transaction status",
  "Primeng": {
    "dayNames": [
      "Sunday",
      "Monday",
      "Tuesday",
      "Wednesday",
      "Thursday",
      "Friday",
      "Saturday"
    ],
    "dayNamesShort": [
      "Sun",
      "Mon",
      "Tue",
      "Wed",
      "Thu",
      "Fri",
      "Sat"
    ],
    "dayNamesMin": [
      "Su",
      "Mo",
      "Tu",
      "We",
      "Th",
      "Fr",
      "Sa"
    ],
    "monthNames": [
      "January",
      "February",
      "March",
      "April",
      "May",
      "June",
      "July",
      "August",
      "September",
      "October",
      "November",
      "December"
    ],
    "monthNamesShort": [
      "Jan",
      "Feb",
      "Mar",
      "Apr",
      "May",
      "Jun",
      "Jul",
      "Aug",
      "Sep",
      "Oct",
      "Nov",
      "Dec"
    ],
    "today": "Today",
    "weekHeader": "Week",
    "clear": "Clear",
    "apply": "Apply",
    "emptyMessage": "No results",
    "emptyFilterMessage": "No results"
  },
  "PartnerUserList": "Users",
  "YouDoNotHaveAnyAchievement": "You haven't earned any points yet.",
  "PointsHistory": "Points History",
  "BusinessSystemList": "Business systems",
  "BusinessSystem": "Business system"
}
""";
        }
    }
}
