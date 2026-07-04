# 管理员自启动器

让需要管理员权限的脚本和程序，在你登录 Windows 后自动运行。

## 它能做什么

每次开机后，总有一些工具或脚本需要管理员权限才能正常工作：

- 开发环境初始化脚本
- 后台服务或守护进程
- 需要管理员权限的 PowerShell 脚本
- 需要高权限启动的自定义工具

本软件会在你登录 Windows 后，自动以管理员身份运行这些项目，省去每次手动右键“以管理员身份运行”的麻烦。

## 主要功能

- 添加 PowerShell 脚本（`.ps1`）或可执行程序（`.exe`）
- 登录后以管理员权限自动运行已启用的项目
- 支持手动“立即运行”单个项目
- 支持停止最近启动的主进程
- 保存每个项目的最近运行状态、退出码、错误信息和日志
- 托盘常驻，双击托盘图标即可打开管理器
- 自动保存窗口大小、位置和左右分栏宽度

## 快速开始

### 1. 下载软件

在 GitHub Releases 下载最新的 `AutoElevateLauncher.exe`。

Release 提供的是单文件自包含版本：

- 只需要下载一个 `AutoElevateLauncher.exe`
- 目标电脑不需要额外安装 .NET Runtime
- 建议放到固定目录，例如 `C:\Tools\AutoElevateLauncher\AutoElevateLauncher.exe`
- 不建议放在下载目录或临时目录，因为管理员开机自启任务会记录这个 exe 的路径

### 2. 首次运行

双击运行 `AutoElevateLauncher.exe`，程序会最小化到系统托盘。

### 3. 添加启动项目

1. 双击托盘图标，或右键托盘图标选择“打开管理器”。
2. 点击“新增脚本”选择 `.ps1` 文件，或点击“新增程序”选择 `.exe` 文件。
3. 在右侧填写名称、参数和工作目录（可选）。
4. 勾选“启用此项目”。
5. 点击“保存”。

> **提示**：软件保存的是脚本或程序的路径，不会把文件复制到配置里。添加后请不要删除或移动原文件。建议把脚本放在固定目录，例如 `C:\Users\你的用户名\Documents\AutoElevateLauncher\Scripts\`。

### 4. 配置管理员开机自启

1. 在管理器窗口右上角，点击“配置管理员自启”。
2. 如果当前不是管理员权限，会弹出 UAC 提示，请点击“是”。
3. 配置成功后，状态会显示“管理员开机自启已配置”。

配置完成后，下次登录 Windows 时，本软件会自动以管理员权限启动，并运行所有已启用的项目。

## 日常使用

### 打开管理器

- 双击系统托盘图标
- 或右键托盘图标 → “打开管理器”

### 手动运行所有启用项

右键托盘图标 → “立即运行所有启用项”。

### 停止正在运行的项目

在管理器左侧选中项目，点击右侧的“停止”。

> **注意**：停止功能只会结束最近一次由本软件启动并记录的主进程，不会递归停止子进程。例如：
> - 如果启动的是 `.exe`，停止的是这个 `.exe` 的进程
> - 如果启动的是 `.ps1`，停止的是承载脚本的 `powershell.exe` 进程
> - 如果脚本或程序又启动了其他子进程，子进程不会被自动停止

### 查看运行日志

每个项目都有独立的日志目录。在管理器中选中项目，点击“打开日志”即可查看。

## 数据位置

本软件所有数据都保存在当前用户的应用数据目录下：

- 配置：`%AppData%\AutoElevateLauncher\config.json`
- 日志：`%AppData%\AutoElevateLauncher\logs\`

配置文件保存启动项目、管理员自启状态、窗口大小、窗口位置、左右分栏宽度和最近运行信息。

## 常见问题

### 为什么需要管理员权限？

因为你要启动的脚本或程序本身需要管理员权限。本软件通过 Windows 任务计划程序，在登录时以 `HighestAvailable` 运行级别启动自己，从而获得管理员权限。

### 软件会写入注册表吗？

不会。软件通过 Windows 任务计划程序实现开机自启，不会写入注册表的 `Run` 项。

### 可以关闭管理员开机自启吗？

可以。在管理器中点击“配置管理员自启”，如果已经配置，会提示取消配置。你也可以在 Windows 任务计划程序中手动删除名为 `AutoElevateLauncher-Manager` 的任务。

### 添加项目后删除了原文件会怎样？

软件会在下次运行时提示“文件不存在”。请在管理器中重新选择正确的路径。

### 日志会占用很多空间吗？

日志不会自动清理，会持续累积在日志目录下。如果空间紧张，可以定期手动清理 `%AppData%\AutoElevateLauncher\logs\` 目录。

## 已知限制

- 可执行程序启动成功后会记录进程 ID，并将启动动作视为成功；当前版本不等待长期运行程序退出，也不记录它最终退出码。
- 停止功能只停止主进程，不停止子进程树。
- 日志不会自动清理，会持续累积在日志目录下。
- 旧版本创建的单项目计划任务不会自动删除；如曾使用旧版本，请在 Windows 任务计划程序中手动清理不需要的旧任务。

## 从源码构建

如果你是开发者，想要从源码构建：

需要：

- Windows
- .NET 8 SDK

运行测试：

```powershell
dotnet test "AutoElevateLauncher.sln" --nologo
```

编译普通 Release：

```powershell
dotnet build "AutoElevateLauncher.sln" -c Release
```

发布单文件自包含版本：

```powershell
dotnet publish "src\AutoElevateLauncher\AutoElevateLauncher.csproj" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o "publish\win-x64-single"
```

生成文件：

`publish\win-x64-single\AutoElevateLauncher.exe`

## 许可证

本项目使用 MIT License 开源。详见 [LICENSE](LICENSE)。
