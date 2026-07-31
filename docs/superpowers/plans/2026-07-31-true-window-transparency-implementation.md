# desk-note 真正整窗透明修复实现计划

> **面向执行代理：** 必须使用 `superpowers:subagent-driven-development`（推荐）或 `superpowers:executing-plans` 按步骤实现本计划。所有步骤使用复选框跟踪。项目 Git 操作由用户手动执行，执行代理不得运行 `git add`、`git commit`、建分支、合并或推送命令。

**目标：** 让主窗口在 20%—99% 透明度下真实透出桌面，并使背景、文字、按钮和图标统一透明，同时保留现有设置持久化和主题外观。

**架构：** 主窗口固定使用 WPF 透明窗口模型：`WindowStyle=None`、`AllowsTransparency=True`、透明窗口背景，现有 `Window.Opacity` 绑定负责完整视觉树的 Alpha 合成。移除与透明窗口冲突的 Mica 生命周期和 DWM 背板服务；透明度模型、ViewModel、防抖保存与主题资源保持不变。

**技术栈：** .NET 10、C#、WPF、MVVM、xUnit、XAML 契约测试、Windows 10/11 桌面合成。

---

## 实施约束

- 实现依据：`docs/superpowers/specs/2026-07-31-true-window-transparency-design.md`。
- 优先回顾：`docs/superpowers/plans/2026-07-31-visual-theme-implementation.md`；与透明窗口或 Mica 冲突时，以本计划为准。
- 只修改 `D:\university\anything\desk-note` 内的文件。
- 代码标识符使用英文；文档、界面文字和执行报告使用中文。
- 使用测试驱动流程：先新增失败契约测试，再修改窗口实现。
- 不修改透明度范围、设置 JSON、SQLite、主题配色和恢复窗口透明度。
- 不添加点击穿透、Acrylic、DirectComposition 或 Windows App SDK。
- 本计划只有一个完整修复 Task。完成后必须暂停供用户审查。
- 不执行任何 Git 写操作。

## 文件结构与职责

```text
desk-note/
├── src/DeskNote.App/
│   ├── App.xaml.cs
│   │   # 组合根：改用无参数 MainWindow，不再注入 DWM 背板服务
│   ├── Views/
│   │   ├── MainWindow.xaml
│   │   │   # 声明透明窗口、整窗 Opacity 绑定和主题根表面
│   │   └── MainWindow.xaml.cs
│   │       # 保留窗口、托盘、侧边栏和标题栏交互；移除 Mica 生命周期
│   └── Services/
│       └── WindowBackdropService.cs
│           # 删除：透明窗口不再存在 Mica 运行路径
├── tests/DeskNote.Tests/
│   ├── Views/
│   │   └── MainWindowTransparencyContractTests.cs
│   │       # 新增：验证透明窗口 XAML 与无 Mica 生命周期契约
│   └── Services/
│       └── WindowBackdropServiceTests.cs
│           # 删除：被测服务随 Mica 运行路径移除
└── docs/testing/windows-acceptance.md
    # 用真正整窗透明验收项替换原 Mica 验收项
```

---

### Task 1：实现并验收真正整窗透明

**文件：**

- 创建：`tests/DeskNote.Tests/Views/MainWindowTransparencyContractTests.cs`
- 修改：`src/DeskNote.App/Views/MainWindow.xaml`
- 修改：`src/DeskNote.App/Views/MainWindow.xaml.cs`
- 修改：`src/DeskNote.App/App.xaml.cs`
- 删除：`src/DeskNote.App/Services/WindowBackdropService.cs`
- 删除：`tests/DeskNote.Tests/Services/WindowBackdropServiceTests.cs`
- 修改：`docs/testing/windows-acceptance.md`

- [ ] **Step 1：新增透明窗口失败契约测试**

创建 `tests/DeskNote.Tests/Views/MainWindowTransparencyContractTests.cs`：

