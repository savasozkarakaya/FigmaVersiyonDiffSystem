using System;

namespace FigmaDiffBackend.Models;

public class Comparison
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string BaselineId { get; set; } = string.Empty;
    public string IssueKey { get; set; } = string.Empty;
    public string SlackChannel { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public string AfterImagePath { get; set; } = string.Empty;
    
    public double ChangedPercent { get; set; }
    public int ChangedPixels { get; set; }
    public bool IsDifferent => ChangedPixels > 0;
    
    public string? JiraCommentId { get; set; }
    public string? SlackTs { get; set; }
    public string? ErrorLog { get; set; }
}
