using Microsoft.Extensions.Logging;
using PartyLights.Core.Interfaces;
using PartyLights.Core.Models;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Web;

namespace PartyLights.Services;

/// <summary>
/// Comprehensive Spotify authentication service with OAuth 2.0 PKCE flow
/// </summary>
public class SpotifyAuthenticationService : ISpotifyAuthenticationService
{
    private readonly ILogger<SpotifyAuthenticationService> _logger;
    private readonly HttpClient _httpClient;
    private readonly SpotifyApiConfig _config;
    private readonly string _tokenStoragePath;
    private SpotifyToken? _currentToken;

    public event EventHandler<SpotifyAuthenticationEventArgs>? AuthenticationChanged;

    public SpotifyAuthenticationService(
        ILogger<SpotifyAuthenticationService> logger,
        HttpClient httpClient,
        SpotifyApiConfig config)
    {
        _logger = logger;
        _httpClient = httpClient;
        _config = config;
        _tokenStoragePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "PartyLights", "spotify_token.json");
    }

    #region Authentication Methods

    /// <summary>
    /// Gets the authorization URL for OAuth 2.0 PKCE flow
    /// </summary>
    public async Task<string> GetAuthorizationUrlAsync()
    {
        try
        {
            _logger.LogInformation("Generating Spotify authorization URL");

            // Generate PKCE parameters
            var codeVerifier = GenerateCodeVerifier();
            var codeChallenge = GenerateCodeChallenge(codeVerifier);

            // Store code verifier for later use
            await StoreCodeVerifierAsync(codeVerifier);

            // Build authorization URL
            var parameters = new Dictionary<string, string>
            {
                ["response_type"] = "code",
                ["client_id"] = _config.ClientId,
                ["scope"] = string.Join(" ", _config.Scopes),
                ["redirect_uri"] = _config.RedirectUri,
                ["code_challenge_method"] = "S256",
                ["code_challenge"] = codeChallenge,
                ["state"] = Guid.NewGuid().ToString()
            };

            var queryString = string.Join("&", parameters.Select(p => $"{p.Key}={HttpUtility.UrlEncode(p.Value)}"));
            var authUrl = $"{_config.AuthUrl}?{queryString}";

            _logger.LogInformation("Spotify authorization URL generated successfully");
            return authUrl;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating Spotify authorization URL");
            throw;
        }
    }

    /// <summary>
    /// Exchanges authorization code for access token
    /// </summary>
    public async Task<SpotifyToken?> ExchangeCodeForTokenAsync(string code)
    {
        try
        {
            _logger.LogInformation("Exchanging authorization code for access token");

            // Load stored code verifier
            var codeVerifier = await LoadCodeVerifierAsync();
            if (string.IsNullOrEmpty(codeVerifier))
            {
                _logger.LogError("Code verifier not found");
                return null;
            }

            // Prepare token request
            var tokenRequest = new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["code"] = code,
                ["redirect_uri"] = _config.RedirectUri,
                ["client_id"] = _config.ClientId,
                ["code_verifier"] = codeVerifier
            };

            var content = new FormUrlEncodedContent(tokenRequest);
            var response = await _httpClient.PostAsync(_config.TokenUrl, content);

            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                var tokenResponse = JsonSerializer.Deserialize<SpotifyTokenResponse>(json);

                if (tokenResponse != null)
                {
                    var token = new SpotifyToken
                    {
                        AccessToken = tokenResponse.AccessToken,
                        RefreshToken = tokenResponse.RefreshToken,
                        TokenType = tokenResponse.TokenType,
                        ExpiresIn = tokenResponse.ExpiresIn,
                        ExpiresAt = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn),
                        Scope = tokenResponse.Scope
                    };

                    _currentToken = token;
                    await StoreTokenAsync(token);
                    await ClearCodeVerifierAsync();

                    AuthenticationChanged?.Invoke(this, new SpotifyAuthenticationEventArgs(true));
                    _logger.LogInformation("Successfully exchanged code for access token");

                    return token;
                }
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Failed to exchange code for token: {StatusCode} - {Content}", response.StatusCode, errorContent);
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exchanging authorization code for access token");
            return null;
        }
    }

    /// <summary>
    /// Refreshes the access token using refresh token
    /// </summary>
    public async Task<SpotifyToken?> RefreshAccessTokenAsync(string refreshToken)
    {
        try
        {
            _logger.LogInformation("Refreshing Spotify access token");

            // Prepare refresh request
            var refreshRequest = new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = refreshToken,
                ["client_id"] = _config.ClientId
            };

            var content = new FormUrlEncodedContent(refreshRequest);
            var response = await _httpClient.PostAsync(_config.TokenUrl, content);

            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                var tokenResponse = JsonSerializer.Deserialize<SpotifyTokenResponse>(json);

                if (tokenResponse != null)
                {
                    var token = new SpotifyToken
                    {
                        AccessToken = tokenResponse.AccessToken,
                        RefreshToken = tokenResponse.RefreshToken ?? refreshToken, // Keep existing refresh token if not provided
                        TokenType = tokenResponse.TokenType,
                        ExpiresIn = tokenResponse.ExpiresIn,
                        ExpiresAt = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn),
                        Scope = tokenResponse.Scope
                    };

                    _currentToken = token;
                    await StoreTokenAsync(token);

                    _logger.LogInformation("Successfully refreshed access token");
                    return token;
                }
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Failed to refresh access token: {StatusCode} - {Content}", response.StatusCode, errorContent);
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing access token");
            return null;
        }
    }

    /// <summary>
    /// Validates if the current token is valid
    /// </summary>
    public async Task<bool> ValidateTokenAsync(SpotifyToken token)
    {
        try
        {
            if (token == null || !token.IsValid)
            {
                return false;
            }

            // Test token by making a simple API call
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
            var response = await _httpClient.GetAsync($"{_config.BaseUrl}/me");

            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating token");
            return false;
        }
    }

    /// <summary>
    /// Revokes the current token
    /// </summary>
    public async Task<bool> RevokeTokenAsync(SpotifyToken token)
    {
        try
        {
            _logger.LogInformation("Revoking Spotify token");

            var revokeRequest = new Dictionary<string, string>
            {
                ["token"] = token.AccessToken,
                ["token_type_hint"] = "access_token"
            };

            var content = new FormUrlEncodedContent(revokeRequest);
            var response = await _httpClient.PostAsync("https://accounts.spotify.com/api/revoke", content);

            if (response.IsSuccessStatusCode)
            {
                _currentToken = null;
                await ClearStoredTokenAsync();
                AuthenticationChanged?.Invoke(this, new SpotifyAuthenticationEventArgs(false));
                _logger.LogInformation("Successfully revoked token");
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error revoking token");
            return false;
        }
    }

    #endregion

    #region Token Storage

    /// <summary>
    /// Loads stored token from file
    /// </summary>
    public async Task<SpotifyToken?> LoadStoredTokenAsync()
    {
        try
        {
            if (!File.Exists(_tokenStoragePath))
            {
                return null;
            }

            var json = await File.ReadAllTextAsync(_tokenStoragePath);
            var token = JsonSerializer.Deserialize<SpotifyToken>(json);

            if (token != null && token.IsValid)
            {
                _currentToken = token;
                return token;
            }

            // Token is expired, try to refresh
            if (token != null && !string.IsNullOrEmpty(token.RefreshToken))
            {
                var refreshedToken = await RefreshAccessTokenAsync(token.RefreshToken);
                if (refreshedToken != null)
                {
                    return refreshedToken;
                }
            }

            // Token is invalid and can't be refreshed, clear it
            await ClearStoredTokenAsync();
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading stored token");
            return null;
        }
    }

    /// <summary>
    /// Stores token to file
    /// </summary>
    public async Task<bool> StoreTokenAsync(SpotifyToken token)
    {
        try
        {
            var directory = Path.GetDirectoryName(_tokenStoragePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(token, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_tokenStoragePath, json);

            _logger.LogDebug("Token stored successfully");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error storing token");
            return false;
        }
    }

    /// <summary>
    /// Clears stored token
    /// </summary>
    public async Task<bool> ClearStoredTokenAsync()
    {
        try
        {
            if (File.Exists(_tokenStoragePath))
            {
                File.Delete(_tokenStoragePath);
            }

            _currentToken = null;
            _logger.LogDebug("Stored token cleared");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing stored token");
            return false;
        }
    }

    #endregion

    #region Private Methods

    private string GenerateCodeVerifier()
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-._~";
        var random = new Random();
        var codeVerifier = new StringBuilder();

        for (int i = 0; i < 128; i++)
        {
            codeVerifier.Append(chars[random.Next(chars.Length)]);
        }

        return codeVerifier.ToString();
    }

    private string GenerateCodeChallenge(string codeVerifier)
    {
        using var sha256 = SHA256.Create();
        var challengeBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(codeVerifier));
        return Convert.ToBase64String(challengeBytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private async Task StoreCodeVerifierAsync(string codeVerifier)
    {
        try
        {
            var verifierPath = Path.Combine(Path.GetDirectoryName(_tokenStoragePath)!, "spotify_code_verifier.txt");
            await File.WriteAllTextAsync(verifierPath, codeVerifier);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error storing code verifier");
        }
    }

    private async Task<string?> LoadCodeVerifierAsync()
    {
        try
        {
            var verifierPath = Path.Combine(Path.GetDirectoryName(_tokenStoragePath)!, "spotify_code_verifier.txt");
            if (File.Exists(verifierPath))
            {
                return await File.ReadAllTextAsync(verifierPath);
            }
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading code verifier");
            return null;
        }
    }

    private async Task ClearCodeVerifierAsync()
    {
        try
        {
            var verifierPath = Path.Combine(Path.GetDirectoryName(_tokenStoragePath)!, "spotify_code_verifier.txt");
            if (File.Exists(verifierPath))
            {
                File.Delete(verifierPath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing code verifier");
        }
    }

    #endregion
}

/// <summary>
/// Spotify token response from API
/// </summary>
internal class SpotifyTokenResponse
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; } = string.Empty;

    [JsonPropertyName("refresh_token")]
    public string? RefreshToken { get; set; }

    [JsonPropertyName("token_type")]
    public string TokenType { get; set; } = "Bearer";

    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }

    [JsonPropertyName("scope")]
    public string Scope { get; set; } = string.Empty;
}
