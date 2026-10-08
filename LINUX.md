# FFXIV Super Launcher Linux 预览版

这是 Soil 的原生 Linux 前端，使用 Avalonia 界面；中国区复用 Soil，繁中区复用所提供 XIVTCLauncher 的协议和补丁代码，国际区参照 xl 的认证及更新协议。
游戏和 Dalamud 仍然是 Windows 程序，通过所选 Proton 运行。当前目标是 x86_64 Linux，首先在 CachyOS 验证。

## 更新策略

| 组件 | 更新方式 |
| --- | --- |
| 启动器本体 | **没有自动更新器**。通过包管理器或手动替换发布包更新 |
| 游戏本体 | 中国区 V3；繁中区/国际区共用 ZiPatch 安装器，按各自协议检查。国际区需先认证拉取游戏补丁；三个游戏区服均可单独更新而不启动游戏 |
| Dalamud | 中国区使用 **Soil**，繁中区使用 **yanmucorp**，国际区使用 **goatcorp**；仅在勾选“启用 Dalamud”并启动游戏时检查更新 |
| Proton / Steam Runtime | 读取本机已有安装，由 Steam、系统包管理器或用户维护 |

本分支已移除 WPF/Velopack 项目，不会检查或覆盖自身可执行文件。它保留 Soil 的插件策略，没有添加插件黑名单。

## 使用发布包

1. 解压 `xivlauncher-super-0.10.6.tar.gz` 到一个长期保留的目录。
2. 运行其中的 `xivlauncher-super`。包内附带 .NET 运行时，无须安装 .NET SDK。
3. 在“语言和区服”中选择游戏区服并填写相应游戏根目录，目录内应有 `game/ffxiv_dx11.exe`、`game/ffxivgame.ver`。
4. 在独立的“兼容性工具设置”页面从下拉列表选择 Proton。CachyOS 上的 `proton-cachyos-slr` 会显示版本名称；也可手动选择工具目录中的 `proton` 脚本。
5. 使用独立的 Proton 数据目录。**选择 `pfx` 的父目录，不是 `pfx` 本身，也不要填写已有 Wine prefix 的 `drive_c` 所在目录。**
6. 点击“环境检测”。首次初始化 prefix 较慢。缺少 Runtime 时，先在 Steam 安装列表提示的依赖 AppID，再刷新。
7. 需要时点击“检查并更新游戏”；更新前退出游戏和其他启动器。
8. 在“游戏”页按所选游戏区服登录：中国区选择自动加载的大区并使用叨鱼，繁中区/国际区使用账号密码及适用的 2FA。国际区还需完整的 `boot` 目录。

设置、补丁缓存、Dalamud 及其插件配置默认位于：

```text
~/.local/share/xivlauncher-super/
```

遵循 `XDG_DATA_HOME`。默认 prefix 是该目录下的 `compatdata/pfx`。
国际区和繁中区临时登录票据不保存；中国区可保存服务端签发的快捷登录凭据。密码仅在勾选“记住当前账号密码”后保存到选定存储；2FA 密钥通过“保存账号凭据”保存。账号标识及独立的设备信息保存在 Linux 专用配置中。
扫码模式可填写一个稳定的账号昵称，用于区分不同账号的设备配置。

Proton 的扫描范围包括系统 `compatibilitytools.d`、用户 Steam 安装、自定义工具、Flatpak Steam 安装目录，以及 `libraryfolders.vdf` 中的其他游戏库。
根据各工具的 `toolmanifest.vdf` / `require_tool_appid`，从 Steam 游戏库解析相应 Runtime；不硬编码某个 Proton 版本。
能发现 Flatpak Steam 内的工具并不表示所有 Flatpak 沙箱组合都经过验证，必要时使用宿主机安装的 Steam/Proton。

## 从源码构建

需要 .NET 10 SDK、C 编译器、Python 3、Git。Linux 前端不需要 Windows SDK、WPF、Rust 或 ApkalluCaller 子模块。

