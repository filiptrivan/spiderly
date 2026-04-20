using MimeDetective;
using MimeDetective.Definitions;
using MimeDetective.Storage;

namespace Spiderly.Shared.Helpers
{
    /// <summary>
    /// Shared magic-byte inspector used by
    /// <see cref="Helper.ValidateFileSignature(System.IO.Stream, string, System.Collections.Generic.IReadOnlyCollection{string}, Microsoft.Extensions.Localization.IStringLocalizer)"/>
    /// to verify an uploaded stream actually matches the declared content type
    /// rather than trusting the client-supplied header.
    /// </summary>
    public static class FileSignatures
    {
        /// <summary>
        /// AVIF major-brand signatures: ISO/BMFF <c>ftyp</c> box at offset 4 with brand
        /// <c>avif</c> (still image) or <c>avis</c> (image sequence). Files that use
        /// <c>mif1</c>/<c>heic</c> as the major brand with AVIF only in compatible brands
        /// are not covered — add more definitions if they show up in the wild.
        /// </summary>
        private static readonly Definition[] AvifDefinitions =
        [
            BuildAvifDefinition("66 74 79 70 61 76 69 66"), // "ftypavif"
            BuildAvifDefinition("66 74 79 70 61 76 69 73"), // "ftypavis"
        ];

        /// <summary>
        /// Shared <see cref="IContentInspector"/> built from the Mime-Detective default
        /// definition pack plus a custom AVIF definition (Default pack does not ship AVIF
        /// signatures as of Mime-Detective 25.8.1). Covers mp4, common images (incl. WebP + AVIF),
        /// office documents, pdf, zip. Building the inspector is expensive; <c>Inspect</c> is cheap
        /// and thread-safe, so the instance is created once per process and reused.
        /// </summary>
        public static readonly IContentInspector Inspector =
            new ContentInspectorBuilder
            {
                Definitions = [.. DefaultDefinitions.All(), .. AvifDefinitions],
            }.Build();

        private static Definition BuildAvifDefinition(string ftypSignatureAtOffset4) => new()
        {
            File = new FileType
            {
                Extensions = ["avif"],
                MimeType = "image/avif",
                Categories = [Category.Image],
            },
            Signature = new Segment[] { PrefixSegment.Create(4, ftypSignatureAtOffset4) }.ToSignature(),
        };
    }
}
