# 第三方依赖

## HIDMaestro Core 1.9.2

- 项目与源码：<https://github.com/hifihedgehog/HIDMaestro/tree/v1.9.2>
- 上游 x64 发布包：<https://github.com/hifihedgehog/HIDMaestro/releases/tag/v1.9.2>
- 许可：MIT，见本目录 `LICENSE-HIDMaestro.txt`。
- 本项目仅使用已发布的 `HIDMaestro.Core.dll`，通过官方 `xbox-360-wired` profile 创建虚拟 XInput 手柄；没有包含本项目编写的驱动。
- DLL SHA-256：`D936B6FD928EBF3F9996B764ACAFCDAE7E1F31F34E989D9803924B89C786B5FA`。
- 上游 v1.9.2 发布说明记录了 62 项回归检查通过；本项目没有在开发机上安装该驱动或复跑其驱动级检查。

HIDMaestro 首次安装会把本地签名证书加入 Windows 信任存储并注册其现有 UMDF2/XUSB 组件，需要 UAC 管理员权限。本程序仅在用户点击启动并确认后调用该安装流程；检测到已有组件时不会主动重装。首次安装时上游 SDK 可能清理本机遗留的 HIDMaestro 虚拟设备。关闭本程序会释放本程序自己创建的控制器。

上游发布说明提到 v1.8.0 和 v1.8.1 曾存在可能影响 Windows 启动的问题，v1.9.0 修复了该问题。本项目固定使用 v1.9.2；不要将依赖随意降级到早期版本。上游也记录过较早版本在 Microsoft Defender 中出现机器学习误报；v1.7.3 增加了二进制版本资源，但上游没有声称已获得微软对该问题的最终确认。运行前请核对本文件中的 DLL 哈希。

## SDL3 C# bindings 2026.722.0

- NuGet：<https://www.nuget.org/packages/ppy.SDL3-CS/2026.722.0>
- 源码：<https://github.com/ppy/SDL3-CS>
- 许可：MIT。
- NuGet 包提供 SDL3 C# 绑定和 Windows 原生 `SDL3.dll`。本项目用 SDL3 Gamepad API 读取 Nintendo Switch Pro Controller。

## Windows XInput

`scripts/Test-XInput.ps1` 使用 Windows 自带的 `xinput1_4.dll` 读取虚拟控制器，仅供用户手动验证；不分发或修改该系统组件。
