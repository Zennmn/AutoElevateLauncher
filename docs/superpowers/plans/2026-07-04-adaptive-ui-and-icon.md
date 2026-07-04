# Adaptive UI And Icon Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fix the manager window layout bugs, persist window layout, add an intentional empty state, and add a custom executable/window/tray icon.

**Architecture:** Keep WinForms and the current service structure. Add small helpers for window layout state and icon loading, then rebuild `MainForm` around a root layout with header, split content, scrollable details, and fixed bottom actions.

**Tech Stack:** C# 12, .NET 8 Windows, WinForms, xUnit, built-in `System.Drawing` / `System.Windows.Forms`; no third-party packages.

## Global Constraints

- Keep WinForms and the current project architecture.
- Default manager size is `1120 x 720`.
- Minimum manager size is `920 x 600`.
- Restore last saved size, position, and splitter distance when available.
- If saved bounds are off-screen, fall back to centered default size.
- The right details section scrolls when space is limited.
- Bottom actions stay visible when the form panel scrolls.
- Empty project list shows `还没有启动项目。点击“新增脚本”或“新增程序”添加。`.
- Create `src/AutoElevateLauncher/Assets/app.ico`.
- Set `<ApplicationIcon>Assets\app.ico</ApplicationIcon>`.
- Use the icon for `MainForm.Icon` and `NotifyIcon.Icon`.
- If icon loading fails, fall back to `SystemIcons.Application`.
- No new third-party UI framework or icon package.
- Every implementation task must run `dotnet test "AutoElevateLauncher.sln" --nologo` before commit.

---

## File Structure

- Modify `src/AutoElevateLauncher/StartupConfig.cs`: add manager window persistence fields.
- Create `src/AutoElevateLauncher/ManagerWindowLayoutState.cs`: validates saved bounds/sizes and computes splitter distance.
- Create `tests/AutoElevateLauncher.Tests/ManagerWindowLayoutStateTests.cs`: tests restore/clamping behavior.
- Create `src/AutoElevateLauncher/AppIcon.cs`: loads the app icon with fallback.
- Create `src/AutoElevateLauncher/Assets/app.ico`: custom shield/arrow/lightning icon.
- Modify `src/AutoElevateLauncher/AutoElevateLauncher.csproj`: set `ApplicationIcon` and include the icon asset.
- Modify `src/AutoElevateLauncher/ManagerContext.cs`: use `AppIcon.Load()` for tray.
- Replace the layout structure inside `src/AutoElevateLauncher/MainForm.cs`: header, split area, scrollable details, fixed action bar, empty state, layout save/restore.
- Modify `README.md`: mention UI window state persistence.

---

### Task 1: Window Layout State Persistence Model

**Files:**
- Modify: `src/AutoElevateLauncher/StartupConfig.cs`
- Create: `src/AutoElevateLauncher/ManagerWindowLayoutState.cs`
- Create: `tests/AutoElevateLauncher.Tests/ManagerWindowLayoutStateTests.cs`

**Interfaces:**
- Produces: `StartupConfig.ManagerWindowWidth`, `ManagerWindowHeight`, `ManagerWindowLeft`, `ManagerWindowTop`, `ManagerSplitterDistance` nullable `int` properties.
- Produces: `internal sealed record ManagerWindowLayoutState(int Width, int Height, int Left, int Top, int SplitterDistance, bool HasSavedBounds)`.
- Produces: `ManagerWindowLayoutState.FromConfig(StartupConfig config, IReadOnlyList<Rectangle> workingAreas)`.
- Produces: `ManagerWindowLayoutState.SaveToConfig(StartupConfig config, Form form, SplitContainer split)`.

- [ ] **Step 1: Write failing tests**

Create `tests/AutoElevateLauncher.Tests/ManagerWindowLayoutStateTests.cs`:

```csharp
using System.Drawing;
using AutoElevateLauncher;

namespace AutoElevateLauncher.Tests;

public sealed class ManagerWindowLayoutStateTests
{
    private static readonly Rectangle Screen = new(0, 0, 1920, 1080);

    [Fact]
    public void FromConfig_UsesDefaultsWhenSavedSizeIsTooSmall()
    {
        var config = new StartupConfig
        {
            ManagerWindowWidth = 800,
            ManagerWindowHeight = 500,
            ManagerWindowLeft = 100,
            ManagerWindowTop = 100,
            ManagerSplitterDistance = 300
        };

        var state = ManagerWindowLayoutState.FromConfig(config, [Screen]);

        Assert.Equal(1120, state.Width);
        Assert.Equal(720, state.Height);
        Assert.False(state.HasSavedBounds);
    }

    [Fact]
    public void FromConfig_UsesDefaultsWhenSavedBoundsAreOffScreen()
    {
        var config = new StartupConfig
        {
            ManagerWindowWidth = 1120,
            ManagerWindowHeight = 720,
            ManagerWindowLeft = 5000,
            ManagerWindowTop = 5000,
            ManagerSplitterDistance = 420
        };

        var state = ManagerWindowLayoutState.FromConfig(config, [Screen]);

        Assert.Equal(1120, state.Width);
        Assert.Equal(720, state.Height);
        Assert.False(state.HasSavedBounds);
    }

    [Fact]
    public void FromConfig_AcceptsValidSavedBounds()
    {
        var config = new StartupConfig
        {
            ManagerWindowWidth = 1200,
            ManagerWindowHeight = 800,
            ManagerWindowLeft = 120,
            ManagerWindowTop = 80,
            ManagerSplitterDistance = 500
        };

        var state = ManagerWindowLayoutState.FromConfig(config, [Screen]);

        Assert.Equal(1200, state.Width);
        Assert.Equal(800, state.Height);
        Assert.Equal(120, state.Left);
        Assert.Equal(80, state.Top);
        Assert.Equal(500, state.SplitterDistance);
        Assert.True(state.HasSavedBounds);
    }

    [Fact]
    public void FromConfig_ClampsSplitterToKeepBothPanesUsable()
    {
        var config = new StartupConfig
        {
            ManagerWindowWidth = 920,
            ManagerWindowHeight = 650,
            ManagerWindowLeft = 20,
            ManagerWindowTop = 20,
            ManagerSplitterDistance = 800
        };

        var state = ManagerWindowLayoutState.FromConfig(config, [Screen]);

        Assert.Equal(640, state.SplitterDistance);
    }
}
```

- [ ] **Step 2: Run tests to verify RED**

Run: `dotnet test "AutoElevateLauncher.sln" --nologo`

Expected: FAIL because `ManagerWindowLayoutState` and config fields do not exist.

- [ ] **Step 3: Add config fields**

Modify `src/AutoElevateLauncher/StartupConfig.cs`:

```csharp
namespace AutoElevateLauncher;

public sealed class StartupConfig
{
    public List<StartupItem> Items { get; set; } = [];
    public bool StartManagerAtLogin { get; set; } = false;
    public int? ManagerWindowWidth { get; set; }
    public int? ManagerWindowHeight { get; set; }
    public int? ManagerWindowLeft { get; set; }
    public int? ManagerWindowTop { get; set; }
    public int? ManagerSplitterDistance { get; set; }
}
```

- [ ] **Step 4: Add layout state helper**

Create `src/AutoElevateLauncher/ManagerWindowLayoutState.cs`:

```csharp
using System.Drawing;

namespace AutoElevateLauncher;

internal sealed record ManagerWindowLayoutState(int Width, int Height, int Left, int Top, int SplitterDistance, bool HasSavedBounds)
{
    public const int DefaultWidth = 1120;
    public const int DefaultHeight = 720;
    public const int MinimumWidth = 920;
    public const int MinimumHeight = 600;
    public const int MinimumPaneWidth = 280;
    public const int DefaultSplitterDistance = 520;

    public static ManagerWindowLayoutState FromConfig(StartupConfig config, IReadOnlyList<Rectangle> workingAreas)
    {
        var width = config.ManagerWindowWidth.GetValueOrDefault(DefaultWidth);
        var height = config.ManagerWindowHeight.GetValueOrDefault(DefaultHeight);
        var left = config.ManagerWindowLeft.GetValueOrDefault(0);
        var top = config.ManagerWindowTop.GetValueOrDefault(0);

        var hasValidSize = width >= MinimumWidth && height >= MinimumHeight;
        var savedBounds = new Rectangle(left, top, width, height);
        var hasValidBounds = hasValidSize && workingAreas.Any(area => area.IntersectsWith(savedBounds));

        if (!hasValidBounds)
        {
            width = DefaultWidth;
            height = DefaultHeight;
            left = 0;
            top = 0;
        }

        var splitter = ClampSplitter(width, config.ManagerSplitterDistance.GetValueOrDefault(DefaultSplitterDistance));
        return new ManagerWindowLayoutState(width, height, left, top, splitter, hasValidBounds);
    }

    public static void SaveToConfig(StartupConfig config, Form form, SplitContainer split)
    {
        if (form.WindowState == FormWindowState.Normal)
        {
            config.ManagerWindowWidth = form.Width;
            config.ManagerWindowHeight = form.Height;
            config.ManagerWindowLeft = form.Left;
            config.ManagerWindowTop = form.Top;
        }

        config.ManagerSplitterDistance = split.SplitterDistance;
    }

    private static int ClampSplitter(int width, int splitterDistance)
    {
        var max = Math.Max(MinimumPaneWidth, width - MinimumPaneWidth);
        return Math.Min(Math.Max(splitterDistance, MinimumPaneWidth), max);
    }
}
```

- [ ] **Step 5: Run verification**

Run: `dotnet test "AutoElevateLauncher.sln" --nologo`

Expected: PASS.

- [ ] **Step 6: Commit**

Run:

```bash
git add src/AutoElevateLauncher/StartupConfig.cs src/AutoElevateLauncher/ManagerWindowLayoutState.cs tests/AutoElevateLauncher.Tests/ManagerWindowLayoutStateTests.cs
git commit -m "feat: add manager window layout state"
```

---

### Task 2: Application Icon Asset And Loader

**Files:**
- Create: `src/AutoElevateLauncher/Assets/app.ico`
- Create: `src/AutoElevateLauncher/AppIcon.cs`
- Modify: `src/AutoElevateLauncher/AutoElevateLauncher.csproj`

**Interfaces:**
- Produces: `internal static class AppIcon`.
- Produces: `public static Icon Load()` that loads `Assets/app.ico` and falls back to `SystemIcons.Application`.
- Produces: executable application icon through `<ApplicationIcon>Assets\app.ico</ApplicationIcon>`.

- [ ] **Step 1: Generate icon file**

Create `src/AutoElevateLauncher/Assets/` and generate `app.ico` with these visual requirements:

- deep blue shield base,
- white upward launch arrow,
- small golden lightning accent,
- readable at 16x16 and 32x32.

Use a temporary PowerShell script under `C:\Users\31396\AppData\Local\Temp\opencode` to generate a multi-size `.ico` using `System.Drawing`. The script must write only `src\AutoElevateLauncher\Assets\app.ico` inside the repo.

Use this exact script content in the temporary script:

```powershell
Add-Type -AssemblyName System.Drawing

$repo = "C:\Users\31396\Documents\Codex\自启"
$assetDir = Join-Path $repo "src\AutoElevateLauncher\Assets"
$iconPath = Join-Path $assetDir "app.ico"
New-Item -ItemType Directory -Force -Path $assetDir | Out-Null

function New-IconPngBytes([int]$size) {
    $bitmap = New-Object System.Drawing.Bitmap($size, $size)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.Clear([System.Drawing.Color]::Transparent)

    $blue = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(18, 76, 153))
    $gold = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 185, 48))
    $white = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::White)

    $shield = New-Object System.Drawing.Drawing2D.GraphicsPath
    $pad = [Math]::Max(2, [int]($size * 0.10))
    $shield.AddArc($pad, $pad, $size - 2 * $pad, $size - 2 * $pad, 200, 140)
    $shield.AddLine($size - $pad, [int]($size * 0.42), [int]($size * 0.50), $size - $pad)
    $shield.AddLine([int]($pad), [int]($size * 0.42), $pad, [int]($size * 0.28))
    $shield.CloseFigure()
    $graphics.FillPath($blue, $shield)

    $arrow = New-Object System.Drawing.Drawing2D.GraphicsPath
    $arrow.AddPolygon([System.Drawing.Point[]]@(
        [System.Drawing.Point]::new([int]($size * 0.50), [int]($size * 0.20)),
        [System.Drawing.Point]::new([int]($size * 0.75), [int]($size * 0.48)),
        [System.Drawing.Point]::new([int]($size * 0.60), [int]($size * 0.48)),
        [System.Drawing.Point]::new([int]($size * 0.60), [int]($size * 0.72)),
        [System.Drawing.Point]::new([int]($size * 0.40), [int]($size * 0.72)),
        [System.Drawing.Point]::new([int]($size * 0.40), [int]($size * 0.48)),
        [System.Drawing.Point]::new([int]($size * 0.25), [int]($size * 0.48))
    ))
    $graphics.FillPath($white, $arrow)

    $bolt = New-Object System.Drawing.Drawing2D.GraphicsPath
    $bolt.AddPolygon([System.Drawing.Point[]]@(
        [System.Drawing.Point]::new([int]($size * 0.66), [int]($size * 0.58)),
        [System.Drawing.Point]::new([int]($size * 0.82), [int]($size * 0.58)),
        [System.Drawing.Point]::new([int]($size * 0.70), [int]($size * 0.78)),
        [System.Drawing.Point]::new([int]($size * 0.86), [int]($size * 0.78)),
        [System.Drawing.Point]::new([int]($size * 0.58), [int]($size * 0.96)),
        [System.Drawing.Point]::new([int]($size * 0.66), [int]($size * 0.74)),
        [System.Drawing.Point]::new([int]($size * 0.54), [int]($size * 0.74))
    ))
    $graphics.FillPath($gold, $bolt)

    $stream = New-Object System.IO.MemoryStream
    $bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
    $bytes = $stream.ToArray()
    $graphics.Dispose(); $bitmap.Dispose(); $stream.Dispose()
    return $bytes
}

$sizes = @(16, 24, 32, 48, 64, 128, 256)
$images = @($sizes | ForEach-Object { [PSCustomObject]@{ Size = $_; Bytes = New-IconPngBytes $_ } })
$fs = [System.IO.File]::Create($iconPath)
$writer = New-Object System.IO.BinaryWriter($fs)
$writer.Write([UInt16]0)
$writer.Write([UInt16]1)
$writer.Write([UInt16]$images.Count)
$offset = 6 + (16 * $images.Count)
foreach ($image in $images) {
    $entrySize = if ($image.Size -eq 256) { 0 } else { $image.Size }
    $writer.Write([byte]$entrySize)
    $writer.Write([byte]$entrySize)
    $writer.Write([byte]0)
    $writer.Write([byte]0)
    $writer.Write([UInt16]1)
    $writer.Write([UInt16]32)
    $writer.Write([UInt32]$image.Bytes.Length)
    $writer.Write([UInt32]$offset)
    $offset += $image.Bytes.Length
}
foreach ($image in $images) {
    $writer.Write($image.Bytes)
}
$writer.Dispose(); $fs.Dispose()
```

- [ ] **Step 2: Add icon loader**

Create `src/AutoElevateLauncher/AppIcon.cs`:

```csharp
using System.Drawing;

namespace AutoElevateLauncher;

internal static class AppIcon
{
    public static Icon Load()
    {
        try
        {
            var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "app.ico");
            return File.Exists(iconPath) ? new Icon(iconPath) : SystemIcons.Application;
        }
        catch
        {
            return SystemIcons.Application;
        }
    }
}
```

- [ ] **Step 3: Configure project icon and asset copy**

Modify `src/AutoElevateLauncher/AutoElevateLauncher.csproj`:

```xml
<PropertyGroup>
  <OutputType>WinExe</OutputType>
  <TargetFramework>net8.0-windows</TargetFramework>
  <Nullable>enable</Nullable>
  <UseWindowsForms>true</UseWindowsForms>
  <ImplicitUsings>enable</ImplicitUsings>
  <AssemblyName>AutoElevateLauncher</AssemblyName>
  <RootNamespace>AutoElevateLauncher</RootNamespace>
  <ApplicationIcon>Assets\app.ico</ApplicationIcon>
</PropertyGroup>

<ItemGroup>
  <Content Include="Assets\app.ico" CopyToOutputDirectory="PreserveNewest" />
</ItemGroup>
```

Keep the existing `InternalsVisibleTo` item group.

- [ ] **Step 4: Run verification**

Run: `dotnet test "AutoElevateLauncher.sln" --nologo`

Expected: PASS.

Run: `dotnet build "AutoElevateLauncher.sln" -c Release`

Expected: 0 errors, 0 warnings, and `src\AutoElevateLauncher\bin\Release\net8.0-windows\Assets\app.ico` exists.

- [ ] **Step 5: Commit**

Run:

```bash
git add src/AutoElevateLauncher/Assets/app.ico src/AutoElevateLauncher/AppIcon.cs src/AutoElevateLauncher/AutoElevateLauncher.csproj
git commit -m "feat: add application icon"
```

---

### Task 3: Adaptive Manager Form Layout

**Files:**
- Modify: `src/AutoElevateLauncher/MainForm.cs`
- Modify: `src/AutoElevateLauncher/ManagerContext.cs`

**Interfaces:**
- Consumes: `ManagerWindowLayoutState.FromConfig(...)` and `SaveToConfig(...)` from Task 1.
- Consumes: `AppIcon.Load()` from Task 2.
- Produces: manager window with header, split content, scrollable details, fixed bottom action bar, empty-state label, saved layout on close.

- [ ] **Step 1: Apply window defaults and icon in `MainForm`**

In the `MainForm` constructor, replace hard-coded `Width = 1000; Height = 650;` with:

```csharp
Text = "管理员自启动器";
MinimumSize = new Size(ManagerWindowLayoutState.MinimumWidth, ManagerWindowLayoutState.MinimumHeight);
Icon = AppIcon.Load();
var layoutState = ManagerWindowLayoutState.FromConfig(_config, Screen.AllScreens.Select(screen => screen.WorkingArea).ToList());
Size = new Size(layoutState.Width, layoutState.Height);
if (layoutState.HasSavedBounds)
{
    StartPosition = FormStartPosition.Manual;
    Location = new Point(layoutState.Left, layoutState.Top);
}
else
{
    StartPosition = FormStartPosition.CenterScreen;
}
```

- [ ] **Step 2: Replace root layout**

Replace the current `BuildLayout()` structure with:

- a root `TableLayoutPanel` docked fill with two rows: header fixed height `76`, main fill,
- a header `Panel` with app icon/name/subtitle on left and permission/self-start controls on right,
- a `SplitContainer` in the main row with `SplitterDistance` from `ManagerWindowLayoutState.FromConfig(...)`.

Use these constants inside `MainForm`:

```csharp
private const int HeaderHeight = 76;
private const int DetailsActionBarHeight = 52;
```

- [ ] **Step 3: Build left pane with empty state**

Left pane structure:

```csharp
var leftPanel = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1, Padding = new Padding(12) };
leftPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
leftPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
leftPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
```

Add title `启动项目`, toolbar buttons, then a `Panel` containing `_items` and an empty-state `Label` named `_emptyState` with text `还没有启动项目。点击“新增脚本”或“新增程序”添加。`.

Update `RefreshList()` to set `_emptyState.Visible = _config.Items.Count == 0;`.

- [ ] **Step 4: Build right pane with scrollable form and fixed bottom actions**

Right pane structure:

```csharp
var rightPanel = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1, Padding = new Padding(12) };
rightPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
rightPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
rightPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, DetailsActionBarHeight));
```

Add title `项目详情`. Add a scroll panel:

```csharp
var detailsScroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
var details = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 1, Padding = new Padding(0, 0, 8, 0) };
detailsScroll.Controls.Add(details);
```

Move all detail labels/fields into `details`. Put buttons `保存`, `立即运行`, `停止`, `打开日志` in the fixed bottom action row.

- [ ] **Step 5: Save layout on form close**

Store the split container in a field:

```csharp
private SplitContainer? _split;
```

After building it, assign `_split = split;`.

Add a form closing handler in the constructor:

```csharp
FormClosing += (_, _) => SaveWindowLayout();
```

Add method:

```csharp
private void SaveWindowLayout()
{
    if (_split is null) return;
    try
    {
        ManagerWindowLayoutState.SaveToConfig(_config, this, _split);
        _configStore.Save(_config);
    }
    catch
    {
        // Layout persistence must not block closing the manager.
    }
}
```

- [ ] **Step 6: Use icon in tray**

In `ManagerContext`, change:

```csharp
Icon = SystemIcons.Application,
```

to:

```csharp
Icon = AppIcon.Load(),
```

- [ ] **Step 7: Run verification**

Run: `dotnet test "AutoElevateLauncher.sln" --nologo`

Expected: PASS.

Run: `dotnet build "AutoElevateLauncher.sln" -c Release`

Expected: 0 errors, 0 warnings.

- [ ] **Step 8: Commit**

Run:

```bash
git add src/AutoElevateLauncher/MainForm.cs src/AutoElevateLauncher/ManagerContext.cs
git commit -m "fix: make manager window layout adaptive"
```

---

### Task 4: README And Final Verification

**Files:**
- Modify: `README.md`

**Interfaces:**
- Consumes: completed UI persistence and icon behavior from Tasks 1-3.
- Produces: README note that UI window state is stored in config.

- [ ] **Step 1: Update README data location section**

In `README.md`, under `## 数据位置`, add this sentence after the config/log bullets:

```markdown
窗口大小、位置和左右分栏宽度也会保存在同一个配置文件中。
```

- [ ] **Step 2: Run final verification**

Run: `dotnet test "AutoElevateLauncher.sln" --nologo`

Expected: PASS.

Run: `dotnet build "AutoElevateLauncher.sln" -c Release`

Expected: 0 errors, 0 warnings.

- [ ] **Step 3: Manual smoke checks**

Run the Release exe:

```powershell
& "src\AutoElevateLauncher\bin\Release\net8.0-windows\AutoElevateLauncher.exe"
```

Check manually:

- default window shows all bottom buttons,
- resizing shorter keeps buttons visible and scrolls details,
- resizing wider adjusts panes,
- close/reopen restores window size and splitter distance,
- empty list shows empty-state text,
- window and tray show custom icon.

- [ ] **Step 4: Commit**

Run:

```bash
git add README.md
git commit -m "docs: document manager window state persistence"
```

---

## Final Review

After all tasks:

- Run `dotnet test "AutoElevateLauncher.sln" --nologo`.
- Run `dotnet build "AutoElevateLauncher.sln" -c Release`.
- Inspect `git status --short` and `git log --oneline -6`.
- Request final code review for the full range from the current base commit through HEAD.
