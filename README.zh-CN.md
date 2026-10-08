<p align="center">
  <img src="src/XIVLauncher.Linux/Resources/icon.png" width="128" height="128" alt="FFXIV Super Launcher 图标">
</p>

<p align="center">
  <strong>简体中文</strong> · <a href="README.zh-TW.md">繁體中文</a> · <a href="README.ja.md">日本語</a> · <a href="README.md">English</a>
</p>

# FFXIV Super Launcher

面向 **Linux** 平台，通过 **Proton** 运行游戏的社区版 FINAL FANTASY XIV（以下简称 FFXIV）启动器，支持**中国区、繁中区和国际区**游戏客户端。

本项目由 **[ImperatorVienna](https://github.com/ImperatorVienna)** 维护，基于 [XIVLauncher](https://github.com/goatcorp/FFXIVQuickLauncher)、[XIVLauncherCN](https://github.com/ottercorp/FFXIVQuickLauncher) 和 [XIVLauncherCN (Soil)](https://github.com/AtmoOmen/FFXIVQuickLauncher) 的源码与经验继续开发，并参考 [XIVTCLauncher](https://github.com/cycleapple/XIVTCLauncher) 的繁中区登录协议与功能设计。它是独立维护的社区衍生项目，不是 Square Enix、Shengqu Games、USERJOY 的官方发行版本，也并非由上述上游项目维护或背书。原作者的版权与许可证声明予以保留。

> **支持平台：x86_64 Linux。** 请从 [Releases](https://github.com/ImperatorVienna/FFXIVSuperLauncher/releases) 页面获取发行文件、版本说明与校验值。

内置浏览器由官方页面与网页验证共用，独立于各游戏区服的登录模块。

## 功能

- **三个游戏区服**：共用前端图形界面和 Proton 启动流程，分别管理客户端目录、账户凭据、Dalamud 和插件数据。
- **Proton 支持**：扫描系统与 Steam 游戏库中的已安装版本，支持手动指定路径；已在基于 Arch Linux 的 CachyOS / proton-cachyos-slr 环境中进行实机测试。
- **账户管理**：按游戏区服添加、选择、编辑和删除账户；可使用 Linux 桌面密码管理器或普通文件保存凭据。
- **验证码**：繁中区与国际区支持手动输入验证码和本地 TOTP 自动生成。国际区仅在账户已启用 OTP 时需要填写。
- **游戏更新**：检查并安装对应游戏区服的客户端补丁，也可以只更新游戏而不启动。
- **Dalamud 和插件**：可选启用 Dalamud；无需进入游戏即可管理插件启用状态，并检查 Dalamud 与插件更新。
- **跨游戏区服同步**：通过“同步 Dalamud 设置”“同步已安装插件”和“同步插件设置”分别同步所选类别；选择来源游戏区服并勾选一个或多个目标后，点击“同步到所选游戏区服”；仅覆盖勾选目标的对应内容。
- **Steam 集成**：支持国际区 Steam 版账户登录；可手动注册为 Steam compatibility tool 以通过 Steam 打开启动器记录游戏时长。
- **界面与公告**：支持简体中文、繁體中文、日本語、English；显示对应游戏区服的官方活动与公告，并提供官方页面入口。
- **中国区超域传送**：支持提交传送、查询历史和返回原区，可在完成后启动游戏。

使用第三方启动器、Dalamud 和插件前，请阅读游戏相关服务条款并自行评估使用风险。Dalamud 默认关闭，项目不提供账号安全或插件可用性保证。

## 特色

- **专为 Linux 平台打造**：仅维护 x86_64 Linux，不提供 Windows、macOS、ARM 或 32 位 x86 版本。采用 AppImage 分发，便于在包括不可变系统在内的 Linux 桌面上部署；具体兼容情况以发行说明为准。
- **使用 Proton 兼容性方案**：移除了 XIVLauncher 和 XIVLauncherCN 的自带 Wine + DXVK 方案，改为调用已安装的 Proton。启动器可自动发现已安装的 Proton，已使用 CachyOS 的 proton-cachyos-slr 进行实机验证。
- **各区服统一管理**：同一个启动器即可切换登录中国区、繁中区和国际区，各自保存账户、客户端与插件数据，各游戏区服的 Dalamud 主程序按需下载；运行包中可包含共用或预置的辅助组件。
- **游戏外管理插件**：无需进入游戏即可调整插件启用状态、选择更新插件，或在游戏区服之间同步设置。
- **凭据管理**：按区服管理账户登录凭据，支持桌面密码管理器或本地明文存储，及本地 TOTP 生成。
- **国际区可复用 Steam 版已有的 pfx**：便于从官方启动器切换到 FFXIV Super Launcher 后继承原 pfx 中已有的本地游戏设置和角色配置文件（不涉及服务器保存的角色进度）。
- **繁中区 Dalamud 注入适配**：针对 Linux／Proton 环境调整注入流程，采用与国际区一致的 entrypoint 路径，并跳过原繁中注入流程中的 ArgFixer；已通过进入角色及安装、运行插件的实机验证。
- **中国区 Dalamud 分支选择**：采用 Soil 使用的 Dalamud-DailyRoutines 分支及更新源，不沿用 ottercorp 分支的插件黑名单策略；这不代表所有插件均兼容或不受游戏服务条款约束。

## 安装与首次使用

### 运行条件

- x86_64 Linux 图形桌面。
- 已安装的 Proton，以及该版本所需的 Steam Linux Runtime；具体依赖以所选 Proton 的说明为准。
- 对应游戏区服的完整客户端。**本项目不提供从空文件夹下载完整客户端的功能。**
- 正常工作的图形驱动及所选 Proton 所需的 Vulkan 支持。
- 如选择桌面密码管理器，需要可用且已解锁的 Secret Service 兼容密钥环，以及 `secret-tool`。

运行包包含 .NET 运行时，普通用户无须另外安装 .NET SDK。其他发行版的依赖与验证情况以具体发行说明为准；能够在 CachyOS 运行不代表所有发行版或沙箱环境都已通过测试。

### AppImage

从 [Releases](https://github.com/ImperatorVienna/FFXIVSuperLauncher/releases) 下载 x86_64 AppImage，赋予执行权限后运行，可放在自行选择的可写目录。仅提供 AppImage 发行包。


首次配置向导会引导选择界面语言、游戏区服、Steam / Proton 目录、凭据保存方式及是否启用 Dalamud。

之后在“语言和区服”中设置客户端目录。应选择包含 `game` 子目录的游戏安装目录；国际区还需完整的 `boot` 子目录。

在“登录凭据管理”中通过“添加账号”填写凭据，点击“保存”后，返回“游戏”页面，通过“选择登录名”选择账号，再点击“登录并启动”。中国区新账户只需填写登录名，首次登录时扫码，成功后按所选存储方式保存快捷凭据。

## Steam 与 Proton

### Steam 授权账户

如果国际区账户的 CDKey 来自 Steam，请勾选“我的 CDKey 购买自 Steam”，并在登录前确保 Steam 客户端已经登录且正在运行。

启动器不会要求填写或保存 Steam 账户密码（仅通过 Steam 验证 CDKey 所有权，登录账户仍然是 Square Enix 账户）。

### 从 Steam 启动本启动器

在“兼容性工具设置”中勾选“注册为 Steam compatibility tool（可随时关闭）”，即可注册 **FFXIV Super Launcher**。重新启动 Steam 后，在 Steam 库中的 FFXIV 游戏属性中选择该 compatibility tool。取消勾选即可移除注册。

这项功能提供 Steam 启动入口，**不等于开启 Steam 账户认证**。账户认证仍由“我的 CDKey 购买自 Steam”控制。

Steam 使用固定启动脚本。手动移动或重命名 AppImage 后，运行一次即可刷新路径。从旧版直接路径注册方式升级时，请重新注册一次。

### compatibility environment / pfx

默认使用“兼容性工具设置”中的“Proton 数据目录（自动在此创建 pfx，建议独立使用）”。国际区可勾选“国际区使用 Steam 已有 pfx”，复用已安装 Steam 版 FFXIV 的既有环境，以使用其中已有的本地游戏设置。

未启用该选项时，国际区与另外两个游戏区服沿用共用环境设置。已有 Steam 环境的选择不等于迁移或同步插件配置。

## 更新方式

启动器每次打开时在后台检查稳定版，也可点击“关于”版本号旁的“检查更新”；不提供更新通道选择。AppImage 更新须经用户确认，完整下载后验证清单签名及 SHA256，再替换原文件。更新文件保存在原目录，文件名使用新版版本号，旧版保留为 `.previous`。Steam 和应用菜单通过 `~/.local/share/xivlauncher-super/appimage-launcher` 启动。手动移动或重命名 AppImage 后，请双击运行一次，以自动刷新路径；之后即可继续从 Steam 或应用菜单启动。有多个副本时，以最后运行的文件为准。请退出游戏后更新，安装后手动重新打开启动器。账户和设置独立保存，后台检查失败不阻塞游戏登录。

在“关于”中点击“添加到应用菜单”。手动移动或重命名后运行一次即可，无需重新添加菜单入口。从旧版直接路径入口升级时，请重新添加一次。

| 操作 | 行为 |
| --- | --- |
| 登录并启动 | 账户认证成功后检查游戏更新；启用 Dalamud 时再检查其更新，随后启动游戏 |
| 检查并更新游戏 | 仅更新当前游戏区服客户端；中国区、繁中区无须先登录，国际区需要账户认证，但不会启动游戏 |
| 检查并更新 Dalamud | 手动检查当前游戏区服的 Dalamud |
| 检查并更新插件 | 按插件记录的来源仓库检查更新，供用户选择安装 |

Dalamud 更新连接超时时，可以选择使用现有可用版本继续启动，或禁用 Dalamud 后启动。选择禁用会保存该设置，之后需要手动重新启用；现有版本缺失或不兼容时，跳过更新不保证能够启动。

以上两个 Dalamud 和插件更新按钮位于“Dalamud 和插件”页面。该页面还提供“刷新插件启用状态”和“应用插件启用状态”；修改勾选状态后需点击后者保存，下次启动游戏生效。

跨游戏区服同步插件前，请确认目标游戏区服的 Dalamud API 与插件版本兼容。同步会覆盖所选目标的数据。目前不要把繁中区插件 DLL 与其他游戏区服的版本直接混用。

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

“登录凭据管理”中的“使用桌面密码管理器”默认勾选。取消勾选并点击“保存”后，后续保存的普通文件凭据位于对应游戏区服目录的 `credentials.json`，其中可能包含明文密码、2FA 密钥或快捷登录凭据。此设置对所有游戏区服生效，已有凭据不会自动搬运。请勿上传或分享这些文件。

2FA 密钥与六位验证码不同：密钥用于生成验证码，验证码仅在当前登录时输入。不要把六位验证码填入密钥字段。

## 问题反馈

请通过 [GitHub Issues](https://github.com/ImperatorVienna/FFXIVSuperLauncher/issues) 提交问题和功能建议。“关于”页面预留了“加入 Discord”按钮，目前禁用；社群建立后将在后续版本启用。

反馈时请尽量提供：

- 启动器版本、Linux 发行版、桌面环境和 Proton 版本。
- 当前游戏区服，以及直接启动还是通过 Steam compatibility tool 启动。
- 复现步骤、预期行为、实际行为和错误发生时间。
- 必要的截图或相关诊断日志。

界面日志保持简洁，完整诊断日志路径可在“关于”页面查看。提交前请检查日志和截图，移除账户信息、登录票据、密码、2FA 密钥及二维码等敏感内容。

## 从源码构建

需要 .NET 10 SDK、C 编译器、Python 3、Git 和 `7z`。构建及打包可能需要下载 NuGet 依赖、固定版本的辅助组件和对应源码。

在仓库根目录执行：

```bash
# 构建 Linux 启动器
dotnet build src/XIVLauncher.Linux/XIVLauncher.Linux.csproj -c Release -m:1

# 运行离线测试
dotnet test src/XIVLauncher.Linux.Tests/XIVLauncher.Linux.Tests.csproj \
  -c Release -m:1 --filter "Category!=Network"

# 检查已记录的许可材料
python3 scripts/check-compliance-materials.py

# 生成运行包和对应源码附件
bash scripts/package-linux.sh
```

默认输出到 `artifacts/linux/`。再次打包时需使用新的输出目录，例如：

```bash
bash scripts/package-linux.sh artifacts/local-build
```

完整打包会同时生成许可证、依赖声明、对应源码附件、`SOURCE-DELIVERY.json` 与校验文件。重新分发时，请保留相关声明，并按适用许可证一并提供对应源码。详情见[源码交付说明](compliance/SOURCE-DELIVERY.txt)。

## 关于页面

“关于”显示启动器版本、维护者及上游致谢，并提供“GitHub 仓库”“报告问题”“维护者主页”“查看许可证”“第三方声明”“源码与来源记录”和“图标来源”入口。网页链接通过内置浏览器打开，并保留“在系统浏览器中打开”按钮；许可证和第三方声明可在启动器内离线查看。完整诊断日志路径仅在此选项卡下显示，位于底部日志框上方。

## 上游与致谢

本项目建立在以下项目及其贡献者的工作之上：

- [goatcorp / FFXIVQuickLauncher](https://github.com/goatcorp/FFXIVQuickLauncher)：原始 XIVLauncher，以及国际区认证、补丁等实现。
- [ottercorp / FFXIVQuickLauncher](https://github.com/ottercorp/FFXIVQuickLauncher)：中国区适配与相关实现。
- [AtmoOmen / FFXIVQuickLauncher](https://github.com/AtmoOmen/FFXIVQuickLauncher)：中国区 Soil 分支，本项目构建之初的主要参考对象。
- [cycleapple / XIVTCLauncher](https://github.com/cycleapple/XIVTCLauncher)：繁中区登录协议与功能设计参考；不代表其整个仓库均按本项目许可证授权。
- [goatcorp / Dalamud](https://github.com/goatcorp/Dalamud)、[Dalamud-DailyRoutines / Dalamud](https://github.com/Dalamud-DailyRoutines/Dalamud)、[yanmucorp / Dalamud](https://github.com/yanmucorp/Dalamud)：对应游戏区服的 Dalamud 及注入组件。
- Avalonia、.NET、Proton、Steamworks、Electron（内含 Chromium 和 Node.js）、xdelta3，以及项目使用的其他第三方组件及其贡献者。

详细的继承关系、修改范围和来源依据见 [SOURCES.txt](SOURCES.txt)，第三方声明见 [THIRD-PARTY-NOTICES.txt](THIRD-PARTY-NOTICES.txt)。

## 许可证

启动器代码按 [GNU GPL 第 3 版](LICENSE) 分发。各第三方组件及素材保留自身的许可证与声明，不能将它们一概视为 GPL 授权。

当前图标由维护者提供，来源页面为 [PNGAAA](https://www.pngaaa.com/detail/6354760)，该页面标注“非商业使用”。这不是本项目对图标的 GPL 授权，也不代表已独立确认原版权方允许再分发；相关记录见[图标来源说明](compliance/provenance/artwork.json)。

分发材料及许可审查记录见[发布清单](compliance/RELEASE-CHECKLIST.txt)。

如您认为本项目中的代码、依赖或素材侵犯了您的权利，请通过 GitHub Issues 联系维护者，并提供相关内容的位置及权利依据。维护者将核实并沟通处理，必要时移除或替换相关内容。请勿在公开 Issue 中提交个人敏感信息。