```csharp
using System.Xml.Linq;
using Xunit;

namespace DeskNote.Tests.Views;

public sealed class MainWindowTransparencyContractTests
{
    private static readonly string ProjectRoot = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));

    [Fact]
    public void MainWindow_UsesWpfTransparencyForTheWholeVisualTree()
    {
        var document = XDocument.Load(ProjectFile("Views", "MainWindow.xaml"));
        var window = Assert.IsType<XElement>(document.Root);

        Assert.Equal("None", (string?)window.Attribute("WindowStyle"));
        Assert.Equal("True", (string?)window.Attribute("AllowsTransparency"));
        Assert.Equal("Transparent", (string?)window.Attribute("Background"));

        var opacity = (string?)window.Attribute("Opacity");
        Assert.NotNull(opacity);
        Assert.Contains("Settings.WindowOpacity", opacity, StringComparison.Ordinal);
    }

    [Fact]
    public void MainWindow_DoesNotRunAMicaBackdropLifecycle()
    {
        var mainWindowCode = File.ReadAllText(
            ProjectFile("Views", "MainWindow.xaml.cs"));
        var appCode = File.ReadAllText(
            Path.Combine(ProjectRoot, "src", "DeskNote.App", "App.xaml.cs"));

        Assert.DoesNotContain(
            "WindowBackdropService",
            mainWindowCode,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "ApplyWindowMaterial",
            mainWindowCode,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "new WindowBackdropService()",
            appCode,
            StringComparison.Ordinal);
    }

    private static string ProjectFile(params string[] relativeSegments) =>
        Path.Combine(
            [ProjectRoot, "src", "DeskNote.App", .. relativeSegments]);
}
```

- [ ] **Step 2：运行契约测试并确认按预期失败**

运行：

```powershell
dotnet test tests/DeskNote.Tests/DeskNote.Tests.csproj --filter "FullyQualifiedName~MainWindowTransparencyContractTests"
```

预期：FAIL。第一个测试报告 `AllowsTransparency` 实际为 `null`；第二个测试报告现有 `MainWindow.xaml.cs` 或 `App.xaml.cs` 仍包含 `WindowBackdropService`/`ApplyWindowMaterial`。

- [ ] **Step 3：启用 WPF 透明窗口能力**

在 `src/DeskNote.App/Views/MainWindow.xaml` 的窗口属性中，将：

```xml
WindowStyle="None"
ResizeMode="CanResize"
Opacity="{Binding Settings.WindowOpacity, Mode=OneWay}"
Background="Transparent"
```

替换为：

```xml
WindowStyle="None"
AllowsTransparency="True"
ResizeMode="CanResize"
Opacity="{Binding Settings.WindowOpacity, Mode=OneWay}"
Background="Transparent"
```

不要修改 `RootSurface` 的 `FallbackWindowBrush`。100% 时该画刷继续提供完整主题背景；低于 100% 时由窗口级 Alpha 统一合成完整视觉树。

- [ ] **Step 4：移除主窗口的 Mica 生命周期**

将 `src/DeskNote.App/Views/MainWindow.xaml.cs` 完整替换为：

```csharp
using System.ComponentModel;
using System.Windows;
using System.Windows.Media.Animation;
using KeyboardFocusChangedEventArgs = System.Windows.Input.KeyboardFocusChangedEventArgs;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;

namespace DeskNote.App.Views;

public partial class MainWindow : Window
{
    private bool allowClose;

    public MainWindow() => InitializeComponent();

    public event EventHandler? Hiding;

    public void ExitApplication()
    {
        allowClose = true;
        Close();
    }

    public void ShowAndActivate()
    {
        Show();
        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }

        Activate();
    }

    private void HideWindow()
    {
        Hiding?.Invoke(this, EventArgs.Empty);
        Hide();
    }

    private void OnSidebarMouseEnter(object sender, MouseEventArgs e) => AnimateSidebar(150);

    private void OnSidebarMouseLeave(object sender, MouseEventArgs e)
    {
        if (!Sidebar.IsKeyboardFocusWithin)
        {
            AnimateSidebar(58);
        }
    }

    private void OnSidebarFocusChanged(object sender, KeyboardFocusChangedEventArgs e) =>
        AnimateSidebar(Sidebar.IsKeyboardFocusWithin ? 150 : 58);

    private void AnimateSidebar(double width)
    {
        var duration = SystemParameters.ClientAreaAnimation
            ? TimeSpan.FromMilliseconds(180)
            : TimeSpan.Zero;
        Sidebar.BeginAnimation(WidthProperty, new DoubleAnimation(width, duration)
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        });
    }

    private async void OnPinChanged(object sender, RoutedEventArgs e)
    {
        var enabled = sender is System.Windows.Controls.Primitives.ToggleButton
        {
            IsChecked: true
        };
        if (DataContext is ViewModels.MainViewModel viewModel)
        {
            await viewModel.Settings.SetAlwaysOnTopCommand.ExecuteAsync(enabled);
            Topmost = viewModel.Settings.AlwaysOnTop;
        }
    }

    private void OnMinimize(object sender, RoutedEventArgs e) =>
        WindowState = WindowState.Minimized;

    private void OnMaximize(object sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;

    private void OnHide(object sender, RoutedEventArgs e) => HideWindow();

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (allowClose)
        {
            Hiding?.Invoke(this, EventArgs.Empty);
            return;
        }

        e.Cancel = true;
        HideWindow();
    }
}
```

