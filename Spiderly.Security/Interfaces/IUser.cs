namespace Spiderly.Security.Interfaces
{
    /// <summary>
    /// A human principal that authenticates by email (passwordless verification code or an external
    /// provider). Identity, disabled-state, and roles are inherited from <see cref="ISecurityPrincipal"/>;
    /// <see cref="IUser"/> adds only the human-specific <see cref="Email"/> that the built-in login flow targets.
    /// </summary>
    public interface IUser : ISecurityPrincipal
    {
        public string Email { get; set; }
    }
}
