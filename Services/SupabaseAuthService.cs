using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OnSiteApi.Services;

public class SupabaseAuthService
{
    private readonly HttpClient _httpClient;
    private readonly string _supabaseUrl;
    private readonly string _supabaseSecretKey;

    public SupabaseAuthService(IConfiguration configuration)
    {
        _supabaseUrl =
            configuration["SUPABASE_URL"]
            ?? throw new InvalidOperationException(
                "SUPABASE_URL environment variable was not found.");

        _supabaseSecretKey =
            configuration["SUPABASE_SECRET_KEY"]
            ?? throw new InvalidOperationException(
                "SUPABASE_SECRET_KEY environment variable was not found.");

        _httpClient = new HttpClient();
    }

    public async Task<Guid> CreateUserAsync(
        string email,
        string password,
        string fullName)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Email is required.");

        if (string.IsNullOrWhiteSpace(password))
            throw new ArgumentException("Password is required.");

        if (password.Length < 6)
            throw new ArgumentException(
                "Password must be at least 6 characters long.");

        var request = new
        {
            email = email.Trim(),
            password,
            email_confirm = true,
            user_metadata = new
            {
                full_name = fullName.Trim()
            }
        };

        using var httpRequest = new HttpRequestMessage(
            HttpMethod.Post,
            $"{_supabaseUrl.TrimEnd('/')}/auth/v1/admin/users");

        httpRequest.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", _supabaseSecretKey);

        httpRequest.Headers.Add("apikey", _supabaseSecretKey);

        httpRequest.Content = JsonContent.Create(request);

        using var response = await _httpClient.SendAsync(httpRequest);

        var responseBody = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            var errorMessage = ExtractErrorMessage(responseBody);

            throw new InvalidOperationException(
                $"Supabase Auth user creation failed: {errorMessage}");
        }

        var authUser =
            JsonSerializer.Deserialize<SupabaseAuthUser>(
                responseBody,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

        if (authUser?.Id == null)
        {
            throw new InvalidOperationException(
                "Supabase created the user but did not return a user ID.");
        }

        if (!Guid.TryParse(authUser.Id, out var userId))
        {
            throw new InvalidOperationException(
                "Supabase returned an invalid user ID.");
        }

        return userId;
    }

    public async Task DeleteUserAsync(Guid userId)
    {
        using var httpRequest = new HttpRequestMessage(
            HttpMethod.Delete,
            $"{_supabaseUrl.TrimEnd('/')}/auth/v1/admin/users/{userId}");

        httpRequest.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", _supabaseSecretKey);

        httpRequest.Headers.Add("apikey", _supabaseSecretKey);

        using var response = await _httpClient.SendAsync(httpRequest);

        if (!response.IsSuccessStatusCode)
        {
            var responseBody =
                await response.Content.ReadAsStringAsync();

            Console.WriteLine(
                $"Failed to roll back Supabase Auth user {userId}: {responseBody}");
        }
    }

    private static string ExtractErrorMessage(string responseBody)
    {
        if (string.IsNullOrWhiteSpace(responseBody))
            return "Unknown Supabase error.";

        try
        {
            using var document =
                JsonDocument.Parse(responseBody);

            var root = document.RootElement;

            if (root.TryGetProperty("msg", out var msg))
                return msg.GetString() ?? responseBody;

            if (root.TryGetProperty("message", out var message))
                return message.GetString() ?? responseBody;

            if (root.TryGetProperty("error_description", out var description))
                return description.GetString() ?? responseBody;

            if (root.TryGetProperty("error", out var error))
                return error.GetString() ?? responseBody;
        }
        catch
        {
            // Fall back to the raw response.
        }

        return responseBody;
    }

    private sealed class SupabaseAuthUser
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }
    }
}