```bash
dotnet build src/XIVLauncher.Linux/XIVLauncher.Linux.csproj -c Release
dotnet test src/XIVLauncher.Linux.Tests/XIVLauncher.Linux.Tests.csproj -c Release -m:1 --filter "Category!=Network"
dotnet run --project src/XIVLauncher.Linux/XIVLauncher.Linux.csproj -c Release
bash scripts/package-linux.sh
```

发布脚本默认输出到 `artifacts/linux/`，包含自包含二进制包、对应源码包和 SHA256SUMS。
每次打包使用新的输出目录，以免把旧版本文件混进新包：

```bash
bash scripts/package-linux.sh /absolute/path/to/new-output
```

也可用 `DOTNET=/path/to/dotnet` 指定 SDK。请分发对应源码包，并保留 LICENSE、Tools/LICENSE.xdelta3 和第三方依赖说明。
GitHub Actions 的 `Linux preview` 工作流执行测试并生成二进制及源码产物，不会自动发布版本。

## 诊断

```bash
./xivlauncher-super --list-proton
./xivlauncher-super --check-game /path/to/ffxiv
./xivlauncher-super --diagnose-proton /path/to/proton /path/to/test-compatdata
./xivlauncher-super --prepare-dalamud /path/to/test-dalamud
```

`--check-game` 只读取版本和远程更新元数据。`--diagnose-proton` 会初始化指定数据目录并执行 Windows 命令；`--prepare-dalamud` 会下载到指定目录。
Dalamud 日志默认在数据目录的 `logs` 下。发生兼容工具错误时，界面仅显示核心原因，完整诊断见“关于”中的日志文件路径；不要在公开报告里附上账号密码或登录票据。

## 为什么不能直接恢复旧 Unix 文件夹

- 三份源码中的 Windows 前端都基于 WPF；前两份包含 Unix 公共组件，完整 Linux 前端另外维护。Soil 中没有可以直接编译为 Linux 的 WPF 界面。
- Soil 把 Common、Account、Login、Dalamud 等库改为 Windows 目标，并移除了 Unix/Wine 实现；Linux 入口需要可移植的目标框架和平台边界。
- Soil 登录/注入器接口经过重构。关闭 Dalamud 时，国服使用发行包固定附带的参数修复组件（`--without-dalamud`），不查询或下载 Dalamud；台服直接启动游戏。
- Proton 不是一个 Wine 路径：必须设置 compatdata、Steam 根目录，并进入其声明的 Runtime。先用 `run` 初始化 prefix，再通过 `runinprefix` 直接调用注入器及路径转换工具，保留注入器输出；准备兼容环境之后才获取短期登录票据。
- Windows 游戏差分工作进程使用命名共享内存。Linux 版改用随包编译的原生 xdelta3 子进程，校验大小和 MD5 后再原子替换目标文件。

## 当前验证范围与限制

已经验证 Linux 构建、图形界面渲染、Proton 自动发现、真实 proton-cachyos 环境执行、游戏更新只读检查，以及 Soil Dalamud 下载/校验/解压。
自动测试覆盖多游戏库、Runtime 缺失、参数引用、进程输出处理、真实 VCDIFF 合并、错误校验和与取消保护。

本版本为预览版。此前 0.1.0 已由用户实机确认可正常登录、启动游戏并注入 Dalamud。真实版本升级和各种插件组合仍需进一步验证。
暂不包含 WeGame、跨区旅行、附加程序管理、崩溃后自动重启及完整游戏首次安装。
支持已有国服和台服客户端的更新；更新失败会保留补丁缓存，并还原原版本文件以便重试，不表示整个游戏目录已整体回滚。
0.2.0 清理了 Windows/macOS 构建入口、旧 WPF 前端、Windows 凭据管理、ACL/WMI/进程内存工具及补丁共享内存桥。原生界面只引用 Avalonia X11/Skia 后端；Wayland 通过 XWayland 运行。保留 Proton 所需的 Windows 游戏参数和 Dalamud 运行时下载，不提供 Windows/macOS 启动器构建。

