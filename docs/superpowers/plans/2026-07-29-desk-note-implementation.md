# desk-note 实现计划

> **面向执行代理：** 必须使用 `superpowers:subagent-driven-development`（推荐）或 `superpowers:executing-plans` 按任务执行本计划。所有步骤使用复选框跟踪。项目 Git 操作由用户手动完成，执行代理不得运行 `git add`、`git commit`、合并或推送命令。

**目标：** 从空项目构建一个支持 Windows 10/11 的轻量 WPF 桌面待办程序，具备桌面便签、悬停侧边栏、托盘、主题、自启动、SQLite 持久化和安全数据迁移。

**架构：** 使用单个 WPF 应用项目承载界面、ViewModel、应用服务和基础设施，通过接口保持模块边界；使用独立 xUnit 项目测试业务逻辑、SQLite、文件存储和迁移。程序单进程、事件驱动，用户操作提交成功后才更新界面。

**技术栈：** .NET 10 LTS、C#、WPF、CommunityToolkit.Mvvm、Microsoft.Data.Sqlite、xUnit、System.Text.Json、System.Windows.Forms.NotifyIcon、Inno Setup。

---

## 实施前提

当前机器能找到 `dotnet` 主机，但没有安装任何 .NET SDK。开始 Task 1 前必须安装 .NET 10 SDK。安装行为需要网络和系统变更授权，执行时先向用户请求许可。

建议命令：

```powershell
winget install --id Microsoft.DotNet.SDK.10 --exact
```

安装后验证：

```powershell
dotnet --version
dotnet --list-sdks
```

预期：`dotnet --version` 输出 `10.0.x`，SDK 列表至少包含一条 `10.0.x`。

## 文件结构与职责

```text
desk-note/
├── DeskNote.sln
├── Directory.Build.props
├── src/DeskNote.App/
│   ├── DeskNote.App.csproj
│   ├── App.xaml
│   ├── App.xaml.cs
│   ├── Models/
│   │   ├── TodoItem.cs
│   │   ├── TodoInput.cs
│   │   ├── AppSettings.cs
│   │   ├── WindowBounds.cs
│   │   └── Enums.cs
│   ├── Validation/TodoInputValidator.cs
│   ├── Repositories/
│   │   ├── ITodoRepository.cs
│   │   ├── SqliteTodoRepository.cs
│   │   └── DatabaseInitializer.cs
│   ├── Services/
│   │   ├── ITodoService.cs
│   │   ├── TodoService.cs
│   │   ├── GatedTodoService.cs
│   │   ├── DataOperationGate.cs
│   │   ├── SettingsService.cs
│   │   ├── DataMigrationService.cs
│   │   ├── RecoveryService.cs
│   │   ├── ConfirmationService.cs
│   │   ├── ThemeService.cs
│   │   ├── WindowStateService.cs
│   │   ├── StartupService.cs
│   │   ├── SingleInstanceService.cs
│   │   ├── TrayIconController.cs
│   │   └── AppLogger.cs
│   ├── Infrastructure/
│   │   ├── AtomicFile.cs
│   │   ├── DataPaths.cs
│   │   ├── JsonDataLocator.cs
│   │   ├── JsonSettingsStore.cs
│   │   └── FileSystemFacade.cs
│   ├── ViewModels/
│   │   ├── MainViewModel.cs
│   │   ├── TodoEditorViewModel.cs
│   │   ├── TodoListViewModel.cs
│   │   └── SettingsViewModel.cs
│   ├── Views/
│   │   ├── MainWindow.xaml
│   │   ├── MainWindow.xaml.cs
│   │   ├── TodoEditorView.xaml
│   │   ├── TodoEditorView.xaml.cs
│   │   ├── IncompleteTodosView.xaml
│   │   ├── IncompleteTodosView.xaml.cs
│   │   ├── CompletedTodosView.xaml
│   │   ├── CompletedTodosView.xaml.cs
│   │   ├── SettingsView.xaml
│   │   ├── SettingsView.xaml.cs
│   │   ├── RecoveryWindow.xaml
│   │   └── RecoveryWindow.xaml.cs
│   ├── Resources/
│   │   ├── Controls.xaml
│   │   └── Themes/
│   │       ├── Light.xaml
│   │       └── Dark.xaml
│   └── Properties/PublishProfiles/win-x64.pubxml
├── tests/DeskNote.Tests/
│   ├── DeskNote.Tests.csproj
│   ├── Validation/TodoInputValidatorTests.cs
│   ├── Services/TodoServiceTests.cs
│   ├── Services/SettingsServiceTests.cs
│   ├── Services/DataMigrationServiceTests.cs
│   ├── Services/RecoveryServiceTests.cs
│   ├── Infrastructure/JsonDataLocatorTests.cs
│   ├── Infrastructure/JsonSettingsStoreTests.cs
│   ├── Repositories/SqliteTodoRepositoryTests.cs
│   ├── ViewModels/TodoListViewModelTests.cs
│   └── ViewModels/SettingsViewModelTests.cs
├── packaging/desk-note.iss
├── scripts/measure-performance.ps1
├── tools/DeskNote.PerformanceData/
│   ├── DeskNote.PerformanceData.csproj
│   └── Program.cs
└── docs/testing/windows-acceptance.md
```

---

### Task 1：创建解决方案与可构建的 WPF 骨架

**文件：**

- 创建：`DeskNote.sln`
- 创建：`Directory.Build.props`
- 创建：`src/DeskNote.App/DeskNote.App.csproj`
- 创建：`tests/DeskNote.Tests/DeskNote.Tests.csproj`
- 修改：`src/DeskNote.App/App.xaml`
- 修改：`src/DeskNote.App/App.xaml.cs`

- [ ] **Step 1：确认 .NET 10 SDK 可用**

运行：

```powershell
dotnet --version
```

预期：输出 `10.0.x`。若提示没有 SDK，停止实施并按“实施前提”请求安装授权。

- [ ] **Step 2：生成解决方案、WPF 项目和测试项目**

运行：

```powershell
dotnet new sln --name DeskNote --format sln
dotnet new wpf --name DeskNote.App --output src/DeskNote.App --framework net10.0
dotnet new xunit --name DeskNote.Tests --output tests/DeskNote.Tests --framework net10.0
dotnet sln DeskNote.sln add src/DeskNote.App/DeskNote.App.csproj
dotnet sln DeskNote.sln add tests/DeskNote.Tests/DeskNote.Tests.csproj
dotnet add tests/DeskNote.Tests/DeskNote.Tests.csproj reference src/DeskNote.App/DeskNote.App.csproj
dotnet add src/DeskNote.App/DeskNote.App.csproj package CommunityToolkit.Mvvm
dotnet add src/DeskNote.App/DeskNote.App.csproj package Microsoft.Data.Sqlite
```

预期：所有命令退出码为 0，解决方案包含两个项目。

- [ ] **Step 3：统一编译设置**

创建 `Directory.Build.props`：

```xml
<Project>
  <PropertyGroup>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <LangVersion>latest</LangVersion>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <Deterministic>true</Deterministic>
  </PropertyGroup>
</Project>
```

将 `src/DeskNote.App/DeskNote.App.csproj` 调整为：

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net10.0-windows</TargetFramework>
    <UseWPF>true</UseWPF>
    <UseWindowsForms>true</UseWindowsForms>
    <AssemblyName>desk-note</AssemblyName>
    <RootNamespace>DeskNote.App</RootNamespace>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="CommunityToolkit.Mvvm" Version="8.4.0" />
    <PackageReference Include="Microsoft.Data.Sqlite" Version="10.0.0" />
  </ItemGroup>
</Project>
```

将 `tests/DeskNote.Tests/DeskNote.Tests.csproj` 调整为：

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0-windows</TargetFramework>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.14.1" />
    <PackageReference Include="xunit" Version="2.9.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="3.1.5">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\src\DeskNote.App\DeskNote.App.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 4：建立最小应用入口**

将 `src/DeskNote.App/App.xaml` 设置为：

```xml
<Application x:Class="DeskNote.App.App"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <Application.Resources />
</Application>
```

将 `src/DeskNote.App/App.xaml.cs` 设置为：

```csharp
using System.Windows;

namespace DeskNote.App;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
    }
}
```

- [ ] **Step 5：验证初始构建和测试**

运行：

```powershell
dotnet build DeskNote.sln -c Debug
dotnet test DeskNote.sln -c Debug --no-build
```

预期：构建成功、模板测试通过、0 个警告。

- [ ] **Step 6：停下并交由用户手动记录 Git 版本**

向用户报告创建的文件和验证结果，不运行任何 Git 写操作。

---

### Task 2：实现待办模型与输入校验

**文件：**

- 创建：`src/DeskNote.App/Models/TodoItem.cs`
- 创建：`src/DeskNote.App/Models/TodoInput.cs`
- 创建：`src/DeskNote.App/Models/Enums.cs`
- 创建：`src/DeskNote.App/Validation/TodoInputValidator.cs`
- 创建：`tests/DeskNote.Tests/Validation/TodoInputValidatorTests.cs`

- [ ] **Step 1：先写失败的输入校验测试**

创建 `tests/DeskNote.Tests/Validation/TodoInputValidatorTests.cs`：

```csharp
using DeskNote.App.Validation;

namespace DeskNote.Tests.Validation;

public sealed class TodoInputValidatorTests
{
    [Fact]
    public void Validate_TrimsValidInput()
    {
        var result = TodoInputValidator.Validate("  Read book  ", "  Chapter 1  ");

        Assert.True(result.IsValid);
        Assert.Equal("Read book", result.Title);
        Assert.Equal("Chapter 1", result.Note);
        Assert.Null(result.ErrorMessage);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_RejectsEmptyTitle(string title)
    {
        var result = TodoInputValidator.Validate(title, null);

        Assert.False(result.IsValid);
        Assert.Equal("待办标题不能为空。", result.ErrorMessage);
    }

    [Fact]
    public void Validate_RejectsTitleLongerThanTwoHundredCharacters()
    {
        var result = TodoInputValidator.Validate(new string('a', 201), null);

        Assert.False(result.IsValid);
        Assert.Equal("待办标题不能超过 200 个字符。", result.ErrorMessage);
    }

    [Fact]
    public void Validate_RejectsNoteLongerThanTwoThousandCharacters()
    {
        var result = TodoInputValidator.Validate("Valid", new string('a', 2001));

        Assert.False(result.IsValid);
        Assert.Equal("待办备注不能超过 2000 个字符。", result.ErrorMessage);
    }
}
```

- [ ] **Step 2：运行测试并确认失败**

运行：

```powershell
dotnet test tests/DeskNote.Tests/DeskNote.Tests.csproj --filter TodoInputValidatorTests
```

预期：FAIL，原因是 `TodoInputValidator` 尚不存在。

- [ ] **Step 3：实现模型和最小校验逻辑**

创建 `src/DeskNote.App/Models/Enums.cs`：

```csharp
namespace DeskNote.App.Models;

public enum ThemeMode
{
    Light,
    Dark
}

public enum TodoSortDirection
{
    NewestFirst,
    OldestFirst
}
```

创建 `src/DeskNote.App/Models/TodoItem.cs`：

```csharp
namespace DeskNote.App.Models;

public sealed record TodoItem(
    Guid Id,
    string Title,
    string? Note,
    bool IsCompleted,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? CompletedAt);
```

创建 `src/DeskNote.App/Models/TodoInput.cs`：

```csharp
namespace DeskNote.App.Models;

public sealed record TodoInput(string Title, string? Note);

public sealed record TodoValidationResult(
    bool IsValid,
    string Title,
    string? Note,
    string? ErrorMessage);
```

创建 `src/DeskNote.App/Validation/TodoInputValidator.cs`：

```csharp
using DeskNote.App.Models;

namespace DeskNote.App.Validation;

public static class TodoInputValidator
{
    public static TodoValidationResult Validate(string? title, string? note)
    {
        var normalizedTitle = title?.Trim() ?? string.Empty;
        var normalizedNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();

        if (normalizedTitle.Length == 0)
        {
            return Invalid(normalizedTitle, normalizedNote, "待办标题不能为空。");
        }

        if (normalizedTitle.Length > 200)
        {
            return Invalid(normalizedTitle, normalizedNote, "待办标题不能超过 200 个字符。");
        }

        if (normalizedNote is { Length: > 2000 })
        {
            return Invalid(normalizedTitle, normalizedNote, "待办备注不能超过 2000 个字符。");
        }

        return new TodoValidationResult(true, normalizedTitle, normalizedNote, null);
    }

    private static TodoValidationResult Invalid(string title, string? note, string message) =>
        new(false, title, note, message);
}
```

- [ ] **Step 4：运行校验测试并确认通过**

运行：

```powershell
dotnet test tests/DeskNote.Tests/DeskNote.Tests.csproj --filter TodoInputValidatorTests
```

预期：4 个测试通过。

- [ ] **Step 5：运行完整测试并停下供用户手动记录 Git 版本**

运行：

```powershell
dotnet test DeskNote.sln
```

预期：全部通过。报告结果，不运行 Git 写操作。

---

### Task 3：实现数据定位与原子设置存储

**文件：**

- 创建：`src/DeskNote.App/Models/AppSettings.cs`
- 创建：`src/DeskNote.App/Models/WindowBounds.cs`
- 创建：`src/DeskNote.App/Infrastructure/DataPaths.cs`
- 创建：`src/DeskNote.App/Infrastructure/AtomicFile.cs`
- 创建：`src/DeskNote.App/Infrastructure/JsonDataLocator.cs`
- 创建：`src/DeskNote.App/Infrastructure/JsonSettingsStore.cs`
- 创建：`tests/DeskNote.Tests/Infrastructure/JsonDataLocatorTests.cs`
- 创建：`tests/DeskNote.Tests/Infrastructure/JsonSettingsStoreTests.cs`

- [ ] **Step 1：先写数据定位和设置存储测试**

创建 `tests/DeskNote.Tests/Infrastructure/JsonDataLocatorTests.cs`：

```csharp
using DeskNote.App.Infrastructure;

namespace DeskNote.Tests.Infrastructure;

public sealed class JsonDataLocatorTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), $"desk-note-{Guid.NewGuid():N}");

    [Fact]
    public async Task GetOrCreateAsync_CreatesDefaultAbsolutePath()
    {
        var locator = new JsonDataLocator(
            Path.Combine(root, "locator.json"),
            Path.Combine(root, "default-data"));

        var result = await locator.GetOrCreateAsync();

        Assert.Equal(Path.GetFullPath(Path.Combine(root, "default-data")), result);
        Assert.True(File.Exists(Path.Combine(root, "locator.json")));
    }

    [Fact]
    public async Task SetAsync_ReplacesExistingLocation()
    {
        var locator = new JsonDataLocator(
            Path.Combine(root, "locator.json"),
            Path.Combine(root, "default-data"));

        await locator.SetAsync(Path.Combine(root, "new-data"));

        Assert.Equal(Path.GetFullPath(Path.Combine(root, "new-data")), await locator.GetOrCreateAsync());
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, true);
        }
    }
}
```

创建 `tests/DeskNote.Tests/Infrastructure/JsonSettingsStoreTests.cs`：

```csharp
using DeskNote.App.Infrastructure;
using DeskNote.App.Models;

namespace DeskNote.Tests.Infrastructure;

public sealed class JsonSettingsStoreTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), $"desk-note-{Guid.NewGuid():N}");

    [Fact]
    public async Task LoadAsync_ReturnsDefaultsWhenFileDoesNotExist()
    {
        var store = new JsonSettingsStore();

        var settings = await store.LoadAsync(root);

        Assert.Equal(ThemeMode.Light, settings.Theme);
        Assert.Equal(TodoSortDirection.NewestFirst, settings.IncompleteSortDirection);
    }

    [Fact]
    public async Task SaveAsync_RoundTripsSettings()
    {
        var store = new JsonSettingsStore();
        var expected = new AppSettings
        {
            Theme = ThemeMode.Dark,
            AlwaysOnTop = true,
            StartWithWindows = true,
            IncompleteSortDirection = TodoSortDirection.OldestFirst
        };

        await store.SaveAsync(root, expected);
        var actual = await store.LoadAsync(root);

        Assert.Equal(expected.Theme, actual.Theme);
        Assert.Equal(expected.AlwaysOnTop, actual.AlwaysOnTop);
        Assert.Equal(expected.StartWithWindows, actual.StartWithWindows);
        Assert.Equal(expected.IncompleteSortDirection, actual.IncompleteSortDirection);
    }

    [Fact]
    public async Task LoadAsync_QuarantinesInvalidJson()
    {
        Directory.CreateDirectory(root);
        await File.WriteAllTextAsync(Path.Combine(root, "settings.json"), "not-json");
        var store = new JsonSettingsStore();

        var settings = await store.LoadAsync(root);

        Assert.Equal(ThemeMode.Light, settings.Theme);
        Assert.Single(Directory.GetFiles(root, "settings.corrupt-*.json"));
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, true);
        }
    }
}
```

- [ ] **Step 2：运行测试并确认缺少类型**

运行：

```powershell
dotnet test tests/DeskNote.Tests/DeskNote.Tests.csproj --filter "JsonDataLocatorTests|JsonSettingsStoreTests"
```

预期：FAIL，缺少设置与基础设施类型。

- [ ] **Step 3：实现设置模型与路径对象**

创建 `src/DeskNote.App/Models/WindowBounds.cs`：

```csharp
namespace DeskNote.App.Models;

public sealed class WindowBounds
{
    public double? Left { get; set; }
    public double? Top { get; set; }
    public double Width { get; set; } = 420;
    public double Height { get; set; } = 600;
    public string? ScreenDeviceName { get; set; }
}
```

创建 `src/DeskNote.App/Models/AppSettings.cs`：

```csharp
namespace DeskNote.App.Models;

public sealed class AppSettings
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;
    public ThemeMode Theme { get; set; } = ThemeMode.Light;
    public bool StartWithWindows { get; set; }
    public bool AlwaysOnTop { get; set; }
    public WindowBounds WindowBounds { get; set; } = new();
    public TodoSortDirection IncompleteSortDirection { get; set; } = TodoSortDirection.NewestFirst;
}
```

创建 `src/DeskNote.App/Infrastructure/DataPaths.cs`：

```csharp
namespace DeskNote.App.Infrastructure;

