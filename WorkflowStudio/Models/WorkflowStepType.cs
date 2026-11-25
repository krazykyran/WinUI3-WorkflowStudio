namespace WorkflowStudio.Models;

/// <summary>
/// Categories of workflow steps supported by the demo workbench.
/// </summary>
public enum WorkflowStepType
{
    Trigger,
    Build,
    Test,
    Script,
    Deploy,
    Notify
}
