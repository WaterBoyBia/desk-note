# desk-note 紫色玻璃主题、程序图标与透明度实现计划

> **面向执行代理：** 必须使用 `superpowers:subagent-driven-development`（推荐）或 `superpowers:executing-plans` 按任务执行本计划。所有步骤使用复选框跟踪。项目 Git 操作由用户手动完成，执行代理不得运行 `git add`、`git commit`、建分支、合并或推送命令。每完成一个 Task 必须暂停，等待用户审查通过后才能开始下一 Task。

**目标：** 将 desk-note 改造成具有统一程序图标、浅紫/白与深紫/黑主题、圆角玻璃控件、Windows 11 Mica 安全降级，以及 20%—100% 整窗透明度设置的桌面应用。

**架构：** 保留现有 WPF + MVVM 结构；主题颜色与控件模板继续由应用级资源字典管理，透明度通过设置模型和主窗口绑定实时应用，所有设置写入通过异步互斥门串行化。新增独立的窗口背板服务封装 DWM 调用，明确在 `NativeMica` 与 `FallbackGlass` 之间切换，平台能力失败不能影响程序启动。

**技术栈：** .NET 10、C#、WPF、CommunityToolkit.Mvvm、System.Text.Json、System.Windows.Forms.NotifyIcon、DWM Win32 API、xUnit、Python 3 + Pillow（仅用于从唯一 PNG 源图生成已提交的 ICO 派生产物）、Inno Setup。

---

## 实施约束

- 实现依据：`docs/superpowers/specs/2026-07-31-visual-theme-design.md`。
- 只修改 `desk-note/` 项目目录内文件。
- 代码标识符和代码注释使用英文；计划、错误提示和界面文字使用中文。
- 不修改待办数据库结构，不引入 Windows App SDK，不使用 Windows 10 未公开 Acrylic API。
- 每个 Task 先写失败测试或验证，再写最小实现；Task 结束时运行指定验证并暂停用户审查。
- `icon/icon.png` 是唯一人工维护的图标源；`desk-note.ico` 只能由脚本重新生成。

## 文件结构与职责

```text
desk-note/
├── icon/icon.png                                  # 唯一图标源
├── scripts/generate-icon.py                       # 确定性生成和校验 ICO
├── src/DeskNote.App/
│   ├── Resources/desk-note.ico                    # 生成的多尺寸 Win32 图标
│   ├── Resources/Controls.xaml                    # 玻璃控件模板
│   ├── Resources/Themes/Light.xaml                # 浅紫/白主题资源
│   ├── Resources/Themes/Dark.xaml                 # 深紫/黑及白字主题资源
│   ├── Infrastructure/ISettingsStore.cs           # 设置存储可测试边界
│   ├── Models/AppSettings.cs                      # 透明度默认值、范围和规范化
│   ├── Services/SettingsService.cs                # 串行设置更新、快照与回滚
│   ├── Services/ThemeService.cs                   # 主题切换及变更通知
│   ├── Services/WindowBackdropService.cs          # DWM Mica、圆角和降级状态
│   ├── ViewModels/SettingsViewModel.cs            # 实时透明度、防抖保存与回滚
│   ├── Views/MainWindow.xaml(.cs)                  # 整窗透明度绑定和材料切换
│   ├── Views/SettingsView.xaml                    # 20%—100% 滑块
│   └── Views/RecoveryWindow.xaml                  # 统一主题和图标，保持 100% 不透明
├── tests/DeskNote.Tests/
│   ├── Infrastructure/JsonSettingsStoreTests.cs
│   ├── Services/SettingsServiceTests.cs
│   ├── Services/WindowBackdropServiceTests.cs
│   ├── ViewModels/SettingsViewModelTests.cs
│   └── Resources/ThemeResourceContractTests.cs
└── packaging/desk-note.iss                        # 安装程序图标
```

### Task 1：生成并接入统一程序图标

**Files:**
- Create: `scripts/generate-icon.py`
- Create (generated): `src/DeskNote.App/Resources/desk-note.ico`
- Modify: `src/DeskNote.App/DeskNote.App.csproj`
- Modify: `src/DeskNote.App/Views/MainWindow.xaml`
- Modify: `src/DeskNote.App/Views/RecoveryWindow.xaml`
- Modify: `src/DeskNote.App/Services/TrayIconController.cs`
- Modify: `packaging/desk-note.iss`

- [ ] **Step 1：写图标生成与校验脚本**

创建 `scripts/generate-icon.py`：

```python
from pathlib import Path

from PIL import Image


ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "icon" / "icon.png"
OUTPUT = ROOT / "src" / "DeskNote.App" / "Resources" / "desk-note.ico"
SIZES = (16, 20, 24, 32, 40, 48, 64, 128, 256)


def main() -> None:
    with Image.open(SOURCE) as source:
        if source.format != "PNG":
            raise ValueError(f"Expected PNG source, got {source.format}")
        if source.width != source.height:
            raise ValueError("Icon source must be square")

        rgba = source.convert("RGBA")
        OUTPUT.parent.mkdir(parents=True, exist_ok=True)
        rgba.save(OUTPUT, format="ICO", sizes=[(size, size) for size in SIZES])

    with Image.open(OUTPUT) as generated:
        actual_sizes = {size[0] for size in generated.ico.sizes()}
        missing = set(SIZES) - actual_sizes
        if missing:
            raise ValueError(f"Generated icon is missing sizes: {sorted(missing)}")

    print(f"Generated {OUTPUT.relative_to(ROOT)} from {SOURCE.relative_to(ROOT)}")


if __name__ == "__main__":
    main()
```

- [ ] **Step 2：运行脚本并验证多尺寸 ICO**

Run:

```powershell
python scripts/generate-icon.py
python -c "from PIL import Image; image=Image.open(r'src/DeskNote.App/Resources/desk-note.ico'); print(sorted(size[0] for size in image.ico.sizes()))"
```

Expected:

```text
Generated src\DeskNote.App\Resources\desk-note.ico from icon\icon.png
[16, 20, 24, 32, 40, 48, 64, 128, 256]
```

- [ ] **Step 3：把 PNG 和 ICO 接入 WPF 项目**

在 `src/DeskNote.App/DeskNote.App.csproj` 的主 `PropertyGroup` 中增加：

```xml
<ApplicationIcon>Resources\desk-note.ico</ApplicationIcon>
```

在包引用 `ItemGroup` 后增加：

```xml
<ItemGroup>
  <Resource Include="..\..\icon\icon.png" Link="Resources\icon.png" />
  <Resource Include="Resources\desk-note.ico" />
</ItemGroup>
```

在 `src/DeskNote.App/Views/MainWindow.xaml` 和 `src/DeskNote.App/Views/RecoveryWindow.xaml` 的 `Window` 根元素中增加：

```xml
Icon="/Resources/icon.png"
```

- [ ] **Step 4：用嵌入图标替换系统托盘默认图标**

把 `src/DeskNote.App/Services/TrayIconController.cs` 替换为：

```csharp
using System.Drawing;
using System.Windows;
using System.Windows.Forms;

namespace DeskNote.App.Services;

public sealed class TrayIconController : IDisposable
{
    private readonly Icon trayIcon;
    private readonly NotifyIcon notifyIcon;

    public TrayIconController(Action show, Action create, Action exit)
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("显示 desk-note", null, (_, _) => show());
        menu.Items.Add("创建新待办", null, (_, _) => create());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("退出", null, (_, _) => exit());

        trayIcon = LoadTrayIcon();
        notifyIcon = new NotifyIcon
        {
            Text = "desk-note",
            Icon = trayIcon,
            ContextMenuStrip = menu,
            Visible = true
        };
        notifyIcon.MouseClick += (_, eventArgs) =>
        {
            if (eventArgs.Button == MouseButtons.Left)
            {
                show();
            }
        };
    }

    public void Dispose()
    {
        notifyIcon.Visible = false;
        notifyIcon.Dispose();
        trayIcon.Dispose();
    }

    private static Icon LoadTrayIcon()
    {
        var resource = Application.GetResourceStream(
            new Uri("pack://application:,,,/Resources/desk-note.ico"));
        if (resource is null)
        {
            return (Icon)SystemIcons.Application.Clone();
        }

        try
        {
            using var stream = resource.Stream;
            using var source = new Icon(stream);
            return (Icon)source.Clone();
        }
        catch (ArgumentException)
        {
            return (Icon)SystemIcons.Application.Clone();
        }
    }
}
```

- [ ] **Step 5：配置 Inno Setup 图标**

在 `packaging/desk-note.iss` 的 `[Setup]` 段增加：

```ini
SetupIconFile=..\src\DeskNote.App\Resources\desk-note.ico
```

保留现有 `UninstallDisplayIcon={app}\{#MyAppExeName}`，让卸载项使用带 `ApplicationIcon` 的 EXE。

- [ ] **Step 6：构建并检查资源**

Run:

```powershell
dotnet build DeskNote.sln
dotnet test DeskNote.sln --no-build
```

Expected: 构建无警告无错误，所有现有测试通过。

手工启动应用，检查主窗口、任务栏和托盘均显示新图标；恢复窗口的图标在 Task 6 故障场景验收时再次检查。

- [ ] **Step 7：暂停用户审查**

汇报生成文件、接入位置和验证结果，不执行 Git 操作。等待用户确认 Task 1 后再开始 Task 2。

### Task 2：为透明度增加持久化模型和串行设置更新

**Files:**
- Create: `src/DeskNote.App/Infrastructure/ISettingsStore.cs`
- Modify: `src/DeskNote.App/Infrastructure/JsonSettingsStore.cs`
- Modify: `src/DeskNote.App/Models/AppSettings.cs`
- Modify: `src/DeskNote.App/Services/SettingsService.cs`
- Test: `tests/DeskNote.Tests/Infrastructure/JsonSettingsStoreTests.cs`
- Test: `tests/DeskNote.Tests/Services/SettingsServiceTests.cs`

- [ ] **Step 1：先写透明度默认值、规范化和往返测试**

在 `tests/DeskNote.Tests/Infrastructure/JsonSettingsStoreTests.cs` 增加：

```csharp
[Fact]
public async Task LoadAsync_UsesFullOpacityWhenExistingJsonOmitsWindowOpacity()
{
    Directory.CreateDirectory(root);
    await File.WriteAllTextAsync(
        Path.Combine(root, "settings.json"),
        """
        {
          "schemaVersion": 1,
          "theme": "light"
        }
        """);
    var store = new JsonSettingsStore();

    var settings = await store.LoadAsync(root);

    Assert.Equal(1.0, settings.WindowOpacity);
}

[Theory]
[InlineData(-1.0, 0.20)]
[InlineData(0.10, 0.20)]
[InlineData(0.60, 0.60)]
[InlineData(1.50, 1.00)]
public async Task LoadAsync_ClampsWindowOpacity(double stored, double expected)
{
    Directory.CreateDirectory(root);
    await File.WriteAllTextAsync(
        Path.Combine(root, "settings.json"),
        $$"""
        {
          "schemaVersion": 1,
          "theme": "light",
          "windowOpacity": {{stored.ToString(System.Globalization.CultureInfo.InvariantCulture)}}
        }
        """);
    var store = new JsonSettingsStore();

    var settings = await store.LoadAsync(root);

    Assert.Equal(expected, settings.WindowOpacity, precision: 2);
}
```

