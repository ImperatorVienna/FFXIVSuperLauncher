<p align="center">
  <img src="src/XIVLauncher.Linux/Resources/icon.png" width="128" height="128" alt="FFXIV Super Launcher 圖示">
</p>

<p align="center">
  <a href="README.zh-CN.md">简体中文</a> · <strong>繁體中文</strong> · <a href="README.ja.md">日本語</a> · <a href="README.md">English</a>
</p>

# FFXIV Super Launcher

適用於 **Linux** 平台、透過 **Proton** 執行遊戲的社群版 FINAL FANTASY XIV（以下簡稱 FFXIV）啟動器，支援**中國區、繁中區和國際區**遊戲用戶端。

本專案由 **[ImperatorVienna](https://github.com/ImperatorVienna)** 維護，基於 [XIVLauncher](https://github.com/goatcorp/FFXIVQuickLauncher)、[XIVLauncherCN](https://github.com/ottercorp/FFXIVQuickLauncher) 和 [XIVLauncherCN (Soil)](https://github.com/AtmoOmen/FFXIVQuickLauncher) 的原始碼與經驗繼續開發，並參考 [XIVTCLauncher](https://github.com/cycleapple/XIVTCLauncher) 的繁中區登入協定與功能設計。這是獨立維護的社群衍生專案，並非 Square Enix、Shengqu Games、USERJOY 的官方發行版本，也並非由上述上游專案維護或背書。原作者的版權與授權聲明均予以保留。

> **支援平台：x86_64 Linux。** 請從 [Releases](https://github.com/ImperatorVienna/FFXIVSuperLauncher/releases) 頁面取得發行檔案、版本說明與校驗值。AUR 套件尚待提供。

## 功能

- **三個遊戲區服**：共用前端圖形介面和 Proton 啟動流程，分別管理用戶端目錄、帳號憑證、Dalamud 和外掛資料。
- **Proton 支援**：掃描系統與 Steam 遊戲收藏庫中已安裝的版本，支援手動指定路徑；已在基於 Arch Linux 的 CachyOS / proton-cachyos-slr 環境中進行實機測試。
- **帳號管理**：依遊戲區服新增、選擇、編輯和刪除帳號；可使用 Linux 桌面密碼管理器或一般檔案儲存憑證。
- **驗證碼**：繁中區與國際區支援手動輸入驗證碼和本機 TOTP 自動產生。國際區僅在帳號已啟用 OTP 時需要填寫。
- **遊戲更新**：檢查並安裝對應遊戲區服的用戶端修補程式，也可以只更新遊戲而不啟動。
- **Dalamud 和外掛**：可選擇啟用 Dalamud；無須進入遊戲即可管理外掛啟用狀態，並檢查 Dalamud 與外掛更新。
- **跨遊戲區服同步**：透過「同步 Dalamud 設定」「同步已安裝的外掛程式」和「同步外掛程式設定」分別同步所選類別；選擇來源遊戲區服並勾選一個或多個目標後，點擊「同步至所選遊戲區服」，僅覆蓋勾選目標的對應內容。
- **Steam 整合**：支援國際區 Steam 版帳號登入；可手動註冊為 Steam compatibility tool，透過 Steam 開啟啟動器並記錄遊戲時數。
- **介面與公告**：支援简体中文、繁體中文、日本語、English；顯示對應遊戲區服的官方活動與公告，並提供官方頁面入口。
- **中國區超域傳送**：支援提交傳送、查詢歷史和返回原區，可在完成後啟動遊戲。

使用第三方啟動器、Dalamud 和外掛前，請閱讀遊戲相關服務條款並自行評估使用風險。Dalamud 預設關閉，本專案不保證帳號安全或外掛可用性。

## 特色

- **專為 Linux 平台打造**：僅維護 x86_64 Linux，不提供 Windows、macOS、ARM 或 32 位元 x86 版本。採用 AppImage 散布，便於在包括不可變系統在內的 Linux 桌面上部署；具體相容情況以發行說明為準。AUR 套件尚待提供。
- **使用 Proton 方案**：移除了 XIVLauncher 和 XIVLauncherCN 內附的 Wine + DXVK 方案，改為呼叫已安裝的 Proton。啟動器可自動尋找已安裝的 Proton，已使用 CachyOS 的 proton-cachyos-slr 進行實機驗證。
- **各遊戲區服統一管理**：同一個啟動器即可切換登入中國區、繁中區和國際區，各自儲存帳號、用戶端與外掛資料。各遊戲區服的 Dalamud 主程式依需要下載；執行套件中可能包含共用或預先安裝的輔助元件。
- **遊戲外管理外掛**：無須進入遊戲即可調整外掛啟用狀態、選擇更新外掛，或在遊戲區服之間同步設定。
- **憑證管理**：依遊戲區服管理帳號登入憑證，支援桌面密碼管理器或本機明文儲存，以及本機 TOTP 產生。
- **國際區可沿用 Steam 版既有的 pfx**：從官方啟動器切換至 FFXIV Super Launcher 後，可保留原 pfx 中的本機遊戲設定和角色設定檔（不涉及伺服器儲存的角色進度）。
- **繁中區 Dalamud 注入調整**：針對 Linux／Proton 環境調整注入流程，採用與國際區一致的 entrypoint 路徑，並跳過原繁中注入流程中的 ArgFixer；已透過進入角色及安裝、執行外掛的實機驗證。
- **中國區 Dalamud 分支選擇**：採用 Soil 使用的 Dalamud-DailyRoutines 分支及更新來源，不沿用 ottercorp 分支的外掛黑名單政策；這不代表所有外掛皆相容或不受遊戲服務條款約束。

## 安裝與首次使用

### 執行條件

- x86_64 Linux 圖形桌面。
- 已安裝的 Proton，以及該版本所需的 Steam Linux Runtime；具體相依項目以所選 Proton 的說明為準。
- 對應遊戲區服的完整用戶端。**本專案不提供從空資料夾下載完整用戶端的功能。**
- 正常運作的顯示驅動程式及所選 Proton 所需的 Vulkan 支援。
- 如選擇桌面密碼管理器，需要可用且已解鎖、相容於 Secret Service 的金鑰環，以及 `secret-tool`。

執行套件包含 .NET 執行環境，一般使用者無須另外安裝 .NET SDK。其他發行版的相依項目與驗證情況以具體發行說明為準；能在 CachyOS 執行，不代表所有發行版或沙箱環境皆已通過測試。

### AppImage

從 [Releases](https://github.com/ImperatorVienna/FFXIVSuperLauncher/releases) 頁面下載 x86_64 AppImage，賦予執行權限後執行。建議使用固定路徑和檔名，例如 `~/Applications/xivlauncher-super.AppImage`，以保留 Steam 註冊入口。

### 壓縮執行套件

如使用壓縮執行套件，請將其解壓縮至長期保留的目錄。解壓縮後的資料夾固定命名為 `xivlauncher-super`，進入該資料夾執行：

```bash
./xivlauncher-super
```

首次設定精靈會引導選擇介面語言、遊戲區服、Steam / Proton 目錄、憑證儲存方式及是否啟用 Dalamud。

之後在「語言和區服」中設定用戶端目錄。應選擇包含 `game` 子資料夾的遊戲安裝目錄；國際區還需要完整的 `boot` 子資料夾。

在「登入憑證管理」中透過「新增帳戶」填寫憑證，點擊「儲存」後，返回「遊戲」頁面，透過「選擇登入名稱」選擇帳號，再點擊「登入並啟動」。中國區新帳號只需填寫登入名稱，首次登入時掃描 QR 碼，成功後依所選儲存方式儲存快捷憑證。

**AUR 套件尚待提供。** 上線後將在此補充套件名稱與安裝說明。

## Steam 與 Proton

### Steam 授權帳號

如果國際區帳號的 CDKey 購自 Steam，請勾選「我的 CDKey 購買自 Steam」，並在登入前確認 Steam 用戶端已登入且正在執行。

啟動器不會要求填寫或儲存 Steam 帳號密碼（僅透過 Steam 驗證 CDKey 所有權，登入帳號仍然是 Square Enix 帳號）。

### 從 Steam 啟動本啟動器

在「相容性工具設定」中勾選「註冊為 Steam compatibility tool（可隨時停用）」，即可註冊 **FFXIV Super Launcher**。重新啟動 Steam 後，在 Steam 收藏庫中 FFXIV 的內容設定中選擇該 compatibility tool。取消勾選即可移除註冊。

這項功能提供 Steam 啟動入口，**不等於啟用 Steam 帳號驗證**。帳號驗證仍由「我的 CDKey 購買自 Steam」控制。

註冊後請保留啟動器的安裝路徑；更新時繼續使用相同路徑，可避免註冊入口指向舊目錄。

### compatibility environment / pfx

預設使用「相容性工具設定」中的「Proton 資料目錄（自動在此建立 pfx，建議單獨使用）」。國際區可勾選「國際區使用 Steam 現有的 pfx」，沿用已安裝 Steam 版 FFXIV 的既有環境，以使用其中的本機遊戲設定。

未啟用該選項時，國際區與另外兩個遊戲區服沿用共用環境設定。選擇 Steam 既有環境不等於搬移或同步外掛設定。

## 更新方式

**啟動器本身沒有自動更新功能。** 請關閉啟動器後，以新版 AppImage 替換原檔案並保留執行權限；使用壓縮執行套件時，依發行說明替換程式目錄並保持安裝路徑不變。帳號和設定儲存在獨立的資料目錄中。AUR 套件提供後，可透過相應套件管理工具更新。

| 操作 | 行為 |
| --- | --- |
| 登入並啟動 | 帳號驗證成功後檢查遊戲更新；啟用 Dalamud 時再檢查其更新，隨後啟動遊戲 |
| 檢查並更新遊戲 | 僅更新目前遊戲區服的用戶端；中國區、繁中區無須先登入，國際區需要帳號驗證，但不會啟動遊戲 |
| 檢查並更新 Dalamud | 手動檢查目前遊戲區服的 Dalamud |
| 檢查並更新外掛程式 | 依外掛記錄的來源儲存庫檢查更新，供使用者選擇安裝 |

Dalamud 更新連線逾時時，可以選擇使用現有可用版本繼續啟動，或停用 Dalamud 後啟動。停用會儲存至設定，之後需要手動重新啟用；現有版本缺失或不相容時，跳過更新不保證能啟動。

上述兩個 Dalamud 和外掛更新按鈕位於「Dalamud 與外掛程式」頁面。該頁面亦提供「重新讀取外掛程式啟用狀態」和「套用外掛程式啟用狀態」；修改勾選狀態後需點擊後者儲存，下次啟動遊戲時生效。

跨遊戲區服同步外掛前，請確認目標遊戲區服的 Dalamud API 與外掛版本是否相容。同步會覆蓋所選目標的資料。目前請勿將繁中區外掛 DLL 與其他遊戲區服的版本直接混用。

## 設定與憑證

預設資料目錄為：

```text
~/.local/share/xivlauncher-super/
├── ffxiv_cn/     # 中國區資料
├── ffxiv_tc/     # 繁中區資料
├── ffxiv/        # 國際區資料
├── compatdata/  # 預設共用環境，內含 pfx
└── logs/        # 診斷日誌
```

程式遵循 `XDG_DATA_HOME`。Steam 與 Proton 等共用設定儲存在啟動器資料目錄中，帳號與 Dalamud 資料依遊戲區服隔離。

「登入憑證管理」中的「使用桌面密碼管理器」預設勾選。取消勾選並點擊「儲存」後，後續儲存的一般檔案憑證位於對應遊戲區服目錄的 `credentials.json`，其中可能包含明文密碼、2FA 密鑰或快捷登入憑證。此設定對所有遊戲區服生效，既有憑證不會自動搬移。請勿上傳或分享這些檔案。

2FA 密鑰與六位數驗證碼不同：密鑰用於產生驗證碼，驗證碼僅在目前這次登入時輸入。請勿將六位數驗證碼填入密鑰欄位。

## 問題回報

請透過 [GitHub Issues](https://github.com/ImperatorVienna/FFXIVSuperLauncher/issues) 提交問題和功能建議。「關於」頁面預留了「加入 Discord」按鈕，目前停用；社群建立後將在後續版本啟用。

回報時請盡量提供：

- 啟動器版本、Linux 發行版、桌面環境和 Proton 版本。
- 目前遊戲區服，以及直接啟動或透過 Steam compatibility tool 啟動。
- 重現步驟、預期行為、實際行為和錯誤發生時間。
- 必要的螢幕截圖或相關診斷日誌。

介面日誌保持簡潔，完整診斷日誌路徑可在「關於」頁面查看。提交前請檢查日誌和截圖，移除帳號資訊、登入票證、密碼、2FA 密鑰及 QR 碼等敏感內容。

## 從原始碼建置

需要 .NET 10 SDK、C 編譯器、Python 3、Git 和 `7z`。建置及打包可能需要下載 NuGet 相依套件、固定版本的輔助元件和對應原始碼。

在儲存庫根目錄執行：

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

完整打包會同時產生授權條款、相依套件聲明、對應原始碼附件、`SOURCE-DELIVERY.json` 與校驗檔案。再次散布時，請保留相關聲明，並依適用授權一併提供對應原始碼。詳情請參閱[原始碼交付說明](compliance/SOURCE-DELIVERY.txt)。

## 關於頁面

「關於」顯示啟動器版本、維護者及上游致謝，並提供「GitHub 儲存庫」「回報問題」「維護者個人頁面」「檢視授權條款」「第三方聲明」「原始碼與來源紀錄」和「圖示來源」入口。網頁連結透過內建瀏覽器開啟，並保留「在系統瀏覽器中開啟」按鈕；授權條款和第三方聲明可在啟動器內離線查看。完整診斷日誌路徑僅在此分頁下顯示，位於底部日誌框上方。

## 上游與致謝

本專案建立在以下專案及其貢獻者的工作之上：

- [goatcorp / FFXIVQuickLauncher](https://github.com/goatcorp/FFXIVQuickLauncher)：原始 XIVLauncher，以及國際區驗證、修補程式等實作。
- [ottercorp / FFXIVQuickLauncher](https://github.com/ottercorp/FFXIVQuickLauncher)：中國區調整與相關實作。
- [AtmoOmen / FFXIVQuickLauncher](https://github.com/AtmoOmen/FFXIVQuickLauncher)：中國區 Soil 分支，本專案建立之初的主要參考對象。
- [cycleapple / XIVTCLauncher](https://github.com/cycleapple/XIVTCLauncher)：繁中區登入協定與功能設計參考；不代表其整個儲存庫皆依本專案的授權條款授權。
- [goatcorp / Dalamud](https://github.com/goatcorp/Dalamud)、[Dalamud-DailyRoutines / Dalamud](https://github.com/Dalamud-DailyRoutines/Dalamud)、[yanmucorp / Dalamud](https://github.com/yanmucorp/Dalamud)：對應遊戲區服的 Dalamud 及注入元件。
- Avalonia、.NET、Proton、Steamworks、Electron（內含 Chromium 和 Node.js）、xdelta3，以及本專案使用的其他第三方元件及其貢獻者。

詳細的繼承關係、修改範圍和來源依據請參閱 [SOURCES.txt](SOURCES.txt)，第三方聲明請參閱 [THIRD-PARTY-NOTICES.txt](THIRD-PARTY-NOTICES.txt)。

## 授權條款

啟動器程式碼依 [GNU GPL 第 3 版](LICENSE) 散布。各第三方元件及素材保留自身的授權與聲明，不能將其一概視為 GPL 授權。

目前圖示由維護者提供，來源頁面為 [PNGAAA](https://www.pngaaa.com/detail/6354760)，該頁面標示「非商業使用」。這不是本專案對圖示的 GPL 授權，也不代表已獨立確認原版權持有人允許再次散布；相關紀錄請參閱[圖示來源說明](compliance/provenance/artwork.json)。

散布材料及授權審查紀錄請參閱[發行清單](compliance/RELEASE-CHECKLIST.txt)。
