using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Graphics;
using WorkflowStudio.Models;
using WorkflowStudio.Services;
using WorkflowStudio.ViewModels;

namespace WorkflowStudio.Views;

/// <summary>
/// Main workbench window. Keeps code-behind focused strictly on View concerns:
/// Window chrome (AppWindow), splitter sizing, native file pickers, and ContentDialogs.
/// Business state lives in <see cref="MainViewModel"/>.
/// </summary>
public sealed partial class MainWindow : Window
{
    private const double DefaultLeftPaneWidth = 280;
    private const double MinLeftPaneWidth = 190;
    private const double MaxLeftPaneWidth = 520;

    private const double DefaultDiagnosticsHeight = 210;
    private const double MinDiagnosticsHeight = 110;
    private const double MaxDiagnosticsHeight = 460;

    private readonly ProjectStorageService _storageService = new();
    private readonly DispatcherTimer _metricsTimer;
    private double _savedLeftPaneWidth = DefaultLeftPaneWidth;
    private double _savedDiagnosticsHeight = DefaultDiagnosticsHeight;

    public MainViewModel ViewModel { get; }

    /// <summary>
    /// Compiled x:Bind function helper for bool -> Visibility (idiomatic WinUI 3 alternative to IValueConverter).
    /// </summary>
    public Visibility BoolToVis(bool value) => value ? Visibility.Visible : Visibility.Collapsed;

