using System.ComponentModel.DataAnnotations;

namespace FigmaDiffBackend.Models;

public class SlackThread
{
    [Key]
    public string IssueKey { get; set; } = string.Empty;
    
    // The timestamp of the parent message to reply to
    public string ThreadTs { get; set; } = string.Empty;
    
    public string Channel { get; set; } = string.Empty;
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}
