<p align="center">
  <img src="src/XIVLauncher.Linux/Resources/icon.png" width="128" height="128" alt="FFXIV Super Launcher 图标">
</p>

<p align="center">
  <strong>简体中文</strong> · <a href="README.zh-TW.md">繁體中文</a> · <a href="README.ja.md">日本語</a> · <a href="README.md">English</a>
</p>

# FFXIV Super Launcher

面向 **Linux** 平台，通过 **Proton** 运行游戏的社区版 FINAL FANTASY XIV（以下简称 FFXIV）启动器，支持**中国区、繁中区和国际区**游戏客户端。

本项目由 **[ImperatorVienna](https://github.com/ImperatorVienna)** 维护，基于 [XIVLauncher](https://github.com/goatcorp/FFXIVQuickLauncher)、[XIVLauncherCN](https://github.com/ottercorp/FFXIVQuickLauncher) 和 [XIVLauncherCN (Soil)](https://github.com/AtmoOmen/FFXIVQuickLauncher) 的源码与经验继续开发，并参考 [XIVTCLauncher](https://github.com/cycleapple/XIVTCLauncher) 的繁中区登录协议与功能设计。它是独立维护的社区衍生项目，不是 Square Enix、Shengqu Games、USERJOY 的官方发行版本，也并非由上述上游项目维护或背书。原作者的版权与许可证声明予以保留。

> **支持平台：x86_64 Linux。** 请从 [Releases](https://github.com/ImperatorVienna/FFXIVSuperLauncher/releases) 获取发布文件和版本说明。

内置浏览器由官方页面与网页验证共用，独立于各游戏区服的登录模块。

## 功能

- **三个游戏区服**：共用前端图形界面和 Proton 启动流程，分别管理客户端目录、账户凭据、Dalamud 和插件数据。
- **Proton 支持**：扫描系统与 Steam 游戏库中的已安装版本，支持手动指定路径；已在基于 Arch Linux 的 CachyOS / proton-cachyos-slr 环境中进行实机测试。
- **账户管理**：按游戏区服添加、选择、编辑和删除账户；使用本地明文文件保存凭据。
- **验证码**：繁中区与国际区支持手动输入验证码和本地 TOTP 自动生成。国际区仅在账户已启用 OTP 时需要填写。
- **游戏更新**：检查并安装对应游戏区服的客户端补丁，也可以只更新游戏而不启动。
- **Dalamud 和插件**：可选启用 Dalamud；无需进入游戏即可管理插件启用状态，并检查 Dalamud 与插件更新。
- **跨游戏区服同步**：通过“同步第三方仓库”“同步已安装插件”和“同步插件设置”分别同步所选类别；选择来源及目标游戏区服后执行。第三方仓库同步仅替换仓库列表及启用状态，始终保留目标主仓库、预置仓库开关和其他 Dalamud 设置；原数据保留备份。
- **Steam 集成**：支持国际区 Steam 版账户登录；可手动注册为 Steam compatibility tool 以通过 Steam 打开启动器记录游戏时长。
- **界面与公告**：Electron 图形界面支持简体中文、繁體中文、日本語、English，即时切换，无须重启；显示对应游戏区服的官方活动与公告，并提供游戏官网、服务器状态等官方页面入口。
- **第三方插件仓库**：在插件管理右侧添加、编辑、启用/停用、删除仓库或检查连接；国服插件更新同时识别启用的预置仓库。
- **中国区超域传送**：支持提交传送、查询历史和返回原区，可在完成后启动游戏。适配 DCTravelerX（[Dalamud-DailyRoutines/DCTraveler](https://github.com/Dalamud-DailyRoutines/DCTraveler)）游戏内跨大区及授权恢复。
- **存储清理**：在“关于”中清理下载缓存、日志和内置浏览器缓存；启动时自动回收可确认失效的会话记录。

使用第三方启动器、Dalamud 和插件前，请阅读游戏相关服务条款并自行评估使用风险。Dalamud 默认关闭，项目不提供账号安全或插件可用性保证。

## 特色

- **专为 Linux 平台打造**：仅维护 x86_64 Linux，不提供 Windows、macOS、ARM 或 32 位 x86 版本。采用 AppImage 分发，便于在包括不可变系统在内的 Linux 桌面上部署；具体兼容情况以发行说明为准。
- **使用 Proton 兼容性方案**：移除了 XIVLauncher 和 XIVLauncherCN 的自带 Wine + DXVK 方案，改为调用已安装的 Proton。启动器可自动发现已安装的 Proton，已使用 CachyOS 的 proton-cachyos-slr 进行实机验证。
- **各区服统一管理**：同一个启动器即可切换登录中国区、繁中区和国际区，各自保存账户、客户端与插件数据，各游戏区服的 Dalamud 主程序按需下载；运行包中可包含共用或预置的辅助组件。
- **游戏外管理插件**：无需进入游戏即可调整插件启用状态、选择更新插件，或在游戏区服之间同步设置。
- **凭据管理**：按区服管理账户登录凭据，支持本地明文存储，及本地 TOTP 生成。
- **国际区可复用 Steam 版已有的 pfx**：便于从官方启动器切换到 FFXIV Super Launcher 后继承原 pfx 中已有的本地游戏设置和角色配置文件（不涉及服务器保存的角色进度）。
- **繁中区 Dalamud 注入适配**：针对 Linux/Proton 环境调整注入流程，采用与国际区一致的 entrypoint 路径，并跳过原繁中注入流程中的 ArgFixer；已通过进入角色及安装、运行插件的实机验证。
- **中国区 Dalamud 分支选择**：采用 Dalamud-DailyRoutines 分支及更新源，不沿用 ottercorp 分支的插件黑名单策略；这不代表所有插件均兼容或不受游戏服务条款约束。

## 安装与首次使用

### 运行条件

- x86_64 Linux 图形桌面。
- 已安装的 Proton，以及该版本所需的 Steam Linux Runtime；具体依赖以所选 Proton 的说明为准。
- 对应游戏区服的完整客户端。**本项目不提供从空文件夹下载完整客户端的功能。**
- 正常工作的图形驱动及所选 Proton 所需的 Vulkan 支持。

运行包已内置 .NET 和 Electron（包含 Chromium 和 Node.js）运行组件，无须单独安装 .NET SDK、Electron 或浏览器。其他发行版的依赖与验证情况以具体发行说明为准；能够在 CachyOS 运行不代表所有发行版或沙箱环境都已通过测试。

### AppImage

从 [Releases](https://github.com/ImperatorVienna/FFXIVSuperLauncher/releases) 下载 x86_64 AppImage，赋予执行权限后运行，可放在自行选择的可写目录。仅提供 AppImage 发行包。

首次配置向导会引导选择界面语言、游戏区服、Steam / Proton 目录及是否启用 Dalamud。

之后在“语言和区服”中设置客户端目录。应选择包含 `game` 子目录的游戏安装目录；国际区还需完整的 `boot` 子目录。

在“登录凭据管理”中通过“添加账户”填写凭据，点击“保存凭据”后，返回“游戏”页面，通过“选择登录名”选择账号，再点击“登录并启动”。中国区新账户只需填写登录名，首次登录时扫码，成功后自动保存快捷凭据。

### 界面与国际区语言设置

在“语言和区服”页面中：

- **启动器界面语言**：选择后立即保存并切换所有页面，无须重启启动器。
- **国际区客户端语言**：仅在选择国际区时显示，可选日本語、English、Français、Deutsch。选择后立即保存，下次启动游戏时生效；游戏运行时禁止修改，切换到其他游戏区服不会清除这一选择。
- **账户 CDKey 版本**：按账户实际授权选择 JP、NA 或 EU，默认 NA。此设置用于选择对应的官网、商城、Mog Station 和活动公告来源，不会改变账户授权。
- **官网及商城偏好语言**：仅CDKey版本选择 EU 时显示，可选 English(UK)、Français、Deutsch，并用于选择相应语言的官网、商城及公告。上述网页偏好更改后自动保存，独立于客户端语言。

## Steam 与 Proton

### Steam 授权账户

如果国际区账户的 CDKey 来自 Steam，请勾选“我的 CDKey 购买自 Steam”，并在登录前确保 Steam 客户端已经登录且正在运行。

启动器不会要求填写或保存 Steam 账户密码（仅通过 Steam 验证 CDKey 所有权，登录账户仍然是 Square Enix 账户）。

### 从 Steam 启动本启动器

在“兼容性工具设置”中勾选“注册为 Steam compatibility tool（可随时关闭）”，即可将 **FFXIV Super Launcher** 注册。重新启动 Steam 后，在 Steam 库中的 FFXIV 游戏属性中选择该 compatibility tool。取消勾选即可移除注册。

这项功能提供 Steam 启动入口，**不等于开启 Steam 账户认证**。账户认证仍由“我的 CDKey 购买自 Steam”控制。

该功能使用固定启动脚本。手动移动或重命名 AppImage 后，运行一次即可刷新路径。启动器自动更新后会自动重启并刷新路径，无需任何手动操作。

### 兼容性环境 / pfx

默认使用“兼容性工具设置”中的“Proton 数据目录（自动在此创建 pfx，建议独立使用）”。国际区可勾选“国际区使用 Steam 已有 pfx”，复用已安装 Steam 版 FFXIV 的既有环境，以使用其中已有的本地游戏设置。

未启用该选项时，国际区与另外两个游戏区服沿用共用环境设置。已有 Steam 环境的选择不等于迁移或同步插件配置。

## 更新方式

自动检查更新默认开启，首次配置完成后检查稳定版，之后每次启动也会检查。可在“关于”的“添加到应用菜单”右侧关闭“自动检查更新”；无论开关状态如何，均可点击“关于”版本号旁的“检查更新”；不提供更新通道选择。AppImage 更新须经用户确认，完整下载后验证清单签名及 SHA256，再替换原文件。更新文件保存在原目录，文件名使用新版版本号，旧版保留为 `.previous`。Steam 和应用菜单通过 `~/.local/share/xivlauncher-super/appimage-launcher` 启动。手动移动或重命名 AppImage 后，请双击运行一次，以自动刷新路径；之后即可继续从 Steam 或应用菜单启动。有多个副本时，以最后运行的文件为准。请退出游戏后更新。安装完成后会直接通过新版 AppImage 的绝对路径自动打开界面，不依赖应用菜单注册。账户和设置独立保存，后台检查失败不阻塞游戏登录。

运行旧 AppImage 可回滚启动器，回滚后请关闭自动检查更新；旧版本是否支持该开关取决于该版本自身。多个 AppImage 共用入口时，以最后运行的文件为准。

应用菜单入口会在 AppImage 启动时自动创建。“关于”中的“添加到应用菜单”用于手动重建入口，通常无须额外操作。默认入口文件为 `~/.local/share/applications/xivlauncher-super.desktop`，遵循 `XDG_DATA_HOME`。手动移动或重命名 AppImage 后，直接运行一次即可刷新记录。

AppImage 启动时先创建缺失的默认配置、写入固定启动脚本和应用菜单入口，再进入首次设置；自动检查更新在首次配置完成后执行。入口也可在“关于”中手动重建。完成设置或添加入口后，会在后台刷新可用的应用菜单缓存，不弹出终端窗口；刷新失败不会阻止启动器使用。菜单入口使用持久保存的图标文件绝对路径，AppImage 卸载后图标仍可读取。

| 操作 | 行为 |
| --- | --- |
| 登录并启动 | 账户认证成功后检查游戏更新；启用 Dalamud 时再检查其更新，随后启动游戏 |
| 检查并更新游戏 | 仅更新当前游戏区服客户端；中国区、繁中区无须先登录，国际区需要账户认证，但不会启动游戏 |
| 检查并更新 Dalamud | 手动检查当前游戏区服的 Dalamud |
| 检查并更新插件 | 按插件记录的来源仓库检查更新，供用户选择安装 |

Dalamud 更新连接超时时，可以选择使用现有可用版本继续启动，或禁用 Dalamud 后启动。选择禁用会保存该设置，之后需要手动重新启用；现有版本缺失或不兼容时，跳过更新不保证能够启动。

以上两个 Dalamud 和插件更新按钮位于“Dalamud 和插件”页面。修改插件勾选状态后立即保存，下次启动游戏生效。“刷新插件启用状态”与“刷新仓库状态”分别重新读取对应数据，并清除各自的操作提示。检查连接的结果会显示在对应仓库下方，同时写入日志。

游戏运行时禁止修改插件、仓库、客户端语言、客户端路径和兼容环境；退出游戏后可继续编辑。需要填写多项内容的登录凭据仍需点击“保存凭据”。

跨游戏区服同步插件前，请确认目标游戏区服的 Dalamud API 与插件版本兼容。同步会覆盖所选目标的数据。目前不要把繁中区插件 DLL 与其他游戏区服的版本直接混用。

## 中国区游戏内跨大区

2.1.0 为 **DCTravelerX**（[Dalamud-DailyRoutines/DCTraveler](https://github.com/Dalamud-DailyRoutines/DCTraveler)）提供 Proton 环境下的启动器通信支持。启用中国区 Dalamud、安装兼容版本的 DCTravelerX，并通过本启动器登录游戏后，可在插件中使用游戏内跨大区功能。此适配不代表 ottercorp 的 DcTraveler 或其他插件已获兼容保证。

游戏启动后前端界面可以关闭，后台会继续维护游戏会话、跨区授权及 Steam 状态。不要手动结束该后台进程。再次打开启动器可恢复前端界面。

跨区登录授权过期时，后台先尝试使用已保存的快捷登录凭据恢复。仍需扫码时，打开启动器，或点击“超域传送”下方的“重新授权游戏内跨区”，使用**当前游戏账号**完成扫码，无须退出游戏。恢复成功后回到游戏重新点击跨区；启动器不会自动重提结果不明确的跨区订单。取消、失败或超时会结束本次扫码，后台扫码最长等待五分钟。

## 存储清理

“关于”页面提供三个清理入口：

| 按钮 | 清理范围与保留内容 |
| --- | --- |
| 清理下载缓存 | 清理启动器管理的补丁缓存、下载和安装暂存文件；保留已安装的游戏、运行组件、插件及备份，不接管 Dalamud 自行管理的内容 |
| 清理日志 | 清理启动器管理的日志；当前日志通过日志模块协调清空，其他进程正在使用或无法安全处理的文件跳过，不清理 Dalamud 日志 |
| 清理内置浏览器缓存 | 清理当前 Electron 会话和网页验证会话的 HTTP、代码及着色器缓存，保留 Cookie 和验证状态；不包含旧版独立验证码浏览器的遗留目录 |

游戏运行时也可以清理。下载、安装与下载缓存清理使用共同的跨进程锁，相关任务进行中会阻止清理下载缓存；当前操作或网页验证进行中不能清理内置浏览器缓存。结果会显示释放空间和跳过项；清理零个文件表示当前没有可清理的目标。

启动时还会自动检查会话记录及已知临时文件，回收能够确认失效的记录；活动会话和无法确认安全性的文件会保留。这不是扫描整个磁盘的垃圾清理功能。

## 设置与凭据

默认数据目录为：

```text
~/.local/share/xivlauncher-super/
├── ffxiv_cn/     # 中国区数据
├── ffxiv_tc/     # 繁中区数据
├── ffxiv/        # 国际区数据
├── compatdata/  # 默认共用环境，内部包含 pfx
└── logs/        # 诊断日志
```

程序遵循 `XDG_DATA_HOME`。Steam 与 Proton 等共用设置保存在启动器数据目录中，账户与 Dalamud 数据按游戏区服隔离。

凭据仅以明文保存在各游戏区服目录下的 `credentials.json` 中，密码、2FA 密钥和快捷登录凭据均不加密。启动器创建的凭据文件仅允许当前用户读写，请勿上传或分享。

2FA 密钥与六位验证码不同：密钥用于生成验证码，验证码仅在当前登录时输入。不要把六位验证码填入密钥字段。

## 问题反馈

请通过 [GitHub Issues](https://github.com/ImperatorVienna/FFXIVSuperLauncher/issues) 提交问题和功能建议。“关于”页面预留了“加入 Discord”按钮，目前禁用；社群建立后将在后续版本启用。

反馈时请尽量提供：

- 启动器版本、Linux 发行版、桌面环境和 Proton 版本。
- 当前游戏区服，以及直接启动还是通过 Steam compatibility tool 启动。
- 复现步骤、预期行为、实际行为和错误发生时间。
- 必要的截图或相关诊断日志。

界面日志使用英语记录操作结果，可选择并复制。日志浮层由用户手动展开，条目增加时内部滚动，不挤压主界面；所有页面的日志栏上方均显示完整诊断日志路径。提交前请检查日志和截图，移除账户信息、登录票据、密码、2FA 密钥及二维码等敏感内容。

## 从源码构建

如需从源码构建或参与开发，请参阅[开发说明](LINUX.zh-CN.md)。

## 关于页面

“关于”显示启动器版本、维护者及上游致谢，并提供“GitHub 仓库”“报告问题”“维护者主页”“查看许可证”“第三方声明”“源码与来源记录”和“美术素材来源”入口。“美术素材来源”弹窗列出启动器图标、GenRyu 和 Cinzel 三个来源入口。网页链接通过内置浏览器打开，并保留“在系统浏览器中打开”按钮；许可证和第三方声明可在启动器内离线查看。

## 上游与致谢

本项目建立在以下项目及其贡献者的工作之上：

- [goatcorp / FFXIVQuickLauncher](https://github.com/goatcorp/FFXIVQuickLauncher)：原始 XIVLauncher，以及国际区认证、补丁等实现。
- [ottercorp / FFXIVQuickLauncher](https://github.com/ottercorp/FFXIVQuickLauncher)：中国区适配与相关实现。
- [AtmoOmen / FFXIVQuickLauncher](https://github.com/AtmoOmen/FFXIVQuickLauncher)：中国区 Soil 分支，本项目构建之初的主要参考对象。
- [cycleapple / XIVTCLauncher](https://github.com/cycleapple/XIVTCLauncher)：繁中区登录协议与功能设计参考；不代表其整个仓库均按本项目许可证授权。
- [goatcorp / Dalamud](https://github.com/goatcorp/Dalamud)、[Dalamud-DailyRoutines / Dalamud](https://github.com/Dalamud-DailyRoutines/Dalamud)、[yanmucorp / Dalamud](https://github.com/yanmucorp/Dalamud)：对应游戏区服的 Dalamud 及注入组件。
- .NET、Proton、Steamworks、Electron（内含 Chromium 和 Node.js）、xdelta3，以及项目使用的其他第三方组件及其贡献者。

详细的继承关系、修改范围和来源依据见 [SOURCES.txt](SOURCES.txt)，第三方声明见 [THIRD-PARTY-NOTICES.txt](THIRD-PARTY-NOTICES.txt)。

## 许可证

启动器代码按 [GNU GPL 第 3 版](LICENSE) 分发。各第三方组件及素材保留自身的许可证与声明，不能将它们一概视为 GPL 授权。

当前图标由维护者提供，来源页面为 [PNGAAA](https://www.pngaaa.com/detail/6354760)，该页面标注“非商业使用”。这不是本项目对图标的 GPL 授权，也不代表已独立确认原版权方允许再分发；相关记录见[图标来源说明](compliance/provenance/artwork.json)。

界面字体使用 [GenRyu](https://github.com/ButTaiwan/genryu-font)（ButTaiwan；v2 TC／PJP）和 [Cinzel](https://github.com/NDISCOVER/Cinzel)（The Cinzel Project Authors），均附带 SIL Open Font License 1.1。字体许可证、来源及文件校验值随发行包提供；详细记录见[字体来源清单](prototypes/electron-ui/assets/font-sources.json)。在“关于”→“美术素材来源”中可打开图标及两种字体的来源页面。

分发材料及许可审查记录见[发布清单](compliance/RELEASE-CHECKLIST.txt)。

如您认为本项目中的代码、依赖或素材侵犯了您的权利，请通过 GitHub Issues 联系维护者，并提供相关内容的位置及权利依据。维护者将核实并沟通处理，必要时移除或替换相关内容。请勿在公开 Issue 中提交个人敏感信息。
