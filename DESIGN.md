# STS2 卡包工具库（PackmasterLib）+ 演示角色 mod 设计文档

目标版本：Slay the Spire 2 **v0.111.0**（net9.0 / Godot 4.5.1 Mono / Harmony）
源码参考：`D:\dev\mod\SlayTheSpire2`（GDRE 反编译导出，与 v0.111.0 对应）
游戏安装：`D:\SteamLibrary\steamapps\common\Slay the Spire 2`
输出目录：`D:\dev\mod\Packmaster\`（本目录）

## 0. 需求（按用户口径修订）

1. **PackmasterLib**：免依赖（不依赖 baselib / ritsulib）的工具 mod，本身无内容，作为其他角色 mod 的依赖：
   - 让角色 mod 能够"创建一个使用卡包的角色"；
   - 卡包的卡在**图鉴中显示在归属角色下**（即出现在该角色自己的卡池 Tab 中），并**支持按卡包排序、按卡包搜索**；
   - **角色选择页面右侧**添加可展开面板：选择卡包数量（3-10）、每个槽位的类型（随机 / 三选一 / 无 / 固定选某个卡包）、**全包模式**开关；
   - 设置页（设置 → 游戏设置）添加可折叠的"卡包大师"组：1 代 mod 设置（允许多个"无"）+ 开发选项（解锁全部卡包、跳过选包）；不重复选人页的槽位配置；
   - 卡包解锁接口 `PackCharacterRegistration.IsPackUnlocked`（默认全部解锁）；
   - 开局时执行卡包选择：固定/随机槽自动解析，"三选一"槽弹出**开局选卡包界面**（复用游戏选卡网格 UI），界面显示 mod 作者提供的**卡包评分/摘要**；
   - 运行存档持久化所选卡包（存档/读档不丢失）；
   - 卡框、卡图、遗物/药水是否与原版共享——**均由角色 mod 作者自行配置**（库不干预）。
2. **VanillaPacks（演示角色 mod）**：一个使用 PackmasterLib 的角色 mod，把原版卡分成若干卡包，并**测试卡包选择逻辑的准确性**（控制台命令 + 日志断言）。

## 1. 关键源码事实（研究结论）

| 主题 | 事实 |
|---|---|
| mod 加载 | exe 旁 `mods/<dir>/`，manifest 为任意名 `.json`（字段 id/name/version/has_dll/has_pck/dependencies/min_game_version/affects_gameplay）；加载 `{id}.dll`，反射调用 `[ModInitializer("方法名")]` 静态方法；无 attribute 则自动 `Harmony.PatchAll`。mod 初始化发生在 `ModelDb.Init()` **之前**（`OneTimeInitialization.ExecuteVeryEarly`） |
| 内容注册 | `ModelDb.Init()` 反射实例化所有 mod 程序集中的 `AbstractModel` 子类（含 CardModel/CardPoolModel/CharacterModel/ModifierModel/RelicModel/PotionModel）。卡要进某卡池需 `ModHelper.AddModelToPool<TPool,TCard>()`（必须在游戏初始化完成前） |
| 卡池 | `CardPoolModel` 子类：抽象成员 `Title/EnergyColorName/CardFrameMaterialPath/DeckEntryCardColor/IsColorless/GenerateAllCards()`。卡牌归属=被哪个池的 `GenerateAllCards()` 列出（`CardModel.Pool` 反查）。图鉴 8 个池 Tab **硬编码**，mod 池默认不可见 |
| 角色 | `CharacterModel` 抽象成员：StartingHp/StartingGold/CardPool/RelicPool/PotionPool/StartingDeck/StartingRelics/Gender/NameColor/UnlocksAfterRunAs/AttackAnimDelay/CastAnimDelay/GetArchitectAttackVfx。`ModelDb.AllCharacters` 硬编码 5 角色；`UnlockState.Characters = AllCharacters − 未解锁纪元角色` → **进 AllCharacters 即自动解锁** |
| 选人页 | `NCharacterSelectScreen._Ready()` 构建按钮（遍历 AllCharacters）；`SelectCharacter(button, characterModel)` 是公开方法（打开页面与点击按钮都会调用）；Embark → `NGame.StartNewSingleplayerRun` → `RunState.CreateForNewRun(players, acts, modifiers, ...)` |
| 开局 modifiers | `RunState.CreateForNewRun` 接收 `IReadOnlyList<ModifierModel>`；`ModifierModel` 子类经反射自动注册；`[SavedProperty]` 属性（int/bool/string/ModelId/enum/int[]）自动持久化进运行存档 `SerializableRun.Modifiers`（跨存档读档）；生命周期 `OnRunCreated`(新开局) / `OnRunLoaded`(读档)；`GenerateNeowOption(EventModel)` 返回 `Func<Task>` 时，Neow（先古）事件会把该选项作为**唯一选项**展示，选中后 await 该任务（Draft/SealedDeck modifier 即此模式） |
| 卡牌奖励 | `CardFactory.CreateForReward` → `CardCreationOptions.GetPossibleCards(player)` → `CardPools.SelectMany(p => p.GetUnlockedCards(...))`；稀有度按 `CardRarityOddsType` 滚动；RNG=`player.PlayerRng.Rewards`。商店/事件/遗物同样走 CardCreationOptions |
| 三选一 UI | `CardSelectCmd.FromSimpleGrid(PlayerChoiceContext, IReadOnlyList<CardModel>, Player, CardSelectorPrefs)`：await 返回所选卡；`prefs.MinSelect=MaxSelect=1` 点卡即确认；`BlockingPlayerChoiceContext` 原生支持多人同步 |
| 图鉴 | `NCardLibrary`：过滤器=`_filter`（含 `_poolFilters` 字典按 `c.Pool is X` 判定），排序=`_sortingPriority` List\<SortingOrders\> + `NCardGrid.SortingAlgorithms` 字典（SortingOrders→比较函数）；搜索=UpdateFilter 内联 TextFilter（标题+描述 contains）。场景节点：`Sidebar/MarginContainer/TopVBox/PoolFilters`(GridContainer)、`%CardTypeSorter` 等（`library_sort_button.tscn` 直接绑定具体类 NCardViewSortButton，`library_pool_toggle.tscn` 绑定 NCardPoolFilter，可运行时克隆注入） |
| 排序注入 | `NCardGrid.SortingAlgorithms` 属性 getter 惰性构建缓存字典 → Harmony postfix 向字典追加 `(SortingOrders)100/101 → 卡包比较函数` 即可（枚举值强转，游戏 switch 不受影响） |
| 控制台 | `AbstractConsoleCmd` 子类自动扫描 mod 程序集（`ReflectionHelper.GetSubtypesInMods`）→ 测试命令可直接注册 |
| 本地化 | 无 pck 时无法放 `res://{id}/localization/...` → **方案：Harmony postfix 注入 LocManager 的表字典**（详见 §4.6）。有 pck 时走标准 `res://{id}/localization/{lang}/{table}.json` 合并（LocManager.cs:468） |
| 资产 | 角色资产路径大多**非虚**（VisualsPath/EnergyCounterPath/RestSiteAnimPath/MerchantAnimPath/TrailPath/CharacterSelectBg 等，按 `Id.Entry` 拼路径）→ 演示 mod 无美术，用 **RedirectedCharacterModel**：Harmony postfix 拦截这些 getter 把路径重定向到所选原版角色（如 ironclad）；虚属性（IconPath/CharacterSelectIconPath/MapMarkerPath/CharacterSelectIcon...）直接 override。卡图：卡牌副本 override `PortraitPath` 指向原版图集路径 |
| 运行时 | net9.0（`sts2.runtimeconfig.json`），引用 `data_sts2_windows_x86_64/{sts2.dll,0Harmony.dll,GodotSharp.dll}` |

