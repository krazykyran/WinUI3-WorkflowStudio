namespace WorkflowStudio.Models;

/// <summary>
/// Serializable root document saved to and loaded from disk (.wfproj / .json).
/// </summary>
public sealed class WorkflowProject
{
    public string Name { get; set; } = "Sample Release Pipeline";

    public string Description { get; set; } = "A starter workflow demonstrating WinUI 3 layout, MVVM, and async execution.";

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;

    public DateTimeOffset ModifiedAt { get; set; } = DateTimeOffset.Now;

    public List<WorkflowStep> Steps { get; set; } = [];
}
