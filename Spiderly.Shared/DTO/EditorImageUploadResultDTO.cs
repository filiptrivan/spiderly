namespace Spiderly.Shared.DTO
{
    /// <summary>
    /// Result returned by editor image upload endpoints. The width and height are read from the
    /// optimized image bytes so callers (e.g. the Quill editor) can write explicit dimensions
    /// onto the inserted &lt;img&gt; tag — preventing layout shift on the storefront and enabling
    /// next/image-style optimizers to render a srcset without re-probing the asset.
    /// </summary>
    public class EditorImageUploadResultDTO
    {
        /// <summary>
        /// Publicly addressable URL of the uploaded image after WebP optimization. Safe to embed
        /// directly in the editor's HTML and to expose to crawlers.
        /// </summary>
        public string Url { get; set; }

        /// <summary>
        /// Pixel width of the encoded image, measured *after* resize. Callers should write this
        /// onto the rendered &lt;img&gt; tag so downstream layout knows the box up-front.
        /// </summary>
        public int Width { get; set; }

        /// <summary>
        /// Pixel height of the encoded image, measured *after* resize. Pairs with <see cref="Width"/>;
        /// together they let renderers reserve the final box and avoid layout shift.
        /// </summary>
        public int Height { get; set; }
    }
}