在现有 `SaveAsync_RoundTripsSettings` 的 `expected` 初始化器中增加：

```csharp
WindowOpacity = 0.62,
```

并在断言末尾增加：

```csharp
Assert.Equal(expected.WindowOpacity, actual.WindowOpacity);
```

- [ ] **Step 2：运行测试并确认失败**

Run:

```powershell
dotnet test tests/DeskNote.Tests/DeskNote.Tests.csproj --filter "FullyQualifiedName~JsonSettingsStoreTests"
```

Expected: FAIL，编译器报告 `AppSettings` 不包含 `WindowOpacity`。

- [ ] **Step 3：增加设置存储接口和透明度模型**

创建 `src/DeskNote.App/Infrastructure/ISettingsStore.cs`：

```csharp
using DeskNote.App.Models;

namespace DeskNote.App.Infrastructure;

public interface ISettingsStore
{
    Task<AppSettings> LoadAsync(
        string dataDirectory,
        CancellationToken cancellationToken = default);

    Task SaveAsync(
        string dataDirectory,
        AppSettings settings,
        CancellationToken cancellationToken = default);
}
```

把 `JsonSettingsStore` 声明改为：

```csharp
public sealed class JsonSettingsStore : ISettingsStore
```

把 `JsonSettingsStore.LoadAsync` 的反序列化返回语句：

```csharp
return JsonSerializer.Deserialize<AppSettings>(json, Options) ?? new AppSettings();
```

替换为：

```csharp
var settings = JsonSerializer.Deserialize<AppSettings>(json, Options) ?? new AppSettings();
settings.Normalize();
return settings;
```

把 `src/DeskNote.App/Models/AppSettings.cs` 替换为：

```csharp
namespace DeskNote.App.Models;

public sealed class AppSettings
{
    public const int CurrentSchemaVersion = 1;
    public const double MinimumWindowOpacity = 0.20;
    public const double MaximumWindowOpacity = 1.00;
    public const double DefaultWindowOpacity = 1.00;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;
    public ThemeMode Theme { get; set; } = ThemeMode.Light;
    public bool StartWithWindows { get; set; }
    public bool AlwaysOnTop { get; set; }
    public double WindowOpacity { get; set; } = DefaultWindowOpacity;
    public WindowBounds WindowBounds { get; set; } = new();
    public TodoSortDirection IncompleteSortDirection { get; set; } = TodoSortDirection.NewestFirst;

    public void Normalize()
    {
        WindowOpacity = double.IsFinite(WindowOpacity)
            ? Math.Clamp(WindowOpacity, MinimumWindowOpacity, MaximumWindowOpacity)
            : DefaultWindowOpacity;
    }
}
```

- [ ] **Step 4：运行存储测试并确认通过**

Run:

```powershell
dotnet test tests/DeskNote.Tests/DeskNote.Tests.csproj --filter "FullyQualifiedName~JsonSettingsStoreTests"
```

Expected: PASS。

- [ ] **Step 5：先写设置更新串行化与失败隔离测试**

在 `tests/DeskNote.Tests/Services/SettingsServiceTests.cs` 增加以下测试，并在测试类末尾加入 `BlockingSettingsStore`：

```csharp
[Fact]
public async Task UpdateAsync_SerializesConcurrentWritesAndKeepsBothChanges()
{
    var store = new BlockingSettingsStore();
    var service = new SettingsService(
        new JsonDataLocator(Path.Combine(root, "locator.json"), Path.Combine(root, "data")),
        store);
    await service.InitializeAsync();

    var themeUpdate = service.UpdateAsync(settings => settings.Theme = ThemeMode.Dark);
    await store.FirstSaveStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
    var opacityUpdate = service.UpdateAsync(settings => settings.WindowOpacity = 0.55);

    Assert.Equal(1, store.MaximumConcurrentSaves);
    store.ReleaseFirstSave.TrySetResult(true);
    await Task.WhenAll(themeUpdate, opacityUpdate);

    Assert.Equal(1, store.MaximumConcurrentSaves);
    Assert.Equal(ThemeMode.Dark, service.Current.Theme);
    Assert.Equal(0.55, service.Current.WindowOpacity);
    Assert.Equal(ThemeMode.Dark, store.LastSaved!.Theme);
    Assert.Equal(0.55, store.LastSaved.WindowOpacity);
}

[Fact]
public async Task UpdateAsync_FailedWriteRestoresOnlyItsOwnSnapshot()
{
    var store = new BlockingSettingsStore { BlockFirstSave = false };
    var service = new SettingsService(
        new JsonDataLocator(Path.Combine(root, "locator.json"), Path.Combine(root, "data")),
        store);
    await service.InitializeAsync();
    await service.UpdateAsync(settings => settings.Theme = ThemeMode.Dark);
    store.FailNextSave = true;

    await Assert.ThrowsAsync<IOException>(
        () => service.UpdateAsync(settings => settings.WindowOpacity = 0.45));

    Assert.Equal(ThemeMode.Dark, service.Current.Theme);
    Assert.Equal(AppSettings.DefaultWindowOpacity, service.Current.WindowOpacity);
}

private sealed class BlockingSettingsStore : ISettingsStore
{
    private int activeSaves;
    private int saveCount;

    public bool BlockFirstSave { get; set; } = true;
    public bool FailNextSave { get; set; }
    public int MaximumConcurrentSaves { get; private set; }
    public AppSettings? LastSaved { get; private set; }
    public TaskCompletionSource<bool> FirstSaveStarted { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource<bool> ReleaseFirstSave { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task<AppSettings> LoadAsync(
        string dataDirectory,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new AppSettings());

    public async Task SaveAsync(
        string dataDirectory,
        AppSettings settings,
        CancellationToken cancellationToken = default)
    {
        var current = Interlocked.Increment(ref activeSaves);
        MaximumConcurrentSaves = Math.Max(MaximumConcurrentSaves, current);
        var currentSave = Interlocked.Increment(ref saveCount);
        try
        {
            if (BlockFirstSave && currentSave == 1)
            {
                FirstSaveStarted.TrySetResult(true);
                await ReleaseFirstSave.Task.WaitAsync(cancellationToken);
            }

            if (FailNextSave)
            {
                FailNextSave = false;
                throw new IOException("simulated settings write failure");
            }

            LastSaved = Clone(settings);
        }
        finally
        {
            Interlocked.Decrement(ref activeSaves);
        }
    }

    private static AppSettings Clone(AppSettings source) => new()
    {
        SchemaVersion = source.SchemaVersion,
        Theme = source.Theme,
        StartWithWindows = source.StartWithWindows,
        AlwaysOnTop = source.AlwaysOnTop,
        WindowOpacity = source.WindowOpacity,
        IncompleteSortDirection = source.IncompleteSortDirection,
        WindowBounds = new WindowBounds
        {
            Left = source.WindowBounds.Left,
            Top = source.WindowBounds.Top,
            Width = source.WindowBounds.Width,
            Height = source.WindowBounds.Height,
            ScreenDeviceName = source.WindowBounds.ScreenDeviceName
        }
    };
}
```

- [ ] **Step 6：运行服务测试并确认并发测试失败**

Run:

```powershell
dotnet test tests/DeskNote.Tests/DeskNote.Tests.csproj --filter "FullyQualifiedName~SettingsServiceTests"
```

Expected: FAIL；在没有异步互斥门时，`MaximumConcurrentSaves` 变为 2，或最终快照丢失一项变更。

- [ ] **Step 7：串行化全部设置写入并补齐透明度快照**

在 `src/DeskNote.App/Services/SettingsService.cs` 中：

1. 将字段类型改为 `ISettingsStore`，增加更新门：

```csharp
private readonly IDataLocator locator;
private readonly ISettingsStore store;
private readonly SemaphoreSlim updateGate = new(1, 1);
```

2. 将构造函数签名改为：

```csharp
public SettingsService(IDataLocator locator, ISettingsStore store)
```

3. 将 `UpdateAsync` 替换为：

```csharp
public async Task UpdateAsync(
    Action<AppSettings> update,
    CancellationToken cancellationToken = default)
{
    await updateGate.WaitAsync(cancellationToken);
    try
    {
        var previous = Clone(Current);
        update(Current);
        Current.Normalize();
        try
        {
            await store.SaveAsync(DataDirectory, Current, cancellationToken);
        }
        catch
        {
            Current = previous;
            throw;
        }
    }
    finally
    {
        updateGate.Release();
    }

    Changed?.Invoke(this, EventArgs.Empty);
}
```

4. 在 `Clone` 初始化器中增加：

```csharp
WindowOpacity = source.WindowOpacity,
```

- [ ] **Step 8：运行 Task 2 测试与全量测试**

Run:

```powershell
dotnet test tests/DeskNote.Tests/DeskNote.Tests.csproj --filter "FullyQualifiedName~JsonSettingsStoreTests|FullyQualifiedName~SettingsServiceTests"
dotnet test DeskNote.sln
```

Expected: 所有测试通过，构建无警告。

- [ ] **Step 9：暂停用户审查**

汇报设置 JSON 兼容性、范围限制、并发测试和全量测试结果，不执行 Git 操作。等待用户确认 Task 2 后再开始 Task 3。

### Task 3：实现 20%—100% 整窗透明度设置

**Files:**
- Modify: `src/DeskNote.App/AssemblyInfo.cs`
- Modify: `src/DeskNote.App/ViewModels/SettingsViewModel.cs`
- Modify: `src/DeskNote.App/Views/SettingsView.xaml`
- Modify: `src/DeskNote.App/Views/MainWindow.xaml`
- Test: `tests/DeskNote.Tests/ViewModels/SettingsViewModelTests.cs`

- [ ] **Step 1：先写防抖、最终值和失败回滚测试**

在 `src/DeskNote.App/AssemblyInfo.cs` 增加：

```csharp
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("DeskNote.Tests")]
```

在 `tests/DeskNote.Tests/ViewModels/SettingsViewModelTests.cs` 增加：

```csharp
using DeskNote.App.Models;
```

然后在测试类中增加：

