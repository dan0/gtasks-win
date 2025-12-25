namespace GTasks.Core.Services;

/// <summary>
/// Handles Google OAuth 2.0 authentication.
/// </summary>
public interface IAuthService
{
    /// <summary>
    /// Gets whether the user is currently authenticated.
    /// </summary>
    bool IsAuthenticated { get; }

    /// <summary>
    /// Gets the current user's email if authenticated.
    /// </summary>
    string? UserEmail { get; }

    /// <summary>
    /// Initiates the OAuth 2.0 login flow.
    /// </summary>
    Task<bool> LoginAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Logs out and clears stored credentials.
    /// </summary>
    Task LogoutAsync();

    /// <summary>
    /// Gets a valid access token, refreshing if necessary.
    /// </summary>
    Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Tries to restore authentication from stored credentials.
    /// </summary>
    Task<bool> TryRestoreAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Event raised when authentication state changes.
    /// </summary>
    event EventHandler<bool>? AuthenticationChanged;
}