该替换只移除以下内容：

- `ThemeService` 和 `WindowBackdropService` 构造参数与字段。
- `HwndSource`、窗口消息 Hook 和 DWM 组合消息。
- `Opacity` 变化时的材料重算。
- 主题变化与托盘恢复时的 Mica 重算。

侧边栏、标题栏、置顶、隐藏到托盘和退出行为保持不变。

- [ ] **Step 5：更新组合根并删除不可达的 Mica 实现**

在 `src/DeskNote.App/App.xaml.cs` 中，将：

```csharp
var window = new AppMainWindow(themeService, new WindowBackdropService())
{
    DataContext = mainViewModel,
    Topmost = settings.Current.AlwaysOnTop
};
```

替换为：

```csharp
var window = new AppMainWindow
{
    DataContext = mainViewModel,
    Topmost = settings.Current.AlwaysOnTop
};
```

随后删除以下文件：

```text
src/DeskNote.App/Services/WindowBackdropService.cs
tests/DeskNote.Tests/Services/WindowBackdropServiceTests.cs
```

删除前运行：

```powershell
rg -n "WindowBackdropService|WindowMaterialState|IWindowBackdropApi" src tests
```

预期：引用只出现在即将修改的 `App.xaml.cs`、`MainWindow.xaml.cs` 和即将删除的服务及测试中。完成替换和删除后再次运行同一命令，预期没有匹配结果。

不要删除主题字典中的现有画刷；`FallbackWindowBrush` 继续作为主窗口主题表面，其他资源清理由后续独立主题任务决定。

- [ ] **Step 6：运行透明窗口契约测试并确认通过**

运行：

```powershell
dotnet test tests/DeskNote.Tests/DeskNote.Tests.csproj --filter "FullyQualifiedName~MainWindowTransparencyContractTests"
```

预期：PASS，2 个契约测试全部通过。若 XAML 编译报告 `AllowsTransparency` 与窗口样式冲突，先确认 `WindowStyle="None"` 未被移除，不得改回 Mica 或动态分层窗口方案。

- [ ] **Step 7：更新 Windows 透明度验收清单**

在 `docs/testing/windows-acceptance.md` 中，将现有的 `## 紫色玻璃主题、图标与透明度` 整节替换为：

```markdown
## 紫色玻璃主题、图标与真正整窗透明

- [ ] Windows 10/11、透明度 100%：主窗口使用 WPF 自绘紫色玻璃表面；浅色为浅紫/白，深色为深紫/黑。
- [ ] 透明度 20%、40%、60%、80%：能透过窗口看到桌面或后方应用，不只是界面亮度降低。
- [ ] 五档透明度下，背景、文字、图标和按钮作为完整窗口视觉树同步变化。
- [ ] 20% 时按钮、滑块、拖动和缩放仍可操作，窗口不发生鼠标穿透。
- [ ] 透明度从 100% 快速拖到 20% 再恢复时无黑底、黑边和明显闪烁。
- [ ] 快速拖动滑块无明显卡顿，停止拖动后自动保存，重启恢复最后值。
- [ ] 透明度低于 40% 时显示可读性提示，但仍允许保存 20%—39%。
- [ ] 浅色/深色切换、托盘隐藏和恢复、单实例唤醒、最大化和还原不重置透明度。
- [ ] 圆角以外区域完全透明；最大化时无圆角缝隙，窗口边缘仍可缩放。
- [ ] 系统关闭透明效果或处于远程桌面时，程序不调用 Mica/DWM 背板 API，也不因材料初始化失败而影响启动。
- [ ] 浅色和深色逐页检查标题栏、侧边栏、编辑、未完成、已完成、设置和恢复窗口。
- [ ] 深色模式全部文字使用白色色相，次要和禁用文字只通过透明度区分。
- [ ] 按钮、置顶切换、文本框、复选框、列表项、滑块无黑色硬边框，圆角一致。
- [ ] 用鼠标和键盘检查默认、悬停、按下、选中、聚焦、禁用状态和滑块 1% 步进。
- [ ] 主窗口、任务栏、资源管理器、托盘、恢复窗口、安装程序、快捷方式和卸载项显示统一图标。
- [ ] 100%、125%、150%、200% DPI 下图标清晰、文字不裁切、圆角无明显锯齿。
```

