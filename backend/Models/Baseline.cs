using System;

namespace FigmaDiffBackend.Models;

public class Baseline
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string IssueKey { get; set; } = string.Empty;
    public string NodeId { get; set; } = string.Empty;
    public string NodeName { get; set; } = string.Empty;
    public string FileKey { get; set; } = string.Empty;
    public string PageName { get; set; } = string.Empty;
    public string User { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    // Path to the stored baseline image file (e.g., "baselines/guid.png")
    public string ImagePath { get; set; } = string.Empty;
    
    // Basic structural snapshot (JSON string)
    public string StructureJson { get; set; } = string.Empty;
}