## 2. 总体架构

```
D:\dev\mod\Packmaster\
├── DESIGN.md                          ← 本文档
├── Directory.Build.props              ← 公共构建属性（游戏路径、SDK 路径）
├── build-and-install.ps1              ← 一键编译+部署两个 mod 到游戏 mods 目录
├── PackmasterLib\                     ← 工具库 mod（mod id: PackmasterLib）
│   ├── PackmasterLib.json
│   ├── PackmasterLib.csproj
│   └── src\
│       ├── PackmasterLibEntry.cs      ← [ModInitializer] 注册 Harmony、公开 API
│       ├── Api\                        ← 对角色 mod 暴露的公共 API
│       │   ├── PackmasterApi.cs       ← RegisterCharacter / 解析 / 查询
│       │   ├── PackDefinition.cs      ← 卡包定义（id/名称key/描述key/作者/评分摘要/卡表/预览卡类型）
│       │   ├── PackCharacterRegistration.cs
│       │   └── PackSlotType.cs        ← Random/ChoiceOf3/None/Fixed
│       ├── Core\
│       │   ├── PackRegistry.cs        ← 注册表（字符→卡包）、懒解析 ModelDb 实例
│       │   ├── PackRunModifier.cs     ← ModifierModel：持久化配置与所选卡包、开局解析、Neow 三选一
│       │   ├── PackResolver.cs        ← 槽位解析算法（固定/随机/三选一→等待玩家）+ 校验
│       │   └── PackState.cs           ← 运行期状态（每玩家已选卡包、待选槽数）
│       ├── Patches\
│       │   ├── RunStartPatch.cs       ← RunState.CreateForNewRun prefix：给卡包角色附加 PackRunModifier
│       │   ├── CardPoolPatch.cs       ← CardCreationOptions.GetPossibleCards prefix：按所选卡包过滤
│       │   ├── CharacterSelectPatch.cs← InitCharacterButtons 无需改（AllCharacters 注入）；
│       │   │                             SelectCharacter postfix：右侧面板刷新；_Ready postfix：建面板
│       │   ├── ModelDbPatch.cs        ← ModelDb.AllCharacters getter postfix：追加注册的卡包角色
│       │   ├── CardLibraryPatch.cs    ← _Ready postfix：注入卡包池过滤按钮、按卡包排序按钮、
│       │   │                             FilterCards prefix：卡包搜索 OR 匹配
│       │   ├── CardGridPatch.cs       ← NCardGrid.SortingAlgorithms getter postfix：注册卡包排序算法
│       │   └── LocPatch.cs            ← LocManager 初始化 postfix：注入 lib 与 mod 的本地化词条
│       ├── Ui\
│       │   ├── PackConfigPanel.cs     ← 角色选择页右侧可展开面板（克隆游戏控件构建）
│       │   ├── PanelWidgets.cs        ← 下拉行/勾选行工厂（克隆 test_dropdown/settings 样式场景）
│       │   └── PackConfigStore.cs     ← 面板配置持久化（user://PackmasterLib/config.json）
│       └── Tests\
│           └── PackTestConsoleCmd.cs  ← 控制台命令：packtest / packstate / packgive
├── VanillaPacks\                      ← 演示角色 mod（mod id: VanillaPacks，依赖 PackmasterLib）
│   ├── VanillaPacks.json
│   ├── VanillaPacks.csproj
│   └── src\
│       ├── VanillaPacksEntry.cs
│       ├── VanillaSlinger.cs          ← RedirectedCharacterModel 子类（复用 ironclad 资产）
│       ├── VanillaSlingerCardPool.cs  ← 卡池（棕褐色主题、原版卡框）
│       ├── Packs\
│       │   ├── VanillaPackDefs.cs     ← 4 个卡包定义 + 评分摘要
│       │   ├── Preview\*.cs           ← 每包一张预览卡（显示包名+摘要+评分）
│       │   ├── StrikesPack 副本卡      ← 1 行/卡 的原版卡副本子类（继承原逻辑）
│       │   └── ...（4 包 × ~8 卡）
│       └── Tests\
│           └── VanillaPackTestCmd.cs  ← 演示侧测试命令（解析断言）
└── pck\（可选，暂无：美术资产由 mod 作者后续用 Godot 导出补充）
```

