# Windows 私有开发状态

此文件和 Windows 实现暂不公开；Linux 2.1.2 已独立发行。

## 本轮完成的内部实现

- 下载缓存锁通过平台存储接口创建；Windows 不再应用 UnixCreateMode。
- 文件占用标记在 Windows 使用进程启动时间，不读取 `/proc`。清理文件允许删除共享，仍禁止其他进程同时写入。
- 后端输出包含已有 Steamworks NuGet 包中的 steam_api64.dll。
- Windows 更新复用版本检查、签名验证和下载。辅助宿主通过纯数据任务取得后端及外层启动器的进程句柄，确认身份后通知界面可关闭；等待退出后再次校验文件哈希，再安装、保留 `.previous` 和重启启动器。重启失败恢复旧入口和下载文件，不启动游戏。
- Steam 二次启动交接区分游戏后端和已有启动器宿主；接受宿主前检查实际 EXE 路径及当前会话的 PID、随机标识，并拒绝交接给自身。
- 快捷方式注册设有超时，失败不应阻塞启动器。已结束的更新辅助目录在之后启动时尝试回收。
- `scripts/package-windows.py` 固定 Electron 44.5.1 Windows x64 官方哈希，保留既有国服辅助组件哈希，使用当前 xdelta3 源码编译并执行差分往返验证；在 Windows 上通过 Native AOT 生成内嵌运行资源的单文件宿主。
- 公告“刷新”等轻量文字按钮悬停只提亮文字，不再出现普通按钮边框；Linux 后续主线同样保存了此修复，已发布包不变。

## 构建入口（尚待 Windows 原生验收）

需 .NET 10 SDK、Visual Studio 的 x64 Native Tools 环境（含 C++ 工具链），以及固定 Electron ZIP 和既有辅助组件目录。

```powershell
python scripts/package-windows.py artifacts/windows-development --version 2.2.0-dev --electron-zip C:\inputs\electron-v44.5.1-win32-x64.zip --support-bundle C:\inputs\xivlauncher-super
```

Electron ZIP 必须来自 `https://github.com/electron/electron/releases/download/v44.5.1/electron-v44.5.1-win32-x64.zip`，SHA256 为 `9b382492dcfee91f8f9e92c91f7972550a1b95d2299cac72279dab33a600d7db`。脚本不自动换版或下载额外辅助组件。

Native AOT 必须在 Windows 构建；Linux 上的普通 .NET 编译通过不能视作最终 EXE 构建通过。不可用自解压 .NET 单文件退路绕过这一限制，因为它可能在启动代码执行前向全局临时目录写入原生库。

## 国服游戏内跨区的边界

保留插件的 `127.0.0.1` HTTP 协议、端口传递、JSON 格式、订单状态查询、会话复用和认证恢复。不因移除 Proton 而改成另一套插件协议。Windows 现在通过独立的 Kestrel 监听适配使用本地 TCP 套接字，不走 HTTP.sys，不安装 URL ACL、不提权或修改注册表。Linux 仍使用原来的 HttpListener。只有 Windows 后端引用 Microsoft.AspNetCore.App，随自包含运行资源打包，不要求用户安装 ASP.NET。两个路径共享单一端口，分别串行处理；最后一个端点释放后关闭监听。

必须实测：实际提交跨区后的查询和确认、授权失效恢复、关闭前端后后端存活、游戏退出后桥接服务关闭、取消启动后的端口和会话文件清理。仅拉取区服/角色列表不算完成验证。

## 尚未完成

- Windows 原生 Native AOT 打包与资源/许可证输出验收。
- 普通用户下跨区监听服务、登录验证、三版本实际登录和 Dalamud 注入。
- Steam 手动启动参数、界面退出后的后台存活、游戏退出后的 Steam 状态。
- Windows 文件占用及杀毒软件介入时的真实升级、失败回退、快捷方式和重命名/移动场景。
- Windows README 和最终四语言发行说明；本轮不发行、不推送 Windows 源码。

## 本轮验证

- 新增 Windows 专用测试项目，直接引用 Windows 后端；复用跨区提交、连续轮询、确认订单、授权恢复和更新安装测试，未复制业务实现。
- 额外测试两个端点共享与独立释放、未完成请求退出、客户端断连后的继续服务、Steam 交接身份校验。
- Windows 打包脚本先运行上述测试，打包后执行宿主版本查询烟雾测试。此处的本地验证运行在 Linux；仍需 Windows 原生构建与真实游戏验收，不能将模拟订单测试当作实际跨区成功。

当前回归结果：Windows 专用测试 39 项通过；Linux 后端 563 项通过；前端单元测试 91 项通过、2 项跳过；单文件宿主普通编译零警告、零错误。Windows 后端仍有原有的 17 条平台调用分析警告，来自暂时共编译的 Linux 平台实现；尚未声称完成 Windows 原生验收。
