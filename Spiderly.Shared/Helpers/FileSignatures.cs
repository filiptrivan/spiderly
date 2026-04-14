namespace Spiderly.Shared.Helpers
{
    /// <summary>
    /// Known file magic-byte signatures used for server-side upload validation.
    /// <see cref="Helper.ValidateFileSignature(System.IO.Stream, string, System.Collections.Generic.IReadOnlyCollection{string}, Microsoft.Extensions.Localization.IStringLocalizer)"/>
    /// uses this to verify an uploaded stream actually matches the declared content type
    /// rather than trusting the client-supplied header.
    /// </summary>
    public static class FileSignatures
    {
        // MIME type → list of byte-sequence candidates the stream may start with.
        // Null-entries inside a candidate match any byte (wildcard) — used for JPEG variants.
        public static readonly IReadOnlyDictionary<string, byte?[][]> Map = new Dictionary<string, byte?[][]>(StringComparer.OrdinalIgnoreCase)
        {
            ["image/jpeg"] = new byte?[][]
            {
                new byte?[] { 0xFF, 0xD8, 0xFF },
            },
            ["image/png"] = new byte?[][]
            {
                new byte?[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A },
            },
            ["image/gif"] = new byte?[][]
            {
                new byte?[] { 0x47, 0x49, 0x46, 0x38, 0x37, 0x61 }, // GIF87a
                new byte?[] { 0x47, 0x49, 0x46, 0x38, 0x39, 0x61 }, // GIF89a
            },
            ["image/webp"] = new byte?[][]
            {
                // RIFF....WEBP — bytes 0-3 "RIFF", 8-11 "WEBP", middle is file size (any).
                new byte?[] { 0x52, 0x49, 0x46, 0x46, null, null, null, null, 0x57, 0x45, 0x42, 0x50 },
            },
            ["image/bmp"] = new byte?[][]
            {
                new byte?[] { 0x42, 0x4D },
            },
            ["application/pdf"] = new byte?[][]
            {
                new byte?[] { 0x25, 0x50, 0x44, 0x46, 0x2D }, // %PDF-
            },
        };

        // Convenience set for the "images only" default.
        public static readonly IReadOnlyCollection<string> ImageMimeTypes = new[]
        {
            "image/jpeg",
            "image/png",
            "image/gif",
            "image/webp",
            "image/bmp",
        };

        internal static bool Matches(byte[] header, byte?[] signature)
        {
            if (header.Length < signature.Length)
                return false;

            for (int i = 0; i < signature.Length; i++)
            {
                byte? expected = signature[i];
                if (expected.HasValue && header[i] != expected.Value)
                    return false;
            }

            return true;
        }
    }
}