## 3. 运行时行为设计

### 3.1 注册 API（角色 mod 作者视角）

```csharp
[ModInitializer(nameof(Init))]
public static void Init()
{
    PackmasterApi.RegisterCharacter(new PackCharacterRegistration {
        CharacterType = typeof(VanillaSlinger),            // CharacterModel 子类
        Packs = new[]
        {
            new PackDefinition(
                id: "strikes",                             // 稳定 id（存档用）
                nameKey: "pack.strikes.name",              // 本地化 key（或 rawText 直填）
                descriptionKey: "pack.strikes.desc",
                author: "Moon",
                summary: " offense ★★★★☆ | defense ★★☆☆☆ | scaling ★★★☆☆",  // 选包界面显示（mod 作者提供）
                cardTypes: new[] { typeof(PackStrike), ... },
                previewCardType: typeof(StrikePackPreview)),   // 三选一界面展示用
            ...
        },
        DefaultSlots = new[] { Fixed("strikes"), Random, ChoiceOf3, ChoiceOf3, Random }, // 默认配置
    });
    new Harmony("vanilla.packs").PatchAll();   // 角色 mod 自己的 patch（若有）
}
```

- 注册发生在 mod 初始化阶段（ModelDb.Init 前），库内部**懒解析**：首次需要时用 `ModelDb.Card<T>()`/`ModelDb.Character<T>()` 取实例。
- 遗物/药水共享、卡框材质、能量图标、卡图 → 角色作者在自己的 `CardPoolModel`/`CharacterModel`/卡类中配置（库不管）。库提供 `RedirectedCharacterModel` 基类帮无美术作者复用原版资产。

