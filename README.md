# Switch Pro → XInput

一个 Windows 10/11 x64 小工具：读取 Nintendo Switch Pro Controller，将输入映射到一个虚拟 Xbox 360 / XInput 手柄，供支持 Xbox 手柄的 PC 游戏使用。

## 做了什么

- 使用 SDL3 识别 VID/PID 为 `057E:2009` 的 Pro Controller，也支持设备名明确为 Switch Pro 的兼容手柄。
- 以 300 ms 间隔扫描设备；断连时向虚拟手柄发送中性状态，重连后自动恢复。
- 左右摇杆各自使用可调的圆形死区，默认 12%；界面可设为 0–30%。
- ZL/ZR 输出为独立的 LT/RT 模拟扳机值。Switch Pro 本身的 ZL/ZR 是数字按键，所以实际输入会是 0 或 100%。
- 通过 HIDMaestro 1.9.2 的现成 `xbox-360-wired` 配置创建 XInput 输出。本项目没有编写或修改任何驱动代码。
- 首次安装仅在用户点击“启动转换”并同意后进行；程序会请求管理员授权。

## 按键布局

面键按实体位置映射到 Xbox 面键位置：

| Switch Pro 位置 | Xbox 输出 |
| --- | --- |
| 下方 | A |
| 右侧 | B |
| 左侧 | X |
| 上方 | Y |
| L / R 肩键 | LB / RB |
| ZL / ZR | LT / RT |
| Minus / Plus | View / Menu |
| 左摇杆按下 / 右摇杆按下 | L3 / R3 |
| 十字键 | D-pad |
| Home | Guide |
| Capture | 不映射 |

摇杆方向与 Xbox 一致，向上为负 Y。死区按每根摇杆的二维向量长度处理，并将死区外的幅度重新缩放到完整行程。

## 运行

1. 连接 Switch Pro 手柄，可使用 USB 或蓝牙。
2. 解压本项目提供的 `SwitchProXInput-1.0.0-win-x64.zip`。
3. 运行 `app\SwitchProXInput.exe`。手柄状态会显示为“已连接”或“正在自动等待连接”。
4. 点击“启动转换”。每次启动虚拟手柄都需要 UAC 管理员授权。
5. 第一次启动时，程序会明确询问是否安装 HIDMaestro 的现有 UMDF2/XUSB 组件。它会将其本地签名证书加入 Windows 信任存储；请先阅读 `third_party\THIRD_PARTY.md`，再决定是否继续。
6. 先启动转换，再启动《我的世界：地下城》。游戏若已打开，请关闭后重开，让它重新枚举手柄。
7. 游戏结束后点击“停止”。窗口关闭时也会移除本程序创建的虚拟手柄；已安装的组件会保留。

本工具不修改 `WindowsApps` 权限、不访问游戏文件，也不改变游戏安装目录。停止转换只移除本次创建的虚拟控制器，不卸载 HIDMaestro。

## 测试步骤

### 分步实现与验收

1. **输入/输出数据与映射层**：纯 C# 状态映射、扳机量程、八向 D-pad 和径向死区。运行本地自动检查，当前 7 项均通过。
2. **SDL3 输入采集**：筛选 Pro Controller 的 USB/Bluetooth 设备标识，读取标准化面键、摇杆和扳机。连接实机后看 GUI 连接状态和摇杆/扳机读数，再拔插一次检查自动重连。
3. **虚拟 XInput 输出**：调用 HIDMaestro 1.9.2 的现成 Xbox 360 profile；用下方 XInput 脚本逐个检查按键、轴以及独立 LT/RT。
4. **GUI 与生命周期**：检查启动/停止、首次安装确认、管理员授权、断连时中性输出、重连恢复；停止后确认虚拟 XInput 插槽消失。

第 2–4 步需要真实 Pro Controller 和 Windows 上的虚拟设备安装；本次交付中未在开发机安装组件或连接手柄，所以这些列为你本地的验收步骤。

### 本地自动检查

在项目根目录运行：

```powershell
dotnet run --project tests\SwitchProXInput.Core.Tests\SwitchProXInput.Core.Tests.csproj
```

这组检查不需要手柄或管理员权限，覆盖面键、肩键、菜单键、ZL/ZR、D-pad、摇杆死区和 SDL 轴端点。

### 检查 XInput 输出

启动转换后，另开 PowerShell 并运行：

```powershell
.\scripts\Test-XInput.ps1
```

脚本读取 Windows 的 `XInputGetState`，列出 0–3 号 XInput 插槽。按下 ZL 时应只增加 LT；按下 ZR 时应只增加 RT；推动摇杆时对应轴应变化。按 `Ctrl+C` 退出。
使用 `-Once` 参数可只读一次并退出。

### 检查重连和游戏

1. 运行工具并启动转换，确认 XInput 脚本看见一个 Xbox 360 手柄。
2. 按动各面键、肩键、ZL/ZR、方向键和摇杆，对照上面的映射表。
3. 断开 Pro Controller，再重新连接；工具应显示等待后恢复连接，XInput 插槽继续输出中性状态再恢复输入。
4. 启动《我的世界：地下城》，确认菜单和实际关卡均响应手柄。
5. 点击“停止”，确认 XInput 脚本不再看到该虚拟控制器。

`joy.cpl` 可确认虚拟 Xbox 控制器是否枚举，但它使用 DirectInput，两个扳机可能显示为共用轴。要验证 LT 和 RT 分开，请用随项目提供的 XInput 脚本。

## 从源码构建

需要 Windows、.NET 10 SDK，以及构建时可访问 NuGet。HIDMaestro 1.9.2 的预编译 SDK 已放在 `third_party`，无需安装 Visual Studio、WDK 或自行编译驱动。

```powershell
dotnet run --project tests\SwitchProXInput.Core.Tests\SwitchProXInput.Core.Tests.csproj
dotnet publish src\SwitchProXInput.App\SwitchProXInput.App.csproj `
  -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=false -p:PublishTrimmed=false `
  -o artifacts\publish
```

`artifacts\publish\SwitchProXInput.exe` 为自包含应用，运行时不要求另装 .NET。`ppy.SDL3-CS` 的托管绑定与 SDL3 原生 Windows 库在发布时由 NuGet 还原。

运行 `.\scripts\package.ps1` 可生成应用 ZIP 和源码 ZIP；源码 ZIP 包含固定版本的 HIDMaestro SDK DLL 及其 MIT 许可。

## 已知限制

- 当前开发环境没有连接 Switch Pro 手柄；XInput 驱动安装和《我的世界：地下城》实机操作尚未在这台机器上验证。请按上面的步骤完成本地实测。
- 创建虚拟控制器是 Windows 系统级设备操作，必然需要管理员权限。本工具仅调用现有 HIDMaestro SDK，不自行写驱动。
- HIDMaestro 使用本地签名证书安装 UMDF2/XUSB 组件。若不接受此系统级安装，请不要同意首次安装提示；可以直接关闭应用。
- 本程序只将控制器输入映射到 XInput，没有把游戏震动反馈回传到 Switch Pro。
- 若 SDL3 无法打开控制器，可先关闭 Steam Input、BetterJoy 等也会接管手柄的程序，再重连 Pro Controller。
