<h3 align="center">這些問題，你也遇過嗎？</h3>
<p align="center">即將主動或被迫切換到 <strong>Linux</strong> 系統，卻擔心無法暢玩 FFXIV？</p>
<p align="center">遊玩<strong>多個區服</strong>，但每個啟動器都只支援單一區服，所以得安裝兩三個風格迥異的啟動器？</p>
<p align="center">某啟動器限制一些比較激進的外掛，而不設限的版本又<strong>不支援 Linux</strong> 平台？</p>
<p align="center">遊戲官方強制使用 <strong>2FA 驗證</strong>，每次登入都得手動輸入？</p>
<p align="center">啟動器內建的 Wine 效能表現十分糟糕，想要<strong>改用 Proton</strong> 卻困難重重？</p>
<p align="center">啟動器的 UI 相較於官方啟動器過於<strong>醜陋</strong>？</p>
<p align="center">某個外掛更新導致遊戲崩潰，每次都得<strong>停用全部外掛</strong>登入，才能再逐一啟用其他外掛？</p>
<p align="center">……</p>
<p align="center"><strong>以上問題，下載 FFXIV Super Launcher，一次為你解決！</strong></p>
<p align="center"><a href="https://github.com/ImperatorVienna/FFXIVSuperLauncher/releases"><strong>下載 FFXIV Super Launcher →</strong></a></p>

<p align="center">
  <img src="src/XIVLauncher.Linux/Resources/icon.png" width="128" height="128" alt="FFXIV Super Launcher 圖示">
</p>

<p align="center">
  <a href="README.zh-CN.md">简体中文</a> · <strong>繁體中文</strong> · <a href="README.ja.md">日本語</a> · <a href="README.md">English</a>
</p>

# FFXIV Super Launcher

## 2.1.2 更新

