using Microsoft.UI;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace WorkflowStudio.Models;

public enum DiagnosticSeverity
{
    Info,
    Warning,
    Error
}

/// <summary>
/// A single row displayed in the bottom Diagnostics pane.
/// </summary>
public sealed class DiagnosticEntry
{
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.Now;

    public DiagnosticSeverity Severity { get; init; } = DiagnosticSeverity.Info;

    public string Source { get; init; } = "Workbench";

    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// Optional reference to the workflow step that produced this diagnostic.
    /// Clicking a diagnostic row jumps straight to that step's tab.
    /// </summary>
    public Guid? StepId { get; init; }

    public string TimeText => Timestamp.ToString("HH:mm:ss.ff");

    public string SeverityLabel => Severity switch
    {
        DiagnosticSeverity.Error => "ERROR",
        DiagnosticSeverity.Warning => "WARN",
        _ => "INFO"
    };

    /// <summary>
    /// Segoe Fluent Icons glyph matching Windows 11 system iconography.
    /// </summary>
    public string IconGlyph => Severity switch
    {
        DiagnosticSeverity.Error => "\uEA39",   // ErrorBadge
        DiagnosticSeverity.Warning => "\uE7BA", // Warning
        _ => "\uE946"                           // Info
    };

    public SolidColorBrush SeverityBrush => Severity switch
    {
        DiagnosticSeverity.Error => new SolidColorBrush(Color.FromArgb(255, 232, 74, 74)),
        DiagnosticSeverity.Warning => new SolidColorBrush(Color.FromArgb(255, 224, 159, 36)),
        _ => new SolidColorBrush(Color.FromArgb(255, 58, 150, 221))
    };
}
