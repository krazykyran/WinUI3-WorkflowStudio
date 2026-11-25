using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.UI.Xaml;
using Windows.Storage.Pickers;
using WinRT.Interop;
using WorkflowStudio.Models;

namespace WorkflowStudio.Services;

/// <summary>
/// Compile-time System.Text.Json source-generation context.
/// Generates reflection-free serialization code at build time so saving/loading JSON
/// is fast and fully NativeAOT-compatible.
/// </summary>
[JsonSourceGenerationOptions(WriteIndented = true, UseStringEnumConverter = true)]
[JsonSerializable(typeof(WorkflowProject))]
internal partial class WorkflowJsonContext : JsonSerializerContext
{
}

/// <summary>
/// Handles creating starter projects and loading/saving JSON workflow files
/// using native Windows 11 FileOpenPicker / FileSavePicker dialogs.
/// </summary>
public sealed class ProjectStorageService
{
    /// <summary>
    /// Builds a rich starter project so the app is immediately interactive on first launch.
    /// </summary>
    public WorkflowProject CreateStarterProject()
    {
        return new WorkflowProject
        {
            Name = "Windows App Release Pipeline",
            Description = "Demonstrates a multi-stage workflow with build, test, packaging, and notification steps.",
            Steps =
            [
                new WorkflowStep
                {
                    Name = "Git Push Trigger",
                    StepType = WorkflowStepType.Trigger,
                    Description = "Starts the pipeline when commits arrive on the main branch.",
                    Command = "on: push branches: [ main ]",
                    WorkingDirectory = "$(RepoRoot)",
                    TimeoutSeconds = 10,
                    IsEnabled = true
                },
                new WorkflowStep
                {
                    Name = "Restore & Build WinUI App",
                    StepType = WorkflowStepType.Build,
                    Description = "Restores NuGet dependencies and compiles the Windows App SDK project.",
                    Command = "dotnet build WorkflowStudio.csproj -c Release -p:Platform=ARM64",
                    WorkingDirectory = "$(ProjectDir)",
                    TimeoutSeconds = 120,
                    IsEnabled = true
                },
                new WorkflowStep
                {
                    Name = "Run Unit & UI Tests",
                    StepType = WorkflowStepType.Test,
                    Description = "Executes automated test suites and verifies coverage thresholds.",
                    Command = "dotnet test --configuration Release --collect:\"XPlat Code Coverage\"",
                    WorkingDirectory = "$(ProjectDir)",
                    TimeoutSeconds = 90,
                    IsEnabled = true,
                    SimulateWarning = true
                },
                new WorkflowStep
                {
                    Name = "Generate Release Notes",
                    StepType = WorkflowStepType.Script,
                    Description = "Collects commit summaries since the last tag into CHANGELOG.md.",
                    Command = "pwsh ./scripts/generate-notes.ps1 -SinceTag latest",
                    WorkingDirectory = "$(RepoRoot)",
                    TimeoutSeconds = 30,
                    IsEnabled = true
                },
                new WorkflowStep
                {
                    Name = "Publish Artifacts",
                    StepType = WorkflowStepType.Deploy,
                    Description = "Uploads signed binaries to the release channel.",
                    Command = "dotnet publish -c Release -o ./artifacts/release",
                    WorkingDirectory = "$(ProjectDir)",
                    TimeoutSeconds = 180,
                    IsEnabled = true
                },
                new WorkflowStep
                {
                    Name = "Notify Release Channel",
                    StepType = WorkflowStepType.Notify,
                    Description = "Posts a deployment summary card to the team channel.",
                    Command = "notify --channel #releases --status $PIPELINE_STATUS",
                    WorkingDirectory = "$(RepoRoot)",
                    TimeoutSeconds = 15,
                    IsEnabled = false
                }
            ]
        };
    }

    public WorkflowProject CreateBlankProject()
    {
        return new WorkflowProject
        {
            Name = "Untitled Workflow",
            Description = "New workflow project.",
            Steps =
            [
                new WorkflowStep
                {
                    Name = "Initial Step",
                    StepType = WorkflowStepType.Script,
                    Description = "Configure this step in the workspace or add more steps from the left pane.",
                    Command = "echo \"Hello from WinUI 3!\"",
                    TimeoutSeconds = 30,
                    IsEnabled = true
                }
            ]
        };
    }

    public async Task<(WorkflowProject? Project, string? FilePath)> PickAndOpenAsync(Window ownerWindow)
    {
        // In unpackaged WinUI 3 apps, WinRT pickers must be initialized with the owner Window's HWND.
        var picker = new FileOpenPicker
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
            ViewMode = PickerViewMode.List
        };
        picker.FileTypeFilter.Add(".wfproj");
        picker.FileTypeFilter.Add(".json");

        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(ownerWindow));

        var file = await picker.PickSingleFileAsync();
        if (file is null)
        {
            return (null, null);
        }

        var json = await File.ReadAllTextAsync(file.Path);
        var project = JsonSerializer.Deserialize(json, WorkflowJsonContext.Default.WorkflowProject)
                      ?? throw new InvalidDataException("The selected file did not contain a valid workflow project.");

        return (project, file.Path);
    }

    public async Task<string?> PickSavePathAsync(Window ownerWindow, string suggestedName)
    {
        var picker = new FileSavePicker
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
            SuggestedFileName = SanitizeFileName(suggestedName)
        };
        picker.FileTypeChoices.Add("Workflow Project (*.wfproj)", [".wfproj"]);
        picker.FileTypeChoices.Add("JSON File (*.json)", [".json"]);

        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(ownerWindow));

        var file = await picker.PickSaveFileAsync();
        return file?.Path;
    }

    public async Task SaveToPathAsync(WorkflowProject project, string filePath)
    {
        project.ModifiedAt = DateTimeOffset.Now;
        var json = JsonSerializer.Serialize(project, WorkflowJsonContext.Default.WorkflowProject);
        await File.WriteAllTextAsync(filePath, json);
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray()).Trim();
        return string.IsNullOrWhiteSpace(cleaned) ? "workflow" : cleaned;
    }
}
