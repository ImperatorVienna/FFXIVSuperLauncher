# Electron 前端及完整测试包

Electron 前端已经连接现有三个区服的业务服务。完整测试包只编译并携带新的 Electron 界面，不包含 Avalonia 界面、程序集或旧窗口入口。它仍是待实机验收的测试包，不代表已完成三端真实登录、游戏启动和插件注入验收。

## 功能范围

- 四语言界面即时切换；按区服管理客户端路径、账户凭据和插件，国际区保留 CDKey 版本、EU 语言、客户端语言、Steam 账户及试玩设置。
- 凭据明确保存后写入既有明文文件；中国区沿用凭据快捷登录、失效后扫码，繁中区及国际区的首次账户引导到凭据管理。选中有密钥的账户默认自动 OTP，独立本地验证码工具无需后端或网络。
- 三端认证、游戏更新、Proton 启动及 entrypoint 注入共用现有服务；可临时禁用全部插件。二维码、手机确认码、手动 OTP 共用“开始游戏”下方的验证区域；小窗口下由页面统一滚动，公告、登录卡和验证区不再各自滚动。
- 繁中区网页验证也使用该区域内的隔离 WebContentsView，沿用原有官方地址、站点密钥和验证码页面生成逻辑。先导航到空白文档再启用调试会话与页面拦截，避免新视图在首次导航前等待调试命令而挂起；初始化命令限时 15 秒，页面导航限时 45 秒，整个验证限时 5 分钟。失败只记录固定阶段，不记录网页内容或令牌；取消会立即中断初始化并销毁视图。网页没有 Node.js、preload 或本地凭据接口；验证令牌只由主进程交给后端，不进入本地页面或日志。后续手动 OTP 隐藏网页但保留会话，认证完成、取消或失败后清理。
- 三端公告、活动和官方链接，包括服务器状态；切换区服、CDKey 版本或 EU 语言后重新读取。网络失败仅影响对应资讯，支持重试。
- 插件启用状态读取、刷新及勾选后即时保存；手动更新 Dalamud、从原插件仓库选择更新；按数据类别和所选目标区服同步，保留备份、并发修改检查及繁中插件 DLL 提醒。
- 中国区超域传送：登录、来源角色和目标世界选择、订单查询、返回原世界、明确确认及可选传送后启动。订单轮询可以停止等待，不会将未确认的结果当成成功。
- 兼容性设置即时保存，注册及取消注册 Steam 兼容性工具立即执行；失败回报实际状态。Proton 自动发现及手动选择、共享环境目录、国际区既有 Steam pfx、环境检测均已接入。
- 首次设置、窗口位置和最大化状态保存、游戏启动后最小化；自动最小化不会覆盖原窗口状态。Steam 启动会维持跟踪进程直到所启动的游戏退出。
- “关于”中的作者、项目、反馈、上游链接、许可证、第三方声明和预留 Discord 按钮；完整诊断路径位于所有页面的底部日志栏上方。
- 默认在首次配置完成后自动检查稳定版，后续启动也会检查，可在“关于”关闭自动检查，“关于”支持手动检查；AppImage 沿用签名验证、下载校验、备份和原子替换，完成后直接运行新版。创建默认配置、稳定启动脚本和应用菜单入口先于更新检查及首次设置；手动移动或重命名 AppImage 后双击运行一次即可刷新入口。

## 开发启动

```bash
dotnet build src/XIVLauncher.Linux/XIVLauncher.Linux.csproj -c Release -m:1
FFXIV_BACKEND="$PWD/src/XIVLauncher.Linux/bin/Release/net10.0/xivlauncher-super" \
FFXIV_ELECTRON="/path/to/xivlauncher-super/Tools/webview/electron" \
  node frontend/run.cjs
```

自定义 SDK 需设置 `DOTNET_ROOT`。开发入口使用仓库内 `artifacts/electron-development-data/`，可通过 `FFXIV_FRONTEND_DATA_DIR` 改为其他独立目录；不自动读取正式版账户。开发入口临时复用现有 Electron 运行库，不修改已安装的官方网页浏览器。

## 纯 Electron 打包