設定、日誌、Electron 與內建瀏覽器資料現在儲存在 AppImage 所在目錄下。舊資料不會自動搬移，既有設定中的 Proton 資料目錄仍沿用原路徑。升級前請閱讀 [2.1.2 更新說明](https://github.com/ImperatorVienna/FFXIVSuperLauncher/blob/main/docs/releases/2.1.2.zh-TW.md)，視需要遷移設定與整個 `compatdata` 資料夾。下一版本計畫提供 Windows 版；本次仍僅發行 Linux 版。

面向 **Linux** 平台，透過 **Proton** 執行遊戲的社群版 FINAL FANTASY XIV（以下簡稱 FFXIV）啟動器，支援**中國區、繁中區和國際區**遊戲用戶端。

本專案由 **[ImperatorVienna](https://github.com/ImperatorVienna)** 維護，基於 [XIVLauncher](https://github.com/goatcorp/FFXIVQuickLauncher)、[XIVLauncherCN](https://github.com/ottercorp/FFXIVQuickLauncher) 和 [XIVLauncherCN (Soil)](https://github.com/AtmoOmen/FFXIVQuickLauncher) 的原始碼與經驗繼續開發，並參考 [XIVTCLauncher](https://github.com/cycleapple/XIVTCLauncher) 的繁中區登入協定與功能設計。它是獨立維護的社群衍生專案，不是 Square Enix、Shengqu Games、USERJOY 的官方發行版本，也並非由上述上游專案維護或背書。原作者的著作權與授權聲明均予以保留。

> **支援平台：x86_64 Linux。** 請從 [Releases](https://github.com/ImperatorVienna/FFXIVSuperLauncher/releases) 取得發行檔案和版本說明。

內建瀏覽器由官方頁面與網頁驗證共用，獨立於各遊戲區服的登入模組。

## 功能

- **三個遊戲區服**：共用前端圖形介面和 Proton 啟動流程，分別管理用戶端目錄、帳號憑證、Dalamud 和外掛資料。
- **Proton 支援**：掃描系統與 Steam 收藏庫中的已安裝版本，支援手動指定路徑；已在基於 Arch Linux 的 CachyOS / proton-cachyos-slr 環境中進行實機測試。
- **帳號管理**：依遊戲區服新增、選擇、編輯和刪除帳號；使用本機明文檔案儲存憑證。
- **驗證碼**：繁中區與國際區支援手動輸入驗證碼和本機 TOTP 自動產生。國際區僅在帳號已啟用 OTP 時需要填寫。
- **遊戲更新**：檢查並安裝對應遊戲區服的用戶端修補程式，也可以只更新遊戲而不啟動。
- **Dalamud 和外掛**：可選擇啟用 Dalamud；無須進入遊戲即可管理外掛啟用狀態，並檢查 Dalamud 與外掛更新。
- **跨遊戲區服同步**：透過「同步第三方倉庫」「同步已安裝的外掛程式」和「同步外掛程式設定」分別同步所選類別；選擇來源及目標遊戲區服後執行。第三方倉庫同步僅替換倉庫清單及啟用狀態，始終保留目標主倉庫、預置倉庫開關和其他 Dalamud 設定；原資料保留備份。
- **Steam 整合**：支援國際區 Steam 版帳號登入；啟用「適配從 Steam 啟動」後註冊為 Steam compatibility tool，透過 Steam 啟動時自動全螢幕，並支援輸入欄位呼叫 Steam 虛擬鍵盤；桌面視窗尺寸獨立保留。
- **介面與公告**：Electron 圖形介面支援简体中文、繁體中文、日本語、English，即時切換，無須重新啟動；顯示對應遊戲區服的官方活動與公告，並提供遊戲官網、伺服器狀態等官方頁面入口。
- **第三方外掛倉庫**：在外掛管理右側新增、編輯、啟用/停用、刪除倉庫或檢查連線；中國區外掛更新同時識別啟用的預置倉庫。
- **中國區超域傳送**：支援提交傳送、查詢歷史和返回原區，可在完成後啟動遊戲。適配 DCTravelerX（[Dalamud-DailyRoutines/DCTraveler](https://github.com/Dalamud-DailyRoutines/DCTraveler)）遊戲內跨大區及授權恢復。
- **儲存空間清理**：在「關於」中清理下載快取、日誌和內建瀏覽器快取；啟動時自動回收可確認失效的工作階段記錄。

使用第三方啟動器、Dalamud 和外掛前，請閱讀遊戲相關服務條款並自行評估使用風險。Dalamud 預設關閉，本專案不提供帳號安全或外掛可用性保證。

## 特色

- **Linux 發行版**：目前提供 x86_64 Linux AppImage，下一版本計畫提供 Windows 版；不提供 macOS、ARM 或 32 位元 x86 版本。 採用 AppImage 散布，便於在包括不可變系統在內的 Linux 桌面上部署；具體相容情況以發行說明為準。
- **使用 Proton 相容性方案**：移除了 XIVLauncher 和 XIVLauncherCN 內附的 Wine + DXVK 方案，改為呼叫已安裝的 Proton。啟動器可自動尋找已安裝的 Proton，已使用 CachyOS 的 proton-cachyos-slr 進行實機驗證。
- **各區服統一管理**：同一個啟動器即可切換登入中國區、繁中區和國際區，各自儲存帳號、用戶端與外掛資料，各遊戲區服的 Dalamud 主程式依需要下載；執行套件中可包含共用或預先安裝的輔助元件。
- **遊戲外管理外掛**：無須進入遊戲即可調整外掛啟用狀態、選擇要更新的外掛，或在遊戲區服之間同步設定。
- **憑證管理**：依區服管理帳號登入憑證，支援本機明文儲存，以及本機 TOTP 產生。
- **國際區可沿用 Steam 版既有的 pfx**：便於從官方啟動器切換到 FFXIV Super Launcher 後，保留原 pfx 中已有的本機遊戲設定和角色設定檔（不涉及伺服器儲存的角色進度）。
- **繁中區 Dalamud 注入適配**：針對 Linux/Proton 環境調整注入流程，採用與國際區一致的 entrypoint 路徑，並跳過原繁中注入流程中的 ArgFixer；已透過進入角色及安裝、執行外掛的實機驗證。
- **中國區 Dalamud 分支選擇**：採用 Dalamud-DailyRoutines 分支及更新來源，不沿用 ottercorp 分支的外掛黑名單政策；這不代表所有外掛皆相容或不受遊戲服務條款約束。

## 安裝與首次使用

### 執行條件

- x86_64 Linux 圖形桌面。
- 已安裝的 Proton，以及該版本所需的 Steam Linux Runtime；具體相依項目以所選 Proton 的說明為準。
- 對應遊戲區服的完整用戶端。**本專案不提供從空資料夾下載完整用戶端的功能。**
- 正常運作的顯示驅動程式及所選 Proton 所需的 Vulkan 支援。

執行套件已內建 .NET 和 Electron（包含 Chromium 和 Node.js）執行元件，無須單獨安裝 .NET SDK、Electron 或瀏覽器。其他發行版的相依項目與驗證情況以具體發行說明為準；能在 CachyOS 執行，不代表所有發行版或沙箱環境皆已通過測試。

### AppImage

從 [Releases](https://github.com/ImperatorVienna/FFXIVSuperLauncher/releases) 下載 x86_64 AppImage，賦予執行權限後執行，可放在自行選擇的可寫入目錄。僅提供 AppImage 發行套件。

首次設定精靈預設使用 English，分步選擇啟動器介面語言、遊戲區服和 Steam / Proton 環境。國際區可同時設定用戶端語言、CDKey 版本及歐版帳號商城偏好語言；初始預設採用 NA、英語用戶端及英語（英國）商城偏好。單項選擇立即儲存，完成設定後才自動檢查啟動器更新。

之後在「語言和區服」中設定用戶端目錄。應選擇包含 `game` 子資料夾的遊戲安裝目錄；國際區還需要完整的 `boot` 子資料夾。

在「登入憑證管理」中透過「新增帳號」填寫憑證，點擊「儲存憑證」後，返回「遊戲」頁面，透過「選擇登入名稱」選擇帳號，再點擊「登入並啟動」。中國區新帳號只需填寫登入名稱，首次登入時掃碼，成功後自動儲存快捷憑證。

### 介面與國際區語言設定

在「語言和區服」頁面中：

- **啟動器介面語言**：選擇後立即儲存並切換所有頁面，無須重新啟動啟動器。
- **國際區用戶端語言**：僅在選擇國際區時顯示，可選日本語、English、Français、Deutsch。選擇後立即儲存，下次啟動遊戲時生效；遊戲執行時禁止修改，切換到其他遊戲區服不會清除這項選擇。
- **帳號 CDKey 版本**：依帳號實際授權選擇 JP、NA 或 EU，預設 NA。此設定用於選擇對應的官網、商城、Mog Station 和活動公告來源，不會改變帳號授權。
- **官網及商城偏好語言**：僅在 CDKey 版本選擇 EU 時顯示，可選 English(UK)、Français、Deutsch，並用於選擇相應語言的官網、商城及公告。上述網頁偏好變更後自動儲存，獨立於用戶端語言。

## Steam 與 Proton

### Steam 授權帳號

如果國際區帳號的 CDKey 購自 Steam，請勾選「我的 CDKey 購買自 Steam」，並在登入前確認 Steam 用戶端已登入且正在執行。

啟動器不會要求填寫或儲存 Steam 帳號密碼（僅透過 Steam 驗證 CDKey 所有權，登入帳號仍然是 Square Enix 帳號）。

### 從 Steam 啟動本啟動器

在「Steam 與 Proton 設定」中勾選「適配從 Steam 啟動」，即可將 **FFXIV Super Launcher** 註冊為 Steam compatibility tool。重新啟動 Steam 後，在 Steam 收藏庫的 FFXIV 遊戲內容中選擇此 compatibility tool。取消勾選即可移除註冊。啟用後，透過 Steam 啟動會自動全螢幕，點擊輸入欄位會嘗試呼叫 Steam 虛擬鍵盤；無法呼叫時提示使用鍵盤輸入，不會為了開啟鍵盤強制啟動 Steam。桌面視窗尺寸與最大化狀態獨立保留。

可使用 **Ctrl+Q** 或 **Alt+F4** 正常結束啟動器；Esc 用於關閉可取消的對話框。針對部分 Gamescope 工作階段僅傳入 `dxgi=n`、導致直接啟動遊戲時混用圖形 DLL 的情況，啟動器會依條件補齊 D3D11 載入設定，並保留明確的使用者設定。先前為此問題手動新增的 `env WINEDLLOVERRIDES="dxgi,d3d11=n" %command%` 參數可以移除；其他自行設定的啟動參數請保留。

這項功能提供 Steam 啟動入口，**不等於啟用 Steam 帳號驗證**。帳號驗證仍由「我的 CDKey 購買自 Steam」控制。

此功能使用固定啟動指令碼。手動移動或重新命名 AppImage 後，執行一次即可更新路徑。啟動器自動更新後會自動重新啟動並更新路徑，無須任何手動操作。

### 相容性環境 / pfx

預設使用「Steam 與 Proton 設定」中的「Proton 資料目錄（自動在此建立 pfx，建議獨立使用）」。國際區可勾選「國際區使用 Steam 既有 pfx」，沿用已安裝 Steam 版 FFXIV 的既有環境，以使用其中已有的本機遊戲設定。

未啟用該選項時，國際區與另外兩個遊戲區服沿用共用環境設定。選擇既有 Steam 環境不等於搬移或同步外掛設定。

## 更新方式

自動檢查更新預設開啟，首次設定完成後檢查穩定版，之後每次啟動也會檢查。可在「關於」的「新增至應用程式選單」右側關閉「自動檢查更新」；無論開關狀態如何，均可點擊「關於」版本號旁的「檢查更新」；不提供更新通道選擇。AppImage 更新須經使用者確認，完整下載後驗證清單簽章及 SHA256，再替換原檔案。更新檔案儲存在原目錄，檔名使用新版版本號，舊版保留為 `.previous`。Steam 和應用程式選單透過 `<AppImage directory>/appimage-launcher` 啟動。手動移動或重新命名 AppImage 後，請按兩下執行一次，以自動更新路徑；之後即可繼續從 Steam 或應用程式選單啟動。有多個副本時，以最後執行的檔案為準。請退出遊戲後更新。安裝完成後會直接透過新版 AppImage 的絕對路徑自動開啟介面，不依賴應用程式選單註冊。帳號和設定獨立儲存，背景檢查失敗不會阻止遊戲登入。

執行舊 AppImage 可回復啟動器版本，回復後請關閉自動檢查更新；舊版本是否支援此開關取決於該版本本身。多個 AppImage 共用入口時，以最後執行的檔案為準。

應用程式選單入口會在 AppImage 啟動時自動建立。「關於」中的「新增至應用程式選單」用於手動重建入口，通常無須額外操作。預設入口檔案為 `~/.local/share/applications/xivlauncher-super.desktop`，遵循 `XDG_DATA_HOME`。手動移動或重新命名 AppImage 後，直接執行一次即可更新記錄。

AppImage 啟動時先建立缺少的預設設定、寫入固定啟動指令碼和應用程式選單入口，再進入首次設定；自動檢查更新在首次設定完成後執行。入口也可在「關於」中手動重建。完成設定或新增入口後，會在背景重新整理可用的應用程式選單快取，不開啟終端機視窗；重新整理失敗不會阻止使用啟動器。選單入口使用持久儲存的圖示檔案絕對路徑，AppImage 卸載後圖示仍可讀取。

| 操作 | 行為 |
| --- | --- |
| 登入並啟動 | 帳號驗證成功後檢查遊戲更新；啟用 Dalamud 時再檢查其更新，隨後啟動遊戲 |
| 檢查並更新遊戲 | 僅更新目前遊戲區服用戶端；中國區、繁中區無須先登入，國際區需要帳號驗證，但不會啟動遊戲 |
| 檢查並更新 Dalamud | 手動檢查目前遊戲區服的 Dalamud |
| 檢查並更新外掛 | 依外掛記錄的來源倉庫檢查更新，供使用者選擇安裝 |

Dalamud 更新連線逾時時，可以選擇使用現有可用版本繼續啟動，或停用 Dalamud 後啟動。選擇停用會儲存該設定，之後需要手動重新啟用；現有版本缺少或不相容時，略過更新不保證能夠啟動。

以上兩個 Dalamud 和外掛更新按鈕位於「Dalamud 和外掛」頁面。修改外掛勾選狀態後立即儲存，下次啟動遊戲時生效。「重新整理外掛啟用狀態」與「重新整理倉庫狀態」分別重新讀取對應資料，並清除各自的操作提示。檢查連線的結果會顯示在對應倉庫下方，同時寫入日誌。

遊戲執行時禁止修改外掛、倉庫、用戶端語言、用戶端路徑和相容環境；退出遊戲後可繼續編輯。需要填寫多項內容的登入憑證仍需點擊「儲存憑證」。

跨遊戲區服同步外掛前，請確認目標遊戲區服的 Dalamud API 與外掛版本相容。同步會覆蓋所選目標的資料。目前不要將繁中區外掛 DLL 與其他遊戲區服的版本直接混用。

## 中國區遊戲內跨大區

2.1.0 為 **DCTravelerX**（[Dalamud-DailyRoutines/DCTraveler](https://github.com/Dalamud-DailyRoutines/DCTraveler)）提供 Proton 環境下的啟動器通訊支援。啟用中國區 Dalamud、安裝相容版本的 DCTravelerX，並透過本啟動器登入遊戲後，可在外掛中使用遊戲內跨大區功能。此適配不代表 ottercorp 的 DcTraveler 或其他外掛已獲相容保證。

遊戲啟動後前端介面可以關閉，背景程序會繼續維護遊戲工作階段、跨區授權及 Steam 狀態。不要手動結束該背景程序。再次開啟啟動器可恢復前端介面。

跨區登入授權過期時，背景程序先嘗試使用已儲存的快速登入憑證恢復。仍需掃碼時，開啟啟動器，或點擊「超域傳送」下方的「重新授權遊戲內跨區」，使用**目前遊戲帳號**完成掃碼，無須退出遊戲。恢復成功後回到遊戲重新點擊跨區；啟動器不會自動重新提交結果不明確的跨區訂單。取消、失敗或逾時會結束本次掃碼，背景掃碼最長等待五分鐘。

## 儲存空間清理

「關於」頁面提供三個清理入口：

| 按鈕 | 清理範圍與保留內容 |
| --- | --- |
| 清理下載快取 | 清理啟動器管理的更新檔快取、下載和安裝暫存檔；保留已安裝的遊戲、執行元件、外掛及備份，不接管 Dalamud 自行管理的內容 |
| 清理日誌 | 清理啟動器管理的日誌；目前日誌透過日誌模組協調清空，其他程序正在使用或無法安全處理的檔案略過，不清理 Dalamud 日誌 |
| 清理內建瀏覽器快取 | 清理目前 Electron 工作階段和網頁驗證工作階段的 HTTP、程式碼及著色器快取，保留 Cookie 和驗證狀態；不包含舊版獨立驗證碼瀏覽器的遺留目錄 |

遊戲執行時也可以清理。下載、安裝與下載快取清理使用共同的跨程序鎖，相關工作進行中會阻止清理下載快取；目前操作或網頁驗證進行中不能清理內建瀏覽器快取。結果會顯示釋放空間和略過項目；清理零個檔案表示目前沒有可清理的目標。

啟動時還會自動檢查工作階段記錄及已知暫存檔，回收能夠確認失效的記錄；活動工作階段和無法確認安全性的檔案會保留。這不是掃描整個磁碟的垃圾清理功能。

## 設定與憑證

預設資料目錄為：

```text
<AppImage directory>/
├── ffxiv_cn/     # 中國區資料
├── ffxiv_tc/     # 繁中區資料
├── ffxiv/        # 國際區資料
├── compatdata/  # 預設共用環境，內含 pfx
└── logs/        # 診斷日誌
```

程式資料儲存在 AppImage 所在目錄下；僅應用程式選單入口遵循 `XDG_DATA_HOME`。Steam 與 Proton 等共用設定儲存在啟動器資料目錄中，帳號與 Dalamud 資料依遊戲區服隔離。

憑證僅以明文儲存在各遊戲區服目錄下的 `credentials.json` 中，密碼、2FA 金鑰和快捷登入憑證均不加密。啟動器建立的憑證檔案僅允許目前使用者讀寫，請勿上傳或分享。

2FA 金鑰與六位數驗證碼不同：金鑰用於產生驗證碼，驗證碼僅在目前這次登入時輸入。請勿將六位數驗證碼填入金鑰欄位。

## 問題回報

請透過 [GitHub Issues](https://github.com/ImperatorVienna/FFXIVSuperLauncher/issues) 提交問題和功能建議。「關於」頁面預留了「加入 Discord」按鈕，目前停用；社群建立後將在後續版本啟用。

回報時請盡量提供：

- 啟動器版本、Linux 發行版、桌面環境和 Proton 版本。
- 目前遊戲區服，以及直接啟動還是透過 Steam compatibility tool 啟動。
- 重現步驟、預期行為、實際行為和錯誤發生時間。
- 必要的螢幕截圖或相關診斷日誌。

介面日誌使用英語記錄操作結果，可選取並複製。日誌浮層由使用者手動展開，項目增加時在內部捲動，不擠壓主介面；所有頁面的日誌欄上方均顯示完整診斷日誌路徑。提交前請檢查日誌和螢幕截圖，移除帳號資訊、登入票證、密碼、2FA 金鑰及 QR 碼等敏感內容。

## 從原始碼建置

如需從原始碼建置或參與開發，請參閱[開發說明](LINUX.zh-TW.md)。

## 關於頁面

「關於」顯示啟動器版本、維護者及上游致謝，並提供「GitHub 倉庫」「回報問題」「維護者個人頁面」「查看授權條款」「第三方聲明」「原始碼與來源記錄」和「美術素材來源」入口。「美術素材來源」彈窗列出啟動器圖示、GenRyu 和 Cinzel 三個來源入口。網頁連結透過內建瀏覽器開啟，並保留「在系統瀏覽器中開啟」按鈕；授權條款和第三方聲明可在啟動器內離線查看。

## 上游與致謝

本專案建立在以下專案及其貢獻者的工作之上：

- [goatcorp / FFXIVQuickLauncher](https://github.com/goatcorp/FFXIVQuickLauncher)：原始 XIVLauncher，以及國際區驗證、修補程式等實作。
- [ottercorp / FFXIVQuickLauncher](https://github.com/ottercorp/FFXIVQuickLauncher)：中國區適配與相關實作。
- [AtmoOmen / FFXIVQuickLauncher](https://github.com/AtmoOmen/FFXIVQuickLauncher)：中國區 Soil 分支，本專案建立之初的主要參考對象。
- [cycleapple / XIVTCLauncher](https://github.com/cycleapple/XIVTCLauncher)：繁中區登入協定與功能設計參考；不代表其整個倉庫均依本專案授權條款授權。
- [goatcorp / Dalamud](https://github.com/goatcorp/Dalamud)、[Dalamud-DailyRoutines / Dalamud](https://github.com/Dalamud-DailyRoutines/Dalamud)、[yanmucorp / Dalamud](https://github.com/yanmucorp/Dalamud)：對應遊戲區服的 Dalamud 及注入元件。
- .NET、Proton、Steamworks、Electron（內含 Chromium 和 Node.js）、xdelta3，以及專案使用的其他第三方元件及其貢獻者。

詳細的繼承關係、修改範圍和來源依據見 [SOURCES.txt](SOURCES.txt)，第三方聲明見 [THIRD-PARTY-NOTICES.txt](THIRD-PARTY-NOTICES.txt)。

## 授權條款

啟動器程式碼依 [GNU GPL 第 3 版](LICENSE) 散布。各第三方元件及素材保留自身的授權條款與聲明，不能將它們一概視為 GPL 授權。

目前圖示由維護者提供，來源頁面為 [PNGAAA](https://www.pngaaa.com/detail/6354760)，該頁面標註「非商業使用」。這不是本專案對圖示的 GPL 授權，也不代表已獨立確認原著作權方允許再次散布；相關記錄見[圖示來源說明](compliance/provenance/artwork.json)。

介面字型使用 [GenRyu](https://github.com/ButTaiwan/genryu-font)（ButTaiwan；v2 TC／PJP）和 [Cinzel](https://github.com/NDISCOVER/Cinzel)（The Cinzel Project Authors），均隨附 SIL Open Font License 1.1。字型授權條款、來源及檔案校驗值隨發行套件提供；詳細記錄請見[字型來源清單](prototypes/electron-ui/assets/font-sources.json)。在「關於」→「美術素材來源」中可開啟圖示及兩種字型的來源頁面。

散布材料及授權審查記錄見[發布清單](compliance/RELEASE-CHECKLIST.txt)。

如您認為本專案中的程式碼、相依元件或素材侵犯了您的權利，請透過 GitHub Issues 聯絡維護者，並提供相關內容的位置及權利依據。維護者將核實並溝通處理，必要時移除或替換相關內容。請勿在公開 Issue 中提交個人敏感資訊。