```csharp
[Fact]
public async Task WindowOpacity_DebouncesRapidChangesAndPersistsLatestValue()
{
    var store = new RecordingSettingsStore();
    var settings = await CreateSettingsAsync(store);
    var viewModel = CreateViewModel(settings, TimeSpan.FromMilliseconds(20));

    viewModel.WindowOpacity = 0.80;
    viewModel.WindowOpacity = 0.60;
    viewModel.WindowOpacity = 0.35;
    await viewModel.PendingWindowOpacitySave.WaitAsync(TimeSpan.FromSeconds(2));

    Assert.Single(store.SavedOpacityValues);
    Assert.Equal(0.35, store.SavedOpacityValues[0]);
    Assert.Equal("35%", viewModel.WindowOpacityPercent);
    Assert.True(viewModel.IsLowOpacity);
}

[Fact]
public async Task WindowOpacity_LatestFailureRestoresLastPersistedValue()
{
    var store = new RecordingSettingsStore();
    var settings = await CreateSettingsAsync(store);
    var viewModel = CreateViewModel(settings, TimeSpan.FromMilliseconds(10));
    viewModel.WindowOpacity = 0.65;
    await viewModel.PendingWindowOpacitySave.WaitAsync(TimeSpan.FromSeconds(2));
    store.FailNextSave = true;

    viewModel.WindowOpacity = 0.30;
    await viewModel.PendingWindowOpacitySave.WaitAsync(TimeSpan.FromSeconds(2));

    Assert.Equal(0.65, viewModel.WindowOpacity);
    Assert.Equal("无法保存窗口透明度。", viewModel.ErrorMessage);
}

[Fact]
public async Task WindowOpacity_OldSuccessfulSaveBecomesRollbackBaselineWithoutReplacingPreview()
{
    var store = new RecordingSettingsStore { BlockFirstSave = true };
    var settings = await CreateSettingsAsync(store);
    var viewModel = CreateViewModel(settings, TimeSpan.FromMilliseconds(10));
    viewModel.WindowOpacity = 0.70;
    await store.FirstSaveStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));

    viewModel.WindowOpacity = 0.30;
    store.FailSecondSave = true;
    store.ReleaseFirstSave.TrySetResult(true);
    await viewModel.PendingWindowOpacitySave.WaitAsync(TimeSpan.FromSeconds(2));

    Assert.Equal(0.70, viewModel.WindowOpacity);
    Assert.Equal("无法保存窗口透明度。", viewModel.ErrorMessage);
}

private async Task<SettingsService> CreateSettingsAsync(ISettingsStore store)
{
    var settings = new SettingsService(
        new JsonDataLocator(Path.Combine(root, "locator.json"), Path.Combine(root, "data")),
        store);
    await settings.InitializeAsync();
    return settings;
}

private static SettingsViewModel CreateViewModel(
    SettingsService settings,
    TimeSpan saveDelay) => new(
        settings,
        new ThemeService(),
        new NoOpStartupService(),
        new NoOpMigrationService(),
        saveDelay);

private sealed class NoOpStartupService : IStartupService
{
    public bool IsEnabled() => false;
    public void SetEnabled(bool enabled)
    {
    }
}

private sealed class RecordingSettingsStore : ISettingsStore
{
    private int saveCount;

    public bool BlockFirstSave { get; set; }
    public bool FailNextSave { get; set; }
    public bool FailSecondSave { get; set; }
    public List<double> SavedOpacityValues { get; } = [];
    public TaskCompletionSource<bool> FirstSaveStarted { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource<bool> ReleaseFirstSave { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task<AppSettings> LoadAsync(
        string dataDirectory,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new AppSettings());

    public async Task SaveAsync(
        string dataDirectory,
        AppSettings settings,
        CancellationToken cancellationToken = default)
    {
        var currentSave = Interlocked.Increment(ref saveCount);
        if (BlockFirstSave && currentSave == 1)
        {
            FirstSaveStarted.TrySetResult(true);
            await ReleaseFirstSave.Task.WaitAsync(cancellationToken);
        }

        if (FailNextSave || (FailSecondSave && currentSave == 2))
        {
            FailNextSave = false;
            throw new IOException("simulated settings write failure");
        }

        SavedOpacityValues.Add(settings.WindowOpacity);
    }
}
```

保留测试文件中已有的 `FailingStartupService` 和 `NoOpMigrationService`；如果已存在同名 `NoOpStartupService`，复用现有实现而不重复声明。

- [ ] **Step 2：运行 ViewModel 测试并确认失败**

Run:

```powershell
dotnet test tests/DeskNote.Tests/DeskNote.Tests.csproj --filter "FullyQualifiedName~SettingsViewModelTests"
```

Expected: FAIL，`SettingsViewModel` 尚无透明度属性、延迟参数和待处理保存任务。

- [ ] **Step 3：在 SettingsViewModel 中实现实时值与有序防抖保存**

在 `src/DeskNote.App/ViewModels/SettingsViewModel.cs` 增加字段：

```csharp
private readonly TimeSpan windowOpacitySaveDelay;
private readonly SemaphoreSlim windowOpacitySaveGate = new(1, 1);
private CancellationTokenSource? windowOpacityDebounceCancellation;
private long windowOpacityVersion;
private double lastPersistedWindowOpacity;
private bool suppressWindowOpacitySave;
```

把构造函数签名改为：

```csharp
public SettingsViewModel(
    SettingsService settings,
    ThemeService themeService,
    IStartupService startupService,
    IDataMigrationService migrationService,
    TimeSpan? windowOpacitySaveDelay = null)
```

在构造函数现有字段赋值后增加：

```csharp
this.windowOpacitySaveDelay = windowOpacitySaveDelay ?? TimeSpan.FromMilliseconds(300);
windowOpacity = settings.Current.WindowOpacity;
lastPersistedWindowOpacity = settings.Current.WindowOpacity;
```

在可观察属性区域增加：

```csharp
[ObservableProperty]
private double windowOpacity;

public string WindowOpacityPercent => WindowOpacity.ToString("P0");
public bool IsLowOpacity => WindowOpacity < 0.40;
internal Task PendingWindowOpacitySave { get; private set; } = Task.CompletedTask;
```

在类末尾增加：

```csharp
partial void OnWindowOpacityChanged(double value)
{
    OnPropertyChanged(nameof(WindowOpacityPercent));
    OnPropertyChanged(nameof(IsLowOpacity));
    if (suppressWindowOpacitySave)
    {
        return;
    }

    ErrorMessage = null;
    var version = Interlocked.Increment(ref windowOpacityVersion);
    var cancellation = new CancellationTokenSource();
    var previous = Interlocked.Exchange(ref windowOpacityDebounceCancellation, cancellation);
    previous?.Cancel();
    PendingWindowOpacitySave = SaveWindowOpacityAfterDelayAsync(value, version, cancellation);
}

private async Task SaveWindowOpacityAfterDelayAsync(
    double value,
    long version,
    CancellationTokenSource cancellation)
{
    try
    {
        try
        {
            await Task.Delay(windowOpacitySaveDelay, cancellation.Token);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            return;
        }

        await windowOpacitySaveGate.WaitAsync();
        try
        {
            try
            {
                await settings.UpdateAsync(current => current.WindowOpacity = value);
                lastPersistedWindowOpacity = value;
            }
            catch (Exception)
            {
                if (version == Volatile.Read(ref windowOpacityVersion))
                {
                    suppressWindowOpacitySave = true;
                    try
                    {
                        WindowOpacity = lastPersistedWindowOpacity;
                    }
                    finally
                    {
                        suppressWindowOpacitySave = false;
                    }

                    ErrorMessage = "无法保存窗口透明度。";
                }
            }
        }
        finally
        {
            windowOpacitySaveGate.Release();
        }
    }
    finally
    {
        Interlocked.CompareExchange(
            ref windowOpacityDebounceCancellation,
            null,
            cancellation);
        cancellation.Dispose();
    }
}
```

此实现只取消仍在 `Task.Delay` 的旧请求；旧请求一旦进入保存门便完成写入并更新 `lastPersistedWindowOpacity`，但不会回写 `WindowOpacity` 覆盖新预览。

- [ ] **Step 4：运行 ViewModel 测试并确认通过**

Run:

```powershell
dotnet test tests/DeskNote.Tests/DeskNote.Tests.csproj --filter "FullyQualifiedName~SettingsViewModelTests"
```

Expected: PASS。

- [ ] **Step 5：在设置页增加透明度滑块和低透明度提示**

在 `src/DeskNote.App/Views/SettingsView.xaml` 的颜色模式按钮组之后、数据位置标题之前增加：

```xml
<Grid Margin="0,18,0,0">
  <Grid.ColumnDefinitions>
    <ColumnDefinition Width="*" />
    <ColumnDefinition Width="Auto" />
  </Grid.ColumnDefinitions>
  <TextBlock FontWeight="SemiBold" Text="窗口透明度" />
  <TextBlock Grid.Column="1" Text="{Binding WindowOpacityPercent}" />
</Grid>
<Slider Margin="0,8,0,0"
        Minimum="{x:Static models:AppSettings.MinimumWindowOpacity}"
        Maximum="{x:Static models:AppSettings.MaximumWindowOpacity}"
        SmallChange="0.01"
        LargeChange="0.10"
        TickFrequency="0.01"
        IsSnapToTickEnabled="True"
        Value="{Binding WindowOpacity, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}" />
<Grid Margin="0,2,0,0">
  <TextBlock HorizontalAlignment="Left" Foreground="{DynamicResource MutedTextBrush}" Text="20%" />
  <TextBlock HorizontalAlignment="Right" Foreground="{DynamicResource MutedTextBrush}" Text="100%" />
</Grid>
<TextBlock Margin="0,6,0,0"
           Foreground="{DynamicResource MutedTextBrush}"
           TextWrapping="Wrap"
           Text="低于 40% 时，文字和控件可能较难辨认。">
  <TextBlock.Style>
    <Style TargetType="TextBlock">
      <Setter Property="Visibility" Value="Collapsed" />
      <Style.Triggers>
        <DataTrigger Binding="{Binding IsLowOpacity}" Value="True">
          <Setter Property="Visibility" Value="Visible" />
        </DataTrigger>
      </Style.Triggers>
    </Style>
  </TextBlock.Style>
</TextBlock>
```

- [ ] **Step 6：把透明度绑定到整个主窗口**

在 `src/DeskNote.App/Views/MainWindow.xaml` 的 `Window` 根元素增加：

```xml
Opacity="{Binding Settings.WindowOpacity, Mode=OneWay}"
```

不要把透明度只绑定到背景容器；`Window.Opacity` 确保文字、图标和按钮一起变化。恢复窗口不增加该绑定。

- [ ] **Step 7：构建、测试并手工检查持久化**

Run:

```powershell
dotnet test DeskNote.sln
dotnet run --project src/DeskNote.App/DeskNote.App.csproj
```

Expected: 自动化测试全部通过。手工把滑块依次拖到 20%、60%、100%，整个主窗口实时变化；停止拖动后退出并重启，最后数值恢复；恢复窗口仍为 100% 不透明。

- [ ] **Step 8：暂停用户审查**

汇报三档透明度预览、重启恢复和失败回滚测试结果，不执行 Git 操作。等待用户确认 Task 3 后再开始 Task 4。

### Task 4：建立浅紫/白、深紫/黑的玻璃控件体系

**Files:**
- Modify: `src/DeskNote.App/Resources/Themes/Light.xaml`
- Modify: `src/DeskNote.App/Resources/Themes/Dark.xaml`
- Modify: `src/DeskNote.App/Resources/Controls.xaml`
- Modify: `src/DeskNote.App/Views/IncompleteTodosView.xaml`
- Modify: `src/DeskNote.App/Views/CompletedTodosView.xaml`
- Modify: `src/DeskNote.App/Views/TodoEditorView.xaml`
- Modify: `src/DeskNote.App/Views/SettingsView.xaml`
- Modify: `src/DeskNote.App/Views/RecoveryWindow.xaml`
- Test: `tests/DeskNote.Tests/Resources/ThemeResourceContractTests.cs`