public sealed record DataPaths(string DataDirectory)
{
    public string DatabaseFile => Path.Combine(DataDirectory, "todos.db");
    public string SettingsFile => Path.Combine(DataDirectory, "settings.json");
    public string BackupsDirectory => Path.Combine(DataDirectory, "backups");
    public string LogsDirectory => Path.Combine(DataDirectory, "logs");
}
```

- [ ] **Step 4：实现原子文件替换和定位文件**

创建 `src/DeskNote.App/Infrastructure/AtomicFile.cs`：

```csharp
using System.Text;

namespace DeskNote.App.Infrastructure;

public static class AtomicFile
{
    public static async Task WriteAllTextAsync(
        string path,
        string content,
        CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(path)
            ?? throw new InvalidOperationException("The target file has no directory.");
        Directory.CreateDirectory(directory);

        var temporaryPath = $"{path}.tmp-{Guid.NewGuid():N}";
        try
        {
            await using (var stream = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                4096,
                FileOptions.WriteThrough | FileOptions.Asynchronous))
            await using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
            {
                await writer.WriteAsync(content.AsMemory(), cancellationToken);
                await writer.FlushAsync(cancellationToken);
                stream.Flush(true);
            }

            File.Move(temporaryPath, path, true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }
}
```

创建 `src/DeskNote.App/Infrastructure/JsonDataLocator.cs`：

```csharp
using System.Text.Json;

namespace DeskNote.App.Infrastructure;

public sealed class JsonDataLocator
{
    private readonly string locatorFile;
    private readonly string defaultDataDirectory;

    public JsonDataLocator(string? locatorFile = null, string? defaultDataDirectory = null)
    {
        var appRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "desk-note");
        this.locatorFile = locatorFile ?? Path.Combine(appRoot, "locator.json");
        this.defaultDataDirectory = defaultDataDirectory ?? Path.Combine(appRoot, "data");
    }

    public async Task<string> GetOrCreateAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(locatorFile))
        {
            await SetAsync(defaultDataDirectory, cancellationToken);
        }

        var json = await File.ReadAllTextAsync(locatorFile, cancellationToken);
        var model = JsonSerializer.Deserialize<LocatorModel>(json)
            ?? throw new InvalidDataException("数据定位文件为空。");
        return Normalize(model.DataDirectory);
    }

    public Task SetAsync(string dataDirectory, CancellationToken cancellationToken = default)
    {
        var model = new LocatorModel(Normalize(dataDirectory));
        var json = JsonSerializer.Serialize(model, new JsonSerializerOptions { WriteIndented = true });
        return AtomicFile.WriteAllTextAsync(locatorFile, json, cancellationToken);
    }

    private static string Normalize(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("数据目录不能为空。", nameof(path));
        }

        return Path.GetFullPath(path);
    }

    private sealed record LocatorModel(string DataDirectory);
}
```

- [ ] **Step 5：实现设置的加载、隔离和保存**

创建 `src/DeskNote.App/Infrastructure/JsonSettingsStore.cs`：

```csharp
using System.Text.Json;
using System.Text.Json.Serialization;
using DeskNote.App.Models;

namespace DeskNote.App.Infrastructure;

public sealed class JsonSettingsStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public async Task<AppSettings> LoadAsync(
        string dataDirectory,
        CancellationToken cancellationToken = default)
    {
        var path = new DataPaths(Path.GetFullPath(dataDirectory)).SettingsFile;
        if (!File.Exists(path))
        {
            return new AppSettings();
        }

        try
        {
            var json = await File.ReadAllTextAsync(path, cancellationToken);
            return JsonSerializer.Deserialize<AppSettings>(json, Options) ?? new AppSettings();
        }
        catch (JsonException)
        {
            var quarantine = Path.Combine(
                Path.GetDirectoryName(path)!,
                $"settings.corrupt-{DateTime.UtcNow:yyyyMMddHHmmssfff}.json");
            File.Move(path, quarantine);
            return new AppSettings();
        }
    }

    public Task SaveAsync(
        string dataDirectory,
        AppSettings settings,
        CancellationToken cancellationToken = default)
    {
        settings.SchemaVersion = AppSettings.CurrentSchemaVersion;
        var path = new DataPaths(Path.GetFullPath(dataDirectory)).SettingsFile;
        var json = JsonSerializer.Serialize(settings, Options);
        return AtomicFile.WriteAllTextAsync(path, json, cancellationToken);
    }
}
```

- [ ] **Step 6：运行针对性测试和完整测试**

运行：

```powershell
dotnet test tests/DeskNote.Tests/DeskNote.Tests.csproj --filter "JsonDataLocatorTests|JsonSettingsStoreTests"
dotnet test DeskNote.sln
```

预期：定位与设置测试全部通过，完整测试无回归。

- [ ] **Step 7：停下并交由用户手动记录 Git 版本**

报告设置文件的实际测试路径和测试结果，不运行 Git 写操作。

---

### Task 4：实现 SQLite 建库与待办仓储

**文件：**

- 创建：`src/DeskNote.App/Repositories/ITodoRepository.cs`
- 创建：`src/DeskNote.App/Repositories/DatabaseInitializer.cs`
- 创建：`src/DeskNote.App/Repositories/SqliteTodoRepository.cs`
- 创建：`tests/DeskNote.Tests/Repositories/SqliteTodoRepositoryTests.cs`

- [ ] **Step 1：先写真实临时数据库测试**

创建 `tests/DeskNote.Tests/Repositories/SqliteTodoRepositoryTests.cs`：

```csharp
using DeskNote.App.Models;
using DeskNote.App.Repositories;

namespace DeskNote.Tests.Repositories;

public sealed class SqliteTodoRepositoryTests : IAsyncLifetime
{
    private readonly string root = Path.Combine(Path.GetTempPath(), $"desk-note-{Guid.NewGuid():N}");
    private string databaseFile = string.Empty;
    private SqliteTodoRepository repository = null!;

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(root);
        databaseFile = Path.Combine(root, "todos.db");
        await DatabaseInitializer.InitializeAsync(databaseFile);
        repository = new SqliteTodoRepository(databaseFile);
    }

    [Fact]
    public async Task InsertAndListIncompleteAsync_RoundTripsTodo()
    {
        var now = DateTimeOffset.UtcNow;
        var todo = new TodoItem(Guid.NewGuid(), "Read", "Chapter 1", false, now, now, null);

        await repository.InsertAsync(todo);
        var items = await repository.ListIncompleteAsync(TodoSortDirection.NewestFirst);

        var actual = Assert.Single(items);
        Assert.Equal(todo.Id, actual.Id);
        Assert.Equal("Read", actual.Title);
        Assert.False(actual.IsCompleted);
    }

    [Fact]
    public async Task SetCompletionAsync_MovesTodoToCompletedQuery()
    {
        var now = DateTimeOffset.UtcNow;
        var todo = new TodoItem(Guid.NewGuid(), "Read", null, false, now, now, null);
        await repository.InsertAsync(todo);

        await repository.SetCompletionAsync(todo.Id, true, now.AddMinutes(1));

        Assert.Empty(await repository.ListIncompleteAsync(TodoSortDirection.NewestFirst));
        var completed = Assert.Single(await repository.ListCompletedAsync());
        Assert.True(completed.IsCompleted);
        Assert.NotNull(completed.CompletedAt);
    }

    [Fact]
    public async Task DeleteCompletedAsync_DoesNotDeleteIncompleteTodos()
    {
        var now = DateTimeOffset.UtcNow;
        var incomplete = new TodoItem(Guid.NewGuid(), "Keep", null, false, now, now, null);
        var completed = new TodoItem(Guid.NewGuid(), "Delete", null, true, now, now, now);
        await repository.InsertAsync(incomplete);
        await repository.InsertAsync(completed);

        await repository.DeleteCompletedAsync();

        Assert.Single(await repository.ListIncompleteAsync(TodoSortDirection.NewestFirst));
        Assert.Empty(await repository.ListCompletedAsync());
    }

    public Task DisposeAsync()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, true);
        }

        return Task.CompletedTask;
    }
}
```

- [ ] **Step 2：运行测试并确认仓储类型缺失**

运行：

```powershell
dotnet test tests/DeskNote.Tests/DeskNote.Tests.csproj --filter SqliteTodoRepositoryTests
```

预期：FAIL，缺少 `DatabaseInitializer` 和 `SqliteTodoRepository`。

- [ ] **Step 3：定义仓储接口和数据库结构**

创建 `src/DeskNote.App/Repositories/ITodoRepository.cs`：

```csharp
using DeskNote.App.Models;

namespace DeskNote.App.Repositories;

public interface ITodoRepository
{
    Task<IReadOnlyList<TodoItem>> ListIncompleteAsync(
        TodoSortDirection direction,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TodoItem>> ListCompletedAsync(
        CancellationToken cancellationToken = default);

    Task<TodoItem?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task InsertAsync(TodoItem item, CancellationToken cancellationToken = default);
    Task UpdateAsync(TodoItem item, CancellationToken cancellationToken = default);
    Task SetCompletionAsync(
        Guid id,
        bool isCompleted,
        DateTimeOffset changedAt,
        CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task DeleteCompletedAsync(CancellationToken cancellationToken = default);
}
```

创建 `src/DeskNote.App/Repositories/DatabaseInitializer.cs`：

```csharp
using Microsoft.Data.Sqlite;

namespace DeskNote.App.Repositories;

public static class DatabaseInitializer
{
    public static async Task InitializeAsync(
        string databaseFile,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(databaseFile)!);
        await using var connection = new SqliteConnection(BuildConnectionString(databaseFile));
        await connection.OpenAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS Todos (
                Id TEXT PRIMARY KEY,
                Title TEXT NOT NULL,
                Note TEXT NULL,
                IsCompleted INTEGER NOT NULL CHECK (IsCompleted IN (0, 1)),
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL,
                CompletedAt TEXT NULL
            );
            CREATE INDEX IF NOT EXISTS IX_Todos_Incomplete
                ON Todos (IsCompleted, CreatedAt);
            CREATE INDEX IF NOT EXISTS IX_Todos_Completed
                ON Todos (IsCompleted, CompletedAt);
            PRAGMA user_version = 1;
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public static async Task<bool> IsHealthyAsync(
        string databaseFile,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(BuildConnectionString(databaseFile));
        await connection.OpenAsync(cancellationToken);
        var command = connection.CreateCommand();
        command.CommandText = "PRAGMA quick_check;";
        return string.Equals(
            (string?)await command.ExecuteScalarAsync(cancellationToken),
            "ok",
            StringComparison.OrdinalIgnoreCase);
    }

    internal static string BuildConnectionString(string databaseFile) =>
        new SqliteConnectionStringBuilder
        {
            DataSource = databaseFile,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared
        }.ToString();
}
```

- [ ] **Step 4：实现 SQLite 仓储**

创建 `src/DeskNote.App/Repositories/SqliteTodoRepository.cs`：

```csharp
using System.Globalization;
using DeskNote.App.Models;
using Microsoft.Data.Sqlite;

namespace DeskNote.App.Repositories;

public sealed class SqliteTodoRepository(string databaseFile) : ITodoRepository
{
    private readonly string connectionString = DatabaseInitializer.BuildConnectionString(databaseFile);

    public Task<IReadOnlyList<TodoItem>> ListIncompleteAsync(
        TodoSortDirection direction,
        CancellationToken cancellationToken = default) =>
        ListAsync(
            $"WHERE IsCompleted = 0 ORDER BY CreatedAt {(direction == TodoSortDirection.NewestFirst ? "DESC" : "ASC")}",
            cancellationToken);

    public Task<IReadOnlyList<TodoItem>> ListCompletedAsync(
        CancellationToken cancellationToken = default) =>
        ListAsync("WHERE IsCompleted = 1 ORDER BY CompletedAt DESC", cancellationToken);

    public async Task<TodoItem?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM Todos WHERE Id = $id;";
        command.Parameters.AddWithValue("$id", id.ToString("D"));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? Read(reader) : null;
    }

    public async Task InsertAsync(TodoItem item, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Todos
                (Id, Title, Note, IsCompleted, CreatedAt, UpdatedAt, CompletedAt)
            VALUES
                ($id, $title, $note, $completed, $created, $updated, $completedAt);
            """;
        AddParameters(command, item);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task UpdateAsync(TodoItem item, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE Todos
            SET Title = $title, Note = $note, UpdatedAt = $updated
            WHERE Id = $id;
            """;
        command.Parameters.AddWithValue("$id", item.Id.ToString("D"));
        command.Parameters.AddWithValue("$title", item.Title);
        command.Parameters.AddWithValue("$note", (object?)item.Note ?? DBNull.Value);
        command.Parameters.AddWithValue("$updated", Format(item.UpdatedAt));
        EnsureOne(await command.ExecuteNonQueryAsync(cancellationToken), item.Id);
    }

    public async Task SetCompletionAsync(
        Guid id,
        bool isCompleted,
        DateTimeOffset changedAt,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var command = connection.CreateCommand();
        command.Transaction = (SqliteTransaction)transaction;
        command.CommandText = """
            UPDATE Todos
            SET IsCompleted = $completed,
                CompletedAt = $completedAt,
                UpdatedAt = $updated
            WHERE Id = $id;
            """;
        command.Parameters.AddWithValue("$id", id.ToString("D"));
        command.Parameters.AddWithValue("$completed", isCompleted ? 1 : 0);
        command.Parameters.AddWithValue("$completedAt", isCompleted ? Format(changedAt) : DBNull.Value);
        command.Parameters.AddWithValue("$updated", Format(changedAt));
        EnsureOne(await command.ExecuteNonQueryAsync(cancellationToken), id);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Todos WHERE Id = $id;";
        command.Parameters.AddWithValue("$id", id.ToString("D"));
        EnsureOne(await command.ExecuteNonQueryAsync(cancellationToken), id);
    }

    public async Task DeleteCompletedAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Todos WHERE IsCompleted = 1;";
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<TodoItem>> ListAsync(
        string clause,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        var command = connection.CreateCommand();
        command.CommandText = $"SELECT * FROM Todos {clause};";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var result = new List<TodoItem>();
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(Read(reader));
        }

        return result;
    }

    private async Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    private static void AddParameters(SqliteCommand command, TodoItem item)
    {
        command.Parameters.AddWithValue("$id", item.Id.ToString("D"));
        command.Parameters.AddWithValue("$title", item.Title);
        command.Parameters.AddWithValue("$note", (object?)item.Note ?? DBNull.Value);
        command.Parameters.AddWithValue("$completed", item.IsCompleted ? 1 : 0);
        command.Parameters.AddWithValue("$created", Format(item.CreatedAt));
        command.Parameters.AddWithValue("$updated", Format(item.UpdatedAt));
        command.Parameters.AddWithValue("$completedAt", item.CompletedAt is null ? DBNull.Value : Format(item.CompletedAt.Value));
    }

    private static TodoItem Read(SqliteDataReader reader) => new(
        Guid.Parse(reader.GetString(reader.GetOrdinal("Id"))),
        reader.GetString(reader.GetOrdinal("Title")),
        reader.IsDBNull(reader.GetOrdinal("Note")) ? null : reader.GetString(reader.GetOrdinal("Note")),
        reader.GetInt32(reader.GetOrdinal("IsCompleted")) == 1,
        Parse(reader.GetString(reader.GetOrdinal("CreatedAt"))),
        Parse(reader.GetString(reader.GetOrdinal("UpdatedAt"))),
        reader.IsDBNull(reader.GetOrdinal("CompletedAt"))
            ? null
            : Parse(reader.GetString(reader.GetOrdinal("CompletedAt"))));

    private static string Format(DateTimeOffset value) => value.ToUniversalTime().ToString("O");

    private static DateTimeOffset Parse(string value) =>
        DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

    private static void EnsureOne(int rows, Guid id)
    {
        if (rows != 1)
        {
            throw new KeyNotFoundException($"Todo {id:D} was not found.");
        }
    }
}
```

- [ ] **Step 5：运行仓储测试并确认通过**

运行：

```powershell
dotnet test tests/DeskNote.Tests/DeskNote.Tests.csproj --filter SqliteTodoRepositoryTests
dotnet test DeskNote.sln
```

预期：仓储测试全部通过，数据库文件能在测试清理时正常删除。

- [ ] **Step 6：停下并交由用户手动记录 Git 版本**

报告数据库结构、测试数量和结果，不运行 Git 写操作。

---

### Task 5：实现待办业务服务

**文件：**

- 创建：`src/DeskNote.App/Services/ITodoService.cs`
- 创建：`src/DeskNote.App/Services/TodoService.cs`
- 创建：`tests/DeskNote.Tests/Services/TodoServiceTests.cs`

- [ ] **Step 1：先写业务规则测试**

创建 `tests/DeskNote.Tests/Services/TodoServiceTests.cs`：

```csharp
using DeskNote.App.Models;
using DeskNote.App.Repositories;
using DeskNote.App.Services;

namespace DeskNote.Tests.Services;

