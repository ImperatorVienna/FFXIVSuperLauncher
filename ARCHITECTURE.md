# Linux / Proton 架构

## 区服与共用功能

`LinuxSettings.SelectedRegion` 是唯一的服务路由依据，与界面语言、Steam 和 Proton 设置分离。

| 层 | 共用实现 | 区服差异 |
| --- | --- | --- |
| 游戏运行 | `CompatibilityRunner`、`UnixDalamudRunner` | 输入游戏路径、注入组件路径、启动参数 |
| 登录 | `IRegionLogin`、`RegionLoginContext` | `ChinaLogin` 盛趣票据；`TraditionalChineseLogin` 台服验证码与会话协议；`GlobalLogin` Square Enix OAuth/Steam 票据 |
| 更新生命周期 | `RegionUpdateService` | `IRegionBackend` 选择 V3 或 ZiPatch 格式适配器 |
| 下载 | `UpdateDownload`、`DownloadScope` | URL、文件大小与校验数据；V3 的分块 MD5 流使用同一进度观察器 |
| Dalamud | `DalamudUpdateService` | `IDalamudSource` / `DalamudSources` 提供发行、资源和运行时版本 |
| Windows .NET | `Common.Runtime.DotNetRuntimeManager` | Soil 与国际区采用各自上游元数据，繁中区固定 9.0.11；均使用共用 NuGet 官方/镜像安装器 |
| 官方资讯 | `OfficialContent` 与统一界面 | 中国区 JSON 接口、繁中区官网分类 HTML、国际区 frontier JSON |
| 网页组件 | `Tools/webview` | 独立验证码会话；带工具栏的官方页面会话 |

`ffxiv_cn`、`ffxiv_tc`、`ffxiv` 各有独立配置、更新缓存、Dalamud 和插件目录，不回退到任何其他游戏区服。提供者按选择创建；台服 HTTP 客户端延迟创建。失败只上报当前操作，不自动改用其他区服来源。

游戏更新的 V3 差分文件与 ZiPatch 命令结构并不相同，因此保留格式适配器；不复制 UI、Proton、版本记录和下载显示逻辑。Dalamud 的下载、MD5 校验、暂存替换、资源准备与运行时安装是一份实现。已删除旧的独立 Soil 更新器、台服专用运行时安装器，以及普通 Wine 启动分支。

## 数据与故障处理

- 启动及每次更新后从本地文件重新读取版本；目录改变或文件缺失时清除过期显示。
- Dalamud 安装收据优先用于定位当前版本，不通过加载程序集执行代码来检查版本。
- 登录启动流程未启用 Dalamud 时不解析其来源、不创建更新器；独立的手动更新按钮可以更新组件但不启用注入。国服无 Dalamud 启动继续使用随包固定辅助组件。
- 下载进度通过异步调用上下文传递，支持并行文件；UI 使用操作代次丢弃旧事件。
- 官方资讯请求切换区服时取消；失败不阻断登录，也不覆盖新区域内容。
- Dalamud 主程序按官方 MD5 清单校验。Soil 资源按 SHA1 校验；台服资源沿用 xl_tw 的存在性策略（上游字体 URL 内容与清单哈希存在不一致），不将不一致的资源误称为通过哈希验证。
- 官方页面只提供 WebContentsView，无远端 Node/preload API；工具栏 IPC 只接受本地工具栏主帧。验证码使用独立 CDP 会话，不接收账号密码。网页资料均保存在私有临时会话目录。

## 边界

一个区域的协议/网络失败不会自动操作其他区域；共享运行时代码自身的缺陷仍可能影响所有区域，因此路由、取消、故障隔离和共同启动路径均需回归测试。这里的隔离不是三个独立操作系统进程。

## 国际区认证与更新顺序

`RegionLoginContext.UpdateAfterLogin` 将共享游戏更新操作安排在成功认证之后、生成最终启动会话之前。ChinaLogin 获得 OAuth 后调用它，再获取 SID；繁中区获取登录令牌后退出网页验证会话，调用它后进行版本握手和会话获取；GlobalLogin 完成 OAuth 后调用它进行 boot 更新，再请求并安装认证游戏补丁。主窗口在上述步骤完成后才更新 Dalamud、准备 Proton 并启动。

独立“检查并更新游戏”入口：中国区和繁中区直接调用 RegionUpdateService；国际区复用 AuthenticateRegionAsync；全部不调用 Dalamud 或创建 Proton runner。

`Patching/GamePatchFiles` 和 `ZiPatchInstaller` 共用校验、下载、应用和版本写入，原 ZiPatch 读取器整体移入 `Patching`；繁中区和国际区仅保留各自清单请求与解析。国际区使用原版停用补丁 URL token 生成后的直接下载方式，成功安装后保留原始登录 UID。

`Authentication/OneTimePassword` 和 `LoginCode` 共用验证码算法；`RegionCredentials` 统一管理三端的本地明文凭据，按游戏区服分别存储；`CredentialDraftStore` 处理凭据编辑与保存。Steam 登录使用原版 Linux Steam API 和票据算法，和 Steam 兼容性工具注册互不替代。`Global/UPSTREAM.md` 记录参考版本及改动。


## 本地验证码与离线插件更新

LocalOtpWindow 和 LoginCode 共用 OneTimePassword，工具输入仅保存在窗口内存中，不调用登录接口。用 RFC 6238 SHA1 测试向量验证，日志不输出密钥或验证码。

PluginUpdates 只读取 JSON，不加载插件 DLL。通过 InstalledFromUrl 的 OFFICIAL 标记或准确仓库 URL 选取清单；主库按游戏区服选源，第三方库必须存在并启用。不跨仓库回退；测试版保留安装通道并检查用户的测试版选择；版本匹配安装的 Dalamud API。下载采用 UpdateDownload，安装使用暂存目录、ZIP 路径验证、清单身份/版本校验、插件设置快照和共用插件锁，保留旧版本。OfflinePluginManager 启用/禁用实现保持不变。
