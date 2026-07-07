using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Spiderly.Shared.Exceptions;
using Spiderly.Shared.Helpers;
using Xunit;

namespace Spiderly.Shared.Tests
{
    /// <summary>
    /// Pins the upload-validation contract of <see cref="Helper.ValidateFileSignature"/>,
    /// <see cref="Helper.ValidateFileSize"/> and <see cref="Helper.ValidateImageDimensions"/>:
    /// <list type="bullet">
    /// <item>Failures throw <see cref="BusinessException"/> (HTTP 400, message shown to the user) — NOT
    /// <see cref="SecurityViolationException"/>, which the exception handler masks behind a generic 403
    /// that admin UIs render as "you are not authorized". These are user-correctable mistakes
    /// (wrong file picked, renamed extension), and the localized messages were authored to be shown.</item>
    /// <item><c>image/svg+xml</c> is validated by XML content sniffing (SVG has no magic bytes, so
    /// Mime-Detective can never identify it) and rejected when it carries active content.</item>
    /// <item>Wildcard entries like <c>image/*</c> — the canonical example in the attribute docs —
    /// match any declared type with that prefix instead of being dead entries that reject everything.</item>
    /// </list>
    /// </summary>
    public class FileUploadValidationTests
    {
        // 8-byte PNG signature + IHDR chunk header — enough for Mime-Detective's prefix match.
        private static readonly byte[] PngBytes =
        [
            0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
            0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,
        ];

        // JPEG SOI + JFIF APP0 marker.
        private static readonly byte[] JpegBytes =
        [
            0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00,
        ];

        private const string ValidSvg =
            """
            <?xml version="1.0" encoding="UTF-8"?>
            <!-- exported from a vector editor -->
            <svg xmlns="http://www.w3.org/2000/svg" width="24" height="24" viewBox="0 0 24 24">
              <rect width="24" height="24" fill="#f00"/>
            </svg>
            """;

        // Illustrator/Inkscape exports commonly carry a DOCTYPE — must still validate.
        private const string ValidSvgWithDoctype =
            """
            <?xml version="1.0" encoding="UTF-8"?>
            <!DOCTYPE svg PUBLIC "-//W3C//DTD SVG 1.1//EN" "http://www.w3.org/Graphics/SVG/1.1/DTD/svg11.dtd">
            <svg xmlns="http://www.w3.org/2000/svg" width="24" height="24">
              <circle cx="12" cy="12" r="10"/>
            </svg>
            """;

        private static MemoryStream Stream(string text) => new(Encoding.UTF8.GetBytes(text));

        private static MemoryStream Stream(byte[] bytes) => new(bytes);

        //#region SVG signature validation

        [Fact]
        public async Task Valid_svg_passes_signature_validation()
        {
            using MemoryStream content = Stream(ValidSvg);

            await Helper.ValidateFileSignature(content, "image/svg+xml", ["image/svg+xml"]);
        }

        [Fact]
        public async Task Valid_svg_with_doctype_passes_signature_validation()
        {
            using MemoryStream content = Stream(ValidSvgWithDoctype);

            await Helper.ValidateFileSignature(content, "image/svg+xml", ["image/svg+xml"]);
        }

        [Fact]
        public async Task Svg_validation_resets_stream_position()
        {
            using MemoryStream content = Stream(ValidSvg);
            content.Position = 3; // simulate a prior read

            await Helper.ValidateFileSignature(content, "image/svg+xml", ["image/svg+xml"]);

            Assert.Equal(0, content.Position);
        }

        [Fact]
        public async Task Svg_with_script_element_is_rejected()
        {
            using MemoryStream content = Stream(
                """<svg xmlns="http://www.w3.org/2000/svg"><script>alert(1)</script></svg>""");

            await Assert.ThrowsAsync<BusinessException>(() =>
                Helper.ValidateFileSignature(content, "image/svg+xml", ["image/svg+xml"]));
        }

        [Fact]
        public async Task Svg_with_event_handler_attribute_is_rejected()
        {
            using MemoryStream content = Stream(
                """<svg xmlns="http://www.w3.org/2000/svg" onload="alert(1)"><rect width="1" height="1"/></svg>""");

            await Assert.ThrowsAsync<BusinessException>(() =>
                Helper.ValidateFileSignature(content, "image/svg+xml", ["image/svg+xml"]));
        }

        [Fact]
        public async Task Svg_with_javascript_href_is_rejected()
        {
            using MemoryStream content = Stream(
                """
                <svg xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink">
                  <a xlink:href="javascript:alert(1)"><text x="0" y="10">x</text></a>
                </svg>
                """);

            await Assert.ThrowsAsync<BusinessException>(() =>
                Helper.ValidateFileSignature(content, "image/svg+xml", ["image/svg+xml"]));
        }

