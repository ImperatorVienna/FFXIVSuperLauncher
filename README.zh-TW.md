<p align="center">
  <img src="src/XIVLauncher.Linux/Resources/icon.png" width="128" height="128" alt="FFXIV Super Launcher 圖示">
</p>

<p align="center">
  <a href="README.zh-CN.md">简体中文</a> · <strong>繁體中文</strong> · <a href="README.ja.md">日本語</a> · <a href="README.md">English</a>
</p>

# FFXIV Super Launcher

面向 **Linux** 平台，透過 **Proton** 執行遊戲的社群版 FINAL FANTASY XIV（以下簡稱 FFXIV）啟動器，支援**中國區、繁中區和國際區**遊戲用戶端。

本專案由 **[ImperatorVienna](https://github.com/ImperatorVienna)** 維護，基於 [XIVLauncher](https://github.com/goatcorp/FFXIVQuickLauncher)、[XIVLauncherCN](https://github.com/ottercorp/FFXIVQuickLauncher) 和 [XIVLauncherCN (Soil)](https://github.com/AtmoOmen/FFXIVQuickLauncher) 的原始碼與經驗繼續開發，並參考 [XIVTCLauncher](https://github.com/cycleapple/XIVTCLauncher) 的繁中區登入協定與功能設計。它是獨立維護的社群衍生專案，不是 Square Enix、Shengqu Games、USERJOY 的官方發行版本，也並非由上述上游專案維護或背書。原作者的著作權與授權聲明均予以保留。

> **支援平台：x86_64 Linux。** 請從 [Releases](https://github.com/ImperatorVienna/FFXIVSuperLauncher/releases) 取得發行檔案和版本說明；1.0.0 的校驗檔案位於 [release-materials/1.0.0](release-materials/1.0.0)。

內建瀏覽器由官方頁面與網頁驗證共用，獨立於各遊戲區服的登入模組。

## 功能

- **三個遊戲區服**：共用前端圖形介面和 Proton 啟動流程，分別管理用戶端目錄、帳號憑證、Dalamud 和外掛資料。
- **Proton 支援**：掃描系統與 Steam 收藏庫中的已安裝版本，支援手動指定路徑；已在基於 Arch Linux 的 CachyOS / proton-cachyos-slr 環境中進行實機測試。
- **帳號管理**：依遊戲區服新增、選擇、編輯和刪除帳號；可使用本機明文檔案儲存憑證。
- **驗證碼**：繁中區與國際區支援手動輸入驗證碼和本機 TOTP 自動產生。國際區僅在帳號已啟用 OTP 時需要填寫。
- **遊戲更新**：檢查並安裝對應遊戲區服的用戶端修補程式，也可以只更新遊戲而不啟動。
- **Dalamud 和外掛**：可選擇啟用 Dalamud；無須進入遊戲即可管理外掛啟用狀態，並檢查 Dalamud 與外掛更新。
- **跨遊戲區服同步**：透過「同步 Dalamud 設定」「同步已安裝的外掛程式」和「同步外掛程式設定」分別同步所選類別；選擇來源遊戲區服並勾選一個或多個目標後，點擊「同步至所選遊戲區服」；僅覆蓋勾選目標的對應內容。
- **Steam 整合**：支援國際區 Steam 版帳號登入；可手動註冊為 Steam compatibility tool，以透過 Steam 開啟啟動器並記錄遊戲時數。
- **介面與公告**：支援简体中文、繁體中文、日本語、English；顯示對應遊戲區服的官方活動與公告，並提供官方頁面入口。
- **中國區超域傳送**：支援提交傳送、查詢歷史和返回原區，可在完成後啟動遊戲。

使用第三方啟動器、Dalamud 和外掛前，請閱讀遊戲相關服務條款並自行評估使用風險。Dalamud 預設關閉，本專案不提供帳號安全或外掛可用性保證。

## 特色

- **專為 Linux 平台打造**：僅維護 x86_64 Linux，不提供 Windows、macOS、ARM 或 32 位元 x86 版本。採用 AppImage 散布，便於在包括不可變系統在內的 Linux 桌面上部署；具體相容情況以發行說明為準。
- **使用 Proton 相容性方案**：移除了 XIVLauncher 和 XIVLauncherCN 內附的 Wine + DXVK 方案，改為呼叫已安裝的 Proton。啟動器可自動尋找已安裝的 Proton，已使用 CachyOS 的 proton-cachyos-slr 進行實機驗證。
- **各區服統一管理**：同一個啟動器即可切換登入中國區、繁中區和國際區，各自儲存帳號、用戶端與外掛資料，各遊戲區服的 Dalamud 主程式依需要下載；執行套件中可包含共用或預先安裝的輔助元件。
- **遊戲外管理外掛**：無須進入遊戲即可調整外掛啟用狀態、選擇要更新的外掛，或在遊戲區服之間同步設定。
- **憑證管理**：依區服管理帳號登入憑證，支援本機明文儲存，以及本機 TOTP 產生。
- **國際區可沿用 Steam 版既有的 pfx**：便於從官方啟動器切換到 FFXIV Super Launcher 後，保留原 pfx 中已有的本機遊戲設定和角色設定檔（不涉及伺服器儲存的角色進度）。
- **繁中區 Dalamud 注入適配**：針對 Linux／Proton 環境調整注入流程，採用與國際區一致的 entrypoint 路徑，並跳過原繁中注入流程中的 ArgFixer；已透過進入角色及安裝、執行外掛的實機驗證。
- **中國區 Dalamud 分支選擇**：採用 Soil 使用的 Dalamud-DailyRoutines 分支及更新來源，不沿用 ottercorp 分支的外掛黑名單政策；這不代表所有外掛皆相容或不受遊戲服務條款約束。

## 安裝與首次使用

### 執行條件

- x86_64 Linux 圖形桌面。
- 已安裝的 Proton，以及該版本所需的 Steam Linux Runtime；具體相依項目以所選 Proton 的說明為準。
- 對應遊戲區服的完整用戶端。**本專案不提供從空資料夾下載完整用戶端的功能。**
- 正常運作的顯示驅動程式及所選 Proton 所需的 Vulkan 支援。

執行套件已內建 .NET 和 Electron（包含 Chromium 和 Node.js）執行元件，無須單獨安裝 .NET SDK、Electron 或瀏覽器。其他發行版的相依項目與驗證情況以具體發行說明為準；能在 CachyOS 執行，不代表所有發行版或沙箱環境皆已通過測試。

### AppImage

從 [Releases](https://github.com/ImperatorVienna/FFXIVSuperLauncher/releases) 下載 x86_64 AppImage，賦予執行權限後執行，可放在自行選擇的可寫入目錄。僅提供 AppImage 發行套件。

首次設定精靈會引導選擇介面語言、遊戲區服、Steam / Proton 目錄及是否啟用 Dalamud。

之後在「語言和區服」中設定用戶端目錄。應選擇包含 `game` 子資料夾的遊戲安裝目錄；國際區還需要完整的 `boot` 子資料夾。

在「登入憑證管理」中透過「新增帳號」填寫憑證，點擊「儲存」後，返回「遊戲」頁面，透過「選擇登入名稱」選擇帳號，再點擊「登入並啟動」。中國區新帳號只需填寫登入名稱，首次登入時掃碼，成功後自動儲存快捷憑證。

### 介面與國際區語言設定

在「語言和區服」頁面中：

- **啟動器介面語言**：選擇後點擊旁邊的「套用」，依提示重新啟動啟動器，以套用到所有頁面。
- **國際區用戶端語言**：僅在選擇國際區時顯示，可選日本語、English、Français、Deutsch。點擊旁邊的「套用」後，下次啟動遊戲時生效；切換到其他遊戲區服不會清除這項選擇。
- **帳號 CDKey 版本**：依帳號實際授權選擇 JP、NA 或 EU，預設 NA。此設定用於選擇對應的官網、商城、Mog Station 和活動公告來源，不會改變帳號授權。
- **官網及商城偏好語言**：僅在 CDKey 版本選擇 EU 時顯示，可選 English(UK)、Français、Deutsch，並用於選擇相應語言的官網、商城及公告。上述網頁偏好變更後自動儲存，獨立於用戶端語言。

## Steam 與 Proton

### Steam 授權帳號

如果國際區帳號的 CDKey 購自 Steam，請勾選「我的 CDKey 購買自 Steam」，並在登入前確認 Steam 用戶端已登入且正在執行。

啟動器不會要求填寫或儲存 Steam 帳號密碼（僅透過 Steam 驗證 CDKey 所有權，登入帳號仍然是 Square Enix 帳號）。

### 從 Steam 啟動本啟動器

在「相容性工具設定」中勾選「註冊為 Steam compatibility tool（可隨時停用）」，即可將 **FFXIV Super Launcher** 註冊。重新啟動 Steam 後，在 Steam 收藏庫中的 FFXIV 遊戲內容設定中選擇該 compatibility tool。取消勾選即可移除註冊。

這項功能提供 Steam 啟動入口，**不等於啟用 Steam 帳號驗證**。帳號驗證仍由「我的 CDKey 購買自 Steam」控制。

此功能使用固定啟動指令碼。手動移動或重新命名 AppImage 後，執行一次即可更新路徑。啟動器自動更新後會自動重新啟動並更新路徑，無須任何手動操作。

### compatibility environment / pfx

預設使用「相容性工具設定」中的「Proton 資料目錄（自動在此建立 pfx，建議獨立使用）」。國際區可勾選「國際區使用 Steam 既有 pfx」，沿用已安裝 Steam 版 FFXIV 的既有環境，以使用其中已有的本機遊戲設定。

未啟用該選項時，國際區與另外兩個遊戲區服沿用共用環境設定。選擇既有 Steam 環境不等於搬移或同步外掛設定。

## 更新方式

啟動器每次開啟時在背景檢查穩定版，也可點擊「關於」版本號旁的「檢查更新」；不提供更新通道選擇。AppImage 更新須經使用者確認，完整下載後驗證清單簽章及 SHA256，再替換原檔案。更新檔案儲存在原目錄，檔名使用新版版本號，舊版保留為 `.previous`。Steam 和應用程式選單透過 `~/.local/share/xivlauncher-super/appimage-launcher` 啟動。手動移動或重新命名 AppImage 後，請按兩下執行一次，以自動更新路徑；之後即可繼續從 Steam 或應用程式選單啟動。有多個副本時，以最後執行的檔案為準。請退出遊戲後更新。安裝完成後會直接透過新版 AppImage 的絕對路徑自動開啟介面，不依賴應用程式選單註冊。帳號和設定獨立儲存，背景檢查失敗不會阻止遊戲登入。

應用程式選單入口會在 AppImage 啟動時自動建立。「關於」中的「新增至應用程式選單」用於手動重建入口，通常無須額外操作。預設入口檔案為 `~/.local/share/applications/xivlauncher-super.desktop`，遵循 `XDG_DATA_HOME`。手動移動或重新命名 AppImage 後，直接執行一次即可更新記錄。

AppImage 啟動時會先依序建立缺少的預設設定、寫入固定啟動指令碼和應用程式選單入口，然後才檢查更新，也可在「關於」中手動重建。完成設定或新增入口後，會在背景重新整理可用的應用程式選單快取，不開啟終端機視窗；重新整理失敗不會阻止使用啟動器。選單入口使用持久儲存的圖示檔案絕對路徑，AppImage 卸載後圖示仍可讀取。

| 操作 | 行為 |
| --- | --- |
| 登入並啟動 | 帳號驗證成功後檢查遊戲更新；啟用 Dalamud 時再檢查其更新，隨後啟動遊戲 |
| 檢查並更新遊戲 | 僅更新目前遊戲區服用戶端；中國區、繁中區無須先登入，國際區需要帳號驗證，但不會啟動遊戲 |
| 檢查並更新 Dalamud | 手動檢查目前遊戲區服的 Dalamud |
| 檢查並更新外掛 | 依外掛記錄的來源倉庫檢查更新，供使用者選擇安裝 |

Dalamud 更新連線逾時時，可以選擇使用現有可用版本繼續啟動，或停用 Dalamud 後啟動。選擇停用會儲存該設定，之後需要手動重新啟用；現有版本缺少或不相容時，略過更新不保證能夠啟動。

以上兩個 Dalamud 和外掛更新按鈕位於「Dalamud 和外掛」頁面。該頁面還提供「重新整理外掛啟用狀態」和「套用外掛啟用狀態」；修改勾選狀態後需點擊後者儲存，下次啟動遊戲時生效。

跨遊戲區服同步外掛前，請確認目標遊戲區服的 Dalamud API 與外掛版本相容。同步會覆蓋所選目標的資料。目前不要將繁中區外掛 DLL 與其他遊戲區服的版本直接混用。

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

憑證僅以明文儲存在各遊戲區服目錄下的 `credentials.json` 中，密碼、2FA 金鑰和快捷登入憑證均不加密。啟動器建立的憑證檔案僅允許目前使用者讀寫，請勿上傳或分享。

2FA 金鑰與六位數驗證碼不同：金鑰用於產生驗證碼，驗證碼僅在目前這次登入時輸入。請勿將六位數驗證碼填入金鑰欄位。

## 問題回報

請透過 [GitHub Issues](https://github.com/ImperatorVienna/FFXIVSuperLauncher/issues) 提交問題和功能建議。「關於」頁面預留了「加入 Discord」按鈕，目前停用；社群建立後將在後續版本啟用。

回報時請盡量提供：

- 啟動器版本、Linux 發行版、桌面環境和 Proton 版本。
- 目前遊戲區服，以及直接啟動還是透過 Steam compatibility tool 啟動。
- 重現步驟、預期行為、實際行為和錯誤發生時間。
- 必要的螢幕截圖或相關診斷日誌。

介面日誌保持簡潔，完整診斷日誌路徑可在「關於」頁面查看。提交前請檢查日誌和螢幕截圖，移除帳號資訊、登入票證、密碼、2FA 金鑰及 QR 碼等敏感內容。

## 從原始碼建置

如需從原始碼建置或參與開發，請參閱[開發說明](LINUX.zh-TW.md)。

## 關於頁面

「關於」顯示啟動器版本、維護者及上游致謝，並提供「GitHub 倉庫」「回報問題」「維護者個人頁面」「查看授權條款」「第三方聲明」「原始碼與來源記錄」和「圖示來源」入口。網頁連結透過內建瀏覽器開啟，並保留「在系統瀏覽器中開啟」按鈕；授權條款和第三方聲明可在啟動器內離線查看。完整診斷日誌路徑僅在此分頁下顯示，位於底部日誌框上方。

## 上游與致謝

本專案建立在以下專案及其貢獻者的工作之上：

- [goatcorp / FFXIVQuickLauncher](https://github.com/goatcorp/FFXIVQuickLauncher)：原始 XIVLauncher，以及國際區驗證、修補程式等實作。
- [ottercorp / FFXIVQuickLauncher](https://github.com/ottercorp/FFXIVQuickLauncher)：中國區適配與相關實作。
- [AtmoOmen / FFXIVQuickLauncher](https://github.com/AtmoOmen/FFXIVQuickLauncher)：中國區 Soil 分支，本專案建立之初的主要參考對象。
- [cycleapple / XIVTCLauncher](https://github.com/cycleapple/XIVTCLauncher)：繁中區登入協定與功能設計參考；不代表其整個倉庫均依本專案授權條款授權。
- [goatcorp / Dalamud](https://github.com/goatcorp/Dalamud)、[Dalamud-DailyRoutines / Dalamud](https://github.com/Dalamud-DailyRoutines/Dalamud)、[yanmucorp / Dalamud](https://github.com/yanmucorp/Dalamud)：對應遊戲區服的 Dalamud 及注入元件。
- Avalonia、.NET、Proton、Steamworks、Electron（內含 Chromium 和 Node.js）、xdelta3，以及專案使用的其他第三方元件及其貢獻者。

詳細的繼承關係、修改範圍和來源依據見 [SOURCES.txt](SOURCES.txt)，第三方聲明見 [THIRD-PARTY-NOTICES.txt](THIRD-PARTY-NOTICES.txt)。

## 授權條款

啟動器程式碼依 [GNU GPL 第 3 版](LICENSE) 散布。各第三方元件及素材保留自身的授權條款與聲明，不能將它們一概視為 GPL 授權。

目前圖示由維護者提供，來源頁面為 [PNGAAA](https://www.pngaaa.com/detail/6354760)，該頁面標註「非商業使用」。這不是本專案對圖示的 GPL 授權，也不代表已獨立確認原著作權方允許再次散布；相關記錄見[圖示來源說明](compliance/provenance/artwork.json)。

散布材料及授權審查記錄見[發布清單](compliance/RELEASE-CHECKLIST.txt)。

如您認為本專案中的程式碼、相依元件或素材侵犯了您的權利，請透過 GitHub Issues 聯絡維護者，並提供相關內容的位置及權利依據。維護者將核實並溝通處理，必要時移除或替換相關內容。請勿在公開 Issue 中提交個人敏感資訊。