- [ ] **Step 1：先写主题资源契约测试**

创建 `tests/DeskNote.Tests/Resources/ThemeResourceContractTests.cs`：

```csharp
using System.Xml.Linq;
using Xunit;

namespace DeskNote.Tests.Resources;

public sealed class ThemeResourceContractTests
{
    private static readonly string ProjectRoot = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    private static readonly XNamespace Xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

    [Theory]
    [InlineData("Light.xaml")]
    [InlineData("Dark.xaml")]
    public void Theme_DefinesAllGlassControlResources(string themeFile)
    {
        var document = LoadTheme(themeFile);
        var keys = document.Descendants()
            .Select(element => (string?)element.Attribute(Xaml + "Key"))
            .Where(key => key is not null)
            .ToHashSet(StringComparer.Ordinal);
        var required = new[]
        {
            "WindowBrush", "FallbackWindowBrush", "NativeBackdropOverlayBrush",
            "PanelBrush", "SidebarBrush", "PrimaryBrush", "TextBrush",
            "MutedTextBrush", "DisabledTextBrush", "BorderBrush",
            "GlassSurfaceBrush", "GlassHoverBrush", "GlassPressedBrush",
            "GlassHighlightBrush", "FocusBrush", "DangerBrush",
            "DangerSurfaceBrush", "SliderTrackBrush", "SliderFillBrush",
            "SliderThumbBrush"
        };

        Assert.All(required, key => Assert.Contains(key, keys));
    }

    [Theory]
    [InlineData("TextBrush")]
    [InlineData("MutedTextBrush")]
    [InlineData("DisabledTextBrush")]
    [InlineData("DangerBrush")]
    public void DarkTheme_TextBrushesUseWhiteHue(string key)
    {
        var color = GetBrushColor(LoadTheme("Dark.xaml"), key);

        Assert.EndsWith("FFFFFF", color, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Controls_DefinesRoundedTemplatesIncludingSlider()
    {
        var controlsPath = Path.Combine(
            ProjectRoot,
            "src",
            "DeskNote.App",
            "Resources",
            "Controls.xaml");
        var xaml = File.ReadAllText(controlsPath);

        Assert.Contains("TargetType=\"{x:Type Button}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("TargetType=\"{x:Type ToggleButton}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("TargetType=\"{x:Type TextBox}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("TargetType=\"{x:Type CheckBox}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("TargetType=\"{x:Type Slider}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("CornerRadius", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("BorderBrush=\"Black\"", xaml, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("#FF000000", xaml, StringComparison.OrdinalIgnoreCase);
    }

    private static XDocument LoadTheme(string fileName) => XDocument.Load(
        Path.Combine(
            ProjectRoot,
            "src",
            "DeskNote.App",
            "Resources",
            "Themes",
            fileName));

    private static string GetBrushColor(XDocument document, string key)
    {
        var element = document.Descendants()
            .Single(candidate => (string?)candidate.Attribute(Xaml + "Key") == key);
        return (string?)element.Attribute("Color")
            ?? throw new InvalidOperationException($"Brush {key} has no Color attribute.");
    }
}
```

- [ ] **Step 2：运行契约测试并确认失败**

Run:

```powershell
dotnet test tests/DeskNote.Tests/DeskNote.Tests.csproj --filter "FullyQualifiedName~ThemeResourceContractTests"
```

Expected: FAIL，现有主题缺少玻璃、滑块和禁用状态资源，控件文件也没有圆角模板。

- [ ] **Step 3：替换浅色主题资源**

把 `src/DeskNote.App/Resources/Themes/Light.xaml` 替换为：

```xml
<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <SolidColorBrush x:Key="WindowBrush" Color="#FFF6F0FF" />
  <SolidColorBrush x:Key="FallbackWindowBrush" Color="#FFF6F0FF" />
  <SolidColorBrush x:Key="NativeBackdropOverlayBrush" Color="#52F0E4FF" />
  <SolidColorBrush x:Key="PanelBrush" Color="#D9FFFFFF" />
  <LinearGradientBrush x:Key="SidebarBrush" StartPoint="0,0" EndPoint="0,1">
    <GradientStop Offset="0" Color="#E8D4B9FF" />
    <GradientStop Offset="1" Color="#CFEBDDFF" />
  </LinearGradientBrush>
  <SolidColorBrush x:Key="PrimaryBrush" Color="#FF7A3FAE" />
  <SolidColorBrush x:Key="TextBrush" Color="#FF332440" />
  <SolidColorBrush x:Key="MutedTextBrush" Color="#FF705F7D" />
  <SolidColorBrush x:Key="DisabledTextBrush" Color="#75332440" />
  <SolidColorBrush x:Key="BorderBrush" Color="#BFFFFFFF" />
  <SolidColorBrush x:Key="GlassSurfaceBrush" Color="#BFFFFFFF" />
  <SolidColorBrush x:Key="GlassHoverBrush" Color="#E8FFFFFF" />
  <SolidColorBrush x:Key="GlassPressedBrush" Color="#BFDCC6F2" />
  <SolidColorBrush x:Key="GlassHighlightBrush" Color="#D9FFFFFF" />
  <SolidColorBrush x:Key="FocusBrush" Color="#FF8A4FC0" />
  <SolidColorBrush x:Key="DangerBrush" Color="#FF9F285C" />
  <SolidColorBrush x:Key="DangerSurfaceBrush" Color="#36C04479" />
  <SolidColorBrush x:Key="SliderTrackBrush" Color="#3D6F4D83" />
  <SolidColorBrush x:Key="SliderFillBrush" Color="#FF8A4FC0" />
  <SolidColorBrush x:Key="SliderThumbBrush" Color="#FFFFFFFF" />
</ResourceDictionary>
```

> **Task 4 执行提示：** 下一段“附录 C”只保存 Task 5 的完整代码清单。执行 Task 4 时先跳过附录 C，从其后的 Step 4 继续；用户批准 Task 4 后，才按文末 Task 5 的步骤使用附录 C。

#### 附录 C：Task 5 Mica 实施细节

**Files:**
- Create: `src/DeskNote.App/Services/WindowBackdropService.cs`
- Modify: `src/DeskNote.App/Services/ThemeService.cs`
- Modify: `src/DeskNote.App/Views/MainWindow.xaml`
- Modify: `src/DeskNote.App/Views/MainWindow.xaml.cs`
- Modify: `src/DeskNote.App/App.xaml.cs`
- Test: `tests/DeskNote.Tests/Services/WindowBackdropServiceTests.cs`

##### C.1 材料状态切换测试

创建 `tests/DeskNote.Tests/Services/WindowBackdropServiceTests.cs`：

```csharp
using DeskNote.App.Models;
using DeskNote.App.Services;
using Xunit;

namespace DeskNote.Tests.Services;

public sealed class WindowBackdropServiceTests
{
    [Fact]
    public void Apply_UsesFallbackWhenOpacityIsBelowOne()
    {
        var api = new FakeWindowBackdropApi { IsNativeMicaSupported = true };
        var service = new WindowBackdropService(api);

        var result = service.Apply((nint)123, ThemeMode.Dark, 0.99);

        Assert.Equal(WindowMaterialState.FallbackGlass, result);
        Assert.Equal(1, api.DisableCalls);
        Assert.Equal(0, api.EnableCalls);
    }

    [Fact]
    public void Apply_UsesNativeMicaWhenEveryRequirementSucceeds()
    {
        var api = new FakeWindowBackdropApi
        {
            IsNativeMicaSupported = true,
            EnableResult = true
        };
        var service = new WindowBackdropService(api);

        var result = service.Apply((nint)123, ThemeMode.Dark, 1.0);

        Assert.Equal(WindowMaterialState.NativeMica, result);
        Assert.True(api.LastDarkMode);
        Assert.Equal(1, api.EnableCalls);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void Apply_FallsBackWhenPlatformOrNativeCallFails(
        bool isSupported,
        bool enableResult)
    {
        var api = new FakeWindowBackdropApi
        {
            IsNativeMicaSupported = isSupported,
            EnableResult = enableResult
        };
        var service = new WindowBackdropService(api);

        var result = service.Apply((nint)123, ThemeMode.Light, 1.0);

        Assert.Equal(WindowMaterialState.FallbackGlass, result);
        Assert.Equal(1, api.DisableCalls);
    }

    private sealed class FakeWindowBackdropApi : IWindowBackdropApi
    {
        public bool IsNativeMicaSupported { get; set; }
        public bool EnableResult { get; set; }
        public bool LastDarkMode { get; private set; }
        public int EnableCalls { get; private set; }
        public int DisableCalls { get; private set; }

        public bool TryEnableMica(nint windowHandle, bool darkMode)
        {
            EnableCalls++;
            LastDarkMode = darkMode;
            return EnableResult;
        }

        public void DisableMica(nint windowHandle) => DisableCalls++;
    }
}
```

##### C.2 首次测试命令

Run:

```powershell
dotnet test tests/DeskNote.Tests/DeskNote.Tests.csproj --filter "FullyQualifiedName~WindowBackdropServiceTests"
```

Expected: FAIL，窗口背板类型和服务尚不存在。

##### C.3 Mica 能力封装

创建 `src/DeskNote.App/Services/WindowBackdropService.cs`：

```csharp
using System.Runtime.InteropServices;
using DeskNote.App.Models;

namespace DeskNote.App.Services;

public enum WindowMaterialState
{
    NativeMica,
    FallbackGlass
}

internal interface IWindowBackdropApi
{
    bool IsNativeMicaSupported { get; }
    bool TryEnableMica(nint windowHandle, bool darkMode);
    void DisableMica(nint windowHandle);
}

public sealed class WindowBackdropService
{
    private readonly IWindowBackdropApi api;

    public WindowBackdropService()
        : this(new DwmWindowBackdropApi())
    {
    }

    internal WindowBackdropService(IWindowBackdropApi api) => this.api = api;

    public WindowMaterialState Apply(
        nint windowHandle,
        ThemeMode theme,
        double opacity)
    {
        if (windowHandle == 0
            || opacity < AppSettings.MaximumWindowOpacity
            || !api.IsNativeMicaSupported)
        {
            if (windowHandle != 0)
            {
                api.DisableMica(windowHandle);
            }

            return WindowMaterialState.FallbackGlass;
        }

        if (api.TryEnableMica(windowHandle, theme == ThemeMode.Dark))
        {
            return WindowMaterialState.NativeMica;
        }

        api.DisableMica(windowHandle);
        return WindowMaterialState.FallbackGlass;
    }
}

internal sealed class DwmWindowBackdropApi : IWindowBackdropApi
{
    private const int DwmwaUseImmersiveDarkMode = 20;
    private const int DwmwaWindowCornerPreference = 33;
    private const int DwmwaSystemBackdropType = 38;
    private const int DwmWindowCornerPreferenceRound = 2;
    private const int DwmSystemBackdropTypeNone = 1;
    private const int DwmSystemBackdropTypeMainWindow = 2;

    public bool IsNativeMicaSupported =>
        OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22621);

    public bool TryEnableMica(nint windowHandle, bool darkMode)
    {
        if (!IsNativeMicaSupported
            || DwmIsCompositionEnabled(out var compositionEnabled) != 0
            || !compositionEnabled)
        {
            return false;
        }

        var darkValue = darkMode ? 1 : 0;
        var cornerValue = DwmWindowCornerPreferenceRound;
        var backdropValue = DwmSystemBackdropTypeMainWindow;
        return DwmSetWindowAttribute(
                   windowHandle,
                   DwmwaUseImmersiveDarkMode,
                   ref darkValue,
                   sizeof(int)) == 0
               && DwmSetWindowAttribute(
                   windowHandle,
                   DwmwaWindowCornerPreference,
                   ref cornerValue,
                   sizeof(int)) == 0
               && DwmSetWindowAttribute(
                   windowHandle,
                   DwmwaSystemBackdropType,
                   ref backdropValue,
                   sizeof(int)) == 0;
    }

    public void DisableMica(nint windowHandle)
    {
        if (!IsNativeMicaSupported || windowHandle == 0)
        {
            return;
        }

        var backdropValue = DwmSystemBackdropTypeNone;
        _ = DwmSetWindowAttribute(
            windowHandle,
            DwmwaSystemBackdropType,
            ref backdropValue,
            sizeof(int));
    }

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmIsCompositionEnabled(
        [MarshalAs(UnmanagedType.Bool)] out bool enabled);

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(
        nint windowHandle,
        int attribute,
        ref int value,
        int valueSize);
}
```

