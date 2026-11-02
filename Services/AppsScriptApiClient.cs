using System.Net.Http;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Elecctron.App.Models;

namespace Elecctron.App.Services;

public sealed class AppsScriptApiClient
{
    // Apps Script reads lowercase keys such as data.action and data.rollNumber.
    // Web defaults serialize anonymous C# properties as camelCase and also allow
    // case-insensitive response deserialization.
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };
    private readonly HttpClient _http;
    private readonly string _webAppUrl;

    public AppsScriptApiClient(string webAppUrl)
    {
        _webAppUrl = webAppUrl.Trim();
        _http = new HttpClient(new HttpClientHandler { AllowAutoRedirect = true })
        {
            Timeout = TimeSpan.FromSeconds(25)
        };
    }

    public async Task<CandidateLoginResponse> CandidateLoginAsync(string rollNumber, string password, string examCode, CancellationToken cancellationToken = default)
    {
        var response = await PostAsync<CandidateLoginResponse>(new
        {
            action = "candidateLogin",
            rollNumber,
            password,
            examCode
        }, cancellationToken);
        EnsureSuccess(response.Status, response.Message);
        return response;
    }

    public async Task<QuestionPackage> GetQuestionsAsync(string examCode, string rollNumber, CancellationToken cancellationToken = default)
    {
        var url = AddQuery(_webAppUrl, new Dictionary<string, string>
        {
            ["action"] = "getQuestions",
            ["examCode"] = examCode,
            ["rollNumber"] = rollNumber
        });
        using var response = await _http.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<QuestionPackage>(JsonOptions, cancellationToken)
                     ?? throw new InvalidOperationException("The Apps Script API returned an empty question response.");
        EnsureSuccess(result.Status, result.Message);
        return result;
    }

    public async Task<ApiStatusResponse> SyncHeartbeatAsync(string rollNumber, string examCode, IReadOnlyDictionary<string, string> answers, CancellationToken cancellationToken = default)
    {
        var response = await PostAsync<ApiStatusResponse>(new { action = "syncHeartbeat", rollNumber, examCode, answers }, cancellationToken);
        EnsureSuccess(response.Status, response.Message);
        return response;
    }

    public async Task<SubmitExamResponse> SubmitExamAsync(string rollNumber, string examCode, IReadOnlyDictionary<string, string> answers, CancellationToken cancellationToken = default)
    {
        var result = await PostAsync<SubmitExamResponse>(new { action = "submitExam", rollNumber, examCode, answers }, cancellationToken);
        EnsureSuccess(result.Status, result.Message);
        return result;
    }

    public async Task<byte[]?> GetCandidatePhotoAsync(string? photoUrl, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(photoUrl) || !Uri.TryCreate(photoUrl, UriKind.Absolute, out var uri)) return null;
        if (uri.Scheme != Uri.UriSchemeHttps || uri.Host != "lh3.googleusercontent.com") return null;
        try
        {
            return await _http.GetByteArrayAsync(uri, cancellationToken);
        }
        catch (HttpRequestException)
        {
            return null;
        }
        catch (TaskCanceledException)
        {
            return null;
        }
    }

    public async Task<byte[]?> GetQuestionImageAsync(string? imageUrl, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(imageUrl) || !Uri.TryCreate(imageUrl, UriKind.Absolute, out var uri)) return null;
        if (uri.Scheme != Uri.UriSchemeHttps || uri.Host is not ("lh3.googleusercontent.com" or "drive.google.com")) return null;

        // Convert common Drive share URLs to a direct image endpoint before fetching.
        if (uri.Host == "drive.google.com")
        {
            var match = System.Text.RegularExpressions.Regex.Match(uri.AbsoluteUri, @"(?:id=|/d/)([A-Za-z0-9_-]{20,})");
            if (match.Success)
                uri = new Uri($"https://drive.google.com/uc?export=view&id={match.Groups[1].Value}");
        }

        try
        {
            return await _http.GetByteArrayAsync(uri, cancellationToken);
        }
        catch (HttpRequestException) { return null; }
        catch (TaskCanceledException) { return null; }
    }
    private async Task<T> PostAsync<T>(object payload, CancellationToken cancellationToken)
    {
        EnsureConfigured();
        using var response = await _http.PostAsJsonAsync(_webAppUrl, payload, JsonOptions, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Apps Script returned HTTP {(int)response.StatusCode} ({response.ReasonPhrase}). {Summarize(body)}",
                null,
                response.StatusCode);
        }

        try
        {
            return JsonSerializer.Deserialize<T>(body, JsonOptions)
                   ?? throw new InvalidOperationException("The Apps Script API returned an empty JSON response.");
        }
        catch (JsonException ex) when (LooksLikeHtml(body, response.Content.Headers.ContentType?.MediaType))
        {
            throw new InvalidOperationException(
                "Apps Script returned an HTML page instead of API JSON. Confirm the deployment is a Web app, access is set to Anyone, and the URL ends in /exec.", ex);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("Apps Script returned a response that is not valid JSON.", ex);
        }
    }

    private static bool LooksLikeHtml(string body, string? mediaType)
        => string.Equals(mediaType, "text/html", StringComparison.OrdinalIgnoreCase)
           || body.TrimStart().StartsWith("<!doctype html", StringComparison.OrdinalIgnoreCase)
           || body.TrimStart().StartsWith("<html", StringComparison.OrdinalIgnoreCase);

    private static string Summarize(string body)
    {
        var compact = string.Join(' ', body.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return compact.Length <= 300 ? compact : compact[..300];
    }

    private void EnsureConfigured()
    {
        if (!Uri.TryCreate(_webAppUrl, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps || !_webAppUrl.Contains("/exec", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Set AppsScriptWebAppUrl in appsettings.json to your deployed HTTPS Apps Script URL ending in /exec.");
        }
    }

    private static string AddQuery(string baseUrl, IReadOnlyDictionary<string, string> values)
    {
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps || !baseUrl.Contains("/exec", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Set AppsScriptWebAppUrl in appsettings.json to your deployed HTTPS Apps Script URL ending in /exec.");
        var query = string.Join("&", values.Select(pair => $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}"));
        return $"{baseUrl}?{query}";
    }

    private static void EnsureSuccess(string? status, string? message)
    {
        if (!string.Equals(status, "success", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(message) ? "The Apps Script request was rejected." : message);
    }
}
