namespace Spiderly.Shared.Interfaces
{
    /// <summary>
    /// Storage adapter for per-property blobs. All methods take the property's <c>keyPrefix</c> —
    /// <c>{EntityName}/{PropertyName}</c> by default, overridable per property via
    /// <c>[…Storage(KeyPrefix = "…")]</c>. The prefix is the listing scope for cleanup and
    /// promotion, which is why it must be unique per blob property (enforced at generation time).
    /// </summary>
    public interface IFileManager
    {
        /// <summary>
        /// Uploads and returns the newly generated file name (key, or URL for public providers).
        /// <paramref name="descriptiveName"/> — when the owning entity can supply one — becomes
        /// the slugified, human/SEO-readable part of the key (see <c>BlobKeyConventions</c>).
        /// </summary>
        Task<string> UploadFileAsync(string fileName, string keyPrefix, string objectId, Stream content, string? descriptiveName = null, string? newFileName = null);

        /// <summary>
        /// Deletes every blob under <c>{keyPrefix}/{objectId}/</c> except the active one.
        /// Authorization happens in the generated save flow before this is called.
        /// </summary>
        Task DeleteNonActiveBlobs(string? activeBlobName, string keyPrefix, string objectId);

        /// <summary>
        /// Deletes every editor-image blob under <c>{keyPrefix}/{objectId}/</c> whose URL no
        /// longer appears in the saved HTML.
        /// </summary>
        Task DeleteNonActiveEditorImages(List<string> activeImageUrls, string keyPrefix, string objectId);

        /// <summary>
        /// Null when the blob no longer exists in storage (see <c>S3PublicStorageService</c>);
        /// disk/private implementations throw instead.
        /// </summary>
        Task<string?> GetFileDataAsync(string key);

        /// <summary>
        /// Moves a blob that was uploaded to the temporary staging prefix to its permanent
        /// entity-scoped path. Called by the generated save flow once the entity has a real id —
        /// the first moment a trusted <paramref name="descriptiveName"/> exists, which is why the
        /// promoted key (not the staged one) carries the slug. Returns the new key (or url for
        /// providers that return urls). If the supplied <paramref name="currentKeyOrUrl"/> is not
        /// under the staging prefix the value is returned unchanged.
        /// <para>
        /// Implementations must delegate the decision to
        /// <c>BlobKeyConventions.TryBuildPromotedKeyAsync</c> rather than re-deriving it. That is
        /// what guarantees <paramref name="resolveDescriptiveName"/> — a factory, not a value — is
        /// awaited ONLY when a promotion actually happens, so the common no-staged-upload save
        /// never pays the consumer's descriptive-name lookup (typically a DB query).
        /// </para>
        /// </summary>
        Task<string> MoveBlobToEntityPathAsync(string currentKeyOrUrl, string keyPrefix, string objectId, Func<Task<string>>? resolveDescriptiveName = null);
    }
}
