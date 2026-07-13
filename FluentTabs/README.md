# FluentTabs

Fluent (Windows 11) style tabs in the title bar for WinForms on .NET Framework 4.8 — built on native
window-shell integration instead of overlay windows, so everything the OS does with a window keeps
working: DWM shadows, rounded corners, snap layouts, Aero shake, and smooth minimize/maximize/move
animations.

## How it works

- The standard window frame is kept; `WM_NCCALCSIZE` reclaims the caption area as client space.
- The tab strip is painted directly on the form — one window, nothing to chase or desync.
- `WM_NCHITTEST` maps the empty strip to `HTCAPTION` (native drag and double-click-maximize) and the
  drawn caption buttons to `HTMINBUTTON`/`HTMAXBUTTON`/`HTCLOSE`, which also enables the Windows 11
  snap-layouts flyout on hover.

## Quick start

```csharp
using FluentTabs;

var window = new FluentTabForm { Text = "My App" };
window.AddTab("Home", new HomeControl());
window.AddTab("Settings", new SettingsControl());

// Keeps the app alive while any tab window is open (torn-off tabs get their own windows)
Application.Run(new FluentTabsApplicationContext(window));
```

Or subclass for a richer app:

```csharp
public class MainWindow : FluentTabForm
{
    public MainWindow()
    {
        Text = "My App";
        AddTab("Home", new HomeControl());
        NewTabRequested += (s, e) => e.Content = new HomeControl();
    }
}
```

## Features

- Drag tabs to reorder; drag a tab out of the strip to tear it off into its own window; drag a tab
  onto another window's strip to merge it back (all Chrome-style, with a native window-move handoff).
- Light / dark / auto theme (`Theme` property); auto follows the Windows setting live.
- Per-tab icon, tooltip on truncated titles, `CanClose` for pinned tabs, middle-click close.
- Keyboard: Ctrl+T, Ctrl+W / Ctrl+F4, Ctrl+Tab / Ctrl+Shift+Tab, Ctrl+1..9.
- Per-monitor DPI aware (give your exe a PerMonitorV2 manifest, as in the demo app).
- Right-click strip for the system menu; single-tab windows drag by their tab.

## API surface

| Member | Purpose |
| --- | --- |
| `AddTab(title, control)` / `InsertTab` / `CloseTab` | Manage tabs; content is any `Control` |
| `Tabs`, `SelectedTab`, `SelectedIndex` | Enumerate and select |
| `Theme`, `IsDarkTheme`, `ContentBackColor`, `ContentForeColor` | Theming |
| `NewTabRequested`, `TabAdded`, `TabClosing` (cancelable), `TabClosed`, `SelectedTabChanged`, `ThemeChanged` | Events |
| `ShowNewTabButton`, `AllowTabDetach`, `ExitOnLastTabClose` | Behavior switches |
| `FluentTab.Title` / `.Icon` / `.CanClose` / `.Tag` | Per-tab state |

No NuGet dependencies. The whole native surface is one small, documented `NativeMethods.cs`.
