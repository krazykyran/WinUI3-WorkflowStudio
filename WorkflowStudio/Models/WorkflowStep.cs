namespace WorkflowStudio.Models;

/// <summary>
/// Serializable model representing a single configurable step in a workflow project.
/// </summary>
public sealed class WorkflowStep
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = "New Step";

    public WorkflowStepType StepType { get; set; } = WorkflowStepType.Script;

    public string Description { get; set; } = string.Empty;

    public string Command { get; set; } = string.Empty;

    public string WorkingDirectory { get; set; } = "$(ProjectDir)";

    public int TimeoutSeconds { get; set; } = 30;

    public bool IsEnabled { get; set; } = true;

    public bool ContinueOnError { get; set; }

    /// <summary>
    /// Demo toggle so you can test warning diagnostics without writing external scripts.
    /// </summary>
    public bool SimulateWarning { get; set; }

    /// <summary>
    /// Demo toggle so you can test failure diagnostics and ContinueOnError behavior.
    /// </summary>
    public bool SimulateFailure { get; set; }
}
