using MimeDetective;
using MimeDetective.Definitions;

namespace Spiderly.Shared.Helpers
{
    /// <summary>
    /// Known MIME sets and the shared magic-byte inspector used by
    /// <see cref="Helper.ValidateFileSignature(System.IO.Stream, string, System.Collections.Generic.IReadOnlyCollection{string}, Microsoft.Extensions.Localization.IStringLocalizer)"/>
    /// to verify an uploaded stream actually matches the declared content type
    /// rather than trusting the client-supplied header.
    /// </summary>
    public static class FileSignatures
    {
        /// <summary>
        /// Convenience set for the "images only" default applied when an entity property
        /// has no <c>[AcceptedFileTypes]</c> attribute.
        /// </summary>
        public static readonly IReadOnlyCollection<string> ImageMimeTypes = new[]
        {
            "image/jpeg",
            "image/png",
            "image/gif",
            "image/webp",
            "image/bmp",
        };

        /// <summary>
        /// Shared <see cref="IContentInspector"/> built from the Mime-Detective default
        /// definition pack. Covers mp4, common images, office documents, pdf, zip.
        /// Building the inspector is expensive; <c>Inspect</c> is cheap and thread-safe,
        /// so the instance is created once per process and reused.
        /// </summary>
        public static readonly IContentInspector Inspector =
            new ContentInspectorBuilder
            {
                Definitions = DefaultDefinitions.All(),
            }.Build();
    }
}
