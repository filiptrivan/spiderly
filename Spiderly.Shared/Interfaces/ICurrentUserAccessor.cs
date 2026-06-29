namespace Spiderly.Shared.Interfaces
{
    /// <summary>
    /// Narrow accessor for the current request's authenticated user id — the slice of the auth stack a domain service
    /// needs to attribute work to a user, without depending on the full <c>AuthenticationService</c> and its HTTP /
    /// cookie / token dependencies. Implemented by <c>AuthenticationService</c>; trivially faked in tests.
    /// </summary>
    public interface ICurrentUserAccessor
    {
        /// <summary>The current authenticated user's id. Throws when there is no authenticated principal.</summary>
        long GetCurrentUserId();

        /// <summary>The current authenticated user's id, or <c>null</c> for an anonymous / guest visitor.</summary>
        long? GetCurrentUserIdOrDefault();
    }
}