### 3.2 角色进入游戏

- `ModelDbPatch`：postfix `ModelDb.get_AllCharacters` → 追加所有注册的卡包角色。由此自动获得：
  - 选人页按钮（`InitCharacterButtons` 遍历 AllCharacters）；
  - 自动解锁（`UnlockState.Characters` 包含无纪元限制的 mod 角色）；
  - `AllCardPools` 缓存包含其卡池（图鉴数据源、`CardModel.Pool` 反查归属）。
  - 缓存时序安全：Harmony patch 在 mod 初始化时安装，先于 `ModelDb` 各惰性缓存的首次访问。

### 3.3 角色选择页右侧可展开面板（PackConfigPanel）

- `NCharacterSelectScreen._Ready` postfix：用游戏控件克隆件构建面板（标题行按钮"卡包配置 ▸/▾"+内容 VBox），锚定屏幕右侧（InfoPanel 在左，右半屏空闲），加入选人屏节点树。
- `NCharacterSelectScreen.SelectCharacter` postfix：按当前角色是否为卡包角色显示/隐藏面板，并回填该角色的已存配置。
- 控件：
  - **全包模式**勾选行（勾选后覆盖槽位配置，开局直接获得全部卡包；对应 1 代 AllPacksMode）；
  - **卡包数量**下拉行（3…10）；
  - **槽位 1..N** 下拉行：选项 = `随机` / `三选一` / `无` / 分隔线 / 每个卡包名（选卡包名即"固定"该包）——与 1 代 Custom Draft 下拉一致；
  - "无"槽位数量即实际生效卡包数的减少（1 代 AllowMultipleNONE 的替代表达，无需额外开关）。