##### C.4 材料状态测试命令

Run:

```powershell
dotnet test tests/DeskNote.Tests/DeskNote.Tests.csproj --filter "FullyQualifiedName~WindowBackdropServiceTests"
```

Expected: PASS；测试不直接调用真实 DWM，因此可稳定验证平台、透明度和失败分支。

##### C.5 主题变更通知

在 `src/DeskNote.App/Services/ThemeService.cs` 的类中增加：

```csharp
public event Action<AppThemeMode>? Changed;
```

在 `Apply` 方法最后、主题资源字典插入完成后增加：

```csharp
Changed?.Invoke(mode);
```

##### C.6 主窗口材料承载层

在 `src/DeskNote.App/Views/MainWindow.xaml` 中执行以下准确修改：

1. 把 `Window` 的背景改为透明，保留 Task 3 的 `Opacity` 绑定：

```xml
Background="Transparent"
Foreground="{DynamicResource TextBrush}"
Opacity="{Binding Settings.WindowOpacity, Mode=OneWay}"
```

2. 把 `WindowChrome` 改为：

```xml
<shell:WindowChrome CaptionHeight="34"
                    ResizeBorderThickness="6"
                    CornerRadius="18" />
```

3. 把当前根 `Border` 开始标签：

```xml
<Border BorderBrush="{DynamicResource BorderBrush}" BorderThickness="1">
```

替换为：

```xml
<Border x:Name="RootSurface"
        Background="{DynamicResource FallbackWindowBrush}"
        BorderThickness="0"
        ClipToBounds="True">
  <Border.Style>
    <Style TargetType="Border">
      <Setter Property="CornerRadius" Value="18" />
      <Style.Triggers>
        <DataTrigger Binding="{Binding RelativeSource={RelativeSource AncestorType=Window}, Path=WindowState}"
                     Value="Maximized">
          <Setter Property="CornerRadius" Value="0" />
        </DataTrigger>
      </Style.Triggers>
    </Style>
  </Border.Style>
```

保留该 `Border` 现有的闭合标签、标题栏、侧边栏和内容区。不可删除 `ResizeBorderThickness`，因为它负责无可见边框时的窗口缩放命中。

##### C.7 主窗口材料生命周期

在 `src/DeskNote.App/Views/MainWindow.xaml.cs` 增加引用：

```csharp
using System.Windows.Interop;
using System.Windows.Media;
using DeskNote.App.Models;
using DeskNote.App.Services;
```

在 `MainWindow` 类中增加常量和字段：

```csharp
private const int WmSettingChange = 0x001A;
private const int WmThemeChanged = 0x031A;
private const int WmDwmCompositionChanged = 0x031E;

private readonly ThemeService themeService;
private readonly WindowBackdropService backdropService;
private HwndSource? windowSource;
```

把构造函数替换为：

```csharp
public MainWindow(
    ThemeService themeService,
    WindowBackdropService backdropService)
{
    this.themeService = themeService;
    this.backdropService = backdropService;
    InitializeComponent();
    SourceInitialized += OnSourceInitialized;
    Closed += OnClosed;
    themeService.Changed += OnThemeChanged;
}
```

在类末尾增加：

```csharp
protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
{
    base.OnPropertyChanged(e);
    if (e.Property == OpacityProperty && IsInitialized)
    {
        ApplyWindowMaterial();
    }
}

private void OnSourceInitialized(object? sender, EventArgs e)
{
    windowSource = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
    windowSource?.AddHook(WindowMessageHook);
    ApplyWindowMaterial();
}

private void OnClosed(object? sender, EventArgs e)
{
    themeService.Changed -= OnThemeChanged;
    windowSource?.RemoveHook(WindowMessageHook);
    windowSource = null;
}

private void OnThemeChanged(ThemeMode mode) => ApplyWindowMaterial(mode);

private nint WindowMessageHook(
    nint windowHandle,
    int message,
    nint wParam,
    nint lParam,
    ref bool handled)
{
    if (message is WmSettingChange or WmThemeChanged or WmDwmCompositionChanged)
    {
        Dispatcher.BeginInvoke(new Action(ApplyWindowMaterial));
    }

    return 0;
}

private void ApplyWindowMaterial()
{
    var theme = DataContext is ViewModels.MainViewModel viewModel
        ? viewModel.Settings.Theme
        : ThemeMode.Light;
    ApplyWindowMaterial(theme);
}

private void ApplyWindowMaterial(ThemeMode theme)
{
    if (RootSurface is null)
    {
        return;
    }

    var handle = new WindowInteropHelper(this).Handle;
    SetResourceReference(BackgroundProperty, "FallbackWindowBrush");
    RootSurface.SetResourceReference(
        System.Windows.Controls.Border.BackgroundProperty,
        "FallbackWindowBrush");
    var state = backdropService.Apply(handle, theme, Opacity);
    if (state == WindowMaterialState.NativeMica)
    {
        Background = Brushes.Transparent;
        RootSurface.SetResourceReference(
            System.Windows.Controls.Border.BackgroundProperty,
            "NativeBackdropOverlayBrush");
    }
}
```

在现有 `ShowAndActivate` 方法的 `Show()` 后增加：

```csharp
ApplyWindowMaterial();
```

这保证窗口从托盘恢复时重新检查系统组合状态。每次重算先恢复能独立保证可读性的后备背景，再让服务关闭旧 Mica 或尝试启用新 Mica；只有启用成功后才切换为透明覆盖层，因此失败时不会留下全透明根层。

##### C.8 应用组合根

在 `src/DeskNote.App/App.xaml.cs` 创建主窗口的位置，把：

```csharp
var window = new AppMainWindow
```

改为：

```csharp
var window = new AppMainWindow(themeService, new WindowBackdropService())
```

保留后续对象初始化器中的 `DataContext` 和 `Topmost`。

##### C.9 验证要求

Run:

```powershell
dotnet test DeskNote.sln
dotnet run --project src/DeskNote.App/DeskNote.App.csproj
```

Expected: 所有测试通过。Windows 11 在 100% 透明度时进入 Mica；调到 99% 或更低时立即使用紫色后备玻璃，回到 100% 时重新尝试 Mica；主题切换和托盘恢复不重置透明度。Windows 10 始终使用后备玻璃且不抛出 DWM 异常。

- [ ] **Step 4：替换控件资源**

把 `src/DeskNote.App/Resources/Controls.xaml` 替换为本 Task 末尾“附录 A：Controls.xaml 完整内容”中的完整 XAML。附录包含 `Button`、`ToggleButton`、`TextBox`、`CheckBox`、`ListBoxItem`、`Slider` 和 `ProgressBar` 模板，不能只复制其中一部分。

- [ ] **Step 5：替换深色主题资源**

把 `src/DeskNote.App/Resources/Themes/Dark.xaml` 替换为本 Task 末尾“附录 B：Dark.xaml 完整内容”中的完整 XAML。完成后确认 `TextBrush`、`MutedTextBrush`、`DisabledTextBrush` 和 `DangerBrush` 都以 `FFFFFF` 为 RGB 色相。

- [ ] **Step 6：让页面使用玻璃卡片和危险状态资源**

在 `src/DeskNote.App/Views/IncompleteTodosView.xaml` 和 `CompletedTodosView.xaml` 中，把待办项外层 `Border` 的以下属性：

```xml
Margin="0,4" Padding="10" CornerRadius="10"
Background="{DynamicResource PanelBrush}"
BorderBrush="{DynamicResource BorderBrush}" BorderThickness="1"
```

替换为：

```xml
Margin="0,4" Style="{StaticResource GlassCardStyle}"
```

在 `IncompleteTodosView.xaml` 的删除按钮、`CompletedTodosView.xaml` 的删除和全部清空按钮上增加：

```xml
Style="{StaticResource DangerButtonStyle}"
```

在 `TodoEditorView.xaml` 中把错误 `TextBlock` 替换为：

```xml
<Border Grid.Row="3" Margin="0,8" Padding="8"
        Background="{DynamicResource DangerSurfaceBrush}"
        CornerRadius="9">
  <TextBlock Foreground="{DynamicResource DangerBrush}"
             TextWrapping="Wrap"
             Text="{Binding ErrorMessage}" />
</Border>
```

把 `src/DeskNote.App/Views/SettingsView.xaml` 替换为以下完整布局，使透明度滑块和其它设置使用同一玻璃分组：

