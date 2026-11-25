namespace WorkflowStudio.Models;

/// <summary>
/// Runtime execution state of a workflow step.
/// </summary>
public enum WorkflowStepStatus
{
    Idle,
    Queued,
    Running,
    Succeeded,
    Warning,
    Failed,
    Skipped
}