```bash
python3 scripts/package-electron.py artifacts/electron-test --version 2.0.0
python3 scripts/package-appimage.py artifacts/electron-test/xivlauncher-super \
  artifacts/electron-test/xivlauncher-super-2.0.0-electron-test-x86_64.AppImage \
  --runtime /path/to/pinned/runtime-x86_64 --public-key packaging/linux/appimage-public.pem
```

`DOTNET` 可指定 SDK 可执行文件。脚本在独立构建目录发布自包含 .NET 后端，项目已不再保留旧前端的条件编译路径；`xivlauncher-super` 只启动 Electron，`xivlauncher-backend` 仅提供具名后台协议。默认准备固定版本的辅助组件；`--support-bundle /path/to/previous/xivlauncher-super` 可复用已验证的组件，复用的第三方文件必须匹配 `packaging/electron/support-hashes.json`，不会复制旧界面或用户数据。包内共用一个 Electron 运行时，通过固定入口分派启动器和独立官方网页浏览器。

AppImage 默认沿用 `~/.local/share/xivlauncher-super`（或 XDG 对应目录）中的现有配置；开发测试若需要完全独立，可设置 `FFXIV_FRONTEND_DATA_DIR`。独立目录模式不安装正式菜单入口，也不执行 AppImage 自更新。源码、字体授权及依赖声明随本地构建归档；公开发行时还应按现有对应源码流程整理辅助组件材料。

## 通信与故障隔离

协议版本为 5。Electron 主进程通过标准输入／输出调用 .NET，不开放 HTTP 或调试监听端口。渲染进程启用沙箱及上下文隔离，只允许当前本地页主框架调用具名接口，没有文件系统、任意命令、任意网址或原始 IPC 权限。繁中网页验证使用该视图内部的调试会话，不开放远程调试端口；令牌提交接口不暴露给渲染进程。

认证、游戏更新、插件操作、环境检测、传送与启动器安装共用可取消的后台任务及冲突保护。公告和版本检查单独限时，失败不会中断登录。旧挑战、旧更新计划及传送确认标识被拒绝；保存失败保留或重新读取真实状态，长操作结束后释放界面。账号密码、密钥、验证码、验证码令牌及原始响应不写入日志。各独立服务共用同一个完整诊断日志文件。

普通浏览器打开 `prototypes/electron-ui/` 仍是静态设计预览；真实 Electron 的 preload 或后端故障不会回退到模拟成功。

## 验证

```bash
dotnet test src/XIVLauncher.Linux.Tests/XIVLauncher.Linux.Tests.csproj \
  -c Release -m:1 --filter 'Category!=Network'
FFXIV_BACKEND="$PWD/src/XIVLauncher.Linux/bin/Release/net10.0/xivlauncher-super" \
  node --test --test-isolation=none frontend/tests/*.test.cjs
node frontend/tests/create-preview-fixture.cjs
python3 -m http.server 8765 --bind 127.0.0.1
# http://127.0.0.1:8765/artifacts/frontend-ui-tests/
```

本轮验证：441 项 .NET 离线测试及 57 项 Node.js 测试通过。所有自动业务测试使用临时目录、合成账户及网络响应，不登录真实账户。测试覆盖配置、凭据、认证回调、插件操作、传送、更新签名及替换、超时取消、启动顺序、窗口状态、网页验证隔离和故障恢复。浏览器模拟页额外提供 `?setup=new`、`?updates=new` 与 `?logs=growing`，分别检查首次向导、更新提示和持续新增日志时的选择／复制；这些模拟接口不进入运行包。真实服务端认证、游戏及 Dalamud 稳定性需要用户使用完整测试包验收。

## 叨鱼手机验证字段核对

2026-10-09 对照项目继承的 Soil 版本 `4c3ba518ce6985ed808283d2e92bd5ed14fa3c4e`：