- 配置按角色 id 持久化到 `user://PackmasterLib/config.json`（Godot FileAccess），进入选人页时回填；单机 Embark 时由 `RunStartPatch` 捕获"待携带配置"。
- 多人模式：面板隐藏（配置不跨机同步），卡包角色在多人下使用 DefaultSlots（文档注明）。

### 3.4 开局流程（PackRunModifier）

1. `RunState.CreateForNewRun` prefix：玩家列表中有卡包角色 → 为每个该类玩家准备配置（单机=面板配置；多人=DefaultSlots），把 `PackRunModifier`（单实例）追加进 modifiers。
2. `PackRunModifier`（`ModifierModel` 子类，[SavedProperty] string Slots / string Selected / string AllMode）：
   - `OnRunCreated`：用 `RunState.Rng.UpFront`（确定序，多人一致）解析固定/随机槽 → 写入 `Selected`；三选一槽生成候选三元组（同 RNG，存内存）。
   - `GenerateNeowOption`：仅当 `eventModel.Owner` 是卡包角色且还有未解析的"三选一"槽时返回选项（标题/描述来自本地化）。选中后 async 循环：每轮 `CardSelectCmd.FromSimpleGrid`（3 张预览卡，MinSelect=MaxSelect=1，不可取消），预览卡描述即包摘要+评分（mod 作者提供，对应 1 代 ShowSummaries）。每轮结果追加进 `Selected`（SavedProperty 自动进存档）。
   - `OnRunLoaded`：从 SavedProperty 恢复；若有未解析三选一槽（读档发生在 Neow 之后已不可能——Neow 是开局第一事件；保险起见读档时剩余三选一槽按随机解析并记日志）。
   - 兜底：若开局未经过 Neow（纪元未解锁等情况）或玩家未遇到选项，首个奖励生成前（GetPossibleCards patch 处）发现未解析 → 用 UpFront RNG 随机补齐并 Log。
3. Neow 选项为**唯一选项**（游戏逻辑：有 modifier 选项时只展示 modifier 选项），相当于 1 代用选包界面替换 Neow 奖励——与 1 代卡包大师开局行为一致。

### 3.5 卡牌奖励走卡包

- `CardCreationOptions.GetPossibleCards` prefix：若 `player.Character` 是卡包角色且本局已激活卡包 → 把原池候选替换为"已选卡包的并集"（预览卡/隐藏卡排除）。稀有度滚动、升级滚动、多人过滤等下游逻辑完全复用（`allowedRarities` 自动来自候选稀有度分布）。
- 事件/遗物/商店发卡同样经 `GetPossibleCards`（CardCreationOptions 统一入口），与 1 代 `getCardPool()` 覆盖行为一致。
- `CardModel.Pool` 反查：卡包副本卡只存在于角色 mod 自己的池 → 图鉴"归属角色"正确。

### 3.6 图鉴集成

- **卡包池过滤按钮**（显示在归属角色下）：`NCardLibrary._Ready` postfix：克隆 `res://scenes/screens/card_library/library_pool_toggle.tscn`（直接绑定 `NCardPoolFilter` 具体类）→ 加入 `Sidebar/MarginContainer/TopVBox/PoolFilters`，设置 Image 纹理（角色选人图标）、悬停提示（Loc）→ 反射注册进私有字典 `_poolFilters[filter] = c => c.Pool is <角色池>`，连接 `Toggled` → 私有 `UpdateCardPoolFilter`，并加入 `_cardPoolFilters[character]`（否则 OnSubmenuOpened 字典索引会抛 KeyNotFound）。
- **按卡包排序**：同 postfix 克隆 `res://scenes/screens/card_library/library_sort_button.tscn`（绑定 `NCardViewSortButton`）置于 `CardTypeModule` 前，`SetLabel(本地化"卡包")`；点击 → 把自定义排序枚举值 `(SortingOrders)100/101` 插入 `_sortingPriority[0]` 并调私有 `DisplayCards`。`NCardGrid.SortingAlgorithms` getter postfix 注册比较：`PackIndex(a).CompareTo(PackIndex(b))`（未进包卡排最后；包序=注册顺序），与游戏稳定性 tiebreak（Id）兼容。
- **按卡包搜索**：`NCardLibraryGrid.FilterCards(filter, priority)` prefix：包装 filter 为 `原filter(c) || 卡包名(c).Contains(当前搜索词)`；当前搜索词取自 `_Ready` postfix 捕获的 `_searchBar`（同屏实例）。未发现卡仍由可见性系统遮蔽（与原逻辑一致）。
- **卡面显示包名**：`NInspectCardScreen.Open` postfix：详情视图加入/更新一行 MegaLabel「 belonging pack: X 」（有卡包归属时）。
- 卡面顶部包名渲染（1 代 RenderBaseGameCardPackTopTextPatches 的库内网格版）：V1 以排序分组 + 详情视图标签实现；网格逐卡叠名留作后续（需逐 holder 场景手术）。