public sealed class TodoServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 29, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task CreateAsync_NormalizesAndPersistsTodo()
    {
        var repository = new InMemoryTodoRepository();
        var service = new TodoService(repository, () => Now);

        var result = await service.CreateAsync("  Read  ", "  Chapter 1  ");

        Assert.Equal("Read", result.Title);
        Assert.Equal("Chapter 1", result.Note);
        Assert.Equal(Now, result.CreatedAt);
        Assert.False(result.IsCompleted);
        Assert.Single(repository.Items);
    }

    [Fact]
    public async Task CreateAsync_RejectsInvalidTitleWithoutWriting()
    {
        var repository = new InMemoryTodoRepository();
        var service = new TodoService(repository, () => Now);

        var exception = await Assert.ThrowsAsync<TodoValidationException>(
            () => service.CreateAsync(" ", null));

        Assert.Equal("待办标题不能为空。", exception.Message);
        Assert.Empty(repository.Items);
    }

    [Fact]
    public async Task CompleteAsync_PersistsCompletionTimestamp()
    {
        var repository = new InMemoryTodoRepository();
        var item = new TodoItem(Guid.NewGuid(), "Read", null, false, Now, Now, null);
        repository.Items.Add(item);
        var service = new TodoService(repository, () => Now.AddMinutes(5));

        await service.CompleteAsync(item.Id);

        var actual = Assert.Single(repository.Items);
        Assert.True(actual.IsCompleted);
        Assert.Equal(Now.AddMinutes(5), actual.CompletedAt);
    }

    [Fact]
    public async Task RestoreAsync_ClearsCompletionTimestamp()
    {
        var repository = new InMemoryTodoRepository();
        var item = new TodoItem(Guid.NewGuid(), "Read", null, true, Now, Now, Now);
        repository.Items.Add(item);
        var service = new TodoService(repository, () => Now.AddMinutes(5));

        await service.RestoreAsync(item.Id);

        var actual = Assert.Single(repository.Items);
        Assert.False(actual.IsCompleted);
        Assert.Null(actual.CompletedAt);
    }

    private sealed class InMemoryTodoRepository : ITodoRepository
    {
        public List<TodoItem> Items { get; } = [];

        public Task<IReadOnlyList<TodoItem>> ListIncompleteAsync(
            TodoSortDirection direction,
            CancellationToken cancellationToken = default)
        {
            IEnumerable<TodoItem> query = Items.Where(item => !item.IsCompleted);
            query = direction == TodoSortDirection.NewestFirst
                ? query.OrderByDescending(item => item.CreatedAt)
                : query.OrderBy(item => item.CreatedAt);
            return Task.FromResult<IReadOnlyList<TodoItem>>(query.ToList());
        }

        public Task<IReadOnlyList<TodoItem>> ListCompletedAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<TodoItem>>(
                Items.Where(item => item.IsCompleted).OrderByDescending(item => item.CompletedAt).ToList());

        public Task<TodoItem?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Items.SingleOrDefault(item => item.Id == id));

        public Task InsertAsync(TodoItem item, CancellationToken cancellationToken = default)
        {
            Items.Add(item);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(TodoItem item, CancellationToken cancellationToken = default)
        {
            Replace(item);
            return Task.CompletedTask;
        }

        public Task SetCompletionAsync(
            Guid id,
            bool isCompleted,
            DateTimeOffset changedAt,
            CancellationToken cancellationToken = default)
        {
            var current = Items.Single(item => item.Id == id);
            Replace(current with
            {
                IsCompleted = isCompleted,
                CompletedAt = isCompleted ? changedAt : null,
                UpdatedAt = changedAt
            });
            return Task.CompletedTask;
        }

        public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            Items.RemoveAll(item => item.Id == id);
            return Task.CompletedTask;
        }

        public Task DeleteCompletedAsync(CancellationToken cancellationToken = default)
        {
            Items.RemoveAll(item => item.IsCompleted);
            return Task.CompletedTask;
        }

        private void Replace(TodoItem item)
        {
            var index = Items.FindIndex(candidate => candidate.Id == item.Id);
            Items[index] = item;
        }
    }
}
```

- [ ] **Step 2：运行测试并确认服务类型缺失**

运行：

```powershell
dotnet test tests/DeskNote.Tests/DeskNote.Tests.csproj --filter TodoServiceTests
```

预期：FAIL，缺少 `TodoService` 和 `TodoValidationException`。

- [ ] **Step 3：实现业务服务**

创建 `src/DeskNote.App/Services/ITodoService.cs`：

```csharp
using DeskNote.App.Models;

namespace DeskNote.App.Services;

public interface ITodoService
{
    Task<IReadOnlyList<TodoItem>> ListIncompleteAsync(
        TodoSortDirection direction,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TodoItem>> ListCompletedAsync(CancellationToken cancellationToken = default);
    Task<TodoItem> CreateAsync(string? title, string? note, CancellationToken cancellationToken = default);
    Task<TodoItem> UpdateAsync(Guid id, string? title, string? note, CancellationToken cancellationToken = default);
    Task CompleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task RestoreAsync(Guid id, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task DeleteCompletedAsync(CancellationToken cancellationToken = default);
}
```

创建 `src/DeskNote.App/Services/TodoService.cs`：

```csharp
using DeskNote.App.Models;
using DeskNote.App.Repositories;
using DeskNote.App.Validation;

namespace DeskNote.App.Services;

public sealed class TodoValidationException(string message) : Exception(message);

public sealed class TodoService : ITodoService
{
    private readonly ITodoRepository repository;
    private readonly Func<DateTimeOffset> utcNow;

    public TodoService(ITodoRepository repository, Func<DateTimeOffset>? utcNow = null)
    {
        this.repository = repository;
        this.utcNow = utcNow ?? (() => DateTimeOffset.UtcNow);
    }

    public Task<IReadOnlyList<TodoItem>> ListIncompleteAsync(
        TodoSortDirection direction,
        CancellationToken cancellationToken = default) =>
        repository.ListIncompleteAsync(direction, cancellationToken);

    public Task<IReadOnlyList<TodoItem>> ListCompletedAsync(
        CancellationToken cancellationToken = default) =>
        repository.ListCompletedAsync(cancellationToken);

    public async Task<TodoItem> CreateAsync(
        string? title,
        string? note,
        CancellationToken cancellationToken = default)
    {
        var input = RequireValid(title, note);
        var now = utcNow();
        var item = new TodoItem(Guid.NewGuid(), input.Title, input.Note, false, now, now, null);
        await repository.InsertAsync(item, cancellationToken);
        return item;
    }

    public async Task<TodoItem> UpdateAsync(
        Guid id,
        string? title,
        string? note,
        CancellationToken cancellationToken = default)
    {
        var input = RequireValid(title, note);
        var current = await RequireExistingAsync(id, cancellationToken);
        var updated = current with { Title = input.Title, Note = input.Note, UpdatedAt = utcNow() };
        await repository.UpdateAsync(updated, cancellationToken);
        return updated;
    }

    public async Task CompleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var current = await RequireExistingAsync(id, cancellationToken);
        if (current.IsCompleted)
        {
            return;
        }

        await repository.SetCompletionAsync(id, true, utcNow(), cancellationToken);
    }

    public async Task RestoreAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var current = await RequireExistingAsync(id, cancellationToken);
        if (!current.IsCompleted)
        {
            return;
        }

        await repository.SetCompletionAsync(id, false, utcNow(), cancellationToken);
    }

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default) =>
        repository.DeleteAsync(id, cancellationToken);

    public Task DeleteCompletedAsync(CancellationToken cancellationToken = default) =>
        repository.DeleteCompletedAsync(cancellationToken);

    private async Task<TodoItem> RequireExistingAsync(Guid id, CancellationToken cancellationToken) =>
        await repository.GetAsync(id, cancellationToken)
        ?? throw new KeyNotFoundException($"Todo {id:D} was not found.");

    private static TodoInput RequireValid(string? title, string? note)
    {
        var result = TodoInputValidator.Validate(title, note);
        if (!result.IsValid)
        {
            throw new TodoValidationException(result.ErrorMessage!);
        }

        return new TodoInput(result.Title, result.Note);
    }
}
```

- [ ] **Step 4：运行服务测试和完整测试**

运行：

```powershell
dotnet test tests/DeskNote.Tests/DeskNote.Tests.csproj --filter TodoServiceTests
dotnet test DeskNote.sln
```

预期：4 个服务测试通过，完整测试无回归。

- [ ] **Step 5：停下并交由用户手动记录 Git 版本**

报告业务规则和测试结果，不运行 Git 写操作。

---

### Task 6：实现可测试的 ViewModel

**文件：**

- 修改：`src/DeskNote.App/Models/Enums.cs`
- 创建：`src/DeskNote.App/ViewModels/MainViewModel.cs`
- 创建：`src/DeskNote.App/ViewModels/TodoEditorViewModel.cs`
- 创建：`src/DeskNote.App/ViewModels/TodoListViewModel.cs`
- 创建：`tests/DeskNote.Tests/ViewModels/TodoListViewModelTests.cs`

- [ ] **Step 1：先写列表 ViewModel 的失败测试**

创建 `tests/DeskNote.Tests/ViewModels/TodoListViewModelTests.cs`：

```csharp
using DeskNote.App.Models;
using DeskNote.App.Services;
using DeskNote.App.ViewModels;

namespace DeskNote.Tests.ViewModels;

public sealed class TodoListViewModelTests
{
    [Fact]
    public async Task LoadCommand_LoadsBothLists()
    {
        var service = new FakeTodoService();
        service.Incomplete.Add(CreateTodo(false));
        service.Completed.Add(CreateTodo(true));
        var viewModel = new TodoListViewModel(service);

        await viewModel.LoadCommand.ExecuteAsync(null);

        Assert.Single(viewModel.IncompleteItems);
        Assert.Single(viewModel.CompletedItems);
        Assert.Null(viewModel.ErrorMessage);
    }

    [Fact]
    public async Task CompleteCommand_ReloadsAfterSuccessfulWrite()
    {
        var service = new FakeTodoService();
        var item = CreateTodo(false);
        service.Incomplete.Add(item);
        var viewModel = new TodoListViewModel(service);
        await viewModel.LoadCommand.ExecuteAsync(null);

        await viewModel.CompleteCommand.ExecuteAsync(item);

        Assert.Equal(item.Id, service.CompletedId);
        Assert.Empty(viewModel.IncompleteItems);
        Assert.Single(viewModel.CompletedItems);
    }

    [Fact]
    public async Task CompleteCommand_LeavesListAndShowsErrorWhenWriteFails()
    {
        var service = new FakeTodoService { CompletionException = new IOException("disk full") };
        var item = CreateTodo(false);
        service.Incomplete.Add(item);
        var viewModel = new TodoListViewModel(service);
        await viewModel.LoadCommand.ExecuteAsync(null);

        await viewModel.CompleteCommand.ExecuteAsync(item);

        Assert.Single(viewModel.IncompleteItems);
        Assert.Equal("操作失败，请重试。", viewModel.ErrorMessage);
    }

    private static TodoItem CreateTodo(bool completed)
    {
        var now = DateTimeOffset.UtcNow;
        return new TodoItem(Guid.NewGuid(), "Read", null, completed, now, now, completed ? now : null);
    }

    private sealed class FakeTodoService : ITodoService
    {
        public List<TodoItem> Incomplete { get; } = [];
        public List<TodoItem> Completed { get; } = [];
        public Guid? CompletedId { get; private set; }
        public Exception? CompletionException { get; init; }

        public Task<IReadOnlyList<TodoItem>> ListIncompleteAsync(
            TodoSortDirection direction,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<TodoItem>>(Incomplete.ToList());

        public Task<IReadOnlyList<TodoItem>> ListCompletedAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<TodoItem>>(Completed.ToList());

        public Task CompleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            if (CompletionException is not null)
            {
                throw CompletionException;
            }

            CompletedId = id;
            var item = Incomplete.Single(candidate => candidate.Id == id);
            Incomplete.Remove(item);
            Completed.Add(item with { IsCompleted = true, CompletedAt = DateTimeOffset.UtcNow });
            return Task.CompletedTask;
        }

        public Task RestoreAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var item = Completed.Single(candidate => candidate.Id == id);
            Completed.Remove(item);
            Incomplete.Add(item with { IsCompleted = false, CompletedAt = null });
            return Task.CompletedTask;
        }

        public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            Incomplete.RemoveAll(item => item.Id == id);
            Completed.RemoveAll(item => item.Id == id);
            return Task.CompletedTask;
        }

        public Task DeleteCompletedAsync(CancellationToken cancellationToken = default)
        {
            Completed.Clear();
            return Task.CompletedTask;
        }

        public Task<TodoItem> CreateAsync(string? title, string? note, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<TodoItem> UpdateAsync(Guid id, string? title, string? note, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
```

- [ ] **Step 2：运行测试并确认 ViewModel 缺失**

运行：

```powershell
dotnet test tests/DeskNote.Tests/DeskNote.Tests.csproj --filter TodoListViewModelTests
```

预期：FAIL，缺少 `TodoListViewModel`。

- [ ] **Step 3：补充导航枚举并实现主 ViewModel**

在 `src/DeskNote.App/Models/Enums.cs` 末尾添加：

```csharp
public enum NavigationPage
{
    Create,
    Incomplete,
    Completed,
    Settings
}
```

创建 `src/DeskNote.App/ViewModels/MainViewModel.cs`：

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DeskNote.App.Models;

namespace DeskNote.App.ViewModels;

public partial class MainViewModel : ObservableObject
{
    public MainViewModel(TodoEditorViewModel editor, TodoListViewModel todoList)
    {
        Editor = editor;
        TodoList = todoList;
    }

    public TodoEditorViewModel Editor { get; }
    public TodoListViewModel TodoList { get; }

    [ObservableProperty]
    private NavigationPage currentPage = NavigationPage.Incomplete;

    [RelayCommand]
    private void Navigate(NavigationPage page) => CurrentPage = page;
}
```

- [ ] **Step 4：实现编辑 ViewModel**

创建 `src/DeskNote.App/ViewModels/TodoEditorViewModel.cs`：

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DeskNote.App.Models;
using DeskNote.App.Services;
using DeskNote.App.Validation;

namespace DeskNote.App.ViewModels;

public partial class TodoEditorViewModel : ObservableObject
{
    private readonly ITodoService todoService;

    public TodoEditorViewModel(ITodoService todoService)
    {
        this.todoService = todoService;
    }

    public event EventHandler? Saved;

    [ObservableProperty]
    private Guid? editingId;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private string title = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private string? note;

    [ObservableProperty]
    private string? errorMessage;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private bool isBusy;

    public void BeginCreate()
    {
        EditingId = null;
        Title = string.Empty;
        Note = null;
        ErrorMessage = null;
    }

    public void BeginEdit(TodoItem item)
    {
        EditingId = item.Id;
        Title = item.Title;
        Note = item.Note;
        ErrorMessage = null;
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync()
    {
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            if (EditingId is Guid id)
            {
                await todoService.UpdateAsync(id, Title, Note);
            }
            else
            {
                await todoService.CreateAsync(Title, Note);
            }

            Saved?.Invoke(this, EventArgs.Empty);
        }
        catch (TodoValidationException exception)
        {
            ErrorMessage = exception.Message;
        }
        catch (Exception)
        {
            ErrorMessage = "保存失败，请重试。";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void Cancel() => BeginCreate();

    private bool CanSave() => !IsBusy && TodoInputValidator.Validate(Title, Note).IsValid;
}
```

- [ ] **Step 5：实现列表 ViewModel**

创建 `src/DeskNote.App/ViewModels/TodoListViewModel.cs`：

```csharp
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DeskNote.App.Models;
using DeskNote.App.Services;

namespace DeskNote.App.ViewModels;

public partial class TodoListViewModel : ObservableObject
{
    private readonly ITodoService todoService;

    public TodoListViewModel(ITodoService todoService)
    {
        this.todoService = todoService;
    }

    public ObservableCollection<TodoItem> IncompleteItems { get; } = [];
    public ObservableCollection<TodoItem> CompletedItems { get; } = [];

    [ObservableProperty]
    private TodoSortDirection sortDirection = TodoSortDirection.NewestFirst;

    [ObservableProperty]
    private string? errorMessage;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoadCommand))]
    private bool isBusy;

    [RelayCommand(CanExecute = nameof(CanRun))]
    private Task LoadAsync() => RunAsync(ReloadAsync);

    [RelayCommand]
    private Task CompleteAsync(TodoItem item) =>
        RunAsync(async () =>
        {
            await todoService.CompleteAsync(item.Id);
            await ReloadAsync();
        });

    [RelayCommand]
    private Task RestoreAsync(TodoItem item) =>
        RunAsync(async () =>
        {
            await todoService.RestoreAsync(item.Id);
            await ReloadAsync();
        });

    [RelayCommand]
    private Task DeleteAsync(TodoItem item) =>
        RunAsync(async () =>
        {
            await todoService.DeleteAsync(item.Id);
            await ReloadAsync();
        });

    [RelayCommand]
    private Task ClearCompletedAsync() =>
        RunAsync(async () =>
        {
            await todoService.DeleteCompletedAsync();
            await ReloadAsync();
        });

    [RelayCommand]
    private Task SetSortAsync(TodoSortDirection direction) =>
        RunAsync(async () =>
        {
            SortDirection = direction;
            await ReloadAsync();
        });

    private bool CanRun() => !IsBusy;

    private async Task ReloadAsync()
    {
        var incomplete = await todoService.ListIncompleteAsync(SortDirection);
        var completed = await todoService.ListCompletedAsync();
        Replace(IncompleteItems, incomplete);
        Replace(CompletedItems, completed);
    }

    private async Task RunAsync(Func<Task> action)
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        ErrorMessage = null;
        try
        {
            await action();
        }
        catch (Exception)
        {
            ErrorMessage = "操作失败，请重试。";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static void Replace(ObservableCollection<TodoItem> target, IEnumerable<TodoItem> source)
    {
        target.Clear();
        foreach (var item in source)
        {
            target.Add(item);
        }
    }
}
```

- [ ] **Step 6：运行 ViewModel 测试和完整测试**

运行：

```powershell
dotnet test tests/DeskNote.Tests/DeskNote.Tests.csproj --filter TodoListViewModelTests
dotnet test DeskNote.sln
```

预期：3 个 ViewModel 测试通过，完整测试无回归。

- [ ] **Step 7：停下并交由用户手动记录 Git 版本**

报告 ViewModel 命令和错误状态验证结果，不运行 Git 写操作。

---

### Task 7：实现设置状态、主题资源和窗口状态服务

**文件：**

- 创建：`src/DeskNote.App/Services/SettingsService.cs`
- 创建：`src/DeskNote.App/Services/ThemeService.cs`
- 创建：`src/DeskNote.App/Services/WindowStateService.cs`
- 创建：`src/DeskNote.App/Resources/Themes/Light.xaml`
- 创建：`src/DeskNote.App/Resources/Themes/Dark.xaml`
- 创建：`src/DeskNote.App/Resources/Controls.xaml`
- 创建：`tests/DeskNote.Tests/Services/SettingsServiceTests.cs`

- [ ] **Step 1：先写设置持久化和目录创建测试**

创建 `tests/DeskNote.Tests/Services/SettingsServiceTests.cs`：

```csharp
using DeskNote.App.Infrastructure;
using DeskNote.App.Models;
using DeskNote.App.Services;

namespace DeskNote.Tests.Services;

public sealed class SettingsServiceTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), $"desk-note-{Guid.NewGuid():N}");

    [Fact]
    public async Task InitializeAndUpdateAsync_PersistsSettings()
    {
        var service = CreateService();
        await service.InitializeAsync();

        await service.UpdateAsync(settings => settings.Theme = ThemeMode.Dark);

        var reloaded = CreateService();
        await reloaded.InitializeAsync();
        Assert.Equal(ThemeMode.Dark, reloaded.Current.Theme);
    }

    [Fact]
    public async Task InitializeAsync_CreatesRequiredDirectories()
    {
        var service = CreateService();

        await service.InitializeAsync();

        Assert.True(Directory.Exists(service.Paths.BackupsDirectory));
        Assert.True(Directory.Exists(service.Paths.LogsDirectory));
    }

    private SettingsService CreateService() => new(
        new JsonDataLocator(Path.Combine(root, "locator.json"), Path.Combine(root, "data")),
        new JsonSettingsStore());

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, true);
        }
    }
}
```

- [ ] **Step 2：运行测试并确认设置服务缺失**

运行：

```powershell
dotnet test tests/DeskNote.Tests/DeskNote.Tests.csproj --filter SettingsServiceTests
```

预期：FAIL，缺少 `SettingsService`。

- [ ] **Step 3：实现设置服务**

创建 `src/DeskNote.App/Services/SettingsService.cs`：

```csharp
using DeskNote.App.Infrastructure;
using DeskNote.App.Models;

