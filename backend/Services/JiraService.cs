using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace FigmaDiffBackend.Services;

public class JiraService
{
    private readonly HttpClient _client;
    private readonly IConfiguration _config;
    private readonly ILogger<JiraService> _logger;

    public JiraService(HttpClient client, IConfiguration config, ILogger<JiraService> logger)
    {
        _client = client;
        _config = config;
        _logger = logger;
        
        var baseUrl = _config["Jira:BaseUrl"];
        if (!string.IsNullOrEmpty(baseUrl))
        {
            _client.BaseAddress = new Uri(baseUrl);
        }
    }

    private void SetupAuth()
    {
        var authMode = _config["Jira:AuthMode"]; // "Basic" or "Bearer"
        var token = _config["Jira:Token"]; 
        var username = _config["Jira:Username"];

        if (authMode == "Basic")
        {
            var authBytes = Encoding.ASCII.GetBytes($"{username}:{token}"); // Token is password here
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(authBytes));
        }
        else // Bearer / PAT
        {
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
        
        // Anti-XSRF / Atlassian headers
        _client.DefaultRequestHeaders.Add("X-Atlassian-Token", "no-check");
    }

    public async Task<string?> AddCommentAsync(string issueKey, string body)
    {
        SetupAuth();
        var payload = new { body = body };
        var json = JsonSerializer.Serialize(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        try
        {
            var response = await _client.PostAsync($"/rest/api/2/issue/{issueKey}/comment", content);
            response.EnsureSuccessStatusCode();
            var respStr = await response.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(respStr);
            return doc.RootElement.GetProperty("id").GetString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding Jira comment to {IssueKey}", issueKey);
            return null;
        }
    }

}