该替换明确取消以下旧验收语义：100% 必须使用 Mica、低于 100% 切换 `FallbackGlass`、恢复 100% 后重新尝试 `NativeMica`。

- [ ] **Step 8：运行 Debug 和 Release 全量自动化验证**

依次运行：

```powershell
dotnet build DeskNote.sln -c Debug
dotnet test DeskNote.sln -c Debug --no-build
dotnet build DeskNote.sln -c Release
dotnet test DeskNote.sln -c Release --no-build
```

预期：

- Debug 和 Release 均构建成功，无警告、无错误。
- 两种配置的全部测试均通过。
- 以当前 61 个测试为基线，删除 4 个 Mica 测试用例并新增 2 个透明窗口契约测试后，预期各执行 59 个测试。

如果数量不同，先使用以下命令列出测试并解释差异，不能只凭退出码宣称完成：

```powershell
dotnet test tests/DeskNote.Tests/DeskNote.Tests.csproj -c Release --no-build --list-tests
```

- [ ] **Step 9：在隔离数据目录执行真正透视透明验收**

先确认没有需要保留的 desk-note 调试实例。为本次验收使用项目内的精确隔离目录：

```powershell
$existingDeskNote = Get-Process -Name 'desk-note' -ErrorAction SilentlyContinue
if ($null -ne $existingDeskNote) {
    throw '请先通过托盘退出正在运行的 desk-note，再开始隔离透明度验收。'
}
$acceptanceRoot = [System.IO.Path]::GetFullPath(
    (Join-Path (Get-Location) 'src\DeskNote.App\bin\TransparencyAcceptance'))
$expectedParent = [System.IO.Path]::GetFullPath(
    (Join-Path (Get-Location) 'src\DeskNote.App\bin'))
if ([System.IO.Path]::GetDirectoryName($acceptanceRoot) -ne $expectedParent) {
    throw 'Transparency acceptance path escaped the expected bin directory.'
}
New-Item -ItemType Directory -Force -Path $acceptanceRoot | Out-Null
$debugExe = Resolve-Path 'src\DeskNote.App\bin\Debug\net10.0-windows\desk-note.exe'
$process = Start-Process -FilePath $debugExe.Path -PassThru -Environment @{
    DESKNOTE_STATE_ROOT = $acceptanceRoot
}
```

把一个颜色和文字都清晰可辨的窗口放在 desk-note 后方，然后在设置页依次选择 100%、80%、60%、40%、20%，逐项确认：

1. 80%—20% 时能看到后方窗口内容，效果不是单纯变暗。
2. 背景、标题、侧边栏、文字、按钮和图标统一透明。
3. 20% 时可操作滑块、按钮、拖动和缩放，鼠标不会穿透。
4. 浅色/深色切换不改变当前透明度。
5. 隐藏到托盘后恢复、最小化、最大化和还原均保持透明度。
6. 圆角外没有黑色矩形背景，拖动滑块时无明显闪烁。
7. 恢复 100% 后自绘紫色玻璃表面完整显示。

通过托盘菜单正常退出本次隔离实例。如果正常退出未完成，只允许按本步骤保存的 `$process.Id` 终止本次测试实例：

```powershell
$process.Refresh()
if (-not $process.HasExited) {
    Stop-Process -Id $process.Id
    $process.WaitForExit()
}
```

记录哪些项目由代理实际观察，哪些需要用户在 Windows 10 或不同 DPI 环境下继续人工确认。

- [ ] **Step 10：清理由本轮诊断和验收创建的临时资源**

