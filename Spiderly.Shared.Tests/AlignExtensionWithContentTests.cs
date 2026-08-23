using System.Text;
using Xunit;
using Spiderly.Shared.Helpers;

namespace Spiderly.Shared.Tests
{
    /// <summary>
    /// Pins <see cref="Helper.AlignExtensionWithContent"/>: blob keys and Content-Type are derived
    /// from the upload's file name, but the optimize hook may transcode the bytes (rasters → WebP
    /// by default, anything a consumer override produces) — so before key construction the name's
    /// extension must be made to match the bytes actually stored. Detection is by magic bytes, not
    /// by trusting what the hook was expected to do, so a consumer override that emits a different
    /// format stays honest too.
    /// </summary>
    public class AlignExtensionWithContentTests
    {
        // "RIFF" + chunk size + "WEBP" — the WebP container signature Mime-Detective matches.
        private static readonly byte[] WebpBytes =
        [
            0x52, 0x49, 0x46, 0x46, 0x24, 0x00, 0x00, 0x00,
            0x57, 0x45, 0x42, 0x50, 0x56, 0x50, 0x38, 0x20,
        ];

        // JPEG SOI + JFIF APP0 marker.
        private static readonly byte[] JpegBytes =
        [
            0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00,
        ];

        [Fact]
        public void TranscodedBytesGetTheirRealExtension()
        {
            // Admin picks photo.jpg; the default optimize hook stored WebP bytes.
            Assert.Equal("photo.webp", Helper.AlignExtensionWithContent("photo.jpg", WebpBytes));
        }

        [Fact]
        public void MatchingExtensionIsLeftAlone()
        {
            // .jpg is a valid extension for JPEG content — no churn to .jpeg or similar.
            Assert.Equal("photo.jpg", Helper.AlignExtensionWithContent("photo.jpg", JpegBytes));
        }

        // An SVG an admin actually uploads: vector editors emit the XML declaration.
        private const string RealSvg =
            """
            <?xml version="1.0" encoding="UTF-8"?>
            <svg xmlns="http://www.w3.org/2000/svg" width="24" height="24"><rect width="24" height="24"/></svg>
            """;

        [Fact]
        public void SvgKeepsItsExtension()
        {
            // SVG is a text format with no magic bytes — detection can only see the generic XML
            // underneath, so "aligning" would rename logo.svg to logo.xml and the browser would
            // stop rendering it as an image. ValidateFileSignature carves SVG out for this reason.
            byte[] svgBytes = Encoding.UTF8.GetBytes(RealSvg);

            Assert.Equal("logo.svg", Helper.AlignExtensionWithContent("logo.svg", svgBytes, "image/svg+xml"));
        }

        [Fact]
        public void AContainerFormatIsNotRenamedToItsGenericParent()
        {
            // An ISO/BMFF box matches BOTH the generic ftyp definition and the custom AVIF one
            // FileSignatures ships. Trusting only the first result would rename photo.avif to
            // photo.mp4 — defeating the very definition that exists so AVIF is recognised.
            byte[] avifBytes =
            [
                0x00, 0x00, 0x00, 0x20, 0x66, 0x74, 0x79, 0x70, // ....ftyp
                0x61, 0x76, 0x69, 0x66, 0x00, 0x00, 0x00, 0x00, // avif
            ];

            Assert.Equal("photo.avif", Helper.AlignExtensionWithContent("photo.avif", avifBytes, "image/avif"));
        }

        [Fact]
        public void UndetectableContentKeepsTheOriginalName()
        {
            byte[] unknownBytes = Encoding.ASCII.GetBytes("no magic bytes here at all");

            Assert.Equal("notes.custom", Helper.AlignExtensionWithContent("notes.custom", unknownBytes));
        }
    }
}