namespace DeskNote.App.Services;

public sealed class SettingsService
{
    private readonly JsonDataLocator locator;
    private readonly JsonSettingsStore store;

    public SettingsService(JsonDataLocator locator, JsonSettingsStore store)
    {
        this.locator = locator;
        this.store = store;
    }

    public AppSettings Current { get; private set; } = new();
    public string DataDirectory { get; private set; } = string.Empty;
    public DataPaths Paths => new(DataDirectory);

    public event EventHandler? Changed;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        DataDirectory = await locator.GetOrCreateAsync(cancellationToken);
        Directory.CreateDirectory(DataDirectory);
        Directory.CreateDirectory(Paths.BackupsDirectory);
        Directory.CreateDirectory(Paths.LogsDirectory);
        Current = await store.LoadAsync(DataDirectory, cancellationToken);
    }

    public async Task UpdateAsync(
        Action<AppSettings> update,
        CancellationToken cancellationToken = default)
    {
        var previous = Clone(Current);
        update(Current);
        try
        {
            await store.SaveAsync(DataDirectory, Current, cancellationToken);
            Changed?.Invoke(this, EventArgs.Empty);
        }
        catch
        {
            Current = previous;
            throw;
        }
    }

    public void SwitchDataDirectory(string dataDirectory)
    {
        DataDirectory = Path.GetFullPath(dataDirectory);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private static AppSettings Clone(AppSettings source) => new()
    {
        SchemaVersion = source.SchemaVersion,
        Theme = source.Theme,
        StartWithWindows = source.StartWithWindows,
        AlwaysOnTop = source.AlwaysOnTop,
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

- [ ] **Step 4：实现主题资源与主题切换**

创建 `src/DeskNote.App/Resources/Themes/Light.xaml`：

```xml
<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <SolidColorBrush x:Key="WindowBrush" Color="#FFF7F8FB" />
  <SolidColorBrush x:Key="PanelBrush" Color="#FFFFFFFF" />
  <SolidColorBrush x:Key="SidebarBrush" Color="#FF252A38" />
  <SolidColorBrush x:Key="PrimaryBrush" Color="#FF6C63FF" />
  <SolidColorBrush x:Key="TextBrush" Color="#FF2E3544" />
  <SolidColorBrush x:Key="MutedTextBrush" Color="#FF858E9E" />
  <SolidColorBrush x:Key="BorderBrush" Color="#FFE1E5EC" />
  <SolidColorBrush x:Key="DangerBrush" Color="#FFD65252" />
</ResourceDictionary>
```

创建 `src/DeskNote.App/Resources/Themes/Dark.xaml`：

```xml
<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <SolidColorBrush x:Key="WindowBrush" Color="#FF20252F" />
  <SolidColorBrush x:Key="PanelBrush" Color="#FF2A303C" />
  <SolidColorBrush x:Key="SidebarBrush" Color="#FF181C25" />
  <SolidColorBrush x:Key="PrimaryBrush" Color="#FF8179FF" />
  <SolidColorBrush x:Key="TextBrush" Color="#FFEEF2F8" />
  <SolidColorBrush x:Key="MutedTextBrush" Color="#FFA7AFBC" />
  <SolidColorBrush x:Key="BorderBrush" Color="#FF39414E" />
  <SolidColorBrush x:Key="DangerBrush" Color="#FFFF7777" />
</ResourceDictionary>
```

创建 `src/DeskNote.App/Resources/Controls.xaml`：

```xml
<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <Style TargetType="Button">
    <Setter Property="Padding" Value="10,7" />
    <Setter Property="Margin" Value="3" />
    <Setter Property="Cursor" Value="Hand" />
  </Style>
  <Style TargetType="TextBox">
    <Setter Property="Padding" Value="8" />
    <Setter Property="Margin" Value="0,4" />
  </Style>
  <Style TargetType="ListBox">
    <Setter Property="Background" Value="Transparent" />
    <Setter Property="BorderThickness" Value="0" />
    <Setter Property="ScrollViewer.CanContentScroll" Value="True" />
    <Setter Property="VirtualizingPanel.IsVirtualizing" Value="True" />
    <Setter Property="VirtualizingPanel.VirtualizationMode" Value="Recycling" />
  </Style>
</ResourceDictionary>
```

创建 `src/DeskNote.App/Services/ThemeService.cs`：

```csharp
using System.Windows;
using System.Windows.Interop;
using DeskNote.App.Models;
using Forms = System.Windows.Forms;

namespace DeskNote.App.Services;

public sealed class ThemeService
{
    public void Apply(ThemeMode mode)
    {
        var dictionaries = Application.Current.Resources.MergedDictionaries;
        var current = dictionaries.FirstOrDefault(dictionary =>
            dictionary.Source?.OriginalString.Contains("/Resources/Themes/", StringComparison.Ordinal) == true);
        if (current is not null)
        {
            dictionaries.Remove(current);
        }

        dictionaries.Insert(0, new ResourceDictionary
        {
            Source = new Uri($"/Resources/Themes/{mode}.xaml", UriKind.Relative)
        });
    }
}
```

- [ ] **Step 5：实现窗口状态捕获与可见区域恢复**

创建 `src/DeskNote.App/Services/WindowStateService.cs`：

```csharp
using System.Windows;
using DeskNote.App.Models;

namespace DeskNote.App.Services;

public sealed class WindowStateService
{
    public void Restore(Window window, WindowBounds bounds)
    {
        window.Width = Math.Max(360, bounds.Width);
        window.Height = Math.Max(480, bounds.Height);

        if (bounds.Left is double left && bounds.Top is double top && IsVisible(left, top, window.Width, window.Height))
        {
            window.WindowStartupLocation = WindowStartupLocation.Manual;
            window.Left = left;
            window.Top = top;
        }
        else
        {
            window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }
    }

    public WindowBounds Capture(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        return new WindowBounds
        {
            Left = window.RestoreBounds.Left,
            Top = window.RestoreBounds.Top,
            Width = window.RestoreBounds.Width,
            Height = window.RestoreBounds.Height,
            ScreenDeviceName = handle == IntPtr.Zero ? null : Forms.Screen.FromHandle(handle).DeviceName
        };
    }

    private static bool IsVisible(double left, double top, double width, double height)
    {
        var right = left + width;
        var bottom = top + height;
        return right > SystemParameters.VirtualScreenLeft + 100
            && left < SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth - 100
            && bottom > SystemParameters.VirtualScreenTop + 100
            && top < SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight - 100;
    }
}
```

- [ ] **Step 6：将资源字典合并到应用入口**

将 `src/DeskNote.App/App.xaml` 改为：

```xml
<Application x:Class="DeskNote.App.App"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <Application.Resources>
    <ResourceDictionary>
      <ResourceDictionary.MergedDictionaries>
        <ResourceDictionary Source="/Resources/Themes/Light.xaml" />
        <ResourceDictionary Source="/Resources/Controls.xaml" />
      </ResourceDictionary.MergedDictionaries>
    </ResourceDictionary>
  </Application.Resources>
</Application>
```

- [ ] **Step 7：运行测试与 XAML 编译**

运行：

```powershell
dotnet test tests/DeskNote.Tests/DeskNote.Tests.csproj --filter SettingsServiceTests
dotnet build DeskNote.sln -c Debug
dotnet test DeskNote.sln -c Debug --no-build
```

预期：设置服务测试通过，主题 XAML 可编译，全部测试无回归。

- [ ] **Step 8：停下并交由用户手动记录 Git 版本**

报告主题资源、窗口状态逻辑和测试结果，不运行 Git 写操作。

---

### Task 8：实现主窗口、悬停侧边栏和待办页面

**文件：**

- 修改：`src/DeskNote.App/ViewModels/MainViewModel.cs`
- 创建：`src/DeskNote.App/Views/MainWindow.xaml`
- 创建：`src/DeskNote.App/Views/MainWindow.xaml.cs`
- 创建：`src/DeskNote.App/Views/TodoEditorView.xaml`
- 创建：`src/DeskNote.App/Views/TodoEditorView.xaml.cs`
- 创建：`src/DeskNote.App/Views/IncompleteTodosView.xaml`
- 创建：`src/DeskNote.App/Views/IncompleteTodosView.xaml.cs`
- 创建：`src/DeskNote.App/Views/CompletedTodosView.xaml`
- 创建：`src/DeskNote.App/Views/CompletedTodosView.xaml.cs`

- [ ] **Step 1：扩展主 ViewModel 的新建、编辑和保存后刷新流程**

将 `src/DeskNote.App/ViewModels/MainViewModel.cs` 替换为：

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DeskNote.App.Models;

namespace DeskNote.App.ViewModels;

public partial class MainViewModel : ObservableObject
{
    public MainViewModel(TodoEditorViewModel editor, TodoListViewModel todoList)
    {
        Editor = editor;
        TodoList = todoList;
        Editor.Saved += OnEditorSaved;
    }

    public TodoEditorViewModel Editor { get; }
    public TodoListViewModel TodoList { get; }

    [ObservableProperty]
    private NavigationPage currentPage = NavigationPage.Incomplete;

    [RelayCommand]
    private void Navigate(NavigationPage page)
    {
        if (page == NavigationPage.Create)
        {
            Editor.BeginCreate();
        }

        CurrentPage = page;
    }

    [RelayCommand]
    private void EditTodo(TodoItem item)
    {
        Editor.BeginEdit(item);
        CurrentPage = NavigationPage.Create;
    }

    private async void OnEditorSaved(object? sender, EventArgs e)
    {
        CurrentPage = NavigationPage.Incomplete;
        await TodoList.LoadCommand.ExecuteAsync(null);
    }
}
```

- [ ] **Step 2：创建主窗口 XAML**

创建 `src/DeskNote.App/Views/MainWindow.xaml`：

```xml
<Window x:Class="DeskNote.App.Views.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:shell="clr-namespace:System.Windows.Shell;assembly=PresentationFramework"
        xmlns:models="clr-namespace:DeskNote.App.Models"
        xmlns:views="clr-namespace:DeskNote.App.Views"
        Title="desk-note"
        Width="420" Height="600"
        MinWidth="360" MinHeight="480"
        WindowStyle="None"
        ResizeMode="CanResize"
        Background="{DynamicResource WindowBrush}"
        Foreground="{DynamicResource TextBrush}"
        Closing="OnClosing">
  <shell:WindowChrome.WindowChrome>
    <shell:WindowChrome CaptionHeight="34" ResizeBorderThickness="6" CornerRadius="0" />
  </shell:WindowChrome.WindowChrome>

  <Window.Resources>
    <DataTemplate x:Key="CreateTemplate">
      <views:TodoEditorView DataContext="{Binding Editor}" />
    </DataTemplate>
    <DataTemplate x:Key="IncompleteTemplate">
      <views:IncompleteTodosView DataContext="{Binding}" />
    </DataTemplate>
    <DataTemplate x:Key="CompletedTemplate">
      <views:CompletedTodosView DataContext="{Binding}" />
    </DataTemplate>
  </Window.Resources>

  <Border BorderBrush="{DynamicResource BorderBrush}" BorderThickness="1">
    <Grid>
      <Grid.RowDefinitions>
        <RowDefinition Height="34" />
        <RowDefinition Height="*" />
      </Grid.RowDefinitions>

      <Grid Grid.Row="0" Background="{DynamicResource PanelBrush}">
        <TextBlock Margin="12,0" VerticalAlignment="Center" FontWeight="SemiBold" Text="desk-note" />
        <StackPanel HorizontalAlignment="Right" Orientation="Horizontal"
                    shell:WindowChrome.IsHitTestVisibleInChrome="True">
          <ToggleButton Width="38" ToolTip="窗口置顶" Checked="OnPinChanged" Unchecked="OnPinChanged">📌</ToggleButton>
          <Button Width="38" Click="OnMinimize">—</Button>
          <Button Width="38" Click="OnMaximize">□</Button>
          <Button Width="38" Click="OnHide">×</Button>
        </StackPanel>
      </Grid>

      <Grid Grid.Row="1">
        <Grid.ColumnDefinitions>
          <ColumnDefinition Width="Auto" />
          <ColumnDefinition Width="*" />
        </Grid.ColumnDefinitions>

        <Border x:Name="Sidebar"
                Grid.Column="0"
                Width="58"
                Panel.ZIndex="2"
                Background="{DynamicResource SidebarBrush}"
                ClipToBounds="True"
                MouseEnter="OnSidebarMouseEnter"
                MouseLeave="OnSidebarMouseLeave"
                GotKeyboardFocus="OnSidebarFocusChanged"
                LostKeyboardFocus="OnSidebarFocusChanged">
          <StackPanel Margin="8,12">
            <Button Height="42" HorizontalContentAlignment="Left"
                    Command="{Binding NavigateCommand}"
                    CommandParameter="{x:Static models:NavigationPage.Create}"
                    ToolTip="创建新待办">
              <StackPanel Orientation="Horizontal"><TextBlock Width="40" Text="＋" /><TextBlock Text="创建新待办" /></StackPanel>
            </Button>
            <Button Height="42" HorizontalContentAlignment="Left"
                    Command="{Binding NavigateCommand}"
                    CommandParameter="{x:Static models:NavigationPage.Incomplete}"
                    ToolTip="未完成待办">
              <StackPanel Orientation="Horizontal"><TextBlock Width="40" Text="○" /><TextBlock Text="未完成待办" /></StackPanel>
            </Button>
            <Button Height="42" HorizontalContentAlignment="Left"
                    Command="{Binding NavigateCommand}"
                    CommandParameter="{x:Static models:NavigationPage.Completed}"
                    ToolTip="已完成待办">
              <StackPanel Orientation="Horizontal"><TextBlock Width="40" Text="✓" /><TextBlock Text="已完成待办" /></StackPanel>
            </Button>
            <Button Height="42" HorizontalContentAlignment="Left"
                    Command="{Binding NavigateCommand}"
                    CommandParameter="{x:Static models:NavigationPage.Settings}"
                    ToolTip="设置">
              <StackPanel Orientation="Horizontal"><TextBlock Width="40" Text="⚙" /><TextBlock Text="设置" /></StackPanel>
            </Button>
          </StackPanel>
        </Border>

        <ContentControl Grid.Column="1" Content="{Binding}" Margin="14">
          <ContentControl.Style>
            <Style TargetType="ContentControl">
              <Setter Property="ContentTemplate" Value="{StaticResource IncompleteTemplate}" />
              <Style.Triggers>
                <DataTrigger Binding="{Binding CurrentPage}" Value="{x:Static models:NavigationPage.Create}">
                  <Setter Property="ContentTemplate" Value="{StaticResource CreateTemplate}" />
                </DataTrigger>
                <DataTrigger Binding="{Binding CurrentPage}" Value="{x:Static models:NavigationPage.Completed}">
                  <Setter Property="ContentTemplate" Value="{StaticResource CompletedTemplate}" />
                </DataTrigger>
              </Style.Triggers>
            </Style>
          </ContentControl.Style>
        </ContentControl>
      </Grid>
    </Grid>
  </Border>
</Window>
```

设置页模板会在 Task 10 添加；在此之前导航到设置仍显示未完成列表，保证工程持续可编译。

- [ ] **Step 3：实现主窗口的悬停动画和窗口按钮**

创建 `src/DeskNote.App/Views/MainWindow.xaml.cs`：

```csharp
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;

namespace DeskNote.App.Views;

public partial class MainWindow : Window
{
    private bool allowClose;

    public MainWindow()
    {
        InitializeComponent();
    }

    public void ExitApplication()
    {
        allowClose = true;
        Close();
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
        var duration = SystemParameters.ClientAreaAnimation ? TimeSpan.FromMilliseconds(180) : TimeSpan.Zero;
        Sidebar.BeginAnimation(WidthProperty, new DoubleAnimation(width, duration)
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        });
    }

    private void OnPinChanged(object sender, RoutedEventArgs e) =>
        Topmost = sender is System.Windows.Controls.Primitives.ToggleButton { IsChecked: true };

    private void OnMinimize(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void OnMaximize(object sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void OnHide(object sender, RoutedEventArgs e) => Hide();

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (!allowClose)
        {
            e.Cancel = true;
            Hide();
        }
    }
}
```

- [ ] **Step 4：创建待办编辑页**

创建 `src/DeskNote.App/Views/TodoEditorView.xaml`：

```xml
<UserControl x:Class="DeskNote.App.Views.TodoEditorView"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <Grid>
    <Grid.RowDefinitions>
      <RowDefinition Height="Auto" />
      <RowDefinition Height="Auto" />
      <RowDefinition Height="*" />
      <RowDefinition Height="Auto" />
      <RowDefinition Height="Auto" />
    </Grid.RowDefinitions>
    <TextBlock Grid.Row="0" FontSize="22" FontWeight="SemiBold" Text="创建或编辑待办" />
    <TextBox Grid.Row="1" Margin="0,16,0,8" Text="{Binding Title, UpdateSourceTrigger=PropertyChanged}" />
    <TextBox Grid.Row="2" AcceptsReturn="True" TextWrapping="Wrap"
             VerticalScrollBarVisibility="Auto"
             Text="{Binding Note, UpdateSourceTrigger=PropertyChanged}" />
    <TextBlock Grid.Row="3" Margin="0,8" Foreground="{DynamicResource DangerBrush}"
               Text="{Binding ErrorMessage}" />
    <StackPanel Grid.Row="4" HorizontalAlignment="Right" Orientation="Horizontal">
      <Button Command="{Binding CancelCommand}" Content="取消" />
      <Button Command="{Binding SaveCommand}" Content="保存" />
    </StackPanel>
  </Grid>
</UserControl>
```

- [ ] **Step 5：创建未完成列表页**

创建 `src/DeskNote.App/Views/IncompleteTodosView.xaml`：

```xml
<UserControl x:Class="DeskNote.App.Views.IncompleteTodosView"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:models="clr-namespace:DeskNote.App.Models">
  <Grid>
    <Grid.RowDefinitions>
      <RowDefinition Height="Auto" />
      <RowDefinition Height="Auto" />
      <RowDefinition Height="*" />
      <RowDefinition Height="Auto" />
    </Grid.RowDefinitions>
    <DockPanel Grid.Row="0">
      <TextBlock DockPanel.Dock="Left" FontSize="22" FontWeight="SemiBold" Text="未完成待办" />
      <StackPanel DockPanel.Dock="Right" Orientation="Horizontal">
        <Button Content="最新" Command="{Binding TodoList.SetSortCommand}"
                CommandParameter="{x:Static models:TodoSortDirection.NewestFirst}" />
        <Button Content="最早" Command="{Binding TodoList.SetSortCommand}"
                CommandParameter="{x:Static models:TodoSortDirection.OldestFirst}" />
      </StackPanel>
    </DockPanel>
    <TextBlock Grid.Row="1" Margin="0,6" Foreground="{DynamicResource DangerBrush}"
               Text="{Binding TodoList.ErrorMessage}" />
    <ListBox Grid.Row="2" ItemsSource="{Binding TodoList.IncompleteItems}">
      <ListBox.ItemTemplate>
        <DataTemplate>
          <Border Margin="0,4" Padding="10" CornerRadius="10"
                  Background="{DynamicResource PanelBrush}"
                  BorderBrush="{DynamicResource BorderBrush}" BorderThickness="1">
            <Grid>
              <Grid.ColumnDefinitions><ColumnDefinition Width="Auto" /><ColumnDefinition Width="*" /><ColumnDefinition Width="Auto" /></Grid.ColumnDefinitions>
              <Button Grid.Column="0" Width="32" Height="32" Content="○"
                      Command="{Binding DataContext.TodoList.CompleteCommand, RelativeSource={RelativeSource AncestorType=Window}}"
                      CommandParameter="{Binding}" />
              <Button Grid.Column="1" HorizontalContentAlignment="Left" Background="Transparent" BorderThickness="0"
                      Command="{Binding DataContext.EditTodoCommand, RelativeSource={RelativeSource AncestorType=Window}}"
                      CommandParameter="{Binding}">
                <StackPanel><TextBlock FontWeight="SemiBold" TextWrapping="Wrap" Text="{Binding Title}" /><TextBlock Foreground="{DynamicResource MutedTextBrush}" TextTrimming="CharacterEllipsis" Text="{Binding Note}" /></StackPanel>
              </Button>
              <Button Grid.Column="2" Content="删除"
                      Command="{Binding DataContext.TodoList.DeleteCommand, RelativeSource={RelativeSource AncestorType=Window}}"
                      CommandParameter="{Binding}" />
            </Grid>
          </Border>
        </DataTemplate>
      </ListBox.ItemTemplate>
    </ListBox>
    <TextBlock Grid.Row="2" HorizontalAlignment="Center" VerticalAlignment="Center"
               Foreground="{DynamicResource MutedTextBrush}" Text="暂无未完成待办">
      <TextBlock.Style>
        <Style TargetType="TextBlock">
          <Setter Property="Visibility" Value="Collapsed" />
          <Style.Triggers>
            <DataTrigger Binding="{Binding TodoList.IncompleteItems.Count}" Value="0">
              <Setter Property="Visibility" Value="Visible" />
            </DataTrigger>
          </Style.Triggers>
        </Style>
      </TextBlock.Style>
    </TextBlock>
    <Button Grid.Row="3" Margin="0,10,0,0" Content="＋ 创建新待办"
            Command="{Binding NavigateCommand}" CommandParameter="{x:Static models:NavigationPage.Create}" />
  </Grid>
</UserControl>
```

- [ ] **Step 6：创建已完成列表页**

创建 `src/DeskNote.App/Views/CompletedTodosView.xaml`：

```xml
<UserControl x:Class="DeskNote.App.Views.CompletedTodosView"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <Grid>
    <Grid.RowDefinitions><RowDefinition Height="Auto" /><RowDefinition Height="*" /><RowDefinition Height="Auto" /></Grid.RowDefinitions>
    <TextBlock Grid.Row="0" FontSize="22" FontWeight="SemiBold" Text="已完成待办" />
    <ListBox Grid.Row="1" ItemsSource="{Binding TodoList.CompletedItems}">
      <ListBox.ItemTemplate>
        <DataTemplate>
          <Border Margin="0,4" Padding="10" CornerRadius="10"
                  Background="{DynamicResource PanelBrush}"
                  BorderBrush="{DynamicResource BorderBrush}" BorderThickness="1">
            <Grid>
              <Grid.ColumnDefinitions><ColumnDefinition Width="*" /><ColumnDefinition Width="Auto" /><ColumnDefinition Width="Auto" /></Grid.ColumnDefinitions>
              <StackPanel Grid.Column="0"><TextBlock TextDecorations="Strikethrough" TextWrapping="Wrap" Text="{Binding Title}" /><TextBlock Foreground="{DynamicResource MutedTextBrush}" Text="{Binding CompletedAt, StringFormat='完成于 {0:g}'}" /></StackPanel>
              <Button Grid.Column="1" Content="恢复"
                      Command="{Binding DataContext.TodoList.RestoreCommand, RelativeSource={RelativeSource AncestorType=Window}}"
                      CommandParameter="{Binding}" />
              <Button Grid.Column="2" Content="删除"
                      Command="{Binding DataContext.TodoList.DeleteCommand, RelativeSource={RelativeSource AncestorType=Window}}"
                      CommandParameter="{Binding}" />
            </Grid>
          </Border>
        </DataTemplate>
      </ListBox.ItemTemplate>
    </ListBox>
    <TextBlock Grid.Row="1" HorizontalAlignment="Center" VerticalAlignment="Center"
               Foreground="{DynamicResource MutedTextBrush}" Text="暂无已完成待办">
      <TextBlock.Style>
        <Style TargetType="TextBlock">
          <Setter Property="Visibility" Value="Collapsed" />
          <Style.Triggers>
            <DataTrigger Binding="{Binding TodoList.CompletedItems.Count}" Value="0">
              <Setter Property="Visibility" Value="Visible" />
            </DataTrigger>
          </Style.Triggers>
        </Style>
      </TextBlock.Style>
    </TextBlock>
    <Button Grid.Row="2" HorizontalAlignment="Right" Margin="0,10,0,0"
            Content="全部清空" Command="{Binding TodoList.ClearCompletedCommand}" />
  </Grid>
</UserControl>
```

- [ ] **Step 7：为三个 UserControl 添加最小代码隐藏文件**

创建 `src/DeskNote.App/Views/TodoEditorView.xaml.cs`：

```csharp
using System.Windows.Controls;

namespace DeskNote.App.Views;

public partial class TodoEditorView : UserControl
{
    public TodoEditorView() => InitializeComponent();
}
```

创建 `src/DeskNote.App/Views/IncompleteTodosView.xaml.cs`：

```csharp
using System.Windows.Controls;

namespace DeskNote.App.Views;

public partial class IncompleteTodosView : UserControl
{
    public IncompleteTodosView() => InitializeComponent();
}
```

创建 `src/DeskNote.App/Views/CompletedTodosView.xaml.cs`：

```csharp
using System.Windows.Controls;

namespace DeskNote.App.Views;

public partial class CompletedTodosView : UserControl
{
    public CompletedTodosView() => InitializeComponent();
}
```

- [ ] **Step 8：验证 XAML 编译和 ViewModel 测试**

运行：

```powershell
dotnet build DeskNote.sln -c Debug
dotnet test DeskNote.sln -c Debug --no-build
```

预期：XAML 和生成命令全部编译通过，测试无回归。此时尚未建立 App 组合根，因此不要求程序完整启动。

- [ ] **Step 9：停下并交由用户手动记录 Git 版本**

报告新增页面、动画行为和构建结果，不运行 Git 写操作。

---

### Task 9：实现可回滚的数据目录迁移

**文件：**

- 修改：`src/DeskNote.App/Infrastructure/JsonDataLocator.cs`
- 修改：`src/DeskNote.App/Repositories/SqliteTodoRepository.cs`
- 修改：`src/DeskNote.App/Services/SettingsService.cs`
- 创建：`src/DeskNote.App/Infrastructure/FileSystemFacade.cs`
- 创建：`src/DeskNote.App/Services/DataOperationGate.cs`
- 创建：`src/DeskNote.App/Services/GatedTodoService.cs`
- 创建：`src/DeskNote.App/Services/DataMigrationService.cs`
- 创建：`tests/DeskNote.Tests/Services/DataMigrationServiceTests.cs`

- [ ] **Step 1：先写成功迁移与定位切换失败测试**

创建 `tests/DeskNote.Tests/Services/DataMigrationServiceTests.cs`：

```csharp
using DeskNote.App.Infrastructure;
using DeskNote.App.Repositories;
using DeskNote.App.Services;

namespace DeskNote.Tests.Services;

public sealed class DataMigrationServiceTests : IAsyncLifetime
{
    private readonly string root = Path.Combine(Path.GetTempPath(), $"desk-note-{Guid.NewGuid():N}");
    private string current = string.Empty;
    private string target = string.Empty;

    public async Task InitializeAsync()
    {
        current = Path.Combine(root, "current");
        target = Path.Combine(root, "target");
        Directory.CreateDirectory(current);
        await DatabaseInitializer.InitializeAsync(Path.Combine(current, "todos.db"));
        await File.WriteAllTextAsync(Path.Combine(current, "settings.json"), "{\"schemaVersion\":1}");
    }

    [Fact]
    public async Task MigrateAsync_CopiesVerifiesAndSwitchesLocation()
    {
        var locator = new TestDataLocator(current);
        var settings = new SettingsService(locator, new JsonSettingsStore());
        await settings.InitializeAsync();
        var service = CreateService(settings, locator);

        var result = await service.MigrateAsync(target);

        Assert.True(result.Succeeded);
        Assert.Equal(Path.GetFullPath(target), settings.DataDirectory);
        Assert.True(File.Exists(Path.Combine(target, "todos.db")));
        Assert.True(File.Exists(Path.Combine(current, "todos.db")));
        Assert.True(await DatabaseInitializer.IsHealthyAsync(Path.Combine(target, "todos.db")));
    }

    [Fact]
    public async Task MigrateAsync_RollsBackWhenLocatorSwitchFails()
    {
        var locator = new TestDataLocator(current) { FailNextSet = true };
        var settings = new SettingsService(locator, new JsonSettingsStore());
        await settings.InitializeAsync();
        var service = CreateService(settings, locator);

        var result = await service.MigrateAsync(target);

        Assert.False(result.Succeeded);
        Assert.Equal(Path.GetFullPath(current), settings.DataDirectory);
        Assert.True(File.Exists(Path.Combine(current, "todos.db")));
    }

    private static DataMigrationService CreateService(SettingsService settings, IDataLocator locator) => new(
        settings,
        locator,
        new DataOperationGate(),
        new FileSystemFacade(),
        new DatabaseVerifier());

    public Task DisposeAsync()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, true);
        }

        return Task.CompletedTask;
    }

    private sealed class TestDataLocator(string initialPath) : IDataLocator
    {
        private string currentPath = Path.GetFullPath(initialPath);
        public bool FailNextSet { get; init; }

        public Task<string> GetOrCreateAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(currentPath);

        public Task SetAsync(string dataDirectory, CancellationToken cancellationToken = default)
        {
            if (FailNextSet)
            {
                throw new IOException("locator write failed");
            }

            currentPath = Path.GetFullPath(dataDirectory);
            return Task.CompletedTask;
        }
    }
}
```

- [ ] **Step 2：运行测试并确认迁移类型缺失**

运行：

```powershell
dotnet test tests/DeskNote.Tests/DeskNote.Tests.csproj --filter DataMigrationServiceTests
```

预期：FAIL，缺少定位接口、操作门和迁移服务。

- [ ] **Step 3：抽象定位接口并让设置服务依赖接口**

在 `src/DeskNote.App/Infrastructure/JsonDataLocator.cs` 的命名空间后添加：

```csharp
public interface IDataLocator
{
    Task<string> GetOrCreateAsync(CancellationToken cancellationToken = default);
    Task SetAsync(string dataDirectory, CancellationToken cancellationToken = default);
}
```

把类型声明改为：

```csharp
public sealed class JsonDataLocator : IDataLocator
```

在 `src/DeskNote.App/Services/SettingsService.cs` 中，把字段和构造函数参数从 `JsonDataLocator` 改为 `IDataLocator`：

```csharp
private readonly IDataLocator locator;