```xml
<UserControl x:Class="DeskNote.App.Views.SettingsView"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:models="clr-namespace:DeskNote.App.Models">
  <ScrollViewer VerticalScrollBarVisibility="Auto">
    <StackPanel>
      <TextBlock FontSize="22" FontWeight="SemiBold" Text="设置" />
      <Border Margin="0,8,0,0" Padding="8"
              Background="{DynamicResource DangerSurfaceBrush}"
              CornerRadius="9">
        <TextBlock Foreground="{DynamicResource DangerBrush}"
                   TextWrapping="Wrap"
                   Text="{Binding ErrorMessage}" />
      </Border>

      <Border Margin="0,12,0,0" Style="{StaticResource GlassCardStyle}">
        <StackPanel>
          <TextBlock FontWeight="SemiBold" Text="窗口" />
          <CheckBox Content="开机自启动" IsChecked="{Binding StartWithWindows, Mode=OneWay}">
            <CheckBox.Command><Binding Path="SetStartWithWindowsCommand" /></CheckBox.Command>
            <CheckBox.CommandParameter><Binding RelativeSource="{RelativeSource Self}" Path="IsChecked" /></CheckBox.CommandParameter>
          </CheckBox>
          <CheckBox Content="窗口置顶" IsChecked="{Binding AlwaysOnTop, Mode=OneWay}">
            <CheckBox.Command><Binding Path="SetAlwaysOnTopCommand" /></CheckBox.Command>
            <CheckBox.CommandParameter><Binding RelativeSource="{RelativeSource Self}" Path="IsChecked" /></CheckBox.CommandParameter>
          </CheckBox>
        </StackPanel>
      </Border>

      <Border Margin="0,12,0,0" Style="{StaticResource GlassCardStyle}">
        <StackPanel>
          <TextBlock FontWeight="SemiBold" Text="颜色模式" />
          <StackPanel Margin="0,6,0,0" Orientation="Horizontal">
            <Button Content="浅色" Command="{Binding SetThemeCommand}"
                    CommandParameter="{x:Static models:ThemeMode.Light}" />
            <Button Content="深色" Command="{Binding SetThemeCommand}"
                    CommandParameter="{x:Static models:ThemeMode.Dark}" />
          </StackPanel>
        </StackPanel>
      </Border>

      <Border Margin="0,12,0,0" Style="{StaticResource GlassCardStyle}">
        <StackPanel>
          <Grid>
            <Grid.ColumnDefinitions>
              <ColumnDefinition Width="*" />
              <ColumnDefinition Width="Auto" />
            </Grid.ColumnDefinitions>
            <TextBlock FontWeight="SemiBold" Text="窗口透明度" />
            <TextBlock Grid.Column="1" Text="{Binding WindowOpacityPercent}" />
          </Grid>
          <Slider Margin="0,8,0,0"
                  Minimum="{x:Static models:AppSettings.MinimumWindowOpacity}"
                  Maximum="{x:Static models:AppSettings.MaximumWindowOpacity}"
                  SmallChange="0.01" LargeChange="0.10" TickFrequency="0.01"
                  IsSnapToTickEnabled="True"
                  Value="{Binding WindowOpacity, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}" />
          <Grid Margin="0,2,0,0">
            <TextBlock HorizontalAlignment="Left" Foreground="{DynamicResource MutedTextBrush}" Text="20%" />
            <TextBlock HorizontalAlignment="Right" Foreground="{DynamicResource MutedTextBrush}" Text="100%" />
          </Grid>
          <TextBlock Margin="0,6,0,0"
                     Foreground="{DynamicResource MutedTextBrush}"
                     TextWrapping="Wrap"
                     Text="低于 40% 时，文字和控件可能较难辨认。">
            <TextBlock.Style>
              <Style TargetType="TextBlock" BasedOn="{StaticResource {x:Type TextBlock}}">
                <Setter Property="Visibility" Value="Collapsed" />
                <Style.Triggers>
                  <DataTrigger Binding="{Binding IsLowOpacity}" Value="True">
                    <Setter Property="Visibility" Value="Visible" />
                  </DataTrigger>
                </Style.Triggers>
              </Style>
            </TextBlock.Style>
          </TextBlock>
        </StackPanel>
      </Border>

      <Border Margin="0,12,0,0" Style="{StaticResource GlassCardStyle}">
        <StackPanel>
          <TextBlock FontWeight="SemiBold" Text="数据位置" />
          <TextBlock Margin="0,6,0,0" TextWrapping="Wrap"
                     Foreground="{DynamicResource MutedTextBrush}"
                     Text="{Binding DataDirectory}" />
          <Button Margin="0,8,0,0" HorizontalAlignment="Left"
                  Click="OnChooseDataDirectory" Content="选择并迁移…" />
          <ProgressBar Margin="0,8" IsIndeterminate="True">
            <ProgressBar.Style>
              <Style TargetType="ProgressBar" BasedOn="{StaticResource {x:Type ProgressBar}}">
                <Setter Property="Visibility" Value="Collapsed" />
                <Style.Triggers>
                  <DataTrigger Binding="{Binding IsMigrating}" Value="True">
                    <Setter Property="Visibility" Value="Visible" />
                  </DataTrigger>
                </Style.Triggers>
              </Style>
            </ProgressBar.Style>
          </ProgressBar>
        </StackPanel>
      </Border>
    </StackPanel>
  </ScrollViewer>
</UserControl>
```

在 `src/DeskNote.App/Views/RecoveryWindow.xaml` 的 `Window` 根元素增加：

```xml
Background="{DynamicResource WindowBrush}"
Foreground="{DynamicResource TextBrush}"
```

并把恢复按钮外层 `StackPanel` 中的退出按钮设置为普通玻璃按钮，恢复按钮保持普通主操作样式；不要给恢复窗口绑定 `WindowOpacity`。

- [ ] **Step 7：应用标题栏与侧边栏专用样式**

在 `src/DeskNote.App/Views/MainWindow.xaml` 中：

- 置顶 `ToggleButton` 增加 `Style="{StaticResource TitleBarToggleButtonStyle}"`，删除其显式 `Width`。
- 最小化、最大化和隐藏三个按钮增加 `Style="{StaticResource TitleBarButtonStyle}"`，删除各自显式 `Width`。
- 四个侧边栏按钮增加 `Style="{StaticResource SidebarButtonStyle}"`，删除显式 `Height` 和 `HorizontalContentAlignment`。

这些改动只替换视觉属性，不改变命令、参数、工具提示或侧边栏动画事件。

- [ ] **Step 8：运行资源测试、全量测试和主题手工验收**

Run:

```powershell
dotnet test tests/DeskNote.Tests/DeskNote.Tests.csproj --filter "FullyQualifiedName~ThemeResourceContractTests"
dotnet test DeskNote.sln
dotnet run --project src/DeskNote.App/DeskNote.App.csproj
```

Expected: 全部测试通过，XAML 编译无错误。

手工验收浅色和深色下的标题栏、侧边栏、编辑页、两个列表、设置页和恢复窗口。检查按钮、置顶切换、文本框、复选框、列表项和滑块的默认、悬停、按下、聚焦、选中和禁用状态；不得出现黑色硬边框，深色所有文字均为白色色相。

- [ ] **Step 9：暂停用户审查**

提供浅色与深色界面截图或让用户直接运行审查，汇报资源测试和全量测试结果，不执行 Git 操作。等待用户确认 Task 4 后再开始 Task 5。


#### 附录 A：Controls.xaml 完整内容

把 `src/DeskNote.App/Resources/Controls.xaml` 替换为：