- [`SendPushMessageAsync`](https://github.com/AtmoOmen/FFXIVQuickLauncher/blob/4c3ba518ce6985ed808283d2e92bd5ed14fa3c4e/src/XIVLauncher.Login/Channels/LoginChannelContext.cs#L246) 调用 `sendPushMessage.json`，分别读取 `data.pushMsgSerialNum`（供用户核对的确认码）和 `data.pushMsgSessionKey`（后端轮询所用，不应交给界面）。
- [`SlideLoginChannel`](https://github.com/AtmoOmen/FFXIVQuickLauncher/blob/4c3ba518ce6985ed808283d2e92bd5ed14fa3c4e/src/XIVLauncher.Login/Channels/SlideLoginChannel.cs#L24) 将确认码原样传给 `ShowVerificationCode`。[`MainWindowLoginUIService`](https://github.com/AtmoOmen/FFXIVQuickLauncher/blob/4c3ba518ce6985ed808283d2e92bd5ed14fa3c4e/src/XIVLauncher/Windows/ViewModel/Main/Services/MainWindowLoginUIService.cs#L32) 在本地拼接“确认码: ”；完整中文操作指引不是这个验证码字段的内容。
- `failReason` 为独立的失败原因字段，可能包含服务端中文说明，不能与正常验证指引混为一谈。
- 手机验证预留回调已分开传递验证类型与 `code`，由前端本地化固定指引；不从中文句子中提取数字，不把会话密钥或确认码写入日志。当前模拟页面中的中文句子和 `12345` 均为合成数据，不能作为服务器响应样本。现有国服仍按保存凭据登录、失效后扫码的顺序执行，不发送手机推送，也不恢复已删除的滑动登录模式。

本次通过上游源码确认字段用途，没有发起真实登录，也没有抓取实时响应。另修正共用 OTP 输入监听器，允许整合面板不包含原型专用错误标签。


### 日志与后台资源

日志增量更新，未变化的文字节点保留；选取及拖选期间暂缓替换内容，取消选择后恢复最新的、最多 1000 行日志。界面自身的错误不会被下一次后端轮询清掉。公告列表只在内容或界面语言变化时重建。进行中任务的状态轮询在前台为 350 ms、最小化后为 2 秒，后台暂停不必要的页面重绘，返回前台时恢复；完成后停止任务轮询。没有繁中网页验证时不发送视图布局 IPC，有验证时只在显示条件或位置变化时同步。保留 Chromium 后台节流、现有透明背景和 3 px 模糊；不关闭全局硬件加速。以上减少重复工作，并非游戏帧率或显存降低幅度的保证，仍需实机比较。

### 1.1.1 实机反馈与退出回归

2026-10-09，用户确认使用 1.1.1 成功完成繁中区登录并启动游戏，退出启动器后关联进程也正常退出。1.1.1 中的 `xivlauncher-super-verification` 是共用 Electron 运行包沿用的包名，不能仅凭该显示名判断它是残留的独立验证窗口。1.1.0 的验证初始化等待会阻塞退出并占住单实例锁；1.1.1 取消时立即中断等待，关闭时直接释放视图。后续新增主进程退出回归，覆盖“验证命令不返回时直接关闭”和“先取消再关闭”，确认均进入后端关闭、会话清理及进程退出流程。

上述真实登录及退出结果来自用户实测；中国区、国际区的 Electron 实机验收、长时间游戏及插件运行、帧率和资源占用对比仍分别记录，不由此次反馈推断。

## 当前分支清理

旧窗口、旧验证码浏览器、旧凭据草稿显示字段及无调用方的后端界面文案已删除；当前构建的 Electron 包名统一为 `ffxiv-super-launcher`。验证视图在主进程独立检查对应操作是否结束，不依赖渲染页面持续发送布局事件。主页面崩溃和终止信号走统一退出流程，各清理步骤独立执行；某个步骤报错不会跳过后端和会话文件清理。详细功能对应、故障边界与测试结果见 [CLEANUP-AUDIT.md](CLEANUP-AUDIT.md)。

### 1.1.3 界面调整

账户选择弹窗与预览共用一套列表结构，用户名按原顺序逐行排列，取消按钮位于列表下方；长列表内部滚动。凭据页的“查看 2FA 验证码”只在繁中区和国际区出现，紧接保存凭据按钮，所有按钮使用现有行间距。验证码工具标题同步更新，旧名称文案已移除；兼容性页仅删除重复标题和分割线，实际 Steam pfx 选项继续保留。

### 1.1.4 导航与选择控件

游戏、凭据管理及插件管理页共用区服跳转按钮，顶部不再保留重复入口。账户选择弹窗的取消按钮使用普通按钮样式。Proton、Steam pfx 和国服大区列表只列出实际选项，未设置时通过空选择显示为空；首次设置也不会代替用户选择首个 Proton。相关旧占位文案、错误占位状态和顶部专用样式已清理，读取失败仍回报日志。

### 待修改计划（1.1.4 用户反馈）

本轮按以下反馈修复，测试结果见下方 1.1.5 记录。

- [x] 国服大区选择框在未选中、显示为空时，尺寸应与选中大区时一致，尤其不能因无文字而压缩高度；同时检查 Proton 版本选择框及其他同类选择框的空值状态，保留未设置时显示为空的行为。
- [x] 简体中文、繁体中文顶部选项卡采用与日文、英文一致的宽度分配规则，避免窗口放大后选项卡挤在一起；检查普通窗口和最大化状态下的文字、间距与排列。

- [x] 插件更新故障：用户安装了三个来自国服 Daily Routines 主库的插件，检查更新失败。核查是否遗漏主库而只检查第三方仓库，并检查插件来源识别、版本比较及错误回报；原因尚未确认。
- [x] 插件管理右侧新增独立的第三方插件仓库管理面板，保持左侧现有窗口布局；计划支持添加、编辑、启用／停用、删除仓库及可访问性检查，与插件更新共用仓库配置。
- [x] 统一小字说明与相邻按钮的垂直间距：已发现插件管理中“选择更新插件”下方的启用状态说明，以及“关于”页面项目链接下方的独立维护声明、上游链接下方的第三方组件致谢过于贴近按钮。适当增大间距并与其他同类文案一致；全面检查其他页面、弹窗和四种语言下相同的按钮／说明组合，避免只修截图位置。

### 后续功能参考项目

2026-10-11 已重新定位工作目录为 `/home/qlan/Projects/xivlauncher-super/`。同级 `XIVTheCalamity/` 是用户克隆的后续新功能参考项目，已读取其 README 并确认源码存在；具体新功能范围待用户说明。当前仅记录参考对象，不引入代码或更换现有前端框架。

### 2026-10-11 功能范围与 CrossXIV 实验调研

- 当前只计划游戏个人设定档保存／备份功能；不计划新增 Proton 高级参数设置，也不因此引入完整多设定档切换体系。
- 已读取同级 `CrossXIV/`（提交 `3e82b50`），本轮仅分析 PC 国际服客户端连接国服及反方向的可能性，排除台服，未运行代理、登录、修改 DNS 或客户端。
- CrossXIV 在客户端到服务器方向将 Lobby `LoginEx` 转为 `ShandaLogin`，填入国服登录会话及版本信息；服务端响应原样转发。现有代码不支持反向转换。
- README 将再次登录崩溃归因于 `FFXIV.cfg` 中残留的国服 `WorldId`；源码没有自动清理此状态。设定档备份本身不能阻止同样状态再次写入，需要实验专用目录及启动前的定向状态恢复，并通过多次正常退出和异常退出后重登验证。
- 这只能针对配置状态引发的崩溃，不能保证不同客户端构建、资源、协议或插件注入兼容。反向使用国服客户端连接国际服仍为待验证假设；保留国际服界面不是协议兼容证据。


### 1.1.5 修复与仓库管理

空选择框保留与已选中状态相同的高度；四种语言统一顶部选项卡宽度分配，按钮与说明文字统一保留 16 px 间距。插件管理左侧保留现有内容，右侧增加独立仓库管理面板，支持添加、编辑、启用／停用、删除和连接检查。窄窗口将右侧面板排列到下方。

插件更新器现在读取国服 Dalamud 的 `AddPresetThirdRepos`（默认开启）及上游六个预置仓库，修复来自 AtmoOmen 预置仓库的插件被误判为来源未启用的问题。仍从原安装仓库更新，不以同名插件跨库替换；仓库保存保留其他 Dalamud 设置和 JSON 元数据，检查并发变更，保留备份，游戏运行中不允许写入。上游依据：[PluginRepository.cs](https://github.com/Dalamud-DailyRoutines/Dalamud/blob/master/Dalamud/Plugin/Internal/Types/PluginRepository.cs)、[PluginManager.cs](https://github.com/Dalamud-DailyRoutines/Dalamud/blob/master/Dalamud/Plugin/Internal/PluginManager.cs)。

### 待本版验收后：游戏设定档迁移

用户已确定迁移实际游戏设定目录的全部内容（包括其中截图），不迁移 `My Games` 上级目录。繁中区、国际区分别存放于自身区服目录下；国服目录在游戏客户端下，暂不迁移且启动时不应用 `UserPath`，仅在设计时保留接入能力。新选项卡置于“语言和区服”与“Dalamud 和插件”之间。本测试版不实施此功能，等待用户测试通过后再开始。CrossXIV 实验暂停，未经用户再次提出不操作。


1.1.5 验证：452 项 .NET 离线回归、58 项 Node.js 测试及 24 种语言／区服／窗口尺寸组合通过；真实仓库只读检查在隔离目录中确认 Daily Routines 2.2.3.2 → 2.2.3.3，另两个已安装插件无更新，检查无警告。未安装真实插件或改写现有用户配置。同仓库多 API 同名条目按当前 Dalamud API 筛选后再判断唯一性，同 API 冲突仍拒绝更新。

### 待修改计划（1.1.5 用户反馈）

- [x] “关于”页面：将“维护者：ImperatorVienna”移到“手动移动或重命名 AppImage 后……”整段说明下方、项目链接按钮组上方；与说明和按钮组均保持统一的垂直间距，避免文字紧贴按钮。四种界面语言同步调整。已完成界面调整，尚未重新打包。


### 第三方仓库同步调整

右侧“刷新仓库”之后新增“同步第三方仓库”。左侧删除整份 Dalamud 设置同步入口及“在不同游戏区服间同步”标题和分割线，保留插件文件与插件设置同步。后端仅复制 `ThirdRepoList` 中的条目及启用状态，目标的 `MainRepoUrl`、`AddPresetThirdRepos` 和其他字段始终保留；字段原本不存在时继续保持不存在，沿用目标区服默认值。保留备份、多目标回滚和并发修改检查。旧 `dalamud-settings` 请求被拒绝，四语言 README 与界面文案同步更新。

第三方仓库添加、编辑、启停、删除与同步均由后端游戏运行检查保护；同步在准备前和每个目标提交前再次检查。读取与连接检查为只读。模拟测试采用 cycleapple/DalamudPlugins-TW、AtmoOmen/DalamudPlugins、extrant/DalamudPlugins 的公开仓库地址，不写入用户配置或安装插件。

本轮验证：458 项 .NET 离线测试、59 项 Node.js 测试及四语言界面检查通过；覆盖全部六种跨区服同步方向。三个公开样本仓库分别读取到 11、14、7 个插件条目（仅为检查时结果），未安装插件。本轮仅修改源码，尚未重新打包。

### 1.1.6 同步弹窗

第三方仓库同步沿用来源下拉菜单与目标复选框布局。来源区服对应的目标始终禁用；切换来源会清空目标选择，防止误操作。

### 操作反馈归属

插件与仓库分别显示错误、进度及结果；所有失败提示使用红色。仓库编辑弹窗也就地显示保存错误。刷新插件启用状态或刷新仓库状态会清除各自的反馈；刷新仅重新读取对应数据，不写入另一侧的配置。

### 刷新职责

刷新按钮直接调用所属控制器的刷新函数，由该函数依次清理本区反馈、读取数据、更新界面和记录结果。插件与仓库分别刷新；首次进入页面显式加载两者；操作完成后显式重载相应数据。渲染函数不靠缓存失效触发业务刷新。委托点击处理只负责将动作派发给所属控制器，其他模块不监听刷新点击来执行附带操作。

界面语言、国际区客户端语言及其他单项设定更改后立即保存；登录凭据仍需统一点击保存。插件开关每次仅提交当前插件，保存失败恢复原状态，成功后下次启动游戏生效。游戏运行时后端禁止修改客户端语言、客户端路径和兼容环境；界面语言仍可切换。


### 2.0.0 发布前错误隔离复核

保存确认与展示刷新分开：accounts.save 只确认写入，前端随后刷新；preferences.update、compatibility.update 和仓库保存从已提交的数据生成确认，不依赖再次扫描。兼容环境更新后的列表刷新失败会明确提示“已保存，但刷新失败”。连接在写入期间超时／中断时，不自动重试，也不声称已回滚，提示重启后核对。

兼容环境发现通过独立只读任务异步回复，7 秒到期只拒绝该读取。一个扫描即使卡在无法取消的系统文件调用中，也不会继续启动更多扫描；原扫描返回后允许重新刷新。退出时不等待卡住的只读线程。IPC 客户端只对已隔离的 compatibility.read、areas.list 使用局部读取超时，其余请求仍保留 10 秒失效连接保护。Dalamud 网络超时和本地回退不变。插件与仓库的读取忙碌状态独立，后台修改任务仍有必要的互斥。

界面故障注入检查：生成 create-preview-fixture.cjs 的测试页并启动仓库 HTTP 服务后，运行 frontend/tests/failure-boundaries.cjs；UI_TEST_URL 可指定测试页地址。验证单次错误归属、两侧独立刷新、保存成功后刷新失败、首次设置与凭据读取隔离及日志保持折叠。

## 通用界面优化（2026-10-12）

首次设置分为界面语言、游戏区服、兼容环境三步。全新配置默认英文，国际区默认 NA / English，欧服商城偏好默认 English(UK)；已保存的选择不会被向导重置。国际区账户 CDKey 和欧服商城偏好语言在同一行。选择即时串行保存，下一步等待在途保存；失败停留并回报。路径草稿与对应错误保留，未保存输入不能越过完成检查。向导不接受 Esc 或点击外部关闭。完成配置后才开始自动更新检查。

更新提示等待初始化、首次设置和其他弹窗结束，安装提交期间防止重复点击和提前关闭；提交失败留在窗口中提示并允许重试。日志保持用户主动展开。页面级异常边界沿用现有实现，设置已保存而后续列表读取失败时分别回报。

`ui-state.js` 只管理普通 DOM 焦点恢复与兼容环境保存响应合并；不轮询输入设备，不实现方向导航。设置值变化尽量保留原控件，插件与仓库按各自数据／状态更新；切页仍使用现有入口。页面统一负责小窗口滚动，弹窗与日志继续保留自己的高度上限。

Electron 回归使用独立临时配置与合成 bridge，不访问实际账户、Steam 或游戏数据：

```sh
node frontend/tests/create-preview-fixture.cjs
FFXIV_ELECTRON=/absolute/path/to/electron node frontend/tests/ui-regressions.cjs
```

需要 Node 可解析 `playwright`；可通过 `NODE_PATH` 指向已安装目录。测试复制运行时入口到临时目录，不改动原 Electron 包。可设 `UI_TEST_ARTIFACTS=/absolute/output/path` 保存截图。覆盖保存顺序、失败恢复、默认值、多语言、弹窗关闭／重试、独立刷新和 1024×600 页面滚动。不接回手柄导航模块。Steam 虚拟键盘通过独立输入适配调用。


Steam 启动适配复用实际的兼容性工具注册状态，不增加第二个偏好开关。
`steam-integration.cjs` 统一识别兼容性入口和带 Steam 应用标识的非 Steam 快捷方式，管理临时全屏及键盘请求；`window-state.cjs` 在临时全屏期间停止写入桌面窗口状态。重复 Steam 入口复用同一模块。
`steam-input.js` 只监听用户点击文本框及 Tab 进入文本框，不接管按键、手柄导航或焦点。键盘请求只携带坐标及输入模式，经主进程校验后通过固定的 `steam -ifrunning steam://open/keyboard` 调用；Steam 未运行不启动客户端，失败在输入框旁提示。请求成功仅代表已发送，Steam 覆盖层是否实际显示仍须游戏模式实测。

Window UI sizing is owned by `window-ui.cjs`: scale up from 1320×860 device-independent content pixels, limited by both dimensions; small windows retain normal control sizes and page scrolling. Native verification bounds use the same zoom. Ctrl+Q / Alt+F4 in the launcher or embedded verification use the normal shutdown path. Esc remains a dialog action.
