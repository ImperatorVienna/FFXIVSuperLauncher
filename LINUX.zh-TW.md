# FFXIV Super Launcher — Linux 使用與開發說明

[English](LINUX.en.md) | [简体中文](LINUX.zh-CN.md) | **繁體中文**

目前正式版本為 1.0.0，僅支援 x86_64 Linux，使用 Proton 執行遊戲。使用者安裝、帳號登入及功能說明以 [簡體中文 README](README.zh-CN.md) 為準；其他語言入口位於 README 頂部。

## 安裝和更新

- 正式發行格式為 AppImage，內建 .NET 和 Electron 執行元件。
- 直接執行 AppImage 後，啟動器會先建立缺少的預設設定、重新整理固定啟動指令碼和應用程式選單入口，再檢查更新。
- 固定入口預設為 `~/.local/share/xivlauncher-super/appimage-launcher`；應用程式選單檔案預設為 `~/.local/share/applications/xivlauncher-super.desktop`。兩者遵循 `XDG_DATA_HOME`。
- 移動或重新命名 AppImage 後，直接執行一次即可重新整理路徑紀錄。「關於」中的「新增至應用程式選單」用於手動重建入口。
- 啟動器自動檢查穩定版；使用者確認後下載並驗證簽章、大小及 SHA256，再安裝新版並重新開啟介面。舊套件保留為 `.previous`。
- Release 保留 AppImage、`appimage-update.json` 和 `appimage-update.sig`。1.0.0 對應原始碼封存檔、相依元件原始碼及校驗檔案位於 [release-materials/1.0.0](release-materials/1.0.0)。

## 功能與資料分區

中國區、繁中區和國際區的內部識別碼分別為 `ffxiv_cn`、`ffxiv_tc`、`ffxiv`。預設資料根目錄為 `~/.local/share/xivlauncher-super/`，遵循 `XDG_DATA_HOME`。

Steam、Proton 和預設 pfx 設定共用；各遊戲區服分別管理用戶端、帳號、Dalamud 和外掛資料。國際區可以選擇使用 Steam 版既有的 pfx。憑證儲存方式和路徑見 README 的「設定與憑證」。

三端登入成功後檢查遊戲更新，並在啟用 Dalamud 時檢查其更新，再啟動遊戲。單獨點擊「檢查並更新遊戲」不更新 Dalamud 或外掛：中國區和繁中區無須先登入，國際區需要驗證。Dalamud 和外掛也可從「Dalamud 和外掛」頁面手動檢查更新。

中國區支援掃碼和快捷登入，以及超域傳送；繁中區和國際區支援手動驗證碼與本機 TOTP。國際區僅在帳號綁定 OTP 時需要提供驗證碼。

## 從原始碼建置

需要 .NET 10 SDK、C 編譯器、Python 3、Git 和 `7z`。建置及打包可能需要下載 NuGet 相依套件、固定版本的輔助元件和對應原始碼。

1.0.0 的完整啟動器原始碼位於本倉庫。原始碼封存檔、隨附相依元件的對應原始碼及校驗檔案保存在 [release-materials/1.0.0](release-materials/1.0.0)。Release 下載僅保留 AppImage 和簽章更新所需的兩個小檔案。

在倉庫根目錄執行：

```bash
# 建置 Linux 啟動器
dotnet build src/XIVLauncher.Linux/XIVLauncher.Linux.csproj -c Release -m:1

# 執行離線測試
dotnet test src/XIVLauncher.Linux.Tests/XIVLauncher.Linux.Tests.csproj \
  -c Release -m:1 --filter "Category!=Network"

# 檢查已記錄的授權材料
python3 scripts/check-compliance-materials.py

# 產生執行套件和對應原始碼附件
bash scripts/package-linux.sh
```

預設輸出至 `artifacts/linux/`。再次打包時需使用新的輸出目錄，例如：

```bash
bash scripts/package-linux.sh artifacts/local-build
```

`package-linux.sh` 產生自包含執行目錄、`.tar.gz` 執行套件及對應原始碼材料，**不會直接產生 AppImage**。產生 AppImage 還需要 `mksquashfs`（squashfs-tools）以及符合指令碼指定 SHA256 的 AppImage type-2 runtime：

```bash
python3 scripts/package-appimage.py \
  artifacts/linux/xivlauncher-super \
  artifacts/linux/xivlauncher-super-1.0.0-x86_64.AppImage \
  --runtime /path/to/runtime-x86_64 \
  --public-key packaging/linux/appimage-public.pem
```

runtime 的固定提交和 SHA256 見 [AppImage 來源記錄](compliance/notices/appimage/SOURCES.txt)。建置自己的執行套件不需要專案的更新簽章私鑰；正式發布自動更新時，維護者還需使用 OpenSSL 和 `scripts/sign-appimage-update.py` 為更新清單簽章。簽章私鑰不在倉庫中。完整流程見 [AppImage 打包與更新說明](packaging/linux/APPIMAGE-UPDATES.md)。

完整打包會同時產生授權條款、相依套件聲明、對應原始碼附件、`SOURCE-DELIVERY.json` 與校驗檔案。再次散布時，請保留相關聲明，並依適用授權一併提供對應原始碼。詳情見[原始碼交付說明](compliance/SOURCE-DELIVERY.txt)。

完整執行目錄位於 `artifacts/linux/xivlauncher-super/`。一般的 `dotnet build` 不會準備全部隨套件附帶的輔助元件；需要驗證完整執行流程時，請使用打包指令碼產生的目錄。指令碼會準備固定版本的中國區啟動輔助元件、繁中區 entrypoint 元件和 Electron 瀏覽器，並產生授權條款及對應原始碼資料。

可以透過 `DOTNET=/path/to/dotnet` 指定 SDK，透過 `VERSION` 指定建置版本。

原始碼目錄和建置指令碼不依賴已刪除的四個參考專案目錄。來源、授權條款和相依元件紀錄分別見 [SOURCES.txt](SOURCES.txt)、[THIRD-PARTY-NOTICES.txt](THIRD-PARTY-NOTICES.txt) 和 [原始碼交付說明](compliance/SOURCE-DELIVERY.txt)。保留這些資料及其對應關係。