```xml
<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <Style TargetType="{x:Type TextBlock}">
    <Setter Property="Foreground" Value="{DynamicResource TextBrush}" />
  </Style>

  <Style x:Key="GlassCardStyle" TargetType="{x:Type Border}">
    <Setter Property="Background" Value="{DynamicResource PanelBrush}" />
    <Setter Property="BorderBrush" Value="{DynamicResource GlassHighlightBrush}" />
    <Setter Property="BorderThickness" Value="1" />
    <Setter Property="CornerRadius" Value="14" />
    <Setter Property="Padding" Value="12" />
  </Style>

  <Style TargetType="{x:Type Button}">
    <Setter Property="Foreground" Value="{DynamicResource TextBrush}" />
    <Setter Property="Background" Value="{DynamicResource GlassSurfaceBrush}" />
    <Setter Property="BorderBrush" Value="{DynamicResource BorderBrush}" />
    <Setter Property="BorderThickness" Value="1" />
    <Setter Property="Padding" Value="11,7" />
    <Setter Property="Margin" Value="3" />
    <Setter Property="Cursor" Value="Hand" />
    <Setter Property="FocusVisualStyle" Value="{x:Null}" />
    <Setter Property="Template">
      <Setter.Value>
        <ControlTemplate TargetType="{x:Type Button}">
          <Border x:Name="Chrome"
                  Background="{TemplateBinding Background}"
                  BorderBrush="{TemplateBinding BorderBrush}"
                  BorderThickness="{TemplateBinding BorderThickness}"
                  CornerRadius="11"
                  SnapsToDevicePixels="True">
            <Grid>
              <ContentPresenter Margin="{TemplateBinding Padding}"
                                HorizontalAlignment="{TemplateBinding HorizontalContentAlignment}"
                                VerticalAlignment="{TemplateBinding VerticalContentAlignment}"
                                RecognizesAccessKey="True"
                                TextElement.Foreground="{TemplateBinding Foreground}" />
              <Border IsHitTestVisible="False"
                      BorderBrush="{DynamicResource GlassHighlightBrush}"
                      BorderThickness="1,1,0,0"
                      CornerRadius="10" />
            </Grid>
          </Border>
          <ControlTemplate.Triggers>
            <Trigger Property="IsMouseOver" Value="True">
              <Setter TargetName="Chrome" Property="Background" Value="{DynamicResource GlassHoverBrush}" />
            </Trigger>
            <Trigger Property="IsPressed" Value="True">
              <Setter TargetName="Chrome" Property="Background" Value="{DynamicResource GlassPressedBrush}" />
            </Trigger>
            <Trigger Property="IsKeyboardFocused" Value="True">
              <Setter TargetName="Chrome" Property="BorderBrush" Value="{DynamicResource FocusBrush}" />
              <Setter TargetName="Chrome" Property="BorderThickness" Value="2" />
            </Trigger>
            <Trigger Property="IsEnabled" Value="False">
              <Setter Property="Foreground" Value="{DynamicResource DisabledTextBrush}" />
              <Setter TargetName="Chrome" Property="Opacity" Value="0.55" />
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <Style x:Key="TitleBarButtonStyle"
         TargetType="{x:Type Button}"
         BasedOn="{StaticResource {x:Type Button}}">
    <Setter Property="Width" Value="38" />
    <Setter Property="Height" Value="30" />
    <Setter Property="Margin" Value="1,2" />
    <Setter Property="Padding" Value="0" />
    <Setter Property="Background" Value="Transparent" />
  </Style>

  <Style x:Key="SidebarButtonStyle"
         TargetType="{x:Type Button}"
         BasedOn="{StaticResource {x:Type Button}}">
    <Setter Property="Height" Value="42" />
    <Setter Property="Margin" Value="0,2" />
    <Setter Property="Padding" Value="8,4" />
    <Setter Property="HorizontalContentAlignment" Value="Left" />
    <Setter Property="Background" Value="Transparent" />
  </Style>

  <Style x:Key="DangerButtonStyle"
         TargetType="{x:Type Button}"
         BasedOn="{StaticResource {x:Type Button}}">
    <Setter Property="Foreground" Value="{DynamicResource DangerBrush}" />
    <Setter Property="Background" Value="{DynamicResource DangerSurfaceBrush}" />
  </Style>

  <Style TargetType="{x:Type ToggleButton}">
    <Setter Property="Foreground" Value="{DynamicResource TextBrush}" />
    <Setter Property="Background" Value="Transparent" />
    <Setter Property="BorderBrush" Value="{DynamicResource BorderBrush}" />
    <Setter Property="BorderThickness" Value="1" />
    <Setter Property="Padding" Value="8,6" />
    <Setter Property="FocusVisualStyle" Value="{x:Null}" />
    <Setter Property="Template">
      <Setter.Value>
        <ControlTemplate TargetType="{x:Type ToggleButton}">
          <Border x:Name="Chrome"
                  Background="{TemplateBinding Background}"
                  BorderBrush="{TemplateBinding BorderBrush}"
                  BorderThickness="{TemplateBinding BorderThickness}"
                  CornerRadius="10">
            <ContentPresenter Margin="{TemplateBinding Padding}"
                              HorizontalAlignment="Center"
                              VerticalAlignment="Center"
                              TextElement.Foreground="{TemplateBinding Foreground}" />
          </Border>
          <ControlTemplate.Triggers>
            <Trigger Property="IsMouseOver" Value="True">
              <Setter TargetName="Chrome" Property="Background" Value="{DynamicResource GlassHoverBrush}" />
            </Trigger>
            <Trigger Property="IsPressed" Value="True">
              <Setter TargetName="Chrome" Property="Background" Value="{DynamicResource GlassPressedBrush}" />
            </Trigger>
            <Trigger Property="IsChecked" Value="True">
              <Setter TargetName="Chrome" Property="Background" Value="{DynamicResource GlassPressedBrush}" />
              <Setter TargetName="Chrome" Property="BorderBrush" Value="{DynamicResource PrimaryBrush}" />
            </Trigger>
            <Trigger Property="IsKeyboardFocused" Value="True">
              <Setter TargetName="Chrome" Property="BorderBrush" Value="{DynamicResource FocusBrush}" />
              <Setter TargetName="Chrome" Property="BorderThickness" Value="2" />
            </Trigger>
            <Trigger Property="IsEnabled" Value="False">
              <Setter Property="Foreground" Value="{DynamicResource DisabledTextBrush}" />
              <Setter TargetName="Chrome" Property="Opacity" Value="0.55" />
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <Style x:Key="TitleBarToggleButtonStyle"
         TargetType="{x:Type ToggleButton}"
         BasedOn="{StaticResource {x:Type ToggleButton}}">
    <Setter Property="Width" Value="38" />
    <Setter Property="Height" Value="30" />
    <Setter Property="Margin" Value="1,2" />
    <Setter Property="Padding" Value="0" />
  </Style>

  <Style TargetType="{x:Type TextBox}">
    <Setter Property="Foreground" Value="{DynamicResource TextBrush}" />
    <Setter Property="Background" Value="{DynamicResource GlassSurfaceBrush}" />
    <Setter Property="BorderBrush" Value="{DynamicResource BorderBrush}" />
    <Setter Property="BorderThickness" Value="1" />
    <Setter Property="Padding" Value="9,7" />
    <Setter Property="Margin" Value="0,4" />
    <Setter Property="CaretBrush" Value="{DynamicResource TextBrush}" />
    <Setter Property="FocusVisualStyle" Value="{x:Null}" />
    <Setter Property="Template">
      <Setter.Value>
        <ControlTemplate TargetType="{x:Type TextBox}">
          <Border x:Name="Chrome"
                  Background="{TemplateBinding Background}"
                  BorderBrush="{TemplateBinding BorderBrush}"
                  BorderThickness="{TemplateBinding BorderThickness}"
                  CornerRadius="11">
            <ScrollViewer x:Name="PART_ContentHost" Margin="{TemplateBinding Padding}" />
          </Border>
          <ControlTemplate.Triggers>
            <Trigger Property="IsMouseOver" Value="True">
              <Setter TargetName="Chrome" Property="Background" Value="{DynamicResource GlassHoverBrush}" />
            </Trigger>
            <Trigger Property="IsKeyboardFocused" Value="True">
              <Setter TargetName="Chrome" Property="BorderBrush" Value="{DynamicResource FocusBrush}" />
              <Setter TargetName="Chrome" Property="BorderThickness" Value="2" />
            </Trigger>
            <Trigger Property="IsEnabled" Value="False">
              <Setter Property="Foreground" Value="{DynamicResource DisabledTextBrush}" />
              <Setter TargetName="Chrome" Property="Opacity" Value="0.55" />
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <Style TargetType="{x:Type CheckBox}">
    <Setter Property="Foreground" Value="{DynamicResource TextBrush}" />
    <Setter Property="Margin" Value="0,8" />
    <Setter Property="FocusVisualStyle" Value="{x:Null}" />
    <Setter Property="Template">
      <Setter.Value>
        <ControlTemplate TargetType="{x:Type CheckBox}">
          <Grid Background="Transparent">
            <Grid.ColumnDefinitions>
              <ColumnDefinition Width="Auto" />
              <ColumnDefinition Width="*" />
            </Grid.ColumnDefinitions>
            <Border x:Name="Box"
                    Width="20" Height="20"
                    Background="{DynamicResource GlassSurfaceBrush}"
                    BorderBrush="{DynamicResource BorderBrush}"
                    BorderThickness="1"
                    CornerRadius="6">
              <TextBlock x:Name="CheckMark"
                         HorizontalAlignment="Center"
                         VerticalAlignment="Center"
                         FontSize="13"
                         FontWeight="Bold"
                         Foreground="{DynamicResource TextBrush}"
                         Text="✓"
                         Visibility="Collapsed" />
            </Border>
            <ContentPresenter Grid.Column="1"
                              Margin="9,0,0,0"
                              VerticalAlignment="Center"
                              RecognizesAccessKey="True"
                              TextElement.Foreground="{TemplateBinding Foreground}" />
          </Grid>
          <ControlTemplate.Triggers>
            <Trigger Property="IsMouseOver" Value="True">
              <Setter TargetName="Box" Property="Background" Value="{DynamicResource GlassHoverBrush}" />
            </Trigger>
            <Trigger Property="IsChecked" Value="True">
              <Setter TargetName="Box" Property="Background" Value="{DynamicResource GlassPressedBrush}" />
              <Setter TargetName="Box" Property="BorderBrush" Value="{DynamicResource PrimaryBrush}" />
              <Setter TargetName="CheckMark" Property="Visibility" Value="Visible" />
            </Trigger>
            <Trigger Property="IsKeyboardFocused" Value="True">
              <Setter TargetName="Box" Property="BorderBrush" Value="{DynamicResource FocusBrush}" />
              <Setter TargetName="Box" Property="BorderThickness" Value="2" />
            </Trigger>
            <Trigger Property="IsEnabled" Value="False">
              <Setter Property="Foreground" Value="{DynamicResource DisabledTextBrush}" />
              <Setter TargetName="Box" Property="Opacity" Value="0.55" />
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <Style TargetType="{x:Type ListBox}">
    <Setter Property="Foreground" Value="{DynamicResource TextBrush}" />
    <Setter Property="Background" Value="Transparent" />
    <Setter Property="BorderThickness" Value="0" />
    <Setter Property="ScrollViewer.CanContentScroll" Value="True" />
    <Setter Property="VirtualizingPanel.IsVirtualizing" Value="True" />
    <Setter Property="VirtualizingPanel.VirtualizationMode" Value="Recycling" />
  </Style>

  <Style TargetType="{x:Type ListBoxItem}">
    <Setter Property="Foreground" Value="{DynamicResource TextBrush}" />
    <Setter Property="Background" Value="Transparent" />
    <Setter Property="HorizontalContentAlignment" Value="Stretch" />
    <Setter Property="FocusVisualStyle" Value="{x:Null}" />
    <Setter Property="Template">
      <Setter.Value>
        <ControlTemplate TargetType="{x:Type ListBoxItem}">
          <Border x:Name="Chrome"
                  Background="{TemplateBinding Background}"
                  BorderBrush="Transparent"
                  BorderThickness="1"
                  CornerRadius="13">
            <ContentPresenter HorizontalAlignment="{TemplateBinding HorizontalContentAlignment}"
                              VerticalAlignment="{TemplateBinding VerticalContentAlignment}" />
          </Border>
          <ControlTemplate.Triggers>
            <Trigger Property="IsMouseOver" Value="True">
              <Setter TargetName="Chrome" Property="Background" Value="{DynamicResource GlassHoverBrush}" />
            </Trigger>
            <Trigger Property="IsSelected" Value="True">
              <Setter TargetName="Chrome" Property="Background" Value="{DynamicResource GlassPressedBrush}" />
              <Setter TargetName="Chrome" Property="BorderBrush" Value="{DynamicResource PrimaryBrush}" />
            </Trigger>
            <Trigger Property="IsKeyboardFocused" Value="True">
              <Setter TargetName="Chrome" Property="BorderBrush" Value="{DynamicResource FocusBrush}" />
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>
```

继续在同一个 `ResourceDictionary` 中追加滑块和进度条模板（不要在前一段末尾提前写 `</ResourceDictionary>`）：

```xml
  <Style x:Key="SliderDecreaseButtonStyle" TargetType="{x:Type RepeatButton}">
    <Setter Property="Focusable" Value="False" />
    <Setter Property="Template">
      <Setter.Value>
        <ControlTemplate TargetType="{x:Type RepeatButton}">
          <Border Height="5" VerticalAlignment="Center"
                  Background="{DynamicResource SliderFillBrush}"
                  CornerRadius="3" />
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <Style x:Key="SliderIncreaseButtonStyle" TargetType="{x:Type RepeatButton}">
    <Setter Property="Focusable" Value="False" />
    <Setter Property="Template">
      <Setter.Value>
        <ControlTemplate TargetType="{x:Type RepeatButton}">
          <Border Height="5" VerticalAlignment="Center"
                  Background="{DynamicResource SliderTrackBrush}"
                  CornerRadius="3" />
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <Style x:Key="GlassSliderThumbStyle" TargetType="{x:Type Thumb}">
    <Setter Property="Width" Value="18" />
    <Setter Property="Height" Value="18" />
    <Setter Property="Template">
      <Setter.Value>
        <ControlTemplate TargetType="{x:Type Thumb}">
          <Border x:Name="ThumbSurface"
                  Background="{DynamicResource SliderThumbBrush}"
                  BorderBrush="{DynamicResource GlassHighlightBrush}"
                  BorderThickness="1"
                  CornerRadius="9" />
          <ControlTemplate.Triggers>
            <Trigger Property="IsMouseOver" Value="True">
              <Setter TargetName="ThumbSurface" Property="BorderBrush" Value="{DynamicResource FocusBrush}" />
            </Trigger>
            <Trigger Property="IsDragging" Value="True">
              <Setter TargetName="ThumbSurface" Property="Background" Value="{DynamicResource SliderFillBrush}" />
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <Style TargetType="{x:Type Slider}">
    <Setter Property="Height" Value="32" />
    <Setter Property="FocusVisualStyle" Value="{x:Null}" />
    <Setter Property="Template">
      <Setter.Value>
        <ControlTemplate TargetType="{x:Type Slider}">
          <Border x:Name="FocusRing"
                  Padding="3"
                  Background="Transparent"
                  BorderBrush="Transparent"
                  BorderThickness="1"
                  CornerRadius="12">
            <Track x:Name="PART_Track">
              <Track.DecreaseRepeatButton>
                <RepeatButton Command="{x:Static Slider.DecreaseLarge}"
                              Style="{StaticResource SliderDecreaseButtonStyle}" />
              </Track.DecreaseRepeatButton>
              <Track.Thumb>
                <Thumb x:Name="PART_Thumb" Style="{StaticResource GlassSliderThumbStyle}" />
              </Track.Thumb>
              <Track.IncreaseRepeatButton>
                <RepeatButton Command="{x:Static Slider.IncreaseLarge}"
                              Style="{StaticResource SliderIncreaseButtonStyle}" />
              </Track.IncreaseRepeatButton>
            </Track>
          </Border>
          <ControlTemplate.Triggers>
            <Trigger Property="IsKeyboardFocused" Value="True">
              <Setter TargetName="FocusRing" Property="BorderBrush" Value="{DynamicResource FocusBrush}" />
            </Trigger>
            <Trigger Property="IsMouseOver" Value="True">
              <Setter TargetName="FocusRing" Property="Background" Value="{DynamicResource GlassSurfaceBrush}" />
            </Trigger>
            <Trigger Property="IsEnabled" Value="False">
              <Setter TargetName="FocusRing" Property="Opacity" Value="0.45" />
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <Style TargetType="{x:Type ProgressBar}">
    <Setter Property="Foreground" Value="{DynamicResource PrimaryBrush}" />
    <Setter Property="Background" Value="{DynamicResource SliderTrackBrush}" />
    <Setter Property="Height" Value="5" />
    <Setter Property="Template">
      <Setter.Value>
        <ControlTemplate TargetType="{x:Type ProgressBar}">
          <Border Background="{TemplateBinding Background}" CornerRadius="3">
            <Grid x:Name="PART_Track" ClipToBounds="True">
              <Border x:Name="PART_Indicator"
                      HorizontalAlignment="Left"
                      Background="{TemplateBinding Foreground}"
                      CornerRadius="3" />
            </Grid>
          </Border>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>
</ResourceDictionary>
```