原有补丁单元测试已迁入 Linux 测试项目。真实网络测试可单独通过 `dotnet test src/XIVLauncher.Linux.slnx -c Release --filter "Category=Network"` 执行。

## 0.3.0 配置目录

配置根目录为 `~/.local/share/xivlauncher-super/`，遵循 `XDG_DATA_HOME`。首次启动按向导选择语言、区服与共享 Steam/Proton 设置；提供简体中文、繁體中文、日本語和 English 界面，首次配置默认 English；语言更改保存后需重启启动器。

全局设置在 `linux-settings.json`，共享 Proton 数据默认在 `compatdata/`（内部创建 `pfx`）。区服设置分别在 `ffxiv_cn/settings.json`、`ffxiv_tc/settings.json`、`ffxiv/settings.json`。国服 Dalamud、插件、运行时及资源使用 `ffxiv_cn/dalamud/`、`ffxiv_cn/addon/`、`ffxiv_cn/runtime/`、`ffxiv_cn/assets/`，日志与补丁缓存也放在该区服目录下。

此前测试版本未对外发布，因此不提供自动迁移。新版直接在新根目录初始化，不读取、搬运或删除旧的 `xivlauncher-cn-soil` 目录。需重新完成配置；外部游戏目录、已安装 Proton 及兼容数据目录可手动选择。

“登录并启动”和取消操作位于“游戏”页；Proton 扫描数量位于“兼容性工具设置”页的选择框下方。简短操作反馈集中在主界面底部日志框，详细诊断单独写入文件，每条带时间；保存成功消息在实际写入完成后追加。

## 可选 Steam 入口

向导及“兼容性工具设置”页提供“注册为 Steam 兼容性工具”复选框，默认不勾选，更改后即时应用。工具写入所选 Steam 根目录的 `compatibilitytools.d/xivlauncher-super`。重启 Steam，在 FFXIV 属性的兼容性页强制选择 `FFXIV Super Launcher`，即可从 Steam 打开启动器。取消勾选即可移除注册；随后重启 Steam 并重新选择原兼容工具。

这不是 Steam 账号认证：不会自动切换区服或采用 Steam 客户端路径。游戏仍使用保存的客户端与真实 Proton。Steam 入口在窗口关闭后等待配置的共享 prefix 会话退出，因此同 prefix 的其他程序也可能使 Steam 继续显示运行中。移动手动解压的启动器后需重新保存注册；包管理器安装路径固定。

当前仅提供普通 Steam 注册，Flatpak Steam 的宿主机入口注册会明确拒绝；其 Proton 探测功能保留。没有自动下载或更新启动器。

