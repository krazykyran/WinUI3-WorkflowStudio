using System.Diagnostics;
using WorkflowStudio.Models;
using WorkflowStudio.ViewModels;

namespace WorkflowStudio.Services;

public sealed record WorkflowRunResult(
    int ExecutedCount,
    int SucceededCount,
    int WarningCount,
    int FailedCount,
    int SkippedCount,
    bool WasCancelled,
    TimeSpan Elapsed);

/// <summary>
/// Simulates validating and executing workflow steps asynchronously, emitting live
/// status updates and structured diagnostics for the bottom pane.
/// </summary>
public sealed class WorkflowRunnerService
{
    public IReadOnlyList<DiagnosticEntry> Validate(IReadOnlyList<WorkflowStepViewModel> steps)
    {
        var results = new List<DiagnosticEntry>();

        if (steps.Count == 0)
        {
            results.Add(new DiagnosticEntry
            {
                Severity = DiagnosticSeverity.Warning,
                Source = "Validator",
                Message = "Workflow contains no steps. Add a step from the Left Pane or Toolbar."
            });
            return results;
        }

        if (steps.All(s => !s.IsEnabled))
        {
            results.Add(new DiagnosticEntry
            {
                Severity = DiagnosticSeverity.Warning,
                Source = "Validator",
                Message = "All workflow steps are currently disabled."
            });
        }

        var duplicateNames = steps
            .GroupBy(s => s.Name.Trim(), StringComparer.OrdinalIgnoreCase)
            .Where(g => !string.IsNullOrWhiteSpace(g.Key) && g.Count() > 1);

        foreach (var group in duplicateNames)
        {
            results.Add(new DiagnosticEntry
            {
                Severity = DiagnosticSeverity.Warning,
                Source = "Validator",
                StepId = group.First().Id,
                Message = $"Duplicate step name '{group.Key}' found ({group.Count()} occurrences)."
            });
        }

        foreach (var step in steps)
        {
            if (string.IsNullOrWhiteSpace(step.Name))
            {
                results.Add(new DiagnosticEntry
                {
                    Severity = DiagnosticSeverity.Error,
                    Source = "Validator",
                    StepId = step.Id,
                    Message = "Step is missing a name."
                });
            }

            if (step.IsEnabled && string.IsNullOrWhiteSpace(step.Command))
            {
                results.Add(new DiagnosticEntry
                {
                    Severity = DiagnosticSeverity.Error,
                    Source = step.Name,
                    StepId = step.Id,
                    Message = $"Enabled step '{step.Name}' has an empty command."
                });
            }

            if (step.TimeoutSeconds <= 0)
            {
                results.Add(new DiagnosticEntry
                {
                    Severity = DiagnosticSeverity.Error,
                    Source = step.Name,
                    StepId = step.Id,
                    Message = $"Step '{step.Name}' has an invalid timeout ({step.TimeoutSeconds}s). Timeout must be > 0."
                });
            }

            if (step.SimulateFailure && !step.ContinueOnError)
            {
                results.Add(new DiagnosticEntry
                {
                    Severity = DiagnosticSeverity.Info,
                    Source = step.Name,
                    StepId = step.Id,
                    Message = $"Step '{step.Name}' is configured to simulate a failure and halt subsequent steps."
                });
            }
        }

        if (results.All(r => r.Severity == DiagnosticSeverity.Info))
        {
            results.Add(new DiagnosticEntry
            {
                Severity = DiagnosticSeverity.Info,
                Source = "Validator",
                Message = $"Validation passed for {steps.Count} step(s) with 0 errors and 0 warnings."
            });
        }

        return results;
    }