    public MainWindow()
    {
        ViewModel = new MainViewModel(_storageService, new WorkflowRunnerService());

        InitializeComponent();

        // Set a comfortable initial desktop window size via WinUI 3's AppWindow API.
        AppWindow.Resize(new SizeInt32(1320, 840));
        Title = ViewModel.WindowTitle;

        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
        ViewModel.LayoutResetRequested += (_, _) => ResetPaneSizes();

        // Periodically refresh Status Bar metrics (memory footprint) on the UI thread.
        _metricsTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _metricsTimer.Tick += (_, _) => ViewModel.RefreshMemoryMetric();
        _metricsTimer.Start();

        Closed += (_, _) => _metricsTimer.Stop();
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(MainViewModel.WindowTitle):
                Title = ViewModel.WindowTitle;
                break;

            case nameof(MainViewModel.IsLeftPaneVisible):
                if (ViewModel.IsLeftPaneVisible)
                {
                    LeftPaneColumn.Width = new GridLength(_savedLeftPaneWidth);
                }
                else
                {
                    if (LeftPaneColumn.Width.Value > 0)
                    {
                        _savedLeftPaneWidth = LeftPaneColumn.Width.Value;
                    }
                    LeftPaneColumn.Width = new GridLength(0);
                }
                break;

            case nameof(MainViewModel.IsDiagnosticsPaneVisible):
                if (ViewModel.IsDiagnosticsPaneVisible)
                {
                    DiagnosticsRow.Height = new GridLength(_savedDiagnosticsHeight);
                }
                else
                {
                    if (DiagnosticsRow.Height.Value > 0)
                    {
                        _savedDiagnosticsHeight = DiagnosticsRow.Height.Value;
                    }
                    DiagnosticsRow.Height = new GridLength(0);
                }
                break;
        }
    }

    private void ResetPaneSizes()
    {
        _savedLeftPaneWidth = DefaultLeftPaneWidth;
        _savedDiagnosticsHeight = DefaultDiagnosticsHeight;
        LeftPaneColumn.Width = new GridLength(DefaultLeftPaneWidth);
        DiagnosticsRow.Height = new GridLength(DefaultDiagnosticsHeight);
    }

    // ------------------------------------------------------------------------
    // Resizable Pane Splitters
    // ------------------------------------------------------------------------

    private void OnLeftSplitterDragDelta(object? sender, double delta)
    {
        var currentWidth = LeftPaneColumn.ActualWidth > 0 ? LeftPaneColumn.ActualWidth : _savedLeftPaneWidth;
        var nextWidth = Math.Clamp(currentWidth + delta, MinLeftPaneWidth, MaxLeftPaneWidth);
        _savedLeftPaneWidth = nextWidth;
        LeftPaneColumn.Width = new GridLength(nextWidth);
    }

    private void OnBottomSplitterDragDelta(object? sender, double delta)
    {
        // Dragging UP (negative delta) grows the bottom pane; dragging DOWN shrinks it.
        var currentHeight = DiagnosticsRow.ActualHeight > 0 ? DiagnosticsRow.ActualHeight : _savedDiagnosticsHeight;
        var nextHeight = Math.Clamp(currentHeight - delta, MinDiagnosticsHeight, MaxDiagnosticsHeight);
        _savedDiagnosticsHeight = nextHeight;
        DiagnosticsRow.Height = new GridLength(nextHeight);
    }

    // ------------------------------------------------------------------------
    // Selection & Tab Synchronization
    // ------------------------------------------------------------------------

    private void OnStepsListSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.FirstOrDefault() is WorkflowStepViewModel step &&
            !ReferenceEquals(ViewModel.SelectedStep, step))
        {
            ViewModel.OpenStepTab(step);
        }
    }

    private void OnWorkspaceTabSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.FirstOrDefault() is WorkflowStepViewModel step &&
            !ReferenceEquals(ViewModel.SelectedStep, step))
        {
            ViewModel.SelectedStep = step;
        }
    }

    private void OnAddTabButtonClick(TabView sender, object args)
    {
        ViewModel.AddStepCommand.Execute("Script");
    }

    private void OnWorkspaceTabCloseRequested(TabView sender, TabViewTabCloseRequestedEventArgs args)
    {
        if (args.Item is WorkflowStepViewModel step)
        {
            ViewModel.CloseTabCommand.Execute(step);
        }
    }

    private void OnDiagnosticItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is DiagnosticEntry entry && entry.StepId is not null)
        {
            ViewModel.SelectStepById(entry.StepId);
        }
    }

    // ------------------------------------------------------------------------
    // Context Menu & Inline Step Actions
    // ------------------------------------------------------------------------

    private async void OnRunThisStepClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: WorkflowStepViewModel step })
        {
            ViewModel.SelectedStep = step;
            await ViewModel.RunSelectedStepCommand.ExecuteAsync(null);
        }
    }

    private async void OnContextRunStepClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: WorkflowStepViewModel step })
        {
            ViewModel.SelectedStep = step;
            await ViewModel.RunSelectedStepCommand.ExecuteAsync(null);
        }
    }

    private void OnContextDuplicateStepClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: WorkflowStepViewModel step })
        {
            ViewModel.SelectedStep = step;
            ViewModel.DuplicateSelectedStepCommand.Execute(null);
        }
    }

    private void OnContextDeleteStepClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: WorkflowStepViewModel step })
        {
            ViewModel.SelectedStep = step;
            ViewModel.DeleteSelectedStepCommand.Execute(null);
        }
    }

    // ------------------------------------------------------------------------
    // File Menu (Native Windows Pickers + JSON Persistence)
    // ------------------------------------------------------------------------

    private async void OnOpenProjectClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var (project, filePath) = await _storageService.PickAndOpenAsync(this);
            if (project is not null)
            {
                ViewModel.LoadProject(project, filePath);
            }
        }
        catch (Exception ex)
        {
            ViewModel.AddDiagnostic(new DiagnosticEntry
            {
                Severity = DiagnosticSeverity.Error,
                Source = "File",
                Message = $"Failed to open project: {ex.Message}"
            });
            await ShowMessageDialogAsync("Unable to Open Project", ex.Message);
        }
    }

    private async void OnSaveProjectClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(ViewModel.ProjectFilePath))
        {
            await SaveProjectAsInternalAsync();
            return;
        }

        await SaveToPathInternalAsync(ViewModel.ProjectFilePath);
    }

    private async void OnSaveProjectAsClick(object sender, RoutedEventArgs e)
    {
        await SaveProjectAsInternalAsync();
    }

    private async Task SaveProjectAsInternalAsync()
    {
        var path = await _storageService.PickSavePathAsync(this, ViewModel.ProjectName);
        if (!string.IsNullOrWhiteSpace(path))
        {
            await SaveToPathInternalAsync(path);
        }
    }

    private async Task SaveToPathInternalAsync(string path)
    {
        try
        {
            var model = ViewModel.BuildProjectModel();
            await _storageService.SaveToPathAsync(model, path);
            ViewModel.MarkSaved(path);
        }
        catch (Exception ex)
        {
            ViewModel.AddDiagnostic(new DiagnosticEntry
            {
                Severity = DiagnosticSeverity.Error,
                Source = "File",
                Message = $"Failed to save project: {ex.Message}"
            });
            await ShowMessageDialogAsync("Unable to Save Project", ex.Message);
        }
    }

    private void OnExitClick(object sender, RoutedEventArgs e) => Close();

    // ------------------------------------------------------------------------
    // View & Window Menu Actions
    // ------------------------------------------------------------------------

    private void OnHideLeftPaneClick(object sender, RoutedEventArgs e) =>
        ViewModel.IsLeftPaneVisible = false;

    private void OnHideDiagnosticsPaneClick(object sender, RoutedEventArgs e) =>
        ViewModel.IsDiagnosticsPaneVisible = false;

    private void OnShowDiagnosticsClick(object sender, RoutedEventArgs e) =>
        ViewModel.IsDiagnosticsPaneVisible = true;

    private void OnThemeRadioClick(object sender, RoutedEventArgs e)
    {
        if (sender is RadioMenuFlyoutItem item && item.Tag is string themeName)
        {
            RootGrid.RequestedTheme = themeName switch
            {
                "Light" => ElementTheme.Light,
                "Dark" => ElementTheme.Dark,
                _ => ElementTheme.Default
            };
        }
    }

    private void OnNewWindowClick(object sender, RoutedEventArgs e)
    {
        App.CreateAndActivateWindow();
    }

    private void OnAlwaysOnTopClick(object sender, RoutedEventArgs e)
    {
        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsAlwaysOnTop = AlwaysOnTopMenuItem.IsChecked;
            ViewModel.StatusMessage = presenter.IsAlwaysOnTop
                ? "Window pinned on top"
                : "Window unpinned";
        }
    }

    // ------------------------------------------------------------------------
    // Edit -> Project Properties & Help Dialogs (WinUI 3 ContentDialog)
    // ------------------------------------------------------------------------

    private async void OnProjectPropertiesClick(object sender, RoutedEventArgs e)
    {
        var nameBox = new TextBox
        {
            Header = "Project Name",
            Text = ViewModel.ProjectName
        };
        var descBox = new TextBox
        {
            Header = "Description",
            Text = ViewModel.ProjectDescription,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            MinHeight = 70
        };

        var panel = new StackPanel { Spacing = 12, MinWidth = 380 };
        panel.Children.Add(nameBox);
        panel.Children.Add(descBox);

        var dialog = new ContentDialog
        {
            Title = "Project Properties",
            Content = panel,
            PrimaryButtonText = "Apply",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = RootGrid.XamlRoot
        };

        if (await dialog.ShowAsync() == ContentDialogResult.Primary &&
            !string.IsNullOrWhiteSpace(nameBox.Text))
        {
            ViewModel.ProjectName = nameBox.Text.Trim();
            ViewModel.ProjectDescription = descBox.Text.Trim();
        }
    }

    private async void OnArchitectureGuideClick(object sender, RoutedEventArgs e)
    {
        var guide =
            "How this WinUI 3 workbench is built:\n\n" +
            "• Window Chrome: Native MicaBackdrop + AppWindow.Resize + OverlappedPresenter.IsAlwaysOnTop\n" +
            "• Menu Bar: WinUI MenuBar with MenuBarItem, ToggleMenuFlyoutItem, RadioMenuFlyoutItem & KeyboardAccelerators\n" +
            "• Toolbar: Fluent Button & DropDownButton bound to CommunityToolkit.Mvvm [RelayCommand]s\n" +
            "• Left Pane: ListView bound to ObservableCollection<WorkflowStepViewModel> with ContextFlyout\n" +
            "• Resizable Splitters: Custom PaneSplitter control using PointerCapture & UIElement.ProtectedCursor\n" +
            "• Middle Workspace: TabView with compiled x:Bind DataTemplates, NumberBox, ComboBox, Expander & ToggleSwitch\n" +
            "• Bottom Diagnostics: Live filtered ListView; click any row to jump directly to the offending step tab\n" +
            "• Status Bar: Live status indicator, ProgressBar, counters, and DispatcherTimer memory metric\n" +
            "• File Pickers: Unpackaged WinRT FileOpenPicker / FileSavePicker initialized via InitializeWithWindow";

        await ShowMessageDialogAsync("WinUI 3 Architecture Guide", guide);
    }

    private async void OnShortcutsClick(object sender, RoutedEventArgs e)
    {
        var shortcuts =
            "Ctrl + N           New Project\n" +
            "Ctrl + O           Open Project (.wfproj / .json)\n" +
            "Ctrl + S           Save Project\n" +
            "Ctrl + Shift + S   Save Project As\n" +
            "Ctrl + D           Duplicate Selected Step\n" +
            "Alt + Up / Down    Reorder Selected Step\n" +
            "F5                 Run Workflow\n" +
            "Shift + F5         Stop Workflow\n" +
            "Ctrl + Shift + B   Validate Workflow\n" +
            "Ctrl + B           Toggle Left Explorer Pane\n" +
            "Ctrl + J           Toggle Diagnostics Pane\n" +
            "Ctrl + W           Close Current Tab\n" +
            "Ctrl + Shift + N   Open New Window";

        var text = new TextBlock
        {
            Text = shortcuts,
            FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Cascadia Mono, Consolas"),
            FontSize = 13,
            IsTextSelectionEnabled = true
        };

        var dialog = new ContentDialog
        {
            Title = "Keyboard Shortcuts",
            Content = text,
            CloseButtonText = "Close",
            XamlRoot = RootGrid.XamlRoot
        };

        await dialog.ShowAsync();
    }

    private async void OnAboutClick(object sender, RoutedEventArgs e)
    {
        var about =
            "Workflow Studio — WinUI 3 Learning Demo\n\n" +
            "Framework: Windows App SDK (WinUI 3) + .NET 10\n" +
            "Pattern: MVVM with CommunityToolkit.Mvvm\n" +
            "Packaging: Unpackaged self-contained desktop app (.exe)\n\n" +
            "Explore Models/, ViewModels/, Services/, Controls/, and Views/ to see how each part connects.";

        await ShowMessageDialogAsync("About Workflow Studio", about);
    }

    private async Task ShowMessageDialogAsync(string title, string message)
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = new TextBlock
            {
                Text = message,
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 520,
                IsTextSelectionEnabled = true
            },
            CloseButtonText = "Close",
            XamlRoot = RootGrid.XamlRoot
        };

        await dialog.ShowAsync();
    }
}