### 3.7 本地化（无 pck 方案）

- `LocManager` 初始化 postfix：反射取表字典（`table → dict`），为 `cards/characters/gameplay_ui/card_library/card_selection/main_menu_ui/ui(若存在)` 等 lib 用到的表合并注入条目；角色 mod 在注册时以 `PackmasterApi.AddLocTable(table, dict)` 提供词条（zhs/eng 双语，回退 eng）。
- key 约定与原版一致：卡 `card.<entry>.title/.description`；角色 `characters.<entry>.title/...`；lib 自有词条放 `gameplay_ui` 前缀 `PACKMASTER_LIB.*`。
- 有 pck 的作者可完全走标准 `res://{id}/localization/`（游戏原生合并），lib 的注入只作为兜底/无 pck 路径。

### 3.8 演示 mod：VanillaPacks

- 角色 **VanillaSlinger**（RedirectedCharacterModel → ironclad 资产；棕褐色 MapDrawingColor；HP 72/金币 99；起始卡组 4 打击 4 防御 + 2 张自有简单卡 `VanillaSlingerStrike/Defend`——直接继承原版 Strike/Defend 行为）。
- 卡池 `VanillaSlingerCardPool`：Title "vanilla_slinger"，卡框用原版 ironclad 材质（演示"卡框作者自配"）。
- **4 个卡包**（每包 ~8 张，全为 1 行/卡的原版卡副本子类，继承原版逻辑与数值，PortraitPath 指回原版图集）：
  1. **基础包 Basics**（Common 为主）：原版各职业打击/防御副本、Bash、Neutralize 等；
  2. **打击艺术 Strikes**：各职业强化打击线（Twin Strike、Clothesline、Skewer...）；
  3. **守护壁垒 Defends**：防御/格挡线（Shrug It Off、Impervious...）；
  4. **禁忌奥能 Powers**：能力线（Inflame、Catalyst、Demon Form...按实现简单度取 8 张）。
  - 每包配 `summary` 评分文本（mod 作者提供）+ 预览卡。
- **测试卡包选择逻辑准确性**（对应"测试卡包选择逻辑的准确性"）：
  - 控制台命令 `packtest`（`AbstractConsoleCmd` 子类，自动注册）：
    - `packtest resolve <config>`：离线跑 PackResolver，断言：固定槽=指定包；随机槽两两不重复且 ∈ 注册包；无 槽=空；数量=槽数−无槽数；AllPacks 模式=全部包；输出 PASS/FAIL 明细；
    - `packtest state`：运行中打印当前 `Selected` 与激活池大小，校验 `GetPossibleCards` 返回 ⊆ 已选卡包；
    - `packtest reward`：模拟 `CardFactory.CreateForReward` 若干次，断言生成卡全部属于已选卡包、稀有度分布合理。
  - `packgive <packId>`：把指定包全部卡加入卡组（体验用）。
  - 首次 Embark 后日志打印解析过程（[PackmasterLib] 前缀）。

