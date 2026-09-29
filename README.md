# Task Manager

Task Manager is a native Windows 11 productivity application built with C#, WinUI 3, and the Windows App SDK. It combines a focused daily workspace, custom calendar, recurring tasks, reminders, insights, and local-first storage in a modern Fluent interface.

## Highlights

- Modern WinUI 3 shell with Mica, NavigationView, responsive Fluent surfaces, native controls, light/dark/system themes, and keyboard access.
- Create, edit, complete, and delete tasks with priorities, categories, tags, due dates, and notes.
- Dashboard centered on what needs attention today, recent completion progress, upcoming work, and priority balance.
- Focused Today view with progress, attention, and next-deadline summaries.
- Chronologically grouped Upcoming view with seven-day and priority planning context.
- All Tasks overview with category distribution and a compact due-soon list.
- Completed history with weekly, monthly, and recent-completion summaries.
- Custom monthly calendar with task previews, day details, and date-aware task creation.
- Modern task cards with contextual actions, category and tag chips, reminder and recurrence indicators, and subtask progress.
- Quick task editor with progressively disclosed notes, tags, reminders, recurrence, and reorderable subtasks.
- Search, due-date/priority sorting, and compact filters for category, priority, tag, status, and recurrence.
- Contextual bulk command bar for completion, category, priority, and deletion changes.
- Persistent local reminders with Windows notifications when available and catch-up when the app next opens.
- Validated JSON export/import, atomic saves, backup recovery, and compatibility with existing Task Manager data.

## Technology

- C# and .NET 10
- WinUI 3 and Windows App SDK 2.5.1
- XAML with an MVVM-oriented presentation layer
- CommunityToolkit.Mvvm
- System.Text.Json

## Architecture

```text
TaskManager.Core/         Shared model, query, recurrence, reminder, and persistence logic
TaskManager.WinUI/        WinUI 3 application, XAML views, view models, dialogs, and Windows adapters
TaskManager.Core.Tests/   Cross-platform regression checks for reusable application logic
```

The model and persistence code live directly in `TaskManager.Core`. The WinUI application and regression checks both reference that project, so the JSON schema and compatibility behavior have a single implementation.

## Build and run

Requirements:

- Windows 11 or a supported Windows 10 release
- .NET 10 SDK
- Windows App SDK development components. The command-line templates can be installed with:

```powershell
dotnet new install Microsoft.WindowsAppSDK.WinUI.CSharp.Templates
```

Build and run from the repository root:

```powershell
dotnet build TaskManager.sln -c Release -p:Platform=x64
dotnet run --project TaskManager.WinUI/TaskManager.WinUI.csproj -c Release -p:Platform=x64
```

The default project configuration is unpackaged and self-contained. It runs without enabling Windows Developer Mode and carries its Windows App SDK runtime dependencies alongside the application. The included package manifest remains available for a future signed MSIX release.

Run the reusable logic checks with:

```powershell
dotnet run --project TaskManager.Core.Tests/TaskManager.Core.Tests.csproj -c Release
```

## Existing data

The WinUI application reads the same per-user data as the previous version:

```text
%LOCALAPPDATA%\SchoolPortfolio\TaskManager\tasks.json
```

The schema remains version 1 with additive optional fields. Older files receive safe defaults for tags, notes, subtasks, recurrence, reminders, and completion timestamps. Atomic replacement, automatic backup recovery, unreadable-file preservation, and unsupported-version protection remain intact.

Theme preference is stored separately as `settings.json` in the same application data folder.

## Keyboard shortcuts

| Shortcut | Action |
| --- | --- |
| `Ctrl+N` | Create a task |
| `Ctrl+F` | Focus search in task views |
| `Escape` | Cancel a multi-selection |
| `Enter` | Activate the focused control |

## Privacy

All task data remains on the local device unless the user explicitly exports a backup. The application has no backend, login, analytics, or cloud dependency.
