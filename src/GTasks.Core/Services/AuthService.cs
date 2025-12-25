using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text.Json;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Util.Store;

namespace GTasks.Core.Services;

public class AuthService : IAuthService
{
    private static readonly string[] Scopes = {
        "https://www.googleapis.com/auth/tasks",
        "https://www.googleapis.com/auth/userinfo.email"
    };

    private static readonly HttpClient _httpClient = new();

    private readonly string _clientId;
    private readonly string _clientSecret;
    private UserCredential? _credential;
    private readonly string _credentialPath;

    public bool IsAuthenticated => _credential?.Token?.AccessToken != null;
    public string? UserEmail { get; private set; }

    public event EventHandler<bool>? AuthenticationChanged;

    public AuthService()
    {
        // Load credentials from environment variables or config file
        _clientId = LoadClientId();
        _clientSecret = LoadClientSecret();

        _credentialPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "GTasks",
            "credentials");
        Directory.CreateDirectory(_credentialPath);
    }

    private static string LoadClientId()
    {
        // Try environment variable first
        var envValue = Environment.GetEnvironmentVariable("GTASKS_CLIENT_ID");
        if (!string.IsNullOrEmpty(envValue)) return envValue;

        // Try config file
        var configPath = GetConfigFilePath();
        if (File.Exists(configPath))
        {
            var config = JsonSerializer.Deserialize<OAuthConfig>(File.ReadAllText(configPath));
            if (!string.IsNullOrEmpty(config?.ClientId)) return config.ClientId;
        }

        throw new InvalidOperationException(
            "Google OAuth Client ID not found. Set GTASKS_CLIENT_ID environment variable or create oauth.json config file.");
    }

    private static string LoadClientSecret()
    {
        // Try environment variable first
        var envValue = Environment.GetEnvironmentVariable("GTASKS_CLIENT_SECRET");
        if (!string.IsNullOrEmpty(envValue)) return envValue;

        // Try config file
        var configPath = GetConfigFilePath();
        if (File.Exists(configPath))
        {
            var config = JsonSerializer.Deserialize<OAuthConfig>(File.ReadAllText(configPath));
            if (!string.IsNullOrEmpty(config?.ClientSecret)) return config.ClientSecret;
        }

        throw new InvalidOperationException(
            "Google OAuth Client Secret not found. Set GTASKS_CLIENT_SECRET environment variable or create oauth.json config file.");
    }

    private static string GetConfigFilePath()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "GTasks",
            "oauth.json");
    }

    private class OAuthConfig
    {
        public string? ClientId { get; set; }
        public string? ClientSecret { get; set; }
    }

    public async Task<bool> LoginAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var clientSecrets = new ClientSecrets
            {
                ClientId = _clientId,
                ClientSecret = _clientSecret
            };

            _credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
                clientSecrets,
                Scopes,
                "user",
                cancellationToken,
                new FileDataStore(_credentialPath, true));

            if (_credential?.Token != null)
            {
                await FetchUserInfoAsync();
                AuthenticationChanged?.Invoke(this, true);
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Login failed: {ex.Message}");
            return false;
        }
    }

    private async Task FetchUserInfoAsync()
    {
        try
        {
            var token = await GetAccessTokenAsync();
            if (string.IsNullOrEmpty(token)) return;

            var request = new HttpRequestMessage(HttpMethod.Get, "https://www.googleapis.com/oauth2/v2/userinfo");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await _httpClient.SendAsync(request);
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("email", out var emailElement))
                {
                    UserEmail = emailElement.GetString();
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to fetch user info: {ex.Message}");
        }
    }

    public async Task LogoutAsync()
    {
        if (_credential != null)
        {
            await _credential.RevokeTokenAsync(CancellationToken.None);
            _credential = null;
        }

        // Clear stored credentials
        if (Directory.Exists(_credentialPath))
        {
            foreach (var file in Directory.GetFiles(_credentialPath))
            {
                File.Delete(file);
            }
        }

        UserEmail = null;
        AuthenticationChanged?.Invoke(this, false);
    }

    public async Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        if (_credential == null)
            return null;

        // Refresh token if needed
        if (_credential.Token.IsStale)
        {
            await _credential.RefreshTokenAsync(cancellationToken);
        }

        return _credential.Token.AccessToken;
    }

    public async Task<bool> TryRestoreAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var clientSecrets = new ClientSecrets
            {
                ClientId = _clientId,
                ClientSecret = _clientSecret
            };

            var flow = new GoogleAuthorizationCodeFlow(new GoogleAuthorizationCodeFlow.Initializer
            {
                ClientSecrets = clientSecrets,
                Scopes = Scopes,
                DataStore = new FileDataStore(_credentialPath, true)
            });

            var token = await flow.LoadTokenAsync("user", cancellationToken);
            if (token != null)
            {
                _credential = new UserCredential(flow, "user", token);

                // Try to refresh if stale
                if (_credential.Token.IsStale)
                {
                    var refreshed = await _credential.RefreshTokenAsync(cancellationToken);
                    if (!refreshed)
                    {
                        _credential = null;
                        return false;
                    }
                }

                await FetchUserInfoAsync();
                AuthenticationChanged?.Invoke(this, true);
                return true;
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    internal UserCredential? GetCredential() => _credential;
}
