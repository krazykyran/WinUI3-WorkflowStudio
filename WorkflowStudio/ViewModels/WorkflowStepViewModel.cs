using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using WorkflowStudio.Models;

namespace WorkflowStudio.ViewModels;

/// <summary>
/// ViewModel for a single workflow step displayed in the Left Explorer Pane and edited inside a Workspace Tab.
/// Uses CommunityToolkit.Mvvm [ObservableProperty] on C# partial properties (WinRT / AOT compatible).
/// </summary>
public partial class WorkflowStepViewModel : ObservableObject
{
    private readonly Action _onChanged;

    public static IReadOnlyList<WorkflowStepType> AvailableStepTypes { get; } = Enum.GetValues<WorkflowStepType>();

    public Guid Id { get; }

    [ObservableProperty]
    public partial string Name { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StepTypeIndex))]
    [NotifyPropertyChangedFor(nameof(TypeGlyph))]
    [NotifyPropertyChangedFor(nameof(TypeLabel))]
    public partial WorkflowStepType StepType { get; set; }

    [ObservableProperty]
    public partial string Description { get; set; }

    [ObservableProperty]
    public partial string Command { get; set; }

    [ObservableProperty]
    public partial string WorkingDirectory { get; set; }

    [ObservableProperty]
    public partial double TimeoutSeconds { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ItemOpacity))]
    [NotifyPropertyChangedFor(nameof(EnabledLabel))]
    public partial bool IsEnabled { get; set; }

    [ObservableProperty]
    public partial bool ContinueOnError { get; set; }

    [ObservableProperty]
    public partial bool SimulateWarning { get; set; }

    [ObservableProperty]
    public partial bool SimulateFailure { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    [NotifyPropertyChangedFor(nameof(StatusBrush))]
    [NotifyPropertyChangedFor(nameof(IsStepRunning))]
    public partial WorkflowStepStatus Status { get; set; } = WorkflowStepStatus.Idle;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    public partial int LastRunDurationMs { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasOutput))]
    [NotifyPropertyChangedFor(nameof(DisplayOutput))]
    public partial string LastOutput { get; set; } = string.Empty;

    public WorkflowStepViewModel(WorkflowStep model, Action onChanged)
    {
        _onChanged = onChanged;
        Id = model.Id;
        Name = model.Name;
        StepType = model.StepType;
        Description = model.Description;
        Command = model.Command;
        WorkingDirectory = model.WorkingDirectory;
        TimeoutSeconds = model.TimeoutSeconds;
        IsEnabled = model.IsEnabled;
        ContinueOnError = model.ContinueOnError;
        SimulateWarning = model.SimulateWarning;
        SimulateFailure = model.SimulateFailure;
    }

    /// <summary>
    /// Integer index helper for strict two-way x:Bind with ComboBox.SelectedIndex.
    /// </summary>
    public int StepTypeIndex
    {
        get => (int)StepType;
        set
        {
            if (value >= 0 && value < AvailableStepTypes.Count)
            {
                StepType = AvailableStepTypes[value];
            }
        }
    }

    public string TypeLabel => StepType.ToString();

    public string EnabledLabel => IsEnabled ? "Enabled" : "Disabled";

    public double ItemOpacity => IsEnabled ? 1.0 : 0.55;

    public bool IsStepRunning => Status == WorkflowStepStatus.Running;

    public bool HasOutput => !string.IsNullOrWhiteSpace(LastOutput);

    public string DisplayOutput => HasOutput
        ? LastOutput
        : "No output yet. Press F5 (Run Workflow) or click 'Run This Step' above to test this step.";

    /// <summary>
    /// Segoe Fluent Icons glyph for each step category.
    /// </summary>
    public string TypeGlyph => StepType switch
    {
        WorkflowStepType.Trigger => "\uE945", // LightningBolt
        WorkflowStepType.Build => "\uE90F",   // Repair / Build
        WorkflowStepType.Test => "\uE9D9",    // Checklist
        WorkflowStepType.Script => "\uE756",  // CommandPrompt
        WorkflowStepType.Deploy => "\uE898",  // Upload
        WorkflowStepType.Notify => "\uEA8F",  // Ringer / Bell
        _ => "\uE7C3"
    };

    public string StatusText => Status switch
    {
        WorkflowStepStatus.Idle => "Idle",
        WorkflowStepStatus.Queued => "Queued",
        WorkflowStepStatus.Running => "Running...",
        WorkflowStepStatus.Succeeded => LastRunDurationMs > 0 ? $"Passed ({LastRunDurationMs} ms)" : "Passed",
        WorkflowStepStatus.Warning => LastRunDurationMs > 0 ? $"Warning ({LastRunDurationMs} ms)" : "Warning",
        WorkflowStepStatus.Failed => LastRunDurationMs > 0 ? $"Failed ({LastRunDurationMs} ms)" : "Failed",
        WorkflowStepStatus.Skipped => "Skipped",
        _ => "Idle"
    };

    public SolidColorBrush StatusBrush => Status switch
    {
        WorkflowStepStatus.Running => new SolidColorBrush(Color.FromArgb(255, 58, 150, 221)),
        WorkflowStepStatus.Succeeded => new SolidColorBrush(Color.FromArgb(255, 62, 180, 96)),
        WorkflowStepStatus.Warning => new SolidColorBrush(Color.FromArgb(255, 224, 159, 36)),
        WorkflowStepStatus.Failed => new SolidColorBrush(Color.FromArgb(255, 232, 74, 74)),
        WorkflowStepStatus.Queued => new SolidColorBrush(Color.FromArgb(255, 160, 130, 230)),
        _ => new SolidColorBrush(Color.FromArgb(255, 135, 135, 135))
    };

    public WorkflowStep ToModel() => new()
    {
        Id = Id,
        Name = Name,
        StepType = StepType,
        Description = Description,
        Command = Command,
        WorkingDirectory = WorkingDirectory,
        TimeoutSeconds = double.IsNaN(TimeoutSeconds) ? 30 : Math.Max(1, (int)Math.Round(TimeoutSeconds)),
        IsEnabled = IsEnabled,
        ContinueOnError = ContinueOnError,
        SimulateWarning = SimulateWarning,
        SimulateFailure = SimulateFailure
    };

    public WorkflowStep CloneModel()
    {
        var copy = ToModel();
        copy.Id = Guid.NewGuid();
        copy.Name = $"{Name} (Copy)";
        return copy;
    }

    // Source-generated partial hooks mark the parent project as dirty whenever user-editable fields change.
    partial void OnNameChanged(string value) => _onChanged?.Invoke();
    partial void OnStepTypeChanged(WorkflowStepType value) => _onChanged?.Invoke();
    partial void OnDescriptionChanged(string value) => _onChanged?.Invoke();
    partial void OnCommandChanged(string value) => _onChanged?.Invoke();
    partial void OnWorkingDirectoryChanged(string value) => _onChanged?.Invoke();
    partial void OnTimeoutSecondsChanged(double value) => _onChanged?.Invoke();
    partial void OnIsEnabledChanged(bool value) => _onChanged?.Invoke();
    partial void OnContinueOnErrorChanged(bool value) => _onChanged?.Invoke();
    partial void OnSimulateWarningChanged(bool value) => _onChanged?.Invoke();
    partial void OnSimulateFailureChanged(bool value) => _onChanged?.Invoke();
}