#### 附录 B：Dark.xaml 完整内容

把 `src/DeskNote.App/Resources/Themes/Dark.xaml` 替换为：

```xml
<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <SolidColorBrush x:Key="WindowBrush" Color="#FF07040B" />
  <SolidColorBrush x:Key="FallbackWindowBrush" Color="#FF07040B" />
  <SolidColorBrush x:Key="NativeBackdropOverlayBrush" Color="#7A160B1F" />
  <SolidColorBrush x:Key="PanelBrush" Color="#D9160B1F" />
  <LinearGradientBrush x:Key="SidebarBrush" StartPoint="0,0" EndPoint="0,1">
    <GradientStop Offset="0" Color="#ED3A165A" />
    <GradientStop Offset="1" Color="#E01B092A" />
  </LinearGradientBrush>
  <SolidColorBrush x:Key="PrimaryBrush" Color="#FFAE73E6" />
  <SolidColorBrush x:Key="TextBrush" Color="#FFFFFFFF" />
  <SolidColorBrush x:Key="MutedTextBrush" Color="#BFFFFFFF" />
  <SolidColorBrush x:Key="DisabledTextBrush" Color="#70FFFFFF" />
  <SolidColorBrush x:Key="BorderBrush" Color="#33FFFFFF" />
  <SolidColorBrush x:Key="GlassSurfaceBrush" Color="#2EFFFFFF" />
  <SolidColorBrush x:Key="GlassHoverBrush" Color="#45FFFFFF" />
  <SolidColorBrush x:Key="GlassPressedBrush" Color="#3EAE73E6" />
  <SolidColorBrush x:Key="GlassHighlightBrush" Color="#38FFFFFF" />
  <SolidColorBrush x:Key="FocusBrush" Color="#FFE0C1FF" />
  <SolidColorBrush x:Key="DangerBrush" Color="#FFFFFFFF" />
  <SolidColorBrush x:Key="DangerSurfaceBrush" Color="#A389234B" />
  <SolidColorBrush x:Key="SliderTrackBrush" Color="#42FFFFFF" />
  <SolidColorBrush x:Key="SliderFillBrush" Color="#FFAE73E6" />
  <SolidColorBrush x:Key="SliderThumbBrush" Color="#FFFFFFFF" />
</ResourceDictionary>
```

### Task 5：实现 Windows 11 Mica 与安全后备玻璃切换

完整文件路径、测试代码、实现代码和命令见本计划“附录 C：Task 5 Mica 实施细节”。以下步骤必须按顺序执行；附录 C 只是代码清单，不代表可在 Task 4 完成前提前实施。

- [ ] **Step 1：写材料状态切换测试**

按附录 C.1 创建 `tests/DeskNote.Tests/Services/WindowBackdropServiceTests.cs`。

- [ ] **Step 2：运行失败测试**

运行附录 C.2 的单测命令，确认因背板服务尚不存在而失败。

- [ ] **Step 3：实现背板服务**

按附录 C.3 创建 `src/DeskNote.App/Services/WindowBackdropService.cs`，完整保留可测试接口、Windows 11 版本检查、DWM 组合检查和失败降级。

- [ ] **Step 4：运行材料状态测试**

运行附录 C.4 的命令并确认通过。

- [ ] **Step 5：增加主题变更通知**

按附录 C.5 修改 `ThemeService.cs`。

- [ ] **Step 6：建立主窗口材料承载层**

按附录 C.6 修改 `MainWindow.xaml`，移除可见硬边框但保留缩放命中区域。

- [ ] **Step 7：接入窗口生命周期**

按附录 C.7 修改 `MainWindow.xaml.cs`，确保句柄创建、透明度、主题、系统组合消息和托盘恢复都会重新计算材料状态。

- [ ] **Step 8：在组合根注入服务**

按附录 C.8 修改 `App.xaml.cs`。

- [ ] **Step 9：验证并暂停用户审查**

执行附录 C.9 的全量测试和 Windows 11/Windows 10 验收。汇报结果，不执行 Git 操作；等待用户确认 Task 5 后再开始 Task 6。

### Task 6：完成发布构建和 Windows 视觉验收

**Files:**
- Modify: `docs/testing/windows-acceptance.md`
- Verify: `src/DeskNote.App/Resources/desk-note.ico`
- Verify: `packaging/desk-note.iss`
- Verify generated: `src/DeskNote.App/bin/Release/net10.0-windows/win-x64/publish/desk-note.exe`
- Verify generated: `packaging/output/desk-note-1.0.0-win-x64.exe`

- [ ] **Step 1：把新视觉要求加入 Windows 验收清单**

在 `docs/testing/windows-acceptance.md` 末尾增加：

```markdown
## 紫色玻璃主题、图标与透明度

- [ ] Windows 11、透明度 100%：主窗口使用 Mica，浅色为浅紫/白，深色为深紫/黑。
- [ ] Windows 10：主窗口使用 WPF 紫色磨砂后备背景，应用启动和主题切换无 DWM 错误。
- [ ] 系统关闭透明效果或远程桌面：窗口自动使用可读的后备背景。
- [ ] 透明度 20%、40%、60%、80%、100%：整个窗口（背景、文字、图标、按钮）同步变化。
- [ ] 透明度低于 100% 时使用 `FallbackGlass`，恢复 100% 后重新尝试 `NativeMica`。
- [ ] 快速拖动滑块无明显卡顿，停止拖动后自动保存，重启恢复最后值。
- [ ] 透明度低于 40% 时显示可读性提示，但仍允许保存 20%—39%。
- [ ] 浅色和深色逐页检查标题栏、侧边栏、编辑、未完成、已完成、设置和恢复窗口。
- [ ] 深色模式全部文字使用白色色相，次要和禁用文字只通过透明度区分。
- [ ] 按钮、置顶切换、文本框、复选框、列表项、滑块无黑色硬边框，圆角一致。
- [ ] 用鼠标和键盘检查默认、悬停、按下、选中、聚焦、禁用状态和滑块 1% 步进。
- [ ] 主窗口最大化时无圆角缝隙，窗口边缘仍可缩放，侧边栏悬停动画正常。
- [ ] 主窗口、任务栏、资源管理器、托盘、恢复窗口、安装程序、快捷方式和卸载项显示统一图标。
- [ ] 100%、125%、150%、200% DPI 下图标清晰、文字不裁切、圆角无明显锯齿。
```

- [ ] **Step 2：运行干净的自动化验证**

Run:

```powershell
dotnet clean DeskNote.sln
dotnet build DeskNote.sln -c Release
dotnet test DeskNote.sln -c Release --no-build
```

Expected: 构建和测试全部成功，零警告、零错误。

- [ ] **Step 3：重新生成图标并确认没有漂移**

Run:

```powershell
python scripts/generate-icon.py
python -c "from PIL import Image; image=Image.open(r'src/DeskNote.App/Resources/desk-note.ico'); print(image.format, sorted(size[0] for size in image.ico.sizes()))"
```

Expected:

```text
ICO [16, 20, 24, 32, 40, 48, 64, 128, 256]
```

- [ ] **Step 4：发布 win-x64 应用并检查 EXE 图标**

Run:

```powershell
dotnet publish src/DeskNote.App/DeskNote.App.csproj -c Release -r win-x64 --self-contained true
Add-Type -AssemblyName System.Drawing
$publishedExe = Resolve-Path 'src\DeskNote.App\bin\Release\net10.0-windows\win-x64\publish\desk-note.exe'
$publishedIcon = [System.Drawing.Icon]::ExtractAssociatedIcon($publishedExe.Path)
if ($null -eq $publishedIcon) { throw 'Published executable has no associated icon.' }
$publishedIcon.Dispose()
```

Expected: 发布成功，`ExtractAssociatedIcon` 返回非空图标。

- [ ] **Step 5：使用已安装的 Inno Setup 生成安装包**

Run:

```powershell
$iscc = Get-Command ISCC.exe -ErrorAction SilentlyContinue
if ($null -eq $iscc) {
    $isccPath = 'C:\Program Files (x86)\Inno Setup 6\ISCC.exe'
} else {
    $isccPath = $iscc.Source
}
if (-not (Test-Path -LiteralPath $isccPath)) { throw 'ISCC.exe was not found.' }
& $isccPath 'packaging\desk-note.iss'
```

Expected: `packaging/output/desk-note-1.0.0-win-x64.exe` 生成成功，编译器无错误。

- [ ] **Step 6：执行 Windows 11 交互验收**

按新增清单逐项检查。重点记录：

- 100% 时 `NativeMica`，99% 时立刻变为 `FallbackGlass`，恢复 100% 后重新启用 Mica。
- 深色模式每一页的文字、占位内容、错误提示、按钮内容和时间文字均为白色色相。
- 20% 透明度虽然较淡，但窗口仍可用，且不会变成 0% 或鼠标穿透。
- 托盘隐藏/显示、单实例唤醒、最小化、最大化和主题切换不重置透明度。

- [ ] **Step 7：执行 Windows 10 或兼容环境验收**

在 Windows 10 实机或项目约定的测试环境中安装发布包，确认始终使用 WPF 后备玻璃，无启动错误、黑色窗口边缘或未处理 DWM 异常。若当前只有 Windows 11 环境，必须把该项明确记录为待人工环境验证，不能伪报通过。

- [ ] **Step 8：汇总结果并暂停最终审查**

向用户汇报：

- Release 构建与测试数量。
- ICO 尺寸、发布 EXE 和安装包路径。
- Windows 11 各材料状态与五档透明度结果。
- Windows 10 验收结果或明确的环境限制。
- 仍需人工确认的项目。

不执行 Git 操作。等待用户完成最终审查和手动版本控制。