public SettingsService(IDataLocator locator, JsonSettingsStore store)
{
    this.locator = locator;
    this.store = store;
}
```

- [ ] **Step 4：让 SQLite 仓储在每次操作时获取当前路径**

对 `src/DeskNote.App/Repositories/SqliteTodoRepository.cs` 应用以下精确修改：

```diff
-public sealed class SqliteTodoRepository(string databaseFile) : ITodoRepository
+public sealed class SqliteTodoRepository : ITodoRepository
 {
-    private readonly string connectionString = DatabaseInitializer.BuildConnectionString(databaseFile);
+    private readonly Func<string> databaseFileProvider;
+
+    public SqliteTodoRepository(string databaseFile) : this(() => databaseFile)
+    {
+    }
+
+    public SqliteTodoRepository(Func<string> databaseFileProvider)
+    {
+        this.databaseFileProvider = databaseFileProvider;
+    }
```

在同一文件的 `OpenAsync` 方法中应用：

```diff
 private async Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken)
 {
-    var connection = new SqliteConnection(connectionString);
+    var connection = new SqliteConnection(
+        DatabaseInitializer.BuildConnectionString(databaseFileProvider()));
     await connection.OpenAsync(cancellationToken);
     return connection;
 }
```

- [ ] **Step 5：实现共享数据操作门和待办服务装饰器**

创建 `src/DeskNote.App/Services/DataOperationGate.cs`：

```csharp
namespace DeskNote.App.Services;

public sealed class DataOperationGate
{
    private readonly SemaphoreSlim semaphore = new(1, 1);

    public async Task RunAsync(Func<Task> action, CancellationToken cancellationToken = default)
    {
        await semaphore.WaitAsync(cancellationToken);
        try { await action(); }
        finally { semaphore.Release(); }
    }

    public async Task<T> RunAsync<T>(Func<Task<T>> action, CancellationToken cancellationToken = default)
    {
        await semaphore.WaitAsync(cancellationToken);
        try { return await action(); }
        finally { semaphore.Release(); }
    }

    public async Task<IAsyncDisposable> PauseAsync(CancellationToken cancellationToken = default)
    {
        await semaphore.WaitAsync(cancellationToken);
        return new Releaser(semaphore);
    }

    private sealed class Releaser(SemaphoreSlim semaphore) : IAsyncDisposable
    {
        public ValueTask DisposeAsync()
        {
            semaphore.Release();
            return ValueTask.CompletedTask;
        }
    }
}
```

创建 `src/DeskNote.App/Services/GatedTodoService.cs`：

```csharp
using DeskNote.App.Models;

namespace DeskNote.App.Services;

public sealed class GatedTodoService(ITodoService inner, DataOperationGate gate) : ITodoService
{
    public Task<IReadOnlyList<TodoItem>> ListIncompleteAsync(TodoSortDirection direction, CancellationToken cancellationToken = default) =>
        gate.RunAsync(() => inner.ListIncompleteAsync(direction, cancellationToken), cancellationToken);

    public Task<IReadOnlyList<TodoItem>> ListCompletedAsync(CancellationToken cancellationToken = default) =>
        gate.RunAsync(() => inner.ListCompletedAsync(cancellationToken), cancellationToken);

    public Task<TodoItem> CreateAsync(string? title, string? note, CancellationToken cancellationToken = default) =>
        gate.RunAsync(() => inner.CreateAsync(title, note, cancellationToken), cancellationToken);

    public Task<TodoItem> UpdateAsync(Guid id, string? title, string? note, CancellationToken cancellationToken = default) =>
        gate.RunAsync(() => inner.UpdateAsync(id, title, note, cancellationToken), cancellationToken);

    public Task CompleteAsync(Guid id, CancellationToken cancellationToken = default) =>
        gate.RunAsync(() => inner.CompleteAsync(id, cancellationToken), cancellationToken);

    public Task RestoreAsync(Guid id, CancellationToken cancellationToken = default) =>
        gate.RunAsync(() => inner.RestoreAsync(id, cancellationToken), cancellationToken);

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default) =>
        gate.RunAsync(() => inner.DeleteAsync(id, cancellationToken), cancellationToken);

    public Task DeleteCompletedAsync(CancellationToken cancellationToken = default) =>
        gate.RunAsync(() => inner.DeleteCompletedAsync(cancellationToken), cancellationToken);
}
```

- [ ] **Step 6：实现可替换文件系统和数据库验证器**

创建 `src/DeskNote.App/Infrastructure/FileSystemFacade.cs`：

```csharp
namespace DeskNote.App.Infrastructure;

