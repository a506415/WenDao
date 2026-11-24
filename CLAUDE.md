# 《邊緣問道》WenDao — RimWorld 修仙 mod

完整設計見 [docs/DESIGN.md](docs/DESIGN.md)。設計討論在 claude.ai 進行，這裡只負責實作；設計有變動時同步更新 `DESIGN.md`，實作與文件衝突時先問使用者，不要自行改設計。

## 硬性規則

- **遊戲版本**：RimWorld 1.6（本機 1.6.4871）。所有內容放在 `1.6/` 下；日後支援 1.7 時再加 `LoadFolders.xml`。
- **零 DLC**：不得依賴、不得引用任何 DLC 的 Def 或類別（Royalty 的 psycast／meditation、Biotech 的 genes、Ideology 的儀式等）。Core 中的通用類別（`AbilityDef`、`HediffDef`…）可用，但 JobDef、AbilityDef 等一律自行定義。
- **前置 mod**：只有 Harmony（`brrainz.harmony`）。
- **命名**：defName 用英文、一律加 `WD_` 前綴（例：`WD_SpiritRoot`）。C# 命名空間 `WenDao`。packageId `a506415.wendao`。
- **語言**：繁體中文為主（`Languages/ChineseTraditional/`），英文為次。Def 內的 label/description 寫英文，繁中放 `DefInjected` 與 `Keyed`。遊戲內不能出現紅字或缺翻譯的 key。
- **優先順序**：能用 XML Def 做的就不寫 C#；C# 只補原版沒有的邏輯；Harmony patch 是最後手段，且要能說明為什麼非 patch 不可。
- **不做**：LLM 整合、Combat Extended 相容（本期）。
- **美術**：先用原版貼圖或改色作佔位。

## 開發分期（依序，每期結束要能在遊戲裡完整玩一輪）

0. 骨架：About.xml、csproj、Harmony 初始化、繁中語言檔結構；遊戲可載入且無紅字。
1. MVP：靈根（含測靈盤）、靈氣格網、蒲團打坐、真元、練氣到金丹、突破與天劫、壽元折損、約 6 個法術、丹爐與 5 種丹、聚靈陣。
2. 世界：三類派系、敵方修士、raid points patch、玉簡功法與契合度、靈石經濟。
3. 深化：元嬰、化神、心魔、走火入魔、煉器與法寶、護體罡氣、悟道分支。
4. 終局：渡劫、飛升守城、傳承系統、秘境。
5. 打磨：妖獸與靈獸、自製貼圖、數值平衡、效能、Workshop 發布。

目前進度記在本檔最下方「進度」一節，每完成一個里程碑就更新。

## 目錄結構（目標）

```
About/About.xml, Preview.png
1.6/Assemblies/WenDao.dll      ← 建置輸出（不進 git）
1.6/Defs/  1.6/Patches/
Languages/ChineseTraditional/{DefInjected,Keyed}/
Languages/English/Keyed/
Textures/
Source/WenDao/WenDao.csproj    ← net472，Krafs.Rimworld.Ref 1.6.*、Lib.Harmony（ExcludeAssets=runtime）
docs/DESIGN.md
```

## 本機環境

- repo：`D:\WenDao`；`C:\Program Files (x86)\Steam\steamapps\common\RimWorld\Mods\WenDao` 是指向它的 junction，遊戲直接讀 repo。
- 遊戲：`C:\Program Files (x86)\Steam\steamapps\common\RimWorld\RimWorldWin64.exe`
- Harmony：Workshop 2009463077（已訂閱）。
- 日誌：`%USERPROFILE%\AppData\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\Player.log`（上一次為 `Player-prev.log`）。
- .NET SDK 11 已安裝；目標框架仍為 `net472`（靠 Krafs 參考組件，不需要 .NET Framework targeting pack）。
- 反編譯查原版寫法：ILSpy／`ilspycmd` 對 `RimWorldWin64_Data\Managed\Assembly-CSharp.dll`。

## 工作流程

- 建置：`dotnet build Source/WenDao/WenDao.csproj -c Release`，輸出到 `1.6/Assemblies/`。每次改 C# 都要先確認建置零錯誤再交給使用者測。
- 測試：使用者進遊戲測試並回報；之後讀 `Player.log` 搜尋 `WenDao`、`WD_`、`Exception`、`Could not`、`XML error`。Def 錯誤與缺翻譯都算 bug。
- 原版繁中翻譯本身就有約 276 個 translation errors（只開 Core 也一樣），不是本 mod 造成；判斷是否為我們的問題要看錯誤內容是否涉及 `WD_` 或本 mod 的檔案。
- 只需確認「載入無錯誤」時，可自行啟動 `RimWorldWin64.exe` 到主選單、讀 log 後關閉（先確認遊戲沒在執行）；行為測試仍交給使用者。
- 1.6 注意：hediff 帶出 Need 用 `HediffStage.enablesNeeds`（沒有 `causesNeed`）；`List<string>` 欄位要整份 `<li>` 翻譯需加 `[TranslationCanChangeCount]`；DebugAction 的 `allowedGameStates` 旗標須全部同時成立。
- 遊戲執行中 DLL 被鎖住，建置失敗時先請使用者關遊戲。
- git：每個可運作的步驟一個 commit，訊息用英文祈使句。`push` 前先確認建置通過。不要 commit `bin/`、`obj/`、`1.6/Assemblies/`。
- 數值（突破成功率、天劫強度等）集中放在 Def 或 `ModSettings`，方便 Phase 1 實測後調整。

## 進度

- [x] repo 建立、設計文件匯入、本機 junction（2026-11-24）
- [x] Phase 0 骨架：遊戲內驗證通過，log 出現繁中載入訊息、無紅字（2026-11-24）
- [ ] Phase 1 MVP
  - [x] 靈根：品階/屬性 Def、生成時擲骰、舊存檔補發、測靈盤與測靈工作、除錯指令（遊戲內驗證通過 2026-11-24）
  - [x] 靈氣格網與 overlay、蒲團與「修煉」工作、修為／境界（練氣→金丹，小階自動推進、瓶頸）、真元 Need、修煉速度 stat（遊戲內驗證通過 2026-11-24）
  - [ ] 突破與天劫、壽元折損：瓶頸累積感悟、蒲團閉關（可強行）、護法、成功率判定、反噬代價、金丹天劫、壽元與坐化——已建置、載入無誤，待遊戲內驗證
  - [ ] 原版老化疾病延後至壽元將盡（設計提到，未做）；避雷陣（未做）
  - [ ] 約 6 個法術（消耗真元）
  - [ ] 丹爐與 5 種丹、聚靈陣
  - [ ] 時間表「修煉」時段（需改 ThinkTree）
