# Workflow Studio — WinUI 3 Learning Demo

A native **Windows 11** desktop workbench demo built with **WinUI 3 (Windows App SDK)**, **C#**, **XAML**, and **MVVM** (`CommunityToolkit.Mvvm`).

Designed as a clean, readable reference project demonstrating a classic IDE/workbench layout and modern reflection-free C# / WinUI 3 patterns.

---

## Layout Overview

```text
+-------------------------------------------------------------------+
| Menu Bar: File | Edit | View | Window | Help                      |
+-------------------------------------------------------------------+
| Toolbar: New/Open/Save | Add Step | Edit | Run/Stop/Validate | ◧⬓ |
+-------------------+-----------------------------------------------+
| Left Pane         | Middle Workspace (TabView)                    |
| (Components)      |  [Tab 1] [Tab 2] [+]                          |
|  - Search filter  |   - Step configuration (Name, Type, Timeout)  |
|  - Step list      |   - Command & working directory               |
|  - Status dots    |   - Execution / diagnostic simulation toggles |
|  - Context menu   |   - Last output preview                       |
|                   +-----------------------------------------------+
|                   | Bottom Pane (Diagnostics)                     |
|                   |  - Errors / Warnings / Info filter pills      |
|                   |  - Click any row to jump to that step's tab   |
+-------------------+-----------------------------------------------+
| Status Bar: State dot | Progress | Err/Warn | Steps | File | Mem  |
+-------------------------------------------------------------------+
```

---

## Features

- **Native Windows 11 Look & Feel:** Uses `<MicaBackdrop />`, Fluent Design `XamlControlsResources`, `Segoe Fluent Icons`, Light/Dark/System theme switching (`View → App Theme`), and `AppWindow` APIs (initial sizing & **Always on Top**).
- **Menu Bar (`File`, `Edit`, `View`, `Window`, `Help`):** Complete keyboard accelerators (`Ctrl+N`, `Ctrl+O`, `Ctrl+S`, `F5`, `Shift+F5`, `Ctrl+B`, `Ctrl+J`, etc.), `ToggleMenuFlyoutItem`, `RadioMenuFlyoutItem`, multi-window support (`Window → New Window`), and built-in `ContentDialog` guides.
- **Toolbar:** Quick file actions, typed **Add Step** dropdown (`Trigger`, `Build`, `Test`, `Script`, `Deploy`, `Notify`), step reordering/duplication, accent **Run Workflow** button with live `ProgressRing`, **Stop**, **Validate**, and pane toggle buttons.
- **Resizable & Collapsible Panes:** Custom zero-dependency `PaneSplitter` control with pointer capture and native resize cursors (`UIElement.ProtectedCursor`), plus `View → Reset Layout`.
- **Tabbed Middle Workspace (`TabView`):** Open, switch, add (`+`), and close multiple step editors with live status dots in each tab header.
- **Bottom Diagnostics Pane:** Live severity-filtered log (`Errors`, `Warnings`, `Info`) with search filtering and click-to-navigate back to the relevant step tab.
- **Status Bar:** Live status indicator, inline `ProgressBar` during runs, clickable error/warning summary, enabled step counter, save state, and periodic working-set memory metric (`DispatcherTimer`).
- **Real JSON File Persistence:** `File → Open / Save / Save As` wired to native Windows 11 `FileOpenPicker` and `FileSavePicker` (`.wfproj` / `.json`).

---

## Tech Stack & Modern Patterns

- **UI Framework:** WinUI 3 (`Microsoft.WindowsAppSDK`) — unpackaged desktop `.exe` with `<WindowsAppSDKSelfContained>true</WindowsAppSDKSelfContained>` so the Windows App Runtime is bundled automatically.
- **Language & Runtime:** C# on `.NET 10` (`net10.0-windows10.0.19041.0`), supporting `ARM64`, `x64`, and `x86`.
- **100% Reflection-Free / NativeAOT-Ready Architecture:**
  - Compiled XAML bindings (`{x:Bind}`) including `x:Bind` function bindings.
  - `CommunityToolkit.Mvvm` source-generated `[ObservableProperty]` on C# **partial properties** (WinRT/AOT-safe) and `[RelayCommand]`.
  - Compile-time `System.Text.Json` source generation (`WorkflowJsonContext`) for JSON serialization.

---

## Prerequisites

- **Windows 10 (1809+) or Windows 11** (x64, ARM64, or x86)
- **.NET 10 SDK** (or newer, e.g., .NET 11 SDK with .NET 10 runtime)
- *(Optional)* **Visual Studio** for IDE debugging and XAML tooling

---

## Getting Started

### Run from the Command Line

The project file automatically detects your CPU architecture (`ARM64`, `x64`, or `x86`):

```powershell
dotnet run --project WorkflowStudio\WorkflowStudio.csproj
```

Or build explicitly for a specific architecture:

```powershell
dotnet build WorkflowStudio\WorkflowStudio.csproj -c Release -p:Platform=ARM64
# or -p:Platform=x64
```

### Run from Visual Studio

1. Open `WorkflowStudio.sln`.
2. Select your architecture (`ARM64`, `x64`, or `x86`) in the solution platform dropdown.
3. Press **F5** to build and run.

---

## Project Structure

```text
WorkflowStudio/
├── WorkflowStudio.csproj              # Unpackaged WinUI 3 + self-contained Windows App SDK config
├── app.manifest                       # PerMonitorV2 DPI awareness & Windows compatibility
├── App.xaml / App.xaml.cs             # Fluent resources, shared styles, multi-window tracking
├── Controls/
│   └── PaneSplitter.cs                # Custom WinUI 3 splitter (PointerCapture + ProtectedCursor)
├── Models/
│   ├── WorkflowProject.cs             # Root serializable project document
│   ├── WorkflowStep.cs                # Individual workflow step model
│   ├── WorkflowStepType.cs            # Trigger, Build, Test, Script, Deploy, Notify
│   ├── WorkflowStepStatus.cs          # Idle, Queued, Running, Succeeded, Warning, Failed, Skipped
│   └── DiagnosticEntry.cs             # Diagnostics pane entry with Segoe Fluent Icons glyphs
├── Services/
│   ├── ProjectStorageService.cs       # Source-generated System.Text.Json + WinRT file pickers
│   └── WorkflowRunnerService.cs       # Async cancellable step runner & configuration validator
├── ViewModels/
│   ├── MainViewModel.cs               # Central workbench state, commands, filters & metrics
│   └── WorkflowStepViewModel.cs       # Per-step ObservableObject with partial properties
└── Views/
    ├── MainWindow.xaml                # Complete workbench UI shell (Mica, MenuBar, TabView, etc.)
    └── MainWindow.xaml.cs             # View-only logic: AppWindow, splitters, pickers & dialogs
```

---

## Keyboard Shortcuts

| Shortcut | Action |
| --- | --- |
| `Ctrl + N` | New Project |
| `Ctrl + O` | Open Project (`.wfproj` / `.json`) |
| `Ctrl + S` | Save Project |
| `Ctrl + Shift + S` | Save Project As |
| `Ctrl + D` | Duplicate Selected Step |
| `Alt + Up` / `Alt + Down` | Move Selected Step Up / Down |
| `F5` | Run Workflow |
| `Shift + F5` | Stop Workflow |
| `Ctrl + Shift + B` | Validate Workflow |
| `Ctrl + B` | Toggle Left Explorer Pane |
| `Ctrl + J` | Toggle Diagnostics Pane |
| `Ctrl + W` | Close Active Tab |
| `Ctrl + Shift + N` | Open New Window |

---

## License

This project is licensed under the [MIT License](LICENSE).