public interface IFileSystemFacade
{
    void EnsureEmptyTarget(string targetDirectory);
    void CopyDirectory(string sourceDirectory, string targetDirectory);
    void MoveDirectory(string sourceDirectory, string targetDirectory);
}

public sealed class FileSystemFacade : IFileSystemFacade
{
    public void EnsureEmptyTarget(string targetDirectory)
    {
        if (!Directory.Exists(targetDirectory))
        {
            return;
        }

        if (Directory.EnumerateFileSystemEntries(targetDirectory).Any())
        {
            throw new IOException("目标数据目录必须为空。");
        }

        Directory.Delete(targetDirectory);
    }

    public void CopyDirectory(string sourceDirectory, string targetDirectory)
    {
        Directory.CreateDirectory(targetDirectory);
        foreach (var directory in Directory.EnumerateDirectories(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(Path.Combine(targetDirectory, Path.GetRelativePath(sourceDirectory, directory)));
        }

        foreach (var file in Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            var destination = Path.Combine(targetDirectory, Path.GetRelativePath(sourceDirectory, file));
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(file, destination, true);
        }
    }

    public void MoveDirectory(string sourceDirectory, string targetDirectory) =>
        Directory.Move(sourceDirectory, targetDirectory);
}
```

- [ ] **Step 7：实现迁移编排与回滚**

创建 `src/DeskNote.App/Services/DataMigrationService.cs`：

```csharp
using System.Text.Json;
using System.Text.Json.Serialization;
using DeskNote.App.Infrastructure;
using DeskNote.App.Models;
using DeskNote.App.Repositories;

namespace DeskNote.App.Services;

public interface IDatabaseVerifier
{
    Task<bool> IsHealthyAsync(string databaseFile, CancellationToken cancellationToken = default);
}

public sealed class DatabaseVerifier : IDatabaseVerifier
{
    public Task<bool> IsHealthyAsync(string databaseFile, CancellationToken cancellationToken = default) =>
        DatabaseInitializer.IsHealthyAsync(databaseFile, cancellationToken);
}

public sealed record DataMigrationResult(bool Succeeded, string? ErrorMessage)
{
    public static DataMigrationResult Success() => new(true, null);
    public static DataMigrationResult Failure(string message) => new(false, message);
}

public sealed class DataMigrationService
{
    private readonly SettingsService settings;
    private readonly IDataLocator locator;
    private readonly DataOperationGate gate;
    private readonly IFileSystemFacade fileSystem;
    private readonly IDatabaseVerifier databaseVerifier;

    public DataMigrationService(
        SettingsService settings,
        IDataLocator locator,
        DataOperationGate gate,
        IFileSystemFacade fileSystem,
        IDatabaseVerifier databaseVerifier)
    {
        this.settings = settings;
        this.locator = locator;
        this.gate = gate;
        this.fileSystem = fileSystem;
        this.databaseVerifier = databaseVerifier;
    }

    public async Task<DataMigrationResult> MigrateAsync(
        string targetDirectory,
        CancellationToken cancellationToken = default)
    {
        var current = Path.GetFullPath(settings.DataDirectory);
        var target = Path.GetFullPath(targetDirectory);
        if (StringComparer.OrdinalIgnoreCase.Equals(current.TrimEnd(Path.DirectorySeparatorChar), target.TrimEnd(Path.DirectorySeparatorChar)))
        {
            return DataMigrationResult.Success();
        }

        if (target.StartsWith(current + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            return DataMigrationResult.Failure("新数据目录不能位于当前数据目录内部。");
        }

        var staging = $"{target}.desk-note-staging-{Guid.NewGuid():N}";
        await using var pause = await gate.PauseAsync(cancellationToken);
        try
        {
            EnsureCapacity(current, target);
            fileSystem.EnsureEmptyTarget(target);
            CreateBackup(current);
            fileSystem.CopyDirectory(current, staging);

            if (!await databaseVerifier.IsHealthyAsync(Path.Combine(staging, "todos.db"), cancellationToken))
            {
                throw new InvalidDataException("迁移后的数据库完整性检查失败。");
            }

            var settingsFile = Path.Combine(staging, "settings.json");
            if (!File.Exists(settingsFile))
            {
                throw new InvalidDataException("迁移后的设置文件不存在。");
            }

            var settingsJson = await File.ReadAllTextAsync(settingsFile, cancellationToken);
            var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            jsonOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
            if (JsonSerializer.Deserialize<AppSettings>(settingsJson, jsonOptions) is null)
            {
                throw new InvalidDataException("迁移后的设置文件内容为空。");
            }
            fileSystem.MoveDirectory(staging, target);
            await locator.SetAsync(target, cancellationToken);
            settings.SwitchDataDirectory(target);

            if (!await databaseVerifier.IsHealthyAsync(settings.Paths.DatabaseFile, cancellationToken))
            {
                throw new InvalidDataException("切换后的数据库无法打开。");
            }

            return DataMigrationResult.Success();
        }
        catch (Exception exception)
        {
            try
            {
                await locator.SetAsync(current, cancellationToken);
                settings.SwitchDataDirectory(current);
            }
            catch
            {
                // 保留原始异常；恢复入口会显示当前和旧路径供人工处理。
            }

            return DataMigrationResult.Failure(exception.Message);
        }
        finally
        {
            if (Directory.Exists(staging))
            {
                Directory.Delete(staging, true);
            }
        }
    }

    private static void CreateBackup(string currentDirectory)
    {
        var backupDirectory = Path.Combine(
            currentDirectory,
            "backups",
            $"migration-{DateTime.UtcNow:yyyyMMddHHmmssfff}");
        Directory.CreateDirectory(backupDirectory);
        foreach (var fileName in new[] { "todos.db", "settings.json" })
        {
            var source = Path.Combine(currentDirectory, fileName);
            if (File.Exists(source))
            {
                File.Copy(source, Path.Combine(backupDirectory, fileName));
            }
        }
    }

    private static void EnsureCapacity(string currentDirectory, string targetDirectory)
    {
        var requiredBytes = Directory.EnumerateFiles(currentDirectory, "*", SearchOption.AllDirectories)
            .Sum(path => new FileInfo(path).Length);
        var root = Path.GetPathRoot(targetDirectory)
            ?? throw new IOException("无法确定目标磁盘。");
        var drive = new DriveInfo(root);
        const long safetyMargin = 10 * 1024 * 1024;
        if (drive.AvailableFreeSpace < requiredBytes + safetyMargin)
        {
            throw new IOException("目标磁盘可用空间不足。");
        }
    }
}
```

- [ ] **Step 8：运行迁移、仓储和完整测试**

运行：

```powershell
dotnet test tests/DeskNote.Tests/DeskNote.Tests.csproj --filter "DataMigrationServiceTests|SqliteTodoRepositoryTests"
dotnet test DeskNote.sln
```

预期：成功迁移与回滚测试通过；旧目录和旧数据库保持存在。

- [ ] **Step 9：停下并交由用户手动记录 Git 版本**

报告目标目录、备份目录、回滚测试结果，不运行 Git 写操作。

---

### Task 10：实现设置页、主题切换和开机自启动

**文件：**

- 修改：`src/DeskNote.App/Services/DataMigrationService.cs`
- 创建：`src/DeskNote.App/Services/StartupService.cs`
- 创建：`src/DeskNote.App/ViewModels/SettingsViewModel.cs`
- 修改：`src/DeskNote.App/ViewModels/MainViewModel.cs`
- 创建：`src/DeskNote.App/Views/SettingsView.xaml`
- 创建：`src/DeskNote.App/Views/SettingsView.xaml.cs`
- 修改：`src/DeskNote.App/Views/MainWindow.xaml`
- 修改：`src/DeskNote.App/Views/MainWindow.xaml.cs`
- 创建：`tests/DeskNote.Tests/ViewModels/SettingsViewModelTests.cs`

- [ ] **Step 1：先写自启动失败时的界面状态测试**

创建 `tests/DeskNote.Tests/ViewModels/SettingsViewModelTests.cs`：

```csharp
using DeskNote.App.Infrastructure;
using DeskNote.App.Services;
using DeskNote.App.ViewModels;

namespace DeskNote.Tests.ViewModels;

public sealed class SettingsViewModelTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), $"desk-note-{Guid.NewGuid():N}");

    [Fact]
    public async Task SetStartWithWindowsCommand_RestoresOldValueWhenRegistryWriteFails()
    {
        var settings = new SettingsService(
            new JsonDataLocator(Path.Combine(root, "locator.json"), Path.Combine(root, "data")),
            new JsonSettingsStore());
        await settings.InitializeAsync();
        var viewModel = new SettingsViewModel(
            settings,
            new ThemeService(),
            new FailingStartupService(),
            new NoOpMigrationService());

        await viewModel.SetStartWithWindowsCommand.ExecuteAsync(true);

        Assert.False(viewModel.StartWithWindows);
        Assert.Equal("无法修改开机自启动。", viewModel.ErrorMessage);
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, true);
        }
    }

    private sealed class FailingStartupService : IStartupService
    {
        public bool IsEnabled() => false;
        public void SetEnabled(bool enabled) => throw new UnauthorizedAccessException();
    }

    private sealed class NoOpMigrationService : IDataMigrationService
    {
        public Task<DataMigrationResult> MigrateAsync(string targetDirectory, CancellationToken cancellationToken = default) =>
            Task.FromResult(DataMigrationResult.Success());
    }
}
```

- [ ] **Step 2：运行测试并确认设置 ViewModel 类型缺失**

运行：

```powershell
dotnet test tests/DeskNote.Tests/DeskNote.Tests.csproj --filter SettingsViewModelTests
```

预期：FAIL，缺少 `IStartupService`、`IDataMigrationService` 和 `SettingsViewModel`。

- [ ] **Step 3：为迁移服务添加接口**

在 `src/DeskNote.App/Services/DataMigrationService.cs` 中添加接口，并让实现类实现它：

```csharp
public interface IDataMigrationService
{
    Task<DataMigrationResult> MigrateAsync(
        string targetDirectory,
        CancellationToken cancellationToken = default);
}

public sealed class DataMigrationService : IDataMigrationService
```

- [ ] **Step 4：实现当前用户注册表启动项**

创建 `src/DeskNote.App/Services/StartupService.cs`：

```csharp
using Microsoft.Win32;

namespace DeskNote.App.Services;

public interface IStartupService
{
    bool IsEnabled();
    void SetEnabled(bool enabled);
}

public sealed class StartupService : IStartupService
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "desk-note";

    public bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, false);
        var actual = key?.GetValue(ValueName) as string;
        return string.Equals(actual, QuotedExecutablePath(), StringComparison.OrdinalIgnoreCase);
    }

    public void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey, true)
            ?? throw new InvalidOperationException("无法打开 Windows 启动项。");
        if (enabled)
        {
            key.SetValue(ValueName, QuotedExecutablePath(), RegistryValueKind.String);
        }
        else
        {
            key.DeleteValue(ValueName, false);
        }
    }

    private static string QuotedExecutablePath()
    {
        var executable = Environment.ProcessPath
            ?? throw new InvalidOperationException("无法确定程序路径。");
        return $"\"{executable}\"";
    }
}
```

- [ ] **Step 5：实现设置 ViewModel**

创建 `src/DeskNote.App/ViewModels/SettingsViewModel.cs`：

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DeskNote.App.Models;
using DeskNote.App.Services;

namespace DeskNote.App.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly SettingsService settings;
    private readonly ThemeService themeService;
    private readonly IStartupService startupService;
    private readonly IDataMigrationService migrationService;

    public SettingsViewModel(
        SettingsService settings,
        ThemeService themeService,
        IStartupService startupService,
        IDataMigrationService migrationService)
    {
        this.settings = settings;
        this.themeService = themeService;
        this.startupService = startupService;
        this.migrationService = migrationService;
        Theme = settings.Current.Theme;
        AlwaysOnTop = settings.Current.AlwaysOnTop;
        StartWithWindows = settings.Current.StartWithWindows;
        DataDirectory = settings.DataDirectory;
        if (startupService.IsEnabled() != StartWithWindows)
        {
            ErrorMessage = "开机自启动设置尚未在 Windows 中生效。";
        }
    }

    [ObservableProperty]
    private ThemeMode theme;

    [ObservableProperty]
    private bool startWithWindows;

    [ObservableProperty]
    private bool alwaysOnTop;

    [ObservableProperty]
    private string dataDirectory = string.Empty;

    [ObservableProperty]
    private string? errorMessage;

    [ObservableProperty]
    private bool isMigrating;

    [RelayCommand]
    private async Task SetThemeAsync(ThemeMode mode)
    {
        ErrorMessage = null;
        try
        {
            await settings.UpdateAsync(value => value.Theme = mode);
            Theme = mode;
            themeService.Apply(mode);
        }
        catch (Exception)
        {
            ErrorMessage = "无法保存颜色模式。";
        }
    }

    [RelayCommand]
    private async Task SetAlwaysOnTopAsync(bool enabled)
    {
        ErrorMessage = null;
        try
        {
            await settings.UpdateAsync(value => value.AlwaysOnTop = enabled);
            AlwaysOnTop = enabled;
        }
        catch (Exception)
        {
            ErrorMessage = "无法保存窗口置顶设置。";
        }
    }

    [RelayCommand]
    private async Task SetStartWithWindowsAsync(bool enabled)
    {
        var previous = StartWithWindows;
        ErrorMessage = null;
        try
        {
            startupService.SetEnabled(enabled);
            try
            {
                await settings.UpdateAsync(value => value.StartWithWindows = enabled);
            }
            catch
            {
                startupService.SetEnabled(previous);
                throw;
            }

            StartWithWindows = enabled;
        }
        catch (Exception)
        {
            StartWithWindows = previous;
            ErrorMessage = "无法修改开机自启动。";
        }
    }

    [RelayCommand]
    private async Task MigrateDataAsync(string targetDirectory)
    {
        IsMigrating = true;
        ErrorMessage = null;
        try
        {
            var result = await migrationService.MigrateAsync(targetDirectory);
            if (!result.Succeeded)
            {
                ErrorMessage = result.ErrorMessage;
                return;
            }

            DataDirectory = settings.DataDirectory;
        }
        catch (Exception)
        {
            ErrorMessage = "无法迁移数据目录。";
        }
        finally
        {
            IsMigrating = false;
        }
    }
}
```

- [ ] **Step 6：把设置 ViewModel 接入主 ViewModel**

把 `MainViewModel` 构造函数改为接收 `SettingsViewModel settings`，并增加属性：

```csharp
public MainViewModel(
    TodoEditorViewModel editor,
    TodoListViewModel todoList,
    SettingsViewModel settings)
{
    Editor = editor;
    TodoList = todoList;
    Settings = settings;
    Editor.Saved += OnEditorSaved;
}

public SettingsViewModel Settings { get; }
```

- [ ] **Step 7：创建设置页及目录选择代码**

创建 `src/DeskNote.App/Views/SettingsView.xaml`：

```xml
<UserControl x:Class="DeskNote.App.Views.SettingsView"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:models="clr-namespace:DeskNote.App.Models">
  <StackPanel>
    <TextBlock FontSize="22" FontWeight="SemiBold" Text="设置" />
    <TextBlock Margin="0,6" Foreground="{DynamicResource DangerBrush}" Text="{Binding ErrorMessage}" />
    <CheckBox Margin="0,10" Content="开机自启动" IsChecked="{Binding StartWithWindows, Mode=OneWay}">
      <CheckBox.Command><Binding Path="SetStartWithWindowsCommand" /></CheckBox.Command>
      <CheckBox.CommandParameter><Binding RelativeSource="{RelativeSource Self}" Path="IsChecked" /></CheckBox.CommandParameter>
    </CheckBox>
    <CheckBox Margin="0,10" Content="窗口置顶" IsChecked="{Binding AlwaysOnTop, Mode=OneWay}">
      <CheckBox.Command><Binding Path="SetAlwaysOnTopCommand" /></CheckBox.Command>
      <CheckBox.CommandParameter><Binding RelativeSource="{RelativeSource Self}" Path="IsChecked" /></CheckBox.CommandParameter>
    </CheckBox>
    <TextBlock Margin="0,10,0,4" FontWeight="SemiBold" Text="颜色模式" />
    <StackPanel Orientation="Horizontal">
      <Button Content="浅色" Command="{Binding SetThemeCommand}" CommandParameter="{x:Static models:ThemeMode.Light}" />
      <Button Content="深色" Command="{Binding SetThemeCommand}" CommandParameter="{x:Static models:ThemeMode.Dark}" />
    </StackPanel>
    <TextBlock Margin="0,18,0,4" FontWeight="SemiBold" Text="数据位置" />
    <TextBlock TextWrapping="Wrap" Foreground="{DynamicResource MutedTextBrush}" Text="{Binding DataDirectory}" />
    <Button Margin="0,8,0,0" HorizontalAlignment="Left" Click="OnChooseDataDirectory" Content="选择并迁移…" />
    <ProgressBar Margin="0,8" Height="4" IsIndeterminate="True">
      <ProgressBar.Style>
        <Style TargetType="ProgressBar">
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
</UserControl>
```

创建 `src/DeskNote.App/Views/SettingsView.xaml.cs`：

```csharp
using System.Windows;
using System.Windows.Controls;
using DeskNote.App.ViewModels;
using Microsoft.Win32;

namespace DeskNote.App.Views;

public partial class SettingsView : UserControl
{
    public SettingsView() => InitializeComponent();

    private async void OnChooseDataDirectory(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "选择 desk-note 数据目录" };
        if (dialog.ShowDialog() == true && DataContext is SettingsViewModel viewModel)
        {
            await viewModel.MigrateDataCommand.ExecuteAsync(dialog.FolderName);
        }
    }
}
```

- [ ] **Step 8：把设置页和标题栏置顶按钮接入主窗口**