        [Fact]
        public async Task Svg_with_foreign_object_is_rejected()
        {
            using MemoryStream content = Stream(
                """<svg xmlns="http://www.w3.org/2000/svg"><foreignObject><div>x</div></foreignObject></svg>""");

            await Assert.ThrowsAsync<BusinessException>(() =>
                Helper.ValidateFileSignature(content, "image/svg+xml", ["image/svg+xml"]));
        }

        [Fact]
        public async Task Non_svg_xml_declared_as_svg_is_rejected()
        {
            using MemoryStream content = Stream(
                """<html xmlns="http://www.w3.org/1999/xhtml"><body>not an image</body></html>""");

            await Assert.ThrowsAsync<BusinessException>(() =>
                Helper.ValidateFileSignature(content, "image/svg+xml", ["image/svg+xml"]));
        }

        [Fact]
        public async Task Binary_content_declared_as_svg_is_rejected()
        {
            using MemoryStream content = Stream(PngBytes);

            await Assert.ThrowsAsync<BusinessException>(() =>
                Helper.ValidateFileSignature(content, "image/svg+xml", ["image/svg+xml"]));
        }

        [Fact]
        public async Task Svg_content_declared_as_png_is_rejected()
        {
            using MemoryStream content = Stream(ValidSvg);

            await Assert.ThrowsAsync<BusinessException>(() =>
                Helper.ValidateFileSignature(content, "image/png", ["image/png", "image/svg+xml"]));
        }

        //#endregion

        //#region Whitelist gate + wildcards

        [Fact]
        public async Task Declared_type_outside_whitelist_is_rejected_with_business_exception()
        {
            using MemoryStream content = Stream(PngBytes);

            BusinessException ex = await Assert.ThrowsAsync<BusinessException>(() =>
                Helper.ValidateFileSignature(content, "image/png", ["image/jpeg"]));

            Assert.Contains("image/png", ex.Message);
        }

        [Fact]
        public async Task Wildcard_image_entry_matches_declared_image_type()
        {
            using MemoryStream content = Stream(PngBytes);

            await Helper.ValidateFileSignature(content, "image/png", ["image/*"]);
        }

        [Fact]
        public async Task Wildcard_image_entry_does_not_match_non_image_type()
        {
            using MemoryStream content = Stream(JpegBytes);

            await Assert.ThrowsAsync<BusinessException>(() =>
                Helper.ValidateFileSignature(content, "application/pdf", ["image/*"]));
        }

        [Fact]
        public async Task Exact_match_still_passes_with_matching_magic_bytes()
        {
            using MemoryStream content = Stream(JpegBytes);

            await Helper.ValidateFileSignature(content, "image/jpeg", ["image/jpeg", "image/png"]);
        }

        [Fact]
        public async Task Spoofed_content_type_is_rejected_with_business_exception()
        {
            using MemoryStream content = Stream(JpegBytes); // real JPEG bytes...

            await Assert.ThrowsAsync<BusinessException>(() =>
                Helper.ValidateFileSignature(content, "image/png", ["image/png"])); // ...declared as PNG
        }

        [Fact]
        public async Task Empty_file_is_rejected_with_business_exception()
        {
            using MemoryStream content = new();

            await Assert.ThrowsAsync<BusinessException>(() =>
                Helper.ValidateFileSignature(content, "image/svg+xml", ["image/svg+xml"]));
        }

        //#endregion

        //#region Size + dimensions

        [Fact]
        public void Oversized_file_is_rejected_with_business_exception()
        {
            BusinessException ex = Assert.Throws<BusinessException>(() =>
                Helper.ValidateFileSize(fileSize: 3_000_000, maxFileSize: 2_000_000));

            Assert.Contains("2", ex.Message); // limit surfaced in MB
        }

        [Fact]
        public void File_within_size_limit_passes()
        {
            Helper.ValidateFileSize(fileSize: 1_000_000, maxFileSize: 2_000_000);
        }

        [Fact]
        public async Task Wrong_image_dimensions_are_rejected_with_business_exception()
        {
            using MemoryStream content = new();
            using (Image<Rgba32> image = new(width: 2, height: 2))
            {
                await image.SaveAsPngAsync(content);
            }
            content.Position = 0;

            await Assert.ThrowsAsync<BusinessException>(() =>
                Helper.ValidateImageDimensions(content, width: 5, height: 5));
        }

        //#endregion

        //#region Optimizable-image routing (generated OnBefore*IsUploaded hook)

        [Theory]
        [InlineData("image/jpeg", true)]
        [InlineData("image/png", true)]
        [InlineData("image/webp", true)]
        [InlineData("image/avif", true)]
        [InlineData("image/svg+xml", false)] // vector — ImageSharp can't decode it; must pass through raw
        [InlineData("IMAGE/SVG+XML", false)]
        [InlineData("video/mp4", false)]
        [InlineData("application/pdf", false)]
        [InlineData(null, false)]
        public void Only_raster_images_are_routed_through_optimization(string contentType, bool expected)
        {
            Assert.Equal(expected, Helper.IsOptimizableImage(contentType));
        }

        //#endregion
    }
}