只处理下列已知临时资源，不删除其他 `bin/` 或 `obj/` 内容：

```text
src/DeskNote.App/bin/TransparencyProbe/
src/DeskNote.App/bin/transparency-probe-screen.png
src/DeskNote.App/bin/transparency-probe-source.png
src/DeskNote.App/bin/transparency-probe-layered.png
src/DeskNote.App/bin/TransparencyAcceptance/
```

执行删除前，逐个把目标解析为绝对路径，并确认其父目录严格等于：

```text
D:\university\anything\desk-note\src\DeskNote.App\bin
```

如果诊断进程 PID `23296` 仍存在，只在其可执行文件路径严格位于 `TransparencyProbe` 目录时终止它；PID 已复用或路径不匹配时不得终止。然后使用 PowerShell `Remove-Item -LiteralPath` 删除上述精确目标。递归删除只允许用于已验证的 `TransparencyProbe` 和 `TransparencyAcceptance` 两个目录。

使用以下脚本执行精确校验和清理：

```powershell
$binRoot = [System.IO.Path]::GetFullPath(
    (Join-Path (Get-Location) 'src\DeskNote.App\bin'))
$probeRoot = [System.IO.Path]::GetFullPath(
    (Join-Path $binRoot 'TransparencyProbe'))
$acceptanceRoot = [System.IO.Path]::GetFullPath(
    (Join-Path $binRoot 'TransparencyAcceptance'))
$directoryTargets = @($probeRoot, $acceptanceRoot)
$fileTargets = @(
    [System.IO.Path]::GetFullPath((Join-Path $binRoot 'transparency-probe-screen.png')),
    [System.IO.Path]::GetFullPath((Join-Path $binRoot 'transparency-probe-source.png')),
    [System.IO.Path]::GetFullPath((Join-Path $binRoot 'transparency-probe-layered.png'))
)

foreach ($target in @($directoryTargets) + @($fileTargets)) {
    if ([System.IO.Path]::GetDirectoryName($target) -ne $binRoot) {
        throw "Refusing to clean unexpected path: $target"
    }
}

$probeProcess = Get-Process -Id 23296 -ErrorAction SilentlyContinue
if ($null -ne $probeProcess) {
    $probeProcessPath = $probeProcess.Path
    $probePrefix = $probeRoot + [System.IO.Path]::DirectorySeparatorChar
    if ($probeProcessPath -and
        [System.IO.Path]::GetFullPath($probeProcessPath).StartsWith(
            $probePrefix,
            [StringComparison]::OrdinalIgnoreCase)) {
        Stop-Process -Id $probeProcess.Id
        $probeProcess.WaitForExit()
    }
}

foreach ($directory in $directoryTargets) {
    if (Test-Path -LiteralPath $directory -PathType Container) {
        Remove-Item -LiteralPath $directory -Recurse -Force
    }
}

foreach ($file in $fileTargets) {
    if (Test-Path -LiteralPath $file -PathType Leaf) {
        Remove-Item -LiteralPath $file -Force
    }
}
```

清理后运行：

```powershell
Get-Process -Id 23296 -ErrorAction SilentlyContinue
Test-Path 'src\DeskNote.App\bin\TransparencyProbe'
Test-Path 'src\DeskNote.App\bin\TransparencyAcceptance'
Test-Path 'src\DeskNote.App\bin\transparency-probe-screen.png'
Test-Path 'src\DeskNote.App\bin\transparency-probe-source.png'
Test-Path 'src\DeskNote.App\bin\transparency-probe-layered.png'
```

预期：PID 不存在或确认不是诊断实例且未被操作；五个 `Test-Path` 均返回 `False`。

- [ ] **Step 11：最终验证并暂停用户审查**

再次运行：

```powershell
dotnet test DeskNote.sln -c Release --no-build
```

执行代理在汇报完成前必须使用 `superpowers:verification-before-completion`，报告：

- 失败契约测试的实际失败证据。
- Debug/Release 构建和测试数量。
- 20%—100% 五档透明度的实际透视效果。
- 鼠标命中、圆角、托盘恢复、主题切换和窗口状态验收结果。
- 已清理的诊断资源。
- Windows 10、不同 DPI 或其他仍需用户人工确认的项目。

完成 Task 1 后暂停，不继续其他任务，不执行任何 Git 写操作。