在 `MainWindow.xaml` 的资源中增加：

```xml
<DataTemplate x:Key="SettingsTemplate">
  <views:SettingsView DataContext="{Binding Settings}" />
</DataTemplate>
```

在 `ContentControl.Style` 中增加：

```xml
<DataTrigger Binding="{Binding CurrentPage}" Value="{x:Static models:NavigationPage.Settings}">
  <Setter Property="ContentTemplate" Value="{StaticResource SettingsTemplate}" />
</DataTrigger>
```

把标题栏置顶按钮改为：

```xml
<ToggleButton Width="38" ToolTip="窗口置顶"
              IsChecked="{Binding Settings.AlwaysOnTop, Mode=OneWay}"
              Checked="OnPinChanged" Unchecked="OnPinChanged">📌</ToggleButton>
```

把 `MainWindow.xaml.cs` 中的 `OnPinChanged` 改为：

```csharp
private async void OnPinChanged(object sender, RoutedEventArgs e)
{
    var enabled = sender is System.Windows.Controls.Primitives.ToggleButton { IsChecked: true };
    if (DataContext is ViewModels.MainViewModel viewModel)
    {
        await viewModel.Settings.SetAlwaysOnTopCommand.ExecuteAsync(enabled);
        Topmost = viewModel.Settings.AlwaysOnTop;
    }
}
```

- [ ] **Step 9：运行设置测试、编译和完整测试**

运行：

```powershell
dotnet test tests/DeskNote.Tests/DeskNote.Tests.csproj --filter SettingsViewModelTests
dotnet build DeskNote.sln -c Debug
dotnet test DeskNote.sln -c Debug --no-build
```

预期：设置 ViewModel 测试通过，设置页和文件夹选择器可编译。

- [ ] **Step 10：停下并交由用户手动记录 Git 版本**

报告自启动测试、主题切换和设置页构建结果，不运行 Git 写操作。

---

### Task 11：实现确认对话、单实例、托盘和应用组合根

**文件：**

- 创建：`src/DeskNote.App/Services/ConfirmationService.cs`
- 修改：`src/DeskNote.App/ViewModels/TodoListViewModel.cs`
- 修改：`tests/DeskNote.Tests/ViewModels/TodoListViewModelTests.cs`
- 创建：`src/DeskNote.App/Services/SingleInstanceService.cs`
- 创建：`src/DeskNote.App/Services/TrayIconController.cs`
- 修改：`src/DeskNote.App/Views/MainWindow.xaml.cs`
- 修改：`src/DeskNote.App/App.xaml.cs`

- [ ] **Step 1：先写取消删除的失败测试**

在 `TodoListViewModelTests` 类中增加：

```csharp
[Fact]
public async Task DeleteCommand_DoesNotDeleteWhenUserCancels()
{
    var service = new FakeTodoService();
    var item = CreateTodo(false);
    service.Incomplete.Add(item);
    var viewModel = new TodoListViewModel(service, new RejectConfirmationService());
    await viewModel.LoadCommand.ExecuteAsync(null);

    await viewModel.DeleteCommand.ExecuteAsync(item);

    Assert.Single(viewModel.IncompleteItems);
}

private sealed class RejectConfirmationService : IConfirmationService
{
    public bool Confirm(string message, string title) => false;
}
```

- [ ] **Step 2：运行测试并确认确认服务类型缺失**

运行：

```powershell
dotnet test tests/DeskNote.Tests/DeskNote.Tests.csproj --filter DeleteCommand_DoesNotDeleteWhenUserCancels
```

预期：FAIL，缺少 `IConfirmationService` 和新的 ViewModel 构造函数参数。

- [ ] **Step 3：实现删除和清空确认服务**

创建 `src/DeskNote.App/Services/ConfirmationService.cs`：

```csharp
using System.Windows;

namespace DeskNote.App.Services;

public interface IConfirmationService
{
    bool Confirm(string message, string title);
}

public sealed class ConfirmationService : IConfirmationService
{
    public bool Confirm(string message, string title) =>
        MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;
}

public sealed class AlwaysConfirmService : IConfirmationService
{
    public bool Confirm(string message, string title) => true;
}
```

修改 `TodoListViewModel` 构造函数并增加排序事件：

```csharp
private readonly IConfirmationService confirmationService;

public TodoListViewModel(
    ITodoService todoService,
    IConfirmationService? confirmationService = null)
{
    this.todoService = todoService;
    this.confirmationService = confirmationService ?? new AlwaysConfirmService();
}

public event EventHandler<TodoSortDirection>? SortDirectionChanged;
```

把删除、清空和排序命令改为：

```csharp
[RelayCommand]
private Task DeleteAsync(TodoItem item)
{
    if (!confirmationService.Confirm($"确定永久删除“{item.Title}”吗？", "删除待办"))
    {
        return Task.CompletedTask;
    }

    return RunAsync(async () =>
    {
        await todoService.DeleteAsync(item.Id);
        await ReloadAsync();
    });
}

[RelayCommand]
private Task ClearCompletedAsync()
{
    if (!confirmationService.Confirm($"确定永久删除全部 {CompletedItems.Count} 条已完成待办吗？", "清空已完成待办"))
    {
        return Task.CompletedTask;
    }

    return RunAsync(async () =>
    {
        await todoService.DeleteCompletedAsync();
        await ReloadAsync();
    });
}

[RelayCommand]
private Task SetSortAsync(TodoSortDirection direction) =>
    RunAsync(async () =>
    {
        SortDirection = direction;
        SortDirectionChanged?.Invoke(this, direction);
        await ReloadAsync();
    });
```

- [ ] **Step 4：运行取消删除测试并确认通过**

运行：

```powershell
dotnet test tests/DeskNote.Tests/DeskNote.Tests.csproj --filter DeleteCommand_DoesNotDeleteWhenUserCancels
```

预期：测试通过，取消确认时不会调用删除服务。

- [ ] **Step 5：实现单实例与命名管道唤醒**

创建 `src/DeskNote.App/Services/SingleInstanceService.cs`：

```csharp
using System.IO.Pipes;
using System.Text;

namespace DeskNote.App.Services;

public sealed class SingleInstanceService : IDisposable
{
    private readonly string pipeName;
    private readonly Mutex mutex;

    public SingleInstanceService()
    {
        var identity = Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(Environment.UserName)))[..12];
        pipeName = $"desk-note-{identity}";
        mutex = new Mutex(true, $@"Local\desk-note-{identity}", out var createdNew);
        IsPrimary = createdNew;
    }

    public bool IsPrimary { get; }
    public event EventHandler? ShowRequested;

    public async Task ListenAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await using var server = new NamedPipeServerStream(
                    pipeName,
                    PipeDirection.In,
                    1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                await server.WaitForConnectionAsync(cancellationToken);
                using var reader = new StreamReader(server, Encoding.UTF8, false, leaveOpen: true);
                if (string.Equals(await reader.ReadLineAsync(cancellationToken), "SHOW", StringComparison.Ordinal))
                {
                    ShowRequested?.Invoke(this, EventArgs.Empty);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    public async Task SignalPrimaryAsync(CancellationToken cancellationToken = default)
    {
        await using var client = new NamedPipeClientStream(
            ".",
            pipeName,
            PipeDirection.Out,
            PipeOptions.Asynchronous,
            System.Security.Principal.TokenImpersonationLevel.Identification);
        await client.ConnectAsync(1000, cancellationToken);
        await using var writer = new StreamWriter(client, Encoding.UTF8, leaveOpen: true) { AutoFlush = true };
        await writer.WriteLineAsync("SHOW".AsMemory(), cancellationToken);
    }

    public void Dispose()
    {
        if (IsPrimary)
        {
            mutex.ReleaseMutex();
        }

        mutex.Dispose();
    }
}
```

- [ ] **Step 6：实现系统托盘控制器**

创建 `src/DeskNote.App/Services/TrayIconController.cs`：

```csharp
using System.Drawing;
using System.Windows.Forms;

namespace DeskNote.App.Services;

public sealed class TrayIconController : IDisposable
{
    private readonly NotifyIcon notifyIcon;

    public TrayIconController(Action show, Action create, Action exit)
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("显示 desk-note", null, (_, _) => show());
        menu.Items.Add("创建新待办", null, (_, _) => create());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("退出", null, (_, _) => exit());

        notifyIcon = new NotifyIcon
        {
            Text = "desk-note",
            Icon = SystemIcons.Application,
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
    }
}
```

- [ ] **Step 7：让主窗口暴露隐藏与退出生命周期事件**

在 `MainWindow.xaml.cs` 中增加：

```csharp
public event EventHandler? Hiding;

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
```

把 `OnHide` 改为调用 `HideWindow()`，并把 `OnClosing` 替换为：

```csharp
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
```

保留 `ExitApplication()` 对 `allowClose` 的设置。

- [ ] **Step 8：建立完整应用组合根**

将 `src/DeskNote.App/App.xaml.cs` 替换为：

```csharp
using System.Windows;
using DeskNote.App.Infrastructure;
using DeskNote.App.Repositories;
using DeskNote.App.Services;
using DeskNote.App.ViewModels;
using DeskNote.App.Views;

namespace DeskNote.App;

public partial class App : Application
{
    private readonly CancellationTokenSource shutdownToken = new();
    private SingleInstanceService? singleInstance;
    private TrayIconController? trayIcon;
    private MainWindow? mainWindow;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        singleInstance = new SingleInstanceService();
        if (!singleInstance.IsPrimary)
        {
            try { await singleInstance.SignalPrimaryAsync(); }
            finally { Shutdown(); }
            return;
        }

        var locator = new JsonDataLocator();
        var settingsStore = new JsonSettingsStore();
        var settings = new SettingsService(locator, settingsStore);
        await settings.InitializeAsync();

        var themeService = new ThemeService();
        themeService.Apply(settings.Current.Theme);
        await DatabaseInitializer.InitializeAsync(settings.Paths.DatabaseFile);

        var gate = new DataOperationGate();
        var repository = new SqliteTodoRepository(() => settings.Paths.DatabaseFile);
        var coreTodoService = new TodoService(repository);
        ITodoService todoService = new GatedTodoService(coreTodoService, gate);
        var migrationService = new DataMigrationService(
            settings,
            locator,
            gate,
            new FileSystemFacade(),
            new DatabaseVerifier());

        var todoList = new TodoListViewModel(todoService, new ConfirmationService())
        {
            SortDirection = settings.Current.IncompleteSortDirection
        };
        todoList.SortDirectionChanged += async (_, direction) =>
            await settings.UpdateAsync(value => value.IncompleteSortDirection = direction);

        var editor = new TodoEditorViewModel(todoService);
        var startupService = new StartupService();
        if (startupService.IsEnabled() != settings.Current.StartWithWindows)
        {
            try
            {
                startupService.SetEnabled(settings.Current.StartWithWindows);
            }
            catch (Exception)
            {
                // SettingsViewModel compares desired and actual state and displays the warning.
            }
        }
        var settingsViewModel = new SettingsViewModel(
            settings,
            themeService,
            startupService,
            migrationService);
        var mainViewModel = new MainViewModel(editor, todoList, settingsViewModel);

        mainWindow = new MainWindow { DataContext = mainViewModel, Topmost = settings.Current.AlwaysOnTop };
        var windowState = new WindowStateService();
        windowState.Restore(mainWindow, settings.Current.WindowBounds);
        mainWindow.Hiding += async (_, _) =>
            await settings.UpdateAsync(value => value.WindowBounds = windowState.Capture(mainWindow));

        trayIcon = new TrayIconController(
            () => Dispatcher.Invoke(mainWindow.ShowAndActivate),
            () => Dispatcher.Invoke(() =>
            {
                editor.BeginCreate();
                mainViewModel.CurrentPage = Models.NavigationPage.Create;
                mainWindow.ShowAndActivate();
            }),
            () => Dispatcher.Invoke(() =>
            {
                mainWindow.ExitApplication();
                Shutdown();
            }));

        singleInstance.ShowRequested += (_, _) => Dispatcher.Invoke(mainWindow.ShowAndActivate);
        _ = singleInstance.ListenAsync(shutdownToken.Token);

        await todoList.LoadCommand.ExecuteAsync(null);
        mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        shutdownToken.Cancel();
        trayIcon?.Dispose();
        singleInstance?.Dispose();
        shutdownToken.Dispose();
        base.OnExit(e);
    }
}
```

- [ ] **Step 9：运行应用并验证核心交互**

运行：

```powershell
dotnet run --project src/DeskNote.App/DeskNote.App.csproj
```

预期：

- 主窗口显示，侧栏悬停展开。
- 创建、编辑、完成、恢复和删除数据能跨重启保存。
- 关闭按钮隐藏到托盘，托盘能重新显示窗口和退出。
- 第二次启动只唤醒原窗口，不出现第二个主窗口。

- [ ] **Step 10：运行完整自动化测试**

运行：

```powershell
dotnet test DeskNote.sln -c Debug
```

预期：全部通过，0 个警告。

- [ ] **Step 11：停下并交由用户手动记录 Git 版本**

报告单实例、托盘和端到端手工验证结果，不运行 Git 写操作。

---

### Task 12：实现有限日志与数据库恢复入口

**文件：**

- 创建：`src/DeskNote.App/Services/AppLogger.cs`
- 创建：`src/DeskNote.App/Services/RecoveryService.cs`
- 创建：`src/DeskNote.App/Views/RecoveryWindow.xaml`
- 创建：`src/DeskNote.App/Views/RecoveryWindow.xaml.cs`
- 修改：`src/DeskNote.App/App.xaml.cs`
- 创建：`tests/DeskNote.Tests/Services/RecoveryServiceTests.cs`

- [ ] **Step 1：先写备份恢复测试**

创建 `tests/DeskNote.Tests/Services/RecoveryServiceTests.cs`：

```csharp
using DeskNote.App.Repositories;
using DeskNote.App.Services;

namespace DeskNote.Tests.Services;

public sealed class RecoveryServiceTests : IAsyncLifetime
{
    private readonly string root = Path.Combine(Path.GetTempPath(), $"desk-note-{Guid.NewGuid():N}");

    [Fact]
    public async Task RestoreAsync_ReplacesCurrentDatabaseWithVerifiedBackup()
    {
        var current = Path.Combine(root, "todos.db");
        var backup = Path.Combine(root, "backups", "migration-1", "todos.db");
        Directory.CreateDirectory(Path.GetDirectoryName(backup)!);
        await File.WriteAllTextAsync(current, "broken");
        await DatabaseInitializer.InitializeAsync(backup);
        var service = new RecoveryService(new DatabaseVerifier());

        await service.RestoreAsync(backup, current);

        Assert.True(await DatabaseInitializer.IsHealthyAsync(current));
        Assert.Single(Directory.GetFiles(root, "todos.failed-*.db"));
    }

    public Task InitializeAsync()
    {
        Directory.CreateDirectory(root);
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, true);
        }

        return Task.CompletedTask;
    }
}
```

- [ ] **Step 2：运行测试并确认恢复服务缺失**

运行：

```powershell
dotnet test tests/DeskNote.Tests/DeskNote.Tests.csproj --filter RecoveryServiceTests
```

预期：FAIL，缺少 `RecoveryService`。

- [ ] **Step 3：实现滚动错误日志**

创建 `src/DeskNote.App/Services/AppLogger.cs`：

```csharp
using System.Text;

namespace DeskNote.App.Services;

public sealed class AppLogger(string logFile)
{
    private const long MaxBytes = 1_048_576;
    private readonly SemaphoreSlim gate = new(1, 1);

    public async Task LogErrorAsync(Exception exception, string operation)
    {
        await gate.WaitAsync();
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(logFile)!);
            if (File.Exists(logFile) && new FileInfo(logFile).Length >= MaxBytes)
            {
                File.Move(logFile, $"{logFile}.1", true);
            }

            var line = $"{DateTimeOffset.UtcNow:O}\t{operation}\t{exception.GetType().Name}\t{exception.Message}{Environment.NewLine}";
            await File.AppendAllTextAsync(logFile, line, new UTF8Encoding(false));
        }
        finally
        {
            gate.Release();
        }
    }
}
```

调用方传入的 `operation` 只能是固定操作名，不得包含待办标题或备注。

- [ ] **Step 4：实现人工确认的数据库恢复服务**

创建 `src/DeskNote.App/Services/RecoveryService.cs`：

```csharp
namespace DeskNote.App.Services;

public sealed class RecoveryService(IDatabaseVerifier databaseVerifier)
{
    public IReadOnlyList<string> ListBackups(string backupsDirectory)
    {
        if (!Directory.Exists(backupsDirectory))
        {
            return [];
        }

        return Directory.EnumerateFiles(backupsDirectory, "todos.db", SearchOption.AllDirectories)
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .ToList();
    }

    public async Task RestoreAsync(
        string backupDatabase,
        string currentDatabase,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(backupDatabase))
        {
            throw new FileNotFoundException("所选备份不存在。", backupDatabase);
        }

        if (!await databaseVerifier.IsHealthyAsync(backupDatabase, cancellationToken))
        {
            throw new InvalidDataException("所选备份数据库未通过完整性检查。");
        }

        var directory = Path.GetDirectoryName(currentDatabase)!;
        Directory.CreateDirectory(directory);
        var temporary = $"{currentDatabase}.restore-{Guid.NewGuid():N}";
        var failed = Path.Combine(directory, $"todos.failed-{DateTime.UtcNow:yyyyMMddHHmmssfff}.db");
        try
        {
            File.Copy(backupDatabase, temporary, true);
            if (!await databaseVerifier.IsHealthyAsync(temporary, cancellationToken))
            {
                throw new InvalidDataException("复制后的备份未通过完整性检查。");
            }

            if (File.Exists(currentDatabase))
            {
                File.Move(currentDatabase, failed);
            }

            try
            {
                File.Move(temporary, currentDatabase, true);
            }
            catch
            {
                if (!File.Exists(currentDatabase) && File.Exists(failed))
                {
                    File.Move(failed, currentDatabase);
                }

                throw;
            }
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }
}
```

- [ ] **Step 5：创建恢复窗口**

创建 `src/DeskNote.App/Views/RecoveryWindow.xaml`：

