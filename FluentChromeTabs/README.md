# WinForms Fluent Chrome Tabs

[![NuGet](https://img.shields.io/nuget/v/WinFormsFluentChromeTabs.svg)](https://www.nuget.org/packages/WinFormsFluentChromeTabs)
[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)
![.NET Framework 4.8](https://img.shields.io/badge/.NET%20Framework-4.8-blue.svg)
![Platform: Windows](https://img.shields.io/badge/platform-Windows%2010%20%7C%2011-lightgrey.svg)

Fluent, Windows 11 style Chrome tabs **in the title bar** for WinForms — with zero dependencies.

Built on native window-shell integration instead of overlay windows: the standard window frame is
kept, so DWM shadows, rounded corners, snap layouts, Aero shake, and minimize/maximize/move
animations all stay perfectly smooth. One window, no chasing, no flicker.

| Dark (follows Windows automatically) | Light |
| --- | --- |
| ![Dark theme](https://raw.githubusercontent.com/KleiKodesh/EasyTabs/master/docs/screenshot-dark.png) | ![Light theme](https://raw.githubusercontent.com/KleiKodesh/EasyTabs/master/docs/screenshot-light.png) |

## Features

- 🗂 **Tabs in the title bar** — Chrome/Edge style, painted directly on the window
- 🖱 **Drag to reorder** tabs within the strip
- 🪟 **Tear off** — drag a tab out of the strip and it becomes its own window, with a seamless
  native window-move handoff (keep dragging, snap it, anything)
- 🧲 **Drag-merge** — drop a tab onto another window's strip to move it there
- 🌓 **Light / Dark / Auto theme** — Auto follows the Windows setting live, including the
  immersive dark mode window frame
- 🎨 **Any custom chrome color** — set `CustomThemeColor` to a single `Color` and a complete
  readable palette (tab surfaces, text, hover states) is derived from it automatically
- 🎛 **Windows 11 snap layouts** flyout on the maximize button
- ⌨️ **Keyboard**: `Ctrl+T` new tab, `Ctrl+W` / `Ctrl+F4` close, `Ctrl+Tab` / `Ctrl+Shift+Tab`
  cycle, `Ctrl+1`–`Ctrl+9` select
- 📌 **Pinnable tabs** (`CanClose = false`), per-tab icons, tooltips for truncated titles,
  middle-click close
- 🖥 **Per-monitor V2 DPI** support
- 🌍 **Full RTL / Hebrew support** — set the standard `RightToLeft = Yes` and
  `RightToLeftLayout = true` and the whole chrome mirrors: tabs flow from the right, caption
  buttons move left, bidi titles render correctly, and favicons stay unmirrored
- 🧩 **Simple API** — inherit one form class, call `AddTab`

## Install

```
dotnet add package WinFormsFluentChromeTabs
```

or via the Package Manager console:

```
Install-Package WinFormsFluentChromeTabs
```

## Quick start

```csharp
using FluentChromeTabs;

var window = new FluentChromeTabsForm { Text = "My App" };
window.AddTab("Home", new HomeControl());
window.AddTab("Settings", new SettingsControl());

// Keeps the app alive while any tab window is open (torn-off tabs get their own windows)
Application.Run(new FluentChromeTabsApplicationContext(window));
```

Tab content is any `Control` (a `UserControl`, a `Panel`, anything) — it is docked below the
tab strip and shown while its tab is selected.

### Loose mode: tab strip only, your content stays static

Tabs don't have to own content. Add them with just a title and they become pure metadata — the
strip still supports reordering, closing, tear-off, and the "+" button — while **one static
control of yours** (a WebView2, a stateful `UserControl`) fills the window and reacts to
selection changes:

```csharp
var window = new FluentChromeTabsForm { Text = "My Browser" };

var webView = new WebView2 { Dock = DockStyle.Fill };   // sits below the strip automatically
window.Controls.Add(webView);

window.AddTab("Google").Tag = "https://google.com";     // content-less tabs: metadata only
window.AddTab("GitHub").Tag = "https://github.com";

window.SelectedTabChanged += (s, e) =>
    webView.Source = new Uri((string) e.Tab.Tag);

Application.Run(new FluentChromeTabsApplicationContext(window));
```

Mixing is fine too: some tabs with `Content`, some without. Per-tab state for loose tabs lives
wherever you like — `FluentTab.Tag` is the natural spot. See `LooseDemoForm` in the demo app.

### Custom chrome colors

Not limited to light and dark — hand the chrome any color and a complete palette (tab surfaces,
readable text, hover states, separators) is derived from its brightness:

```csharp
window.CustomThemeColor = Color.FromArgb(24, 34, 58);   // midnight blue chrome
window.CustomThemeColor = Color.FromArgb(238, 227, 206); // sand — light colors work too
window.CustomThemeColor = null;                          // back to Theme (Light/Dark/Auto)
```

`ContentBackColor` / `ContentForeColor` and the `ThemeChanged` event track the custom color, so
your content can restyle itself the same way it does for light/dark. Try the **Colors** tab in
the demo app.

### A richer app

```csharp
public class MainWindow : FluentChromeTabsForm
{
    public MainWindow()
    {
        Text = "My App";
        Theme = FluentChromeTabsTheme.Auto;             // follow Windows light/dark, live

        AddTab("Home", new HomeControl());
        AddTab("Pinned", new DashboardControl()).CanClose = false;

        NewTabRequested += (s, e) =>                     // the "+" button and Ctrl+T
        {
            e.Title = "Untitled";
            e.Content = new DocumentControl();
        };

        TabClosing += (s, e) =>                          // cancelable
        {
            if (HasUnsavedChanges(e.Tab))
                e.Cancel = !ConfirmClose();
        };
    }
}
```

## API reference

### `FluentChromeTabsForm` (inherit this instead of `Form`)

| Member | Purpose |
| --- | --- |
| `AddTab(title, control)` / `AddTab(title)` / `AddTab(tab)` / `InsertTab(index, tab)` | Add tabs (title-only = loose mode); returns the `FluentTab` |
| `CloseTab(tab)` | Close programmatically (raises `TabClosing` first) |
| `RequestNewTab()` | Same as the user pressing "+" |
| `Tabs`, `SelectedTab`, `SelectedIndex` | Enumerate and select |
| `Theme` (`Auto` / `Light` / `Dark`), `IsDarkTheme` | Theming |
| `CustomThemeColor` | Any `Color` as the chrome color; full palette derived from its brightness. Null returns to `Theme` |
| `ContentBackColor`, `ContentForeColor` | Suggested colors so your content matches the chrome |
| `ShowNewTabButton`, `AllowTabDetach`, `ExitOnLastTabClose` | Behavior switches |

### Events

| Event | When |
| --- | --- |
| `NewTabRequested` | "+" button or Ctrl+T; set `Content`/`Title`, or `Cancel` |
| `TabAdded` / `TabClosed` | After add / after close |
| `TabClosing` | Before a close; cancelable |
| `SelectedTabChanged` | Selection changed |
| `ThemeChanged` | Effective theme changed (including Windows switching in Auto mode) |

### `FluentTab`

| Member | Purpose |
| --- | --- |
| `Title`, `Icon` | Text and optional 16×16 icon on the tab |
| `CanClose` | `false` = pinned: no close button, immune to Ctrl+W |
| `Content` | The hosted control, or null for a loose (metadata-only) tab |
| `Tag` | Your data |

## High DPI

Give your exe a PerMonitorV2 manifest (copy `app.manifest` and the
`System.Windows.Forms.ApplicationConfigurationSection` from
[`FluentChromeTabs.Demo`](FluentChromeTabs.Demo)) and the tab strip scales crisply per monitor.

## How it works

No layered overlay window hovering over the real one (the classic approach, and the reason most
title-bar tab libraries feel laggy). Instead:

- `WM_NCCALCSIZE` reclaims the caption area as ordinary client space while keeping the real
  window frame — DWM keeps drawing the shadow, border, and rounded corners, and all window
  animations remain native.
- `WM_NCHITTEST` tells Windows what the strip means: empty space is `HTCAPTION` (native drag,
  double-click maximize), the drawn caption buttons are `HTMINBUTTON` / `HTMAXBUTTON` /
  `HTCLOSE` — which is also what summons the Windows 11 snap-layouts flyout.
- The tab strip itself is double-buffered GDI+ painted directly on the form, so it moves
  atomically with the window.

The entire native surface lives in one small, documented
[`NativeMethods.cs`](FluentChromeTabs/NativeMethods.cs).

## Building from source

```
msbuild FluentChromeTabs.slnx /restore /p:Configuration=Release
```

Requirements: Windows 10/11, .NET Framework 4.8, Visual Studio 2019+ (or Build Tools).
Run `FluentChromeTabs.Demo` to try everything interactively.

## License

[MIT](LICENSE)
