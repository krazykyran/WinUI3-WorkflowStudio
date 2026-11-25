using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using WorkflowStudio.Models;
using WorkflowStudio.Services;

namespace WorkflowStudio.ViewModels;

/// <summary>
/// Central MVVM ViewModel driving the entire workbench shell:
/// Menu Bar, Toolbar, Left Explorer Pane, Middle Tabbed Workspace, Bottom Diagnostics Pane, and Status Bar.
/// </summary>
public partial class MainViewModel : ObservableObject
{
    private readonly ProjectStorageService _storageService;
    private readonly WorkflowRunnerService _runnerService;
    private CancellationTokenSource? _runCts;
    private bool _suppressDirtyNotifications;

    public ObservableCollection<WorkflowStepViewModel> Steps { get; } = [];

    public ObservableCollection<WorkflowStepViewModel> FilteredSteps { get; } = [];

    public ObservableCollection<WorkflowStepViewModel> OpenTabs { get; } = [];

    public ObservableCollection<DiagnosticEntry> Diagnostics { get; } = [];

    public ObservableCollection<DiagnosticEntry> FilteredDiagnostics { get; } = [];

    /// <summary>
    /// Raised when the user triggers View -> Reset Layout so MainWindow can restore default splitter widths/heights.
    /// </summary>
    public event EventHandler? LayoutResetRequested;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WindowTitle))]
    public partial string ProjectName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ProjectDescription { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ProjectFileLabel))]
    public partial string? ProjectFilePath { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WindowTitle))]
    [NotifyPropertyChangedFor(nameof(UnsavedIndicator))]
    public partial bool HasUnsavedChanges { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DuplicateSelectedStepCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeleteSelectedStepCommand))]
    [NotifyCanExecuteChangedFor(nameof(MoveStepUpCommand))]
    [NotifyCanExecuteChangedFor(nameof(MoveStepDownCommand))]
    [NotifyCanExecuteChangedFor(nameof(RunSelectedStepCommand))]
    [NotifyCanExecuteChangedFor(nameof(CloseCurrentTabCommand))]
    public partial WorkflowStepViewModel? SelectedStep { get; set; }

    [ObservableProperty]
    public partial string StepSearchText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsLeftPaneVisible { get; set; } = true;

    [ObservableProperty]
    public partial bool IsDiagnosticsPaneVisible { get; set; } = true;

    [ObservableProperty]
    public partial bool IsToolbarVisible { get; set; } = true;

    [ObservableProperty]
    public partial bool IsStatusBarVisible { get; set; } = true;

    [ObservableProperty]
    public partial bool ShowInfoDiagnostics { get; set; } = true;

    [ObservableProperty]
    public partial bool ShowWarningDiagnostics { get; set; } = true;

    [ObservableProperty]
    public partial bool ShowErrorDiagnostics { get; set; } = true;

    [ObservableProperty]
    public partial string DiagnosticSearchText { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RunWorkflowCommand))]
    [NotifyCanExecuteChangedFor(nameof(RunSelectedStepCommand))]
    [NotifyCanExecuteChangedFor(nameof(StopWorkflowCommand))]
    [NotifyPropertyChangedFor(nameof(StatusIndicatorBrush))]
    public partial bool IsRunning { get; set; }

    [ObservableProperty]
    public partial double RunProgress { get; set; }

    [ObservableProperty]
    public partial string StatusMessage { get; set; } = "Ready";

    [ObservableProperty]
    public partial string MemoryUsageText { get; set; } = "Mem: -- MB";

    [ObservableProperty]
    public partial int InfoCount { get; set; }

    [ObservableProperty]
    public partial int WarningCount { get; set; }

    [ObservableProperty]
    public partial int ErrorCount { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StepsSummaryText))]
    public partial int EnabledStepCount { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StepsSummaryText))]
    public partial int TotalStepCount { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNoOpenTabs))]
    public partial bool HasOpenTabs { get; set; }

    public MainViewModel(ProjectStorageService storageService, WorkflowRunnerService runnerService)
    {
        _storageService = storageService;
        _runnerService = runnerService;

        OpenTabs.CollectionChanged += (_, _) =>
        {
            HasOpenTabs = OpenTabs.Count > 0;
            CloseAllTabsCommand.NotifyCanExecuteChanged();
        };

        LoadProject(_storageService.CreateStarterProject(), filePath: null, isStarterProject: true);
        RefreshMemoryMetric();
    }

    public string WindowTitle => $"{ProjectName}{(HasUnsavedChanges ? " *" : "")} — Workflow Studio (WinUI 3)";

    public string ProjectFileLabel => string.IsNullOrWhiteSpace(ProjectFilePath)
        ? "Unsaved Project"
        : Path.GetFileName(ProjectFilePath);

    public string UnsavedIndicator => HasUnsavedChanges ? "Modified" : "Saved";

    public bool HasNoOpenTabs => !HasOpenTabs;

    public string StepsSummaryText => $"Steps: {EnabledStepCount}/{TotalStepCount} enabled";

    public SolidColorBrush StatusIndicatorBrush
    {
        get
        {
            if (IsRunning)
            {
                return new SolidColorBrush(Color.FromArgb(255, 58, 150, 221));
            }

            if (ErrorCount > 0)
            {
                return new SolidColorBrush(Color.FromArgb(255, 232, 74, 74));
            }

            if (WarningCount > 0)
            {
                return new SolidColorBrush(Color.FromArgb(255, 224, 159, 36));
            }

            return new SolidColorBrush(Color.FromArgb(255, 62, 180, 96));
        }
    }

    public void LoadProject(WorkflowProject project, string? filePath, bool isStarterProject = false)
    {
        _suppressDirtyNotifications = true;
        try
        {
            ProjectName = project.Name;
            ProjectDescription = project.Description;
            ProjectFilePath = filePath;

            Steps.Clear();
            OpenTabs.Clear();
            Diagnostics.Clear();
            FilteredDiagnostics.Clear();

            foreach (var step in project.Steps)
            {
                Steps.Add(new WorkflowStepViewModel(step, OnProjectDataModified));
            }

            HasUnsavedChanges = false;
            RefreshFilteredSteps();
            RefreshStepCounts();
            RefreshDiagnosticsView();

            if (Steps.Count > 0)
            {
                OpenStepTab(Steps[0]);
                if (isStarterProject && Steps.Count > 1)
                {
                    OpenTabs.Add(Steps[1]);
                }
            }

            AddDiagnostic(new DiagnosticEntry
            {
                Severity = DiagnosticSeverity.Info,
                Source = "Workbench",
                Message = isStarterProject
                    ? "Loaded starter project. Try pressing F5 to run the workflow, dragging pane dividers, or saving via File → Save."
                    : filePath is not null
                        ? $"Opened project '{project.Name}' from {filePath}."
                        : $"Created new project '{project.Name}'."
            });

            StatusMessage = isStarterProject ? "Ready — Press F5 to run sample workflow" : $"Loaded '{project.Name}'";
        }
        finally
        {
            _suppressDirtyNotifications = false;
        }
    }

    public WorkflowProject BuildProjectModel() => new()
    {
        Name = ProjectName,
        Description = ProjectDescription,
        ModifiedAt = DateTimeOffset.Now,
        Steps = Steps.Select(s => s.ToModel()).ToList()
    };

    public void MarkSaved(string filePath)
    {
        ProjectFilePath = filePath;
        HasUnsavedChanges = false;
        StatusMessage = $"Saved to {Path.GetFileName(filePath)}";
        AddDiagnostic(new DiagnosticEntry
        {
            Severity = DiagnosticSeverity.Info,
            Source = "File",
            Message = $"Project saved to {filePath}."
        });
    }

    public void RefreshMemoryMetric()
    {
        using var process = Process.GetCurrentProcess();
        var mb = process.WorkingSet64 / (1024.0 * 1024.0);
        MemoryUsageText = $"Mem: {mb:F0} MB";
    }

    public void OpenStepTab(WorkflowStepViewModel? step)
    {
        if (step is null)
        {
            return;
        }

        if (!OpenTabs.Contains(step))
        {
            OpenTabs.Add(step);
        }

        SelectedStep = step;
    }

    public void SelectStepById(Guid? stepId)
    {
        if (stepId is null)
        {
            return;
        }

        var match = Steps.FirstOrDefault(s => s.Id == stepId.Value);
        if (match is not null)
        {
            OpenStepTab(match);
        }
    }

    public void AddDiagnostic(DiagnosticEntry entry)
    {
        Diagnostics.Add(entry);
        RefreshDiagnosticsView();
    }

    [RelayCommand]
    private void NewProject()
    {
        LoadProject(_storageService.CreateBlankProject(), filePath: null);
    }

    [RelayCommand]
    private void LoadStarterProject()
    {
        LoadProject(_storageService.CreateStarterProject(), filePath: null, isStarterProject: true);
    }

    [RelayCommand]
    private void AddStep(string? typeParameter)
    {
        var stepType = Enum.TryParse<WorkflowStepType>(typeParameter, ignoreCase: true, out var parsed)
            ? parsed
            : WorkflowStepType.Script;

        var defaultCommand = stepType switch
        {
            WorkflowStepType.Trigger => "on: workflow_dispatch",
            WorkflowStepType.Build => "dotnet build -c Release",
            WorkflowStepType.Test => "dotnet test --no-build",
            WorkflowStepType.Deploy => "dotnet publish -c Release -o ./out",
            WorkflowStepType.Notify => "notify --channel #builds --status $STATUS",
            _ => "pwsh -Command \"Write-Host 'Step executed'\""
        };

        var newStep = new WorkflowStepViewModel(new WorkflowStep
        {
            Name = $"{stepType} Step {Steps.Count + 1}",
            StepType = stepType,
            Description = $"Configured {stepType.ToString().ToLowerInvariant()} step.",
            Command = defaultCommand,
            TimeoutSeconds = 30,
            IsEnabled = true
        }, OnProjectDataModified);

        Steps.Add(newStep);
        OnProjectDataModified();
        RefreshFilteredSteps();
        OpenStepTab(newStep);

        StatusMessage = $"Added '{newStep.Name}'";
        AddDiagnostic(new DiagnosticEntry
        {
            Severity = DiagnosticSeverity.Info,
            Source = "Components",
            StepId = newStep.Id,
            Message = $"Added new {stepType} component: '{newStep.Name}'."
        });
    }

    [RelayCommand(CanExecute = nameof(HasSelectedStep))]
    private void DuplicateSelectedStep()
    {
        if (SelectedStep is null)
        {
            return;
        }

        var index = Steps.IndexOf(SelectedStep);
        var copy = new WorkflowStepViewModel(SelectedStep.CloneModel(), OnProjectDataModified);
        Steps.Insert(index >= 0 ? index + 1 : Steps.Count, copy);

        OnProjectDataModified();
        RefreshFilteredSteps();
        OpenStepTab(copy);

        StatusMessage = $"Duplicated '{copy.Name}'";
        AddDiagnostic(new DiagnosticEntry
        {
            Severity = DiagnosticSeverity.Info,
            Source = "Components",
            StepId = copy.Id,
            Message = $"Duplicated step as '{copy.Name}'."
        });
    }

    [RelayCommand(CanExecute = nameof(HasSelectedStep))]
    private void DeleteSelectedStep()
    {
        if (SelectedStep is null)
        {
            return;
        }

        var toRemove = SelectedStep;
        var index = Steps.IndexOf(toRemove);

        OpenTabs.Remove(toRemove);
        Steps.Remove(toRemove);

        SelectedStep = OpenTabs.LastOrDefault()
                       ?? (Steps.Count > 0 ? Steps[Math.Clamp(index - 1, 0, Steps.Count - 1)] : null);

        OnProjectDataModified();
        RefreshFilteredSteps();

        StatusMessage = $"Deleted '{toRemove.Name}'";
        AddDiagnostic(new DiagnosticEntry
        {
            Severity = DiagnosticSeverity.Info,
            Source = "Components",
            Message = $"Deleted component '{toRemove.Name}'."
        });
    }

    [RelayCommand(CanExecute = nameof(CanMoveStepUp))]
    private void MoveStepUp()
    {
        if (SelectedStep is null)
        {
            return;
        }

        var index = Steps.IndexOf(SelectedStep);
        if (index <= 0)
        {
            return;
        }

        var current = SelectedStep;
        Steps.Move(index, index - 1);
        OnProjectDataModified();
        RefreshFilteredSteps();
        SelectedStep = current;
        MoveStepUpCommand.NotifyCanExecuteChanged();
        MoveStepDownCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanMoveStepDown))]
    private void MoveStepDown()
    {
        if (SelectedStep is null)
        {
            return;
        }

        var index = Steps.IndexOf(SelectedStep);
        if (index < 0 || index >= Steps.Count - 1)
        {
            return;
        }

        var current = SelectedStep;
        Steps.Move(index, index + 1);
        OnProjectDataModified();
        RefreshFilteredSteps();
        SelectedStep = current;
        MoveStepUpCommand.NotifyCanExecuteChanged();
        MoveStepDownCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private void ValidateWorkflow()
    {
        IsDiagnosticsPaneVisible = true;
        var results = _runnerService.Validate(Steps);
        foreach (var entry in results)
        {
            Diagnostics.Add(entry);
        }

        RefreshDiagnosticsView();
        var errors = results.Count(r => r.Severity == DiagnosticSeverity.Error);
        var warnings = results.Count(r => r.Severity == DiagnosticSeverity.Warning);
        StatusMessage = errors > 0
            ? $"Validation found {errors} error(s), {warnings} warning(s)"
            : warnings > 0
                ? $"Validation passed with {warnings} warning(s)"
                : "Validation passed with no issues";
    }

    [RelayCommand(CanExecute = nameof(CanRunWorkflow))]
    private async Task RunWorkflowAsync()
    {
        await ExecuteStepsAsync(Steps.ToList(), "workflow");
    }

    [RelayCommand(CanExecute = nameof(CanRunSelectedStep))]
    private async Task RunSelectedStepAsync()
    {
        if (SelectedStep is null)
        {
            return;
        }

        await ExecuteStepsAsync(new[] { SelectedStep }, $"step '{SelectedStep.Name}'");
    }

    [RelayCommand(CanExecute = nameof(IsRunning))]
    private void StopWorkflow()
    {
        _runCts?.Cancel();
        StatusMessage = "Cancelling run...";
    }

    [RelayCommand]
    private void ClearDiagnostics()
    {
        Diagnostics.Clear();
        RefreshDiagnosticsView();
        StatusMessage = "Diagnostics cleared";
    }

    [RelayCommand]
    private void CloseTab(WorkflowStepViewModel? step)
    {
        if (step is null)
        {
            return;
        }

        var index = OpenTabs.IndexOf(step);
        if (index < 0)
        {
            return;
        }

        OpenTabs.RemoveAt(index);

        if (ReferenceEquals(SelectedStep, step))
        {
            SelectedStep = OpenTabs.Count > 0
                ? OpenTabs[Math.Clamp(index - 1, 0, OpenTabs.Count - 1)]
                : null;
        }
    }

    [RelayCommand(CanExecute = nameof(HasSelectedStep))]
    private void CloseCurrentTab()
    {
        CloseTab(SelectedStep);
    }

    [RelayCommand(CanExecute = nameof(CanCloseAllTabs))]
    private void CloseAllTabs()
    {
        OpenTabs.Clear();
        SelectedStep = null;
    }

    [RelayCommand]
    private void ResetLayout()
    {
        IsLeftPaneVisible = true;
        IsDiagnosticsPaneVisible = true;
        IsToolbarVisible = true;
        IsStatusBarVisible = true;
        LayoutResetRequested?.Invoke(this, EventArgs.Empty);
        StatusMessage = "Layout reset to defaults";
    }

    private async Task ExecuteStepsAsync(IReadOnlyList<WorkflowStepViewModel> stepsToRun, string label)
    {
        if (IsRunning || stepsToRun.Count == 0)
        {
            return;
        }

        IsDiagnosticsPaneVisible = true;
        IsRunning = true;
        RunProgress = 0;
        StatusMessage = $"Running {label}...";
        _runCts = new CancellationTokenSource();

        try
        {
            var result = await _runnerService.RunAsync(
                stepsToRun,
                AddDiagnostic,
                (progress, status) =>
                {
                    RunProgress = progress;
                    StatusMessage = status;
                },
                _runCts.Token);

            RunProgress = result.WasCancelled ? 0 : 100;
            StatusMessage = result.WasCancelled
                ? "Run cancelled"
                : result.FailedCount > 0
                    ? $"Completed with {result.FailedCount} failure(s) in {result.Elapsed.TotalSeconds:F1}s"
                    : result.WarningCount > 0
                        ? $"Completed with {result.WarningCount} warning(s) in {result.Elapsed.TotalSeconds:F1}s"
                        : $"Succeeded ({result.SucceededCount} step(s)) in {result.Elapsed.TotalSeconds:F1}s";
        }
        finally
        {
            _runCts?.Dispose();
            _runCts = null;
            IsRunning = false;
            RefreshMemoryMetric();
        }
    }

    private bool HasSelectedStep() => SelectedStep is not null;

    private bool CanMoveStepUp() => SelectedStep is not null && Steps.IndexOf(SelectedStep) > 0;

    private bool CanMoveStepDown()
    {
        if (SelectedStep is null)
        {
            return false;
        }

        var idx = Steps.IndexOf(SelectedStep);
        return idx >= 0 && idx < Steps.Count - 1;
    }

    private bool CanRunWorkflow() => !IsRunning && Steps.Count > 0;

    private bool CanRunSelectedStep() => !IsRunning && SelectedStep is not null;

    private bool CanCloseAllTabs() => OpenTabs.Count > 0;

    private void OnProjectDataModified()
    {
        if (_suppressDirtyNotifications)
        {
            return;
        }

        HasUnsavedChanges = true;
        RefreshStepCounts();
    }

    private void RefreshStepCounts()
    {
        TotalStepCount = Steps.Count;
        EnabledStepCount = Steps.Count(s => s.IsEnabled);
        RunWorkflowCommand.NotifyCanExecuteChanged();
    }

    private void RefreshFilteredSteps()
    {
        var query = StepSearchText.Trim();
        FilteredSteps.Clear();

        foreach (var step in Steps)
        {
            if (string.IsNullOrEmpty(query) ||
                step.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                step.TypeLabel.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                step.Command.Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                FilteredSteps.Add(step);
            }
        }
    }

    private void RefreshDiagnosticsView()
    {
        var query = DiagnosticSearchText.Trim();

        InfoCount = Diagnostics.Count(d => d.Severity == DiagnosticSeverity.Info);
        WarningCount = Diagnostics.Count(d => d.Severity == DiagnosticSeverity.Warning);
        ErrorCount = Diagnostics.Count(d => d.Severity == DiagnosticSeverity.Error);
        OnPropertyChanged(nameof(StatusIndicatorBrush));

        FilteredDiagnostics.Clear();
        foreach (var entry in Diagnostics)
        {
            var matchesSeverity = entry.Severity switch
            {
                DiagnosticSeverity.Info => ShowInfoDiagnostics,
                DiagnosticSeverity.Warning => ShowWarningDiagnostics,
                DiagnosticSeverity.Error => ShowErrorDiagnostics,
                _ => true
            };

            var matchesQuery = string.IsNullOrEmpty(query) ||
                               entry.Message.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                               entry.Source.Contains(query, StringComparison.OrdinalIgnoreCase);

            if (matchesSeverity && matchesQuery)
            {
                FilteredDiagnostics.Add(entry);
            }
        }
    }

    partial void OnProjectNameChanged(string value) => OnProjectDataModified();
    partial void OnProjectDescriptionChanged(string value) => OnProjectDataModified();

    partial void OnStepSearchTextChanged(string value) => RefreshFilteredSteps();

    partial void OnShowInfoDiagnosticsChanged(bool value) => RefreshDiagnosticsView();
    partial void OnShowWarningDiagnosticsChanged(bool value) => RefreshDiagnosticsView();
    partial void OnShowErrorDiagnosticsChanged(bool value) => RefreshDiagnosticsView();
    partial void OnDiagnosticSearchTextChanged(string value) => RefreshDiagnosticsView();

    partial void OnSelectedStepChanged(WorkflowStepViewModel? value)
    {
        if (value is not null && !OpenTabs.Contains(value))
        {
            OpenTabs.Add(value);
        }
    }
}