Steam 工具格式参考 Valve 的 [兼容工具清单](https://github.com/ValveSoftware/Proton/blob/proton_11.0/compatibilitytool.vdf.template) 和 [命令清单](https://github.com/ValveSoftware/Proton/blob/proton_11.0/toolmanifest_x86_64.vdf)。上游的 Steam 账号认证代码不用于本入口。

## 本地化开发

四语言资源位于 `src/XIVLauncher.Linux/Resources/Locales/`，界面语言与客户端语言、网站语言独立。点击启动器语言旁的“应用”保存并提示重启；日志保持英文。动态界面文字保留占位符，第三方页面、玩家名和服务器消息不翻译。

## 跨区服插件同步

在“Dalamud和插件”页分别点击“同步 Dalamud 设置”“同步已安装插件”或“同步插件设置”，选择有对应数据的来源区服并确认。所选类别将完整覆盖另外两个区服，来源保持不变。请先退出各区服游戏。

只复制来源区服下对应的 `dalamud/dalamudConfig.json`、`dalamud/installedPlugins/` 或 `dalamud/pluginConfigs/`，不复制 Dalamud 程序、运行时、资源、账号或游戏客户端。插件文件可复制，但是否兼容取决于目标区服的 Dalamud 和插件版本。

目标原内容保存在数据根目录的 `sync-backups/<时间-标识>/<区服>/`；普通同步异常会尝试自动还原。需要手动恢复时，退出游戏和启动器，从该备份目录将对应文件或目录替换回目标区服。备份不会自动删除。符号链接路径不参与同步。

## 离线插件管理

“插件管理”页按当前选择的区服显示已安装插件。勾选调整后点击“应用插件启用状态”，下次启动游戏生效；“放弃修改”重新读取已保存状态。横线代表插件受到集合或角色条件控制，未操作的项保持原规则。手动启用加入默认集合；禁用会关闭该插件在所有集合中的启用项。应用前请退出游戏。

仅更新 `dalamud/dalamudConfig.json` 中选定插件的集合状态，不删除插件或插件设置。原配置备份在数据根目录 `plugin-state-backups/<时间-标识>/<区服>/dalamudConfig.json`。列表读取后如果文件被其他程序改动，会要求重新读取，避免覆盖新配置。未知集合格式和异常插件文件不会被强行改写。

## 台服登录与凭据（0.4.0）

标识固定为国服 `ffxiv_cn`、台服 `ffxiv_tc`、全球服 `ffxiv`；共享配置根目录仍为 `~/.local/share/xivlauncher-super/`，不是将整个根目录改成区服名。三个客户端路径互相独立，Steam/Proton/prefix 继续共用。

从 0.5.0 起发行包自带网页组件，不再要求安装 Chromium。台服验证码使用独立临时会话；官网、商城、充值及公告在独立窗口打开，提供前进、后退、重新加载和“在系统浏览器中打开”。网页窗口会话关闭后清除，验证码与官网浏览不共用配置。组件基于固定版本 Electron 44.5.1，随启动器发行包更新，不单独自动更新。远端网页无 Node 权限，保持沙箱；系统需要允许普通用户的 namespace 沙箱并提供 GTK3/NSS 等运行库。

台服填写电子邮件和密码。2FA 支持本次六位验证码；自动生成时填入 Base32 密钥，勾选自动生成并点击“保存账号凭据”，后续自动计算当前验证码。普通六位码不能作为长期密钥保存。勾选记住密码并保存后，下次可留空密码一键登录。


## 固定国服启动辅助组件

国服在不加载 Dalamud 时仍需要参数修复。发行包 `Tools/cn-launch-support/` 只包含固定 Soil 26-10-05-02 的注入器及其启动依赖、Windows .NET 10.0.1 runtime；不包含 `Dalamud.dll` 或插件资源。它只以 `--without-dalamud` 运行，不访问更新源，随启动器包手动升级。台服启用 Dalamud 使用上游固定 .NET 9.0.11，存放在 `ffxiv_tc/runtime/`。

源码调试若要关闭 Dalamud 启动国服，先运行 `python3 scripts/prepare-cn-launch-support.py <构建输出目录>`；该命令只在构建时获取固定组件，发行包由打包脚本自动执行。构建需要 Python、7z 和网络，或通过 `CN_SUPPORT_ADDON`、`CN_SUPPORT_RUNTIME` 指向固定版本缓存。辅助组件不能由 Linux 的 .NET 运行时替代。

## 0.5.0 界面及架构

- 登录页右侧为按区服加载的官方活动和更新公告，资讯失败不会阻止登录。
- 0.6.0 起，每个下载文件在底部日志中占一行，原行更新百分比，成功显示 100%，失败或取消明确标记。大小未知时显示已下载量，不虚构百分比。
- “关于”显示启动器版本与各区服版本记录。首次保存一个客户端路径时读取本地版本作为基线，之后仅成功更新游戏时刷新；打开“关于”不重新扫描、不查询远端。Dalamud 仅在启用并成功准备后记录版本。
- 新配置中的 Dalamud 默认关闭，首次向导明确告知用户协议风险；已有显式设置保留。
- 诊断面板采用英文技术信息、异常类型、诊断参考及堆栈；用户操作提示保持本地化。外部进程尽量采用 C.UTF-8，原始输出不翻译。
- 共用更新流程、下载和运行时安装，区服只提供登录实现、更新协议/格式适配及源配置。详见 ARCHITECTURE.md。
- 从源码测试网页功能前，运行 `python3 scripts/prepare-webview.py <构建输出目录>`。可通过 `WEBVIEW_ARCHIVE` 提供已下载的固定版本压缩包，仍强制核对 SHA256。

## 0.5.1 更新超时与修复

Dalamud 更新超时后可选择使用本地版本、禁用并继续或取消。禁用保存到当前区服，下次不会自动重新启用；本地文件不完整时无法跳过更新。离线使用不能确认当前游戏兼容性。插件管理重新读取当前文件后应用整张勾选列表；台服请求对齐原版协议，失败提示包含验证阶段。详见 [RELEASE-0.5.1.md](RELEASE-0.5.1.md)。

## 0.6.1 游戏区服与语言

界面名称统一为中国区、繁中区、国际区，内部及日志标识仍为 `ffxiv_cn`、`ffxiv_tc`、`ffxiv`。在“语言和区服”页选择国际区后可设置客户端语言（日语、英语、法语、德语），首次启动向导同样可选；选择保存在国际区配置中，切换其他游戏区服不会清除。国际区目前仅开放设置，不支持登录启动。界面日志为当前会话内最多 1000 行，可选择复制或清空。0.6.1 起，完整诊断另存为数据根目录下 `logs/launcher-日期时间-进程号.log`，实际路径在“关于”显示；保留诊断脱敏，清空界面不会删除文件。

## 0.6.2 登录凭据

中国区勾选“记住登录凭据”后，扫码、滑动或密码登录成功时保存 Soil 登录库返回的快捷登录凭据。后续优先使用它并保存轮换后的凭据；明确失效时重新验证，普通网络失败不删除凭据。保持首次认证使用的设备配置，不保存一次性的游戏启动票据。服务端决定实际有效期，不能保证永久免扫码。


保存凭据不会清空输入框；没有新凭据可写时不会报告保存成功。成功保存后密码可留空，登录流程会读取已保存密码；手动填写的密码优先。2FA 自动生成仍需保存密钥并勾选自动生成。清除当前存储中的凭据会同时清除密码、快捷登录凭据和 2FA 密钥。取消“记住”会停止使用中国区快捷登录凭据，不自动删除它。

中国区在启动和切换回来后自动获取游戏大区并恢复已保存选择。没有有效选择时，游戏页直接提示选择大区；获取失败可使用“获取大区”重试。

## 0.7.0 账号、验证与超域传送

游戏页提供已保存账号列表及保存记录、添加、删除操作。账号设置保存在各游戏区服 settings.json 的 Accounts 中，不包含秘密；凭据仍按各账号选择使用桌面密钥环或 credentials.json。删除会清理该账号关联存储的秘密，失败保留记录供重试。国际区只提供记录管理。

繁中区不再在常驻页面输入一次性验证码；关闭自动生成时，完成游戏/Proton/Dalamud 准备并拿到网页验证令牌后，在日志框上方提示输入当前六位验证码。90 秒内未提交需重新验证。未启用 2FA 的账号可显式选择无验证码继续。密码可单独保存；保存 2FA 密钥则必须同时提供密码。自动模式没有对应账号密钥时在准备前阻止启动，并显示处理方法。

中国区的超域传送入口位于时长充值旁。它使用 Soil 接口代码和中国区登录会话，不需要 Dalamud 插件或本地 RPC 服务。提供角色/目的地确认、传送、返回原区、可返回的已完成记录（与 Soil 的历史筛选一致）及成功后启动选项。只有服务端确认成功后才更新当前账号及大区选择；停止等待不会撤销服务端已提交的操作。普通网络错误不会自动重发传送请求。


## 0.8.0 国际区

国际区已接入 Square Enix 普通账号和 Steam 账号登录。填写 Square Enix ID（不是邮箱地址）；Steam 账号额外勾选“使用 Steam 发行的国际区账号登录”，并保持桌面 Steam 在线且使用已绑定账号。免费试玩账号勾选对应选项，Steam 试玩使用独立 App ID。首次账号绑定、接受协议等仍按原版要求在官方启动器完成。

国际区共用已保存账号、明文/桌面密钥环存储以及手动/自动 2FA；手动验证码在取得登录页面后提示，自动验证码在提交前生成。硬件安全令牌仅手动输入，不将其序列号当成 Base32 密钥。账号未启用 2FA 时，在手动提示中选择无验证码继续。

“检查并更新游戏”位于“登录并启动”旁，三端共用。国际区更新需要登录取得补丁列表，但该按钮不会启动游戏、准备 Proton 或更新 Dalamud。启动按钮则执行环境准备、所选 Dalamud 更新、登录及游戏补丁安装后再启动。仍要求已有完整客户端，不提供空目录下载完整游戏。

国际区客户端语言使用“语言和区服”中保存的日语/英语/法语/德语，传给游戏和 Dalamud，切换游戏区服不会重置。国际区 Dalamud 来自 goatcorp 官方发行元数据，继续使用共用更新器及超时选择。原有中国区、繁中区来源不变。

普通和 Steam 账号的实际服务端登录、认证后的真实游戏补丁以及游戏/Dalamud 注入需实机验收；离线协议测试、公开 boot 补丁安装及公开 Dalamud 完整安装不等同于真实账号验证。

## 0.9.0 登录凭据管理与 Steam 环境

“登录凭据管理”统一列出三端账号。新增账号归属当前游戏区服；编辑、清除、删除仅在底部“保存”后写入。登录名旁的“选择登录名”只列出当前游戏区服账号。中国区仅登记登录名，扫码或一键快捷登录成功后自动保存服务端提供的快捷凭据；不支持静态密码。繁中区及国际区在管理页保存密码和可选的 2FA 密钥，新密钥必须同时填写密码。未启用自动生成或没有有效密钥时，在登录验证阶段手动输入验证码。

兼容性工具设置可扫描 Steam 国际区正式/试玩版已有 compatdata，也支持手动指定。只有勾选“国际区使用 Steam 已有 pfx”后，国际区使用该目录；取消后恢复三端共用。直接复用会读写已有环境，所选 Proton 可能升级它，不复制或删除原文件；不要同时启动 Steam 官方启动器或游戏。

0.9.1：凭据管理页自动跟随当前游戏区服，仅列出对应账号；无账号时直接显示空白表单。草稿按游戏区服分别保留，保存和放弃修改只作用于当前游戏区服。“登录凭据管理”位于“游戏”右侧。

## 0.9.2 凭据编辑与全局存储


管理器以遮蔽字符加载已有密码和 2FA 密钥，直接修改后保存；清空 2FA 字段后保存即删除。移除旧常驻提示和独立清除按钮。“保存”位于“放弃修改”右侧。自动生成验证码仅在游戏页选择，编辑凭据不改变该选择；没有有效密钥时仍使用手动验证码流程。以上行为取代旧版本章节中的不回填、留空保留及单独清除按钮说明。


## 0.10.0 本地验证码、更新与版本显示

登录凭据管理页提供“本地 2FA 验证码生成器”，可使用正在编辑的密钥或临时输入的 Base32 密钥。只在本地显示六位验证码、UTC 时间和有效期，可复制；不保存输入，也不发起登录请求。它与登录自动验证码复用同一 SHA1/30 秒/六位 TOTP 实现。标准测试通过不代表真实账号登录问题已经解决。

三端“登录并启动”均先认证，再更新游戏，启用 Dalamud 时再更新 Dalamud，最后准备并启动游戏。繁中区在获得登录令牌后关闭网页验证会话，再更新游戏，避免下载被网页验证超时打断。国际区先完成 OAuth，再更新 boot 和拉取认证后的游戏补丁。

“检查并更新游戏”只更新当前游戏区服客户端：中国区和繁中区直接更新，无需账号；国际区先认证。此按钮不更新 Dalamud 或插件，也不启动游戏。

“Dalamud和插件”仅显示当前游戏区服的 Dalamud 版本，提供手动更新 Dalamud 和检查插件更新的入口；手动更新 Dalamud 不改变是否启用注入的选择。插件检查读取 Dalamud 主库/第三方库设置及各插件 InstalledFromUrl，严格从原仓库选取，不跨库替换同名插件。来源缺失、禁用或不兼容时在弹窗说明，不猜测来源。弹窗支持全选或逐项选择；安装保留原 WorkingPluginId、启用配置、插件设置和旧版本，下载校验及暂存完成后才增加新版本目录。

启动时刷新本地版本记录；客户端版本位于“语言和区服”各目录下方；Dalamud 版本位于“Dalamud和插件”。“关于”仅保留启动器版本和日志路径。普通启动默认进入“游戏”，首次配置向导仍引导完成设置。

官方启动器不再被当成实际游戏而阻止修改插件；真实游戏运行时仍禁止写入插件。客户端更新仍要求关闭官方启动器，防止并发改写客户端。允许官方启动器运行时保存 Steam pfx 选择，但实际使用该 pfx 时仍检查占用。

## 0.10.2 繁中区启动方式

繁中区启用 Dalamud 时，使用包内 `Tools/game-start.exe` 在 Proton 内启动游戏，等待窗口后按 Windows PID 调用上游注入器的 `inject` 模式，避免 `launch` 模式中不适用的 ArgFixer。其他游戏区服启动方式不变。构建新增依赖：MinGW GCC，或 clang、lld 和 Wine 开发头文件/导入库（Arch：clang lld wine）。使用发行包不需要另装系统 Wine。

更换解压目录后，请从新目录直接打开启动器，在兼容性工具设置页保持注册 Steam 选项勾选并保存，将入口更新为新路径；旧注册脚本不会自动寻找新解压目录。

## 0.10.3 繁中区网页验证配置

参考 xl_tw 保留网页验证用户数据于 `ffxiv_tc/captcha-browser/`（权限 700），关闭窗口时优先正常退出以保存浏览器状态。该目录不接收账号密码或 2FA 密钥，每次登录仍执行新的 reCAPTCHA 请求，不缓存或复用令牌。禁止两个窗口同时使用该目录。此调整缩小与原版的差异，尚不能证明已解决服务端通用连接错误。

## 0.10.4 Steam 辅助调用过滤

兼容性工具仅在 Steam 调用 FFXIV 启动程序或游戏程序时打开界面。Steam 的 d3ddriverquery64.exe、iscriptevaluator.exe 等探测/维护调用不会打开启动器。升级后请从新版目录打开启动器，重新保存兼容性工具注册，以更新 Steam 目录中的脚本。

## 0.10.5 账户版本与官方资讯

账户 CDKey 版本统一为 JP / NA / EU，默认 NA，界面和内部配置采用相同标识。旧 US 配置读取后规范为 NA，下次保存写入 NA；官方 en-us/en-US 语言代码不变。活动、公告、官网和商城统一按账户版本选择：JP 日文站、NA 北美英文站、EU 按 English(UK)/Français/Deutsch 选择欧版对应语言站点，不跟随客户端语言改变。

“在不同游戏区服间同步插件设置”及三个同步按钮现位于“Dalamud和插件”页面，现有同步内容和覆盖确认机制不变。

## 0.10.6 使用说明

运行包解压后固定为 `xivlauncher-super/`。关闭启动器后，在同一位置替换目录即可保持 Steam 注册路径不变。首次从旧的带版本号目录切换时，在新版取消并重新勾选注册，然后重启 Steam，选择 **FFXIV Super Launcher**。目录位置变化仍需重新注册。兼容性设置即时保存，路径在完成编辑后应用；注册失败立即回报。

新增四语言界面初稿及可编辑翻译对照表。普通文件同步、密钥环交互、网络停滞优先采用隔离模拟测试，不要求用户提供真实账号或等待真实断网。繁中区服务端验证的偶发失败仍未确认修复。