    public async Task<WorkflowRunResult> RunAsync(
        IReadOnlyList<WorkflowStepViewModel> steps,
        Action<DiagnosticEntry> emitDiagnostic,
        Action<double, string> reportProgress,
        CancellationToken cancellationToken)
    {
        var totalWatch = Stopwatch.StartNew();
        int executed = 0, succeeded = 0, warnings = 0, failed = 0, skipped = 0;
        var halted = false;
        var cancelled = false;

        foreach (var step in steps)
        {
            step.Status = step.IsEnabled ? WorkflowStepStatus.Queued : WorkflowStepStatus.Skipped;
            step.LastOutput = string.Empty;
            step.LastRunDurationMs = 0;
        }

        emitDiagnostic(new DiagnosticEntry
        {
            Severity = DiagnosticSeverity.Info,
            Source = "Runner",
            Message = $"Starting workflow run ({steps.Count(s => s.IsEnabled)} of {steps.Count} steps enabled)..."
        });

        for (var i = 0; i < steps.Count; i++)
        {
            var step = steps[i];
            var baseProgress = (double)i / Math.Max(1, steps.Count) * 100.0;

            if (cancellationToken.IsCancellationRequested)
            {
                cancelled = true;
                step.Status = WorkflowStepStatus.Skipped;
                skipped++;
                continue;
            }

            if (!step.IsEnabled || halted)
            {
                step.Status = WorkflowStepStatus.Skipped;
                step.LastOutput = halted
                    ? "Skipped because a previous step failed."
                    : "Skipped because step is disabled.";
                skipped++;
                emitDiagnostic(new DiagnosticEntry
                {
                    Severity = DiagnosticSeverity.Info,
                    Source = step.Name,
                    StepId = step.Id,
                    Message = step.LastOutput
                });
                continue;
            }

            if (string.IsNullOrWhiteSpace(step.Command))
            {
                executed++;
                failed++;
                step.Status = WorkflowStepStatus.Failed;
                step.LastOutput = "ERROR: Command cannot be empty.";
                emitDiagnostic(new DiagnosticEntry
                {
                    Severity = DiagnosticSeverity.Error,
                    Source = step.Name,
                    StepId = step.Id,
                    Message = "Execution failed: Command cannot be empty."
                });

                if (!step.ContinueOnError)
                {
                    halted = true;
                }
                continue;
            }

            step.Status = WorkflowStepStatus.Running;
            reportProgress(baseProgress + (50.0 / Math.Max(1, steps.Count)), $"Running [{i + 1}/{steps.Count}]: {step.Name}");
            emitDiagnostic(new DiagnosticEntry
            {
                Severity = DiagnosticSeverity.Info,
                Source = step.Name,
                StepId = step.Id,
                Message = $"Executing ({step.StepType}): {step.Command}"
            });

            var stepWatch = Stopwatch.StartNew();
            try
            {
                // Simulate realistic step execution work while staying responsive to cancellation.
                var delayMs = step.StepType switch
                {
                    WorkflowStepType.Trigger => 350,
                    WorkflowStepType.Build => 950,
                    WorkflowStepType.Test => 850,
                    WorkflowStepType.Deploy => 900,
                    _ => 550
                };
                await Task.Delay(delayMs, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                cancelled = true;
                step.Status = WorkflowStepStatus.Skipped;
                step.LastOutput = "Cancelled by user.";
                skipped++;
                emitDiagnostic(new DiagnosticEntry
                {
                    Severity = DiagnosticSeverity.Warning,
                    Source = "Runner",
                    StepId = step.Id,
                    Message = $"Run cancelled during '{step.Name}'."
                });
                continue;
            }

            stepWatch.Stop();
            step.LastRunDurationMs = (int)stepWatch.ElapsedMilliseconds;
            executed++;

            if (step.SimulateFailure)
            {
                failed++;
                step.Status = WorkflowStepStatus.Failed;
                step.LastOutput =
                    $"> {step.Command}\r\n" +
                    $"Working Dir: {step.WorkingDirectory}\r\n" +
                    $"Exit Code: 1 (simulated failure after {step.LastRunDurationMs} ms)";

                emitDiagnostic(new DiagnosticEntry
                {
                    Severity = DiagnosticSeverity.Error,
                    Source = step.Name,
                    StepId = step.Id,
                    Message = $"Step '{step.Name}' exited with code 1 ({step.LastRunDurationMs} ms)." +
                              (step.ContinueOnError ? " Continuing because ContinueOnError is enabled." : " Halting pipeline.")
                });

                if (!step.ContinueOnError)
                {
                    halted = true;
                }
            }
            else if (step.SimulateWarning)
            {
                warnings++;
                succeeded++;
                step.Status = WorkflowStepStatus.Warning;
                step.LastOutput =
                    $"> {step.Command}\r\n" +
                    $"Working Dir: {step.WorkingDirectory}\r\n" +
                    $"WARN: 2 deprecated API calls detected during execution.\r\n" +
                    $"Exit Code: 0 (completed with warnings in {step.LastRunDurationMs} ms)";

                emitDiagnostic(new DiagnosticEntry
                {
                    Severity = DiagnosticSeverity.Warning,
                    Source = step.Name,
                    StepId = step.Id,
                    Message = $"Step '{step.Name}' completed with warnings ({step.LastRunDurationMs} ms)."
                });
            }
            else
            {
                succeeded++;
                step.Status = WorkflowStepStatus.Succeeded;
                step.LastOutput =
                    $"> {step.Command}\r\n" +
                    $"Working Dir: {step.WorkingDirectory}\r\n" +
                    $"Exit Code: 0 (completed cleanly in {step.LastRunDurationMs} ms)";

                emitDiagnostic(new DiagnosticEntry
                {
                    Severity = DiagnosticSeverity.Info,
                    Source = step.Name,
                    StepId = step.Id,
                    Message = $"Step '{step.Name}' succeeded in {step.LastRunDurationMs} ms."
                });
            }

            reportProgress((double)(i + 1) / Math.Max(1, steps.Count) * 100.0, $"Finished: {step.Name}");
        }

        totalWatch.Stop();

        var summarySeverity = failed > 0
            ? DiagnosticSeverity.Error
            : (warnings > 0 || cancelled) ? DiagnosticSeverity.Warning : DiagnosticSeverity.Info;

        emitDiagnostic(new DiagnosticEntry
        {
            Severity = summarySeverity,
            Source = "Runner",
            Message = cancelled
                ? $"Workflow cancelled after {totalWatch.Elapsed.TotalSeconds:F2}s ({succeeded} passed, {failed} failed, {skipped} skipped)."
                : $"Workflow finished in {totalWatch.Elapsed.TotalSeconds:F2}s — {succeeded} passed, {warnings} warning(s), {failed} failed, {skipped} skipped."
        });

        return new WorkflowRunResult(executed, succeeded, warnings, failed, skipped, cancelled, totalWatch.Elapsed);
    }
}
