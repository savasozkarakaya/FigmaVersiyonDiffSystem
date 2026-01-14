using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FigmaDiffBackend.Data;
using FigmaDiffBackend.Models;
using Microsoft.EntityFrameworkCore;

namespace FigmaDiffBackend.Services;

public class SlackService
{
    private readonly HttpClient _client;
    private readonly IConfiguration _config;
    private readonly DiffContext _db;
    private readonly ILogger<SlackService> _logger;

    public SlackService(HttpClient client, IConfiguration config, DiffContext db, ILogger<SlackService> logger)
    {
        _client = client;
        _config = config;
        _db = db;
        _logger = logger;
        
        var token = _config["Slack:BotToken"];
        if (!string.IsNullOrEmpty(token))
        {
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
    }

    public async Task<string?> PostDiffMessageAsync(string channel, string issueKey, string messageText, string? threadTs = null)
    {
        // Check if we need to find an existing thread for this IssueKey if threadTs is null
        if (threadTs == null)
        {
            var existingThread = await _db.SlackThreads.FindAsync(issueKey);
            if (existingThread != null)
            {
                threadTs = existingThread.ThreadTs;
                // Optimization: Maybe verify if thread is too old? For now assume it's good.
            }
        }

        var payload = new 
        { 
            channel = channel,
            text = messageText, // Fallback
            thread_ts = threadTs,
            // We can add blocks here for rich content
        };

        var json = JsonSerializer.Serialize(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        try
        {
            var response = await _client.PostAsync("https://slack.com/api/chat.postMessage", content);
            var respStr = await response.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(respStr);
            
            if (!doc.RootElement.GetProperty("ok").GetBoolean())
            {
                 _logger.LogError("Slack API Error: {Response}", respStr);
                 return null;
            }

            var ts = doc.RootElement.GetProperty("ts").GetString();

            // Save thread if this was a new root conversation for this issue
            if (threadTs == null && ts != null)
            {
                if (await _db.SlackThreads.FindAsync(issueKey) == null)
                {
                    _db.SlackThreads.Add(new SlackThread 
                    { 
                        IssueKey = issueKey, 
                        Channel = channel, 
                        ThreadTs = ts 
                    });
                    await _db.SaveChangesAsync();
                }
            }

            return ts;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error posting to Slack");
            return null;
        }
    }
}
