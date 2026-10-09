# FFXIV Super Launcher — Linux 使用与开发说明

[English](LINUX.en.md) | **简体中文** | [繁體中文](LINUX.zh-TW.md)

当前正式版本为 1.0.0，仅支持 x86_64 Linux，使用 Proton 运行游戏。用户安装、账户登录及功能说明以 [简体中文 README](README.zh-CN.md) 为准；其他语言入口位于 README 顶部。

## 安装和更新

- 正式发行格式为 AppImage，内置 .NET 和 Electron 运行组件。
- 直接运行 AppImage 后，启动器会先创建缺失的默认配置、刷新固定启动脚本和应用菜单入口，再检查更新。
- 固定入口默认为 `~/.local/share/xivlauncher-super/appimage-launcher`；应用菜单文件默认为 `~/.local/share/applications/xivlauncher-super.desktop`。两者遵循 `XDG_DATA_HOME`。
- 移动或重命名 AppImage 后，直接运行一次即可刷新路径记录。“关于”中的“添加到应用菜单”用于手动重建入口。
- 启动器自动检查稳定版；用户确认后下载并验证签名、大小及 SHA256，再安装新版并重新打开界面。旧包保留为 `.previous`。
- Release 保留 AppImage、`appimage-update.json` 和 `appimage-update.sig`。1.0.0 对应源码归档、依赖源码及校验文件位于 [release-materials/1.0.0](release-materials/1.0.0)。

## 功能与数据分区

中国区、繁中区和国际区的内部标识分别为 `ffxiv_cn`、`ffxiv_tc`、`ffxiv`。默认数据根目录为 `~/.local/share/xivlauncher-super/`，遵循 `XDG_DATA_HOME`。

Steam、Proton 和默认 pfx 配置共用；各游戏区服分别管理客户端、账户、Dalamud 和插件数据。国际区可以选择使用 Steam 版已有的 pfx。凭据保存方式和路径见 README 的“设置与凭据”。

三端登录成功后检查游戏更新，并在启用 Dalamud 时检查其更新，再启动游戏。单独点击“检查并更新游戏”不更新 Dalamud 或插件：中国区和繁中区无须先登录，国际区需要认证。Dalamud 和插件也可从“Dalamud 和插件”页面手动检查更新。

中国区支持扫码和快捷登录，以及超域传送；繁中区和国际区支持手动验证码与本地 TOTP。国际区仅在账户绑定 OTP 时需要提供验证码。

## 从源码构建

需要 .NET 10 SDK、C 编译器、Python 3、Git 和 `7z`。构建及打包可能需要下载 NuGet 依赖、固定版本的辅助组件和对应源码。

1.0.0 的完整启动器源码位于本仓库。原始源码归档、随包依赖的对应源码及校验文件保存在 [release-materials/1.0.0](release-materials/1.0.0)。Release 下载仅保留 AppImage 和签名更新所需的两个小文件。

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

`package-linux.sh` 生成自包含运行目录、`.tar.gz` 运行包及对应源码材料，**不会直接生成 AppImage**。生成 AppImage 还需要 `mksquashfs`（squashfs-tools）以及脚本指定 SHA256 的 AppImage type-2 runtime：

```bash
python3 scripts/package-appimage.py \
  artifacts/linux/xivlauncher-super \
  artifacts/linux/xivlauncher-super-1.0.0-x86_64.AppImage \
  --runtime /path/to/runtime-x86_64 \
  --public-key packaging/linux/appimage-public.pem
```

runtime 的固定提交和 SHA256 见 [AppImage 来源记录](compliance/notices/appimage/SOURCES.txt)。构建自己的运行包不需要项目的更新签名私钥；正式发布自动更新时，维护者还需使用 OpenSSL 和 `scripts/sign-appimage-update.py` 为更新清单签名。签名私钥不在仓库中。完整流程见 [AppImage 打包与更新说明](packaging/linux/APPIMAGE-UPDATES.md)。

完整打包会同时生成许可证、依赖声明、对应源码附件、`SOURCE-DELIVERY.json` 与校验文件。重新分发时，请保留相关声明，并按适用许可证一并提供对应源码。详情见[源码交付说明](compliance/SOURCE-DELIVERY.txt)。

完整运行目录位于 `artifacts/linux/xivlauncher-super/`。普通 `dotnet build` 不会准备全部随包辅助组件；需要验证完整运行流程时，请使用打包脚本生成的目录。脚本会准备固定版本的中国区启动辅助组件、繁中区 entrypoint 组件和 Electron 浏览器，并生成许可证及对应源码材料。

可以通过 `DOTNET=/path/to/dotnet` 指定 SDK，通过 `VERSION` 指定构建版本。

源码目录和构建脚本不依赖已经删除的四个参考项目目录。来源、许可证和依赖记录分别见 [SOURCES.txt](SOURCES.txt)、[THIRD-PARTY-NOTICES.txt](THIRD-PARTY-NOTICES.txt) 和 [源码交付说明](compliance/SOURCE-DELIVERY.txt)。保留这些材料及其对应关系。
