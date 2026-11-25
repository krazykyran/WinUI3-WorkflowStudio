using Microsoft.UI.Xaml;
using WorkflowStudio.Views;

namespace WorkflowStudio;

/// <summary>
/// Application entry point. Tracks active windows so Window -> New Window works cleanly.
/// </summary>
public partial class App : Application
{
    private static readonly List<MainWindow> OpenWindows = [];

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        CreateAndActivateWindow();
    }

    public static MainWindow CreateAndActivateWindow()
    {
        var window = new MainWindow();
        OpenWindows.Add(window);
        window.Closed += (_, _) => OpenWindows.Remove(window);
        window.Activate();
        return window;
    }
}