### 3.9 存档与多人边界

- 运行内：`PackRunModifier` SavedProperties 持久化（SchemaVersion 迁移机制不涉及 mod 字段，安全）。
- 多人：面板配置不跨端同步 → 使用 DefaultSlots；PackRunModifier 持久化/同步走 modifier 标准通道（SavedProperties 随 SerializableRun 进多人 packet 序列化）。
- 联机内容一致性：manifest `affects_gameplay: true`；`AssemblyInfo` 自动关联 mod 程序集。

## 4. 实现要点备忘（坑位）

1. **ModelDb 缓存时序**：`AllCharacters/AllCardPools/AllCards` 都是惰性缓存属性；postfix 必须在任何首次访问前安装（mod init 阶段满足）。
2. `NCardLibrary.OnSubmenuOpened` 用 `_cardPoolFilters[characterModel]`（字典索引，缺 key 抛异常）→ 注入过滤按钮时**必须**同时 `_cardPoolFilters[character]=filter`。
3. `SortingOrders` 强转值 (100/101) 不落进游戏任何 switch（仅字典查找）→ 安全。
4. 预览卡：`ShouldShowInCardLibrary=false`、`Rarity=Basic` 且加入角色池但通过池的 `FilterThroughEpochs` override 排除出奖励候选；`CanBeGeneratedInCombat=false`。
5. 副本卡继承原版类（`class PackTwinStrike : TwinStrike {}`）自动获得 Id/逻辑；必须确保原版类**非 sealed**（实现时逐卡确认，sealed 的用行为复制）。
6. `CardModel.Pool` 反查失败会抛异常 → 所有 CardModel 子类必须被 `AddModelToPool` 挂到本 mod 池（含预览卡、起始卡）。
7. 克隆控件：`PreloadManager.Cache.GetScene(path).Instantiate<T>()` 或 `ResourceLoader.Load<PackedScene>`；`test_dropdown.tscn` 的 Dropdown 子节点绑 NDropdown 基类（可直接实例化），条目用 `scenes/ui/dropdown_item.tscn` 克隆后按 NActDropdown.PopulateOptions 模式接线。
8. 面板配置存 `user://PackmasterLib/config.json`（Godot `FileAccess`）；key=角色 `Id.Entry`。
9. Harmony patch 目标中私有成员用 `AccessTools`；`DisplayCards`/`UpdateCardPoolFilter`/`_poolFilters`/`_sortingPriority`/`_searchBar` 均私有。
10. 构建引用：`sts2.dll`、`0Harmony.dll`、`GodotSharp.dll` 均 `Private=false`；SDK 用 `D:\env\dotnet-sdk-9`；部署：`dotnet build` 后拷 `{id}.dll`+`{id}.json` 到 `D:\SteamLibrary\steamapps\common\Slay the Spire 2\mods\{id}\`。
11. Godot 4.5.1 Mono 编辑器在 `D:\env\godot_4.5.1\`（后续如需 pck/美术可用 `--headless --export-pack`）。
12. 游戏运行中的本地化表：postfix 注入点在 `LocManager` 初始化完成后、任何界面读取前（boot 链早期，ModManager.Initialize 之后立刻可用）。

## 5. 里程碑（全部完成）

1. ✅ 源码研究（本文件 §1）
2. ✅ 设计定稿（本文件）
3. ✅ 工程：两 mod 项目 + build-and-install.ps1（0 错误构建）
4. ✅ 核心：PackmasterApi / ModelDb.AllCharacters 注入（角色自动解锁）/ RunState.CreateForNewRun prefix 挂 PackRunModifier / SavedProperty 存档持久化 / CardCreationOptions.GetPossibleCards postfix 奖励过滤
5. ✅ UI：选人页右侧可展开面板（克隆 test_dropdown/rarity_tickbox/library_sort_button 场景构建）；Neow 三选一（CardSelectCmd.FromSimpleGrid + BlockingPlayerChoiceContext）；图鉴池过滤按钮克隆 + "卡包"排序按钮 + 卡包名搜索（FilterCards prefix 包装）+ LocManager.SetLanguageInternal postfix 本地化注入（免 pck）；库词条覆盖全部 16 种原版语言（LibLoc.RegisterAll：英文基座+各语言覆盖），演示角色仅中英、其余语言回退英文
6. ✅ 演示 mod：VanillaSlinger（RedirectedCharacterModel 复用 ironclad 资产）+ 4 包 27 张副本卡 + 4 预览卡 + 免 pck 中英本地化（tools/gen_loc.py 从原版表生成）
7. ✅ 运行验证：--packmastertest 无头自动测试 931 checks / 0 failures（autotest10.log）

## 6. 实施后与设计的差异（重要）

1. **卡包排序实现改为 `NCardGrid.SetCards` prefix**（§3.6 中"SortingAlgorithms 字典注入"方案在真机上不生效——反编译源码与实际 dll 的 SetCards 细节可能有出入）。prefix 检测 priority[0] 为 (SortingOrders)100/101 时自行排序（卡包 → 稀有度 → 标题（本地化序），与 1 代 packSort 一致）并把 priority[0] 改写为 `Ascending` 使游戏跳过自身排序。
2. **Id.Entry 是大写蛇形**（StringHelper.Slugify 大写化）：本地化 key 为 `PACK_STRIKE.title` 而非 `pack_strike.title`；卡图图集路径仍用小写（ImageHelper 路径自行 ToLower）。
3. 卡面详情视图的包名标签未实现（v1 以图鉴分组排序 + 搜索代替）。
4. AutoSlay（--autoslay）在 release 构建被禁用（NGame.IsReleaseGame），无法用于自动化；改用 `--packmastertest` 自定义无头测试。
5. 演示环境无法进行前台输入/截屏（锁屏会话），所有验证通过日志 + 无头测试完成；选人面板视觉、Neow 选包界面留待用户人工确认（面板 Attach/SelectCharacter 挂钩日志已确认执行）。
6. **修复（v0.1.1）**：
   - 角色无法选中：`CharacterRedirectPatch` 原用正则 `vanilla_slinger`，但路径形如 `char_select_bg_vanilla_slinger`（`_` 是单词字符，`` 不匹配）→ 重定向全部失效、`SelectCharacter` 加载背景抛异常。改为普通字符串替换。
   - 缺 `characters` 表词条（标题/描述/代词等）→ 演示 mod 补齐中英；`CharacterTransitionSfx` 指向 `wipe_ironclad`。
   - 打败 Boss 时 `ProgressSaveManager.ObtainCharUnlockEpoch` 会 `EpochModel.Get("VANILLA_SLINGER2_EPOCH")` 抛异常 → 对卡包角色跳过。
   - 三选一槽会重复给已有卡包：候选在槽位顺序上预先抽取，之后的随机槽/前一个三选一会拿走其中的包。现在先解析固定/随机槽再解析三选一，且出示/兜底时按已选包重新过滤候选（`PlayerPackState.TakeNextChoice`）。
   - 配置 UI 统一为原生 `NPaginator`（`PackConfigRows`），设置页与选人页共用；顶栏 modifier 图标改用原版 draft 图标。
7. **v0.1.2**：选人面板改为按内容自适应高度的 PanelContainer（折叠时整体收起）、上移并缩放到 0.7，7 槽时不遮挡出发按钮；行布局改为 HBox（标签裁剪+省略号，不再与翻页控件重叠）；"全包模式（…）"改为"全卡包"（各语言同步缩短）；`PackConfigStore` 之前从未真正写盘（目录不存在时 `FileAccess.Open` 返回 null），已补 `MakeDirRecursiveAbsolute`；自动测试全程静音、关闭首次启动的抢先体验弹窗、非 headless 时截图。