```xml
<Window x:Class="DeskNote.App.Views.RecoveryWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="desk-note 数据恢复" Width="560" Height="380" WindowStartupLocation="CenterScreen">
  <Grid Margin="20">
    <Grid.RowDefinitions><RowDefinition Height="Auto" /><RowDefinition Height="Auto" /><RowDefinition Height="*" /><RowDefinition Height="Auto" /></Grid.RowDefinitions>
    <TextBlock FontSize="20" FontWeight="SemiBold" Text="待办数据库无法正常打开" />
    <TextBlock Grid.Row="1" Margin="0,8" TextWrapping="Wrap" Text="程序已停止写入。请选择一个已通过校验的备份恢复；当前损坏文件会被保留。" />
    <ListBox x:Name="BackupList" Grid.Row="2" Margin="0,10" />
    <StackPanel Grid.Row="3" HorizontalAlignment="Right" Orientation="Horizontal">
      <Button Click="OnExit" Content="退出" />
      <Button Click="OnRestore" Content="恢复所选备份" />
    </StackPanel>
  </Grid>
</Window>
```

创建 `src/DeskNote.App/Views/RecoveryWindow.xaml.cs`：

```csharp
using System.Windows;
using DeskNote.App.Services;

namespace DeskNote.App.Views;

public partial class RecoveryWindow : Window
{
    private readonly RecoveryService recoveryService;
    private readonly string currentDatabase;

    public RecoveryWindow(RecoveryService recoveryService, string currentDatabase, string backupsDirectory)
    {
        InitializeComponent();
        this.recoveryService = recoveryService;
        this.currentDatabase = currentDatabase;
        BackupList.ItemsSource = recoveryService.ListBackups(backupsDirectory);
    }

    private async void OnRestore(object sender, RoutedEventArgs e)
    {
        if (BackupList.SelectedItem is not string backup)
        {
            MessageBox.Show("请先选择一个备份。", "desk-note");
            return;
        }

        if (MessageBox.Show("确定使用所选备份恢复吗？", "desk-note", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            await recoveryService.RestoreAsync(backup, currentDatabase);
            DialogResult = true;
        }
        catch (Exception exception)
        {
            MessageBox.Show(exception.Message, "恢复失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OnExit(object sender, RoutedEventArgs e) => DialogResult = false;
}
```

- [ ] **Step 6：用带健康检查和日志保护的完整启动流程替换组合根**

把 `App.OnStartup` 替换为以下完整方法，并在 `App` 类中增加 `ListenAndLogAsync`：

```csharp
protected override async void OnStartup(StartupEventArgs e)
{
    base.OnStartup(e);
    ShutdownMode = ShutdownMode.OnExplicitShutdown;
    var fallbackLog = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "desk-note",
        "logs",
        "app.log");
    var logger = new AppLogger(fallbackLog);

    try
    {
        singleInstance = new SingleInstanceService();
        if (!singleInstance.IsPrimary)
        {
            try { await singleInstance.SignalPrimaryAsync(); }
            finally { Shutdown(); }
            return;
        }

        var locator = new JsonDataLocator();
        var settingsStore = new JsonSettingsStore();
        var settings = new SettingsService(locator, settingsStore);
        await settings.InitializeAsync();
        logger = new AppLogger(Path.Combine(settings.Paths.LogsDirectory, "app.log"));

        var themeService = new ThemeService();
        themeService.Apply(settings.Current.Theme);
        if (File.Exists(settings.Paths.DatabaseFile)
            && !await DatabaseInitializer.IsHealthyAsync(settings.Paths.DatabaseFile))
        {
            var recovery = new RecoveryWindow(
                new RecoveryService(new DatabaseVerifier()),
                settings.Paths.DatabaseFile,
                settings.Paths.BackupsDirectory);
            if (recovery.ShowDialog() != true)
            {
                Shutdown();
                return;
            }
        }

        await DatabaseInitializer.InitializeAsync(settings.Paths.DatabaseFile);
        var gate = new DataOperationGate();
        var repository = new SqliteTodoRepository(() => settings.Paths.DatabaseFile);
        ITodoService todoService = new GatedTodoService(new TodoService(repository), gate);
        var migrationService = new DataMigrationService(
            settings,
            locator,
            gate,
            new FileSystemFacade(),
            new DatabaseVerifier());

        var todoList = new TodoListViewModel(todoService, new ConfirmationService())
        {
            SortDirection = settings.Current.IncompleteSortDirection
        };
        todoList.SortDirectionChanged += async (_, direction) =>
        {
            try
            {
                await settings.UpdateAsync(value => value.IncompleteSortDirection = direction);
            }
            catch (Exception exception)
            {
                await logger.LogErrorAsync(exception, "save-sort-setting");
            }
        };

        var startupService = new StartupService();
        if (startupService.IsEnabled() != settings.Current.StartWithWindows)
        {
            try
            {
                startupService.SetEnabled(settings.Current.StartWithWindows);
            }
            catch (Exception exception)
            {
                await logger.LogErrorAsync(exception, "reconcile-startup-setting");
            }
        }

        var editor = new TodoEditorViewModel(todoService);
        var settingsViewModel = new SettingsViewModel(
            settings,
            themeService,
            startupService,
            migrationService);
        var mainViewModel = new MainViewModel(editor, todoList, settingsViewModel);

        mainWindow = new MainWindow
        {
            DataContext = mainViewModel,
            Topmost = settings.Current.AlwaysOnTop
        };
        var windowState = new WindowStateService();
        windowState.Restore(mainWindow, settings.Current.WindowBounds);
        mainWindow.Hiding += async (_, _) =>
        {
            try
            {
                await settings.UpdateAsync(value => value.WindowBounds = windowState.Capture(mainWindow));
            }
            catch (Exception exception)
            {
                await logger.LogErrorAsync(exception, "save-window-state");
            }
        };

        trayIcon = new TrayIconController(
            () => Dispatcher.Invoke(mainWindow.ShowAndActivate),
            () => Dispatcher.Invoke(() =>
            {
                editor.BeginCreate();
                mainViewModel.CurrentPage = Models.NavigationPage.Create;
                mainWindow.ShowAndActivate();
            }),
            () => Dispatcher.Invoke(() =>
            {
                mainWindow.ExitApplication();
                Shutdown();
            }));

        singleInstance.ShowRequested += (_, _) => Dispatcher.Invoke(mainWindow.ShowAndActivate);
        _ = ListenAndLogAsync(logger);
        await todoList.LoadCommand.ExecuteAsync(null);
        mainWindow.Show();
    }
    catch (Exception exception)
    {
        await logger.LogErrorAsync(exception, "application-startup");
        MessageBox.Show(
            "desk-note 启动失败。详细信息已写入本地错误日志。",
            "desk-note",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        Shutdown();
    }
}

private async Task ListenAndLogAsync(AppLogger logger)
{
    try
    {
        await singleInstance!.ListenAsync(shutdownToken.Token);
    }
    catch (Exception exception)
    {
        await logger.LogErrorAsync(exception, "single-instance-listener");
    }
}
```

- [ ] **Step 7：运行恢复测试和完整测试**

运行：

```powershell
dotnet test tests/DeskNote.Tests/DeskNote.Tests.csproj --filter RecoveryServiceTests
dotnet test DeskNote.sln
```

预期：备份恢复测试通过，完整测试无回归。

- [ ] **Step 8：手工验证恢复流程并停下供用户记录 Git 版本**

复制一份测试数据库到备份目录，破坏当前测试数据库后启动程序。预期：程序停止写入、显示恢复窗口、恢复后能正常进入主界面，损坏文件以 `todos.failed-*.db` 保留。报告结果，不运行 Git 写操作。

---

### Task 13：发布、性能测量和 Windows 验收

**文件：**

- 修改：`src/DeskNote.App/Infrastructure/JsonDataLocator.cs`
- 创建：`src/DeskNote.App/Properties/PublishProfiles/win-x64.pubxml`
- 创建：`tools/DeskNote.PerformanceData/DeskNote.PerformanceData.csproj`
- 创建：`tools/DeskNote.PerformanceData/Program.cs`
- 修改：`DeskNote.sln`
- 创建：`scripts/measure-performance.ps1`
- 创建：`packaging/desk-note.iss`
- 创建：`docs/testing/windows-acceptance.md`

- [ ] **Step 1：添加隔离的性能测试状态目录入口**

在 `JsonDataLocator` 构造函数中，把 `appRoot` 的计算替换为：

```csharp
var appRootOverride = Environment.GetEnvironmentVariable("DESKNOTE_STATE_ROOT");
var appRoot = string.IsNullOrWhiteSpace(appRootOverride)
    ? Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "desk-note")
    : Path.GetFullPath(appRootOverride);
```

生产环境不设置该变量，行为保持不变。性能脚本使用临时目录，避免接触用户真实数据。

- [ ] **Step 2：添加 win-x64 自包含发布配置**

创建 `src/DeskNote.App/Properties/PublishProfiles/win-x64.pubxml`：

```xml
<Project>
  <PropertyGroup>
    <Configuration>Release</Configuration>
    <RuntimeIdentifier>win-x64</RuntimeIdentifier>
    <SelfContained>true</SelfContained>
    <PublishSingleFile>false</PublishSingleFile>
    <PublishReadyToRun>false</PublishReadyToRun>
    <PublishTrimmed>false</PublishTrimmed>
    <DebugType>none</DebugType>
    <DebugSymbols>false</DebugSymbols>
  </PropertyGroup>
</Project>
```

- [ ] **Step 3：创建 1000 条待办性能数据工具**

运行：

```powershell
dotnet new console --name DeskNote.PerformanceData --output tools/DeskNote.PerformanceData --framework net10.0
dotnet add tools/DeskNote.PerformanceData/DeskNote.PerformanceData.csproj reference src/DeskNote.App/DeskNote.App.csproj
dotnet sln DeskNote.sln add tools/DeskNote.PerformanceData/DeskNote.PerformanceData.csproj
```

将 `tools/DeskNote.PerformanceData/DeskNote.PerformanceData.csproj` 调整为：

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0-windows</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\src\DeskNote.App\DeskNote.App.csproj" />
  </ItemGroup>
</Project>
```

将 `tools/DeskNote.PerformanceData/Program.cs` 设置为：

```csharp
using DeskNote.App.Models;
using DeskNote.App.Repositories;

if (args.Length != 2 || !int.TryParse(args[1], out var count) || count < 1)
{
    Console.Error.WriteLine("Usage: DeskNote.PerformanceData <data-directory> <count>");
    return 1;
}

var dataDirectory = Path.GetFullPath(args[0]);
var databaseFile = Path.Combine(dataDirectory, "todos.db");
Directory.CreateDirectory(dataDirectory);
await DatabaseInitializer.InitializeAsync(databaseFile);
var repository = new SqliteTodoRepository(databaseFile);
var baseline = DateTimeOffset.UtcNow;
for (var index = 0; index < count; index++)
{
    var createdAt = baseline.AddSeconds(-index);
    await repository.InsertAsync(new TodoItem(
        Guid.NewGuid(),
        $"Performance todo {index + 1}",
        "Performance test note",
        false,
        createdAt,
        createdAt,
        null));
}

Console.WriteLine($"Created {count} todos in {databaseFile}");
return 0;
```

- [ ] **Step 4：创建性能测量脚本**

创建 `scripts/measure-performance.ps1`：

```powershell
param(
    [Parameter(Mandatory = $true)]
    [string]$Executable,
    [string]$StateRoot = (Join-Path $env:TEMP ("desk-note-performance-" + [Guid]::NewGuid().ToString("N")))
)

$resolvedExecutable = (Resolve-Path -LiteralPath $Executable).Path
$dataDirectory = Join-Path $StateRoot "data"
New-Item -ItemType Directory -Force -Path $dataDirectory | Out-Null

dotnet run --project tools/DeskNote.PerformanceData/DeskNote.PerformanceData.csproj -- $dataDirectory 1000
if ($LASTEXITCODE -ne 0) {
    throw "Failed to create performance data."
}

$startedAt = Get-Date
$process = Start-Process -FilePath $resolvedExecutable -PassThru -WindowStyle Hidden -Environment @{
    DESKNOTE_STATE_ROOT = $StateRoot
}

for ($attempt = 0; $attempt -lt 100; $attempt++) {
    $process.Refresh()
    if ($process.MainWindowHandle -ne 0) { break }
    Start-Sleep -Milliseconds 50
}

$readyAt = Get-Date
$process.Refresh()
$cpuBefore = $process.TotalProcessorTime.TotalSeconds
$sampleStartedAt = Get-Date
Start-Sleep -Seconds 60
$process.Refresh()
$sampleEndedAt = Get-Date
$cpuAfter = $process.TotalProcessorTime.TotalSeconds
$elapsedSeconds = ($sampleEndedAt - $sampleStartedAt).TotalSeconds
$cpuPercent = (($cpuAfter - $cpuBefore) / ($elapsedSeconds * [Environment]::ProcessorCount)) * 100
$memorySample = Get-CimInstance Win32_PerfFormattedData_PerfProc_Process |
    Where-Object { $_.IDProcess -eq $process.Id } |
    Select-Object -First 1

[pscustomobject]@{
    StartupMilliseconds = [math]::Round(($readyAt - $startedAt).TotalMilliseconds, 0)
    IdleCpuPercent = [math]::Round($cpuPercent, 3)
    PrivateWorkingSetMb = [math]::Round($memorySample.WorkingSetPrivate / 1MB, 1)
    ProcessId = $process.Id
    StateRoot = $StateRoot
}

Stop-Process -Id $process.Id
```

该脚本只终止自己刚启动并通过进程 ID 精确记录的测试进程。

- [ ] **Step 5：创建 Inno Setup 安装脚本**

创建 `packaging/desk-note.iss`：

```ini
#define MyAppName "desk-note"
#define MyAppVersion "1.0.0"
#define MyAppExeName "desk-note.exe"

[Setup]
AppId={{B2DC4D59-4F93-47B8-AF4E-BA697458B7CB}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
DefaultDirName={localappdata}\Programs\desk-note
DefaultGroupName=desk-note
PrivilegesRequired=lowest
OutputDir=output
OutputBaseFilename=desk-note-{#MyAppVersion}-win-x64
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
UninstallDisplayIcon={app}\{#MyAppExeName}
CloseApplications=yes

[Files]
Source: "..\src\DeskNote.App\bin\Release\net10.0-windows\win-x64\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\desk-note"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\desk-note"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "创建桌面快捷方式"; GroupDescription: "附加图标："

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "启动 desk-note"; Flags: nowait postinstall skipifsilent
```

脚本不声明删除 `%LocalAppData%\desk-note` 或自定义数据目录，因此默认卸载保留用户数据。

- [ ] **Step 6：创建 Windows 验收清单**

创建 `docs/testing/windows-acceptance.md`：

```markdown
# desk-note Windows 验收清单

## 基础环境

- [ ] Windows 10 x64 安装、启动、退出和卸载通过。
- [ ] Windows 11 x64 安装、启动、退出和卸载通过。
- [ ] 普通用户安装和运行不请求管理员权限。

## 界面与交互

- [ ] 100%、125%、150%、200% DPI 下没有文字裁切。
- [ ] 侧边栏默认 58px，悬停或键盘聚焦后展开。
- [ ] 浅色和深色主题立即切换，重启后保持。
- [ ] 窗口置顶、移动、缩放和多显示器恢复正确。

## 待办

- [ ] 创建、编辑、完成、恢复、单删和全部清空符合确认语义。
- [ ] 最新/最早排序重启后保持。
- [ ] 1000 条待办列表滚动流畅，界面使用虚拟化。

## Windows 集成

- [ ] 关闭按钮隐藏到托盘；托盘显示、快速新建和退出可用。
- [ ] 第二次启动只唤醒已有窗口。
- [ ] 开机自启动启用、禁用和重启验证通过。

## 数据与恢复

- [ ] 中文、空格和较长数据路径可用。
- [ ] 只读目录、非空目标目录和空间不足时不切换路径。
- [ ] 迁移成功后新旧数据库均可打开。
- [ ] 迁移失败后定位文件和运行状态回到旧目录。
- [ ] 数据库损坏时停止写入并显示恢复入口。

## 性能

- [ ] 冷启动到可操作不高于 1.5 秒。
- [ ] 1000 条待办时专用工作集不高于 120 MB。
- [ ] 空闲 60 秒平均 CPU 不高于 0.5%。
- [ ] 空闲期间没有持续数据库或日志写入。
```

- [ ] **Step 7：构建、测试、发布和测量**

运行：

```powershell
dotnet restore DeskNote.sln
dotnet build DeskNote.sln -c Release --no-restore
dotnet test DeskNote.sln -c Release --no-build
dotnet publish src/DeskNote.App/DeskNote.App.csproj -c Release -p:PublishProfile=win-x64
powershell -ExecutionPolicy Bypass -File scripts/measure-performance.ps1 -Executable src/DeskNote.App/bin/Release/net10.0-windows/win-x64/publish/desk-note.exe
```

预期：构建和测试成功；发布目录存在；性能输出满足设计文档目标。若指标不满足，保留原始测量，定位启动、列表或后台活动的具体原因后再重新测量。

- [ ] **Step 8：生成安装包并执行清单**

在安装 Inno Setup 后运行：

```powershell
& "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe" packaging\desk-note.iss
```

预期：生成 `packaging/output/desk-note-1.0.0-win-x64.exe`。在 Windows 10/11 上逐项完成 `docs/testing/windows-acceptance.md`。

- [ ] **Step 9：执行最终验证并停下供用户手动记录 Git 版本**

再次运行：

```powershell
dotnet test DeskNote.sln -c Release
```

执行代理在宣称完成前必须使用 `superpowers:verification-before-completion`，报告自动化测试、发布、性能和手工验收的实际结果。不得运行 Git 写操作。

---

## 执行顺序与检查点

严格按 Task 1 → Task 13 顺序执行。每个任务都必须先看到预期失败，再编写最小实现并看到测试通过。每个任务结束后暂停，让用户决定何时手动提交 Git 版本。不得因为后续任务会覆盖当前代码而跳过当前测试。
