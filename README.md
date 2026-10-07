# STS2 卡包大师工具库 + 演示角色

复刻《杀戮尖塔1》卡包大师（The Packmaster）核心体验的 **Slay the Spire 2** 模组套件。
**零第三方依赖**（不依赖 BaseLib / RitsuLib，只用游戏原生 Modding API + Harmony）。

| Mod | ID | 作用 |
|---|---|---|
| `PackmasterLib\` | `PackmasterLib` | 工具库：卡包注册 API、选人页卡包配置面板、开局选包界面、卡包卡池、卡面包名、右上角本局卡包按钮、图鉴按卡包排序/搜索、设置页、存档持久化；库 UI 词条覆盖 16 种原版语言（新增词条中/英，其余回退英文） |
| `VanillaPacks\` | `VanillaPacks` | 演示角色 **原版卡包师 / Vanilla Slinger**：11 个由原版卡组成的卡包 + 端到端自动测试 |

对应游戏版本：**v0.111.0**（net9.0）。设计与实现细节见 [DESIGN.md](DESIGN.md)。

## 构建 / 安装

```
powershell -ExecutionPolicy Bypass -File build-and-install.ps1
```

编译并部署到 `D:\SteamLibrary\steamapps\common\Slay the Spire 2\mods\`（首次启用 mod 需在游戏里确认"加载模组"并重启）。

## 玩法（与 1 代卡包大师一致）

1. **角色选择**：选中原版卡包师 → 右上角**卡包配置**面板（点标题折叠）：全卡包 / 卡包数量（3-10）/ 每个槽位（随机、三选一、无、指定卡包）。按角色保存。
2. **开局选包界面**（进入第一个房间、在涅奥之前弹出）：
   - 上方「已选定卡包」：固定和随机槽的卡包；
   - 下方逐轮**三选一**：点一个卡包加入，未选中的两个离开卡池（1 代规则：同一卡包不会被提供两次）；
   - 悬停卡包：**卡包概要**（伤害/防御/辅助/即效/成长 星级 + 标签）；卡下方显示作者；左下角可关闭评分显示（1 代"显示卡包评分"）；
   - 全部选完后卡包居中排列，点**确认**后继续——**涅奥的奖励照常出现，不受影响**。
   - 中途存档退出：读档后选包界面以同样的候选重新打开。全卡包模式跳过此界面（同 1 代）。多人模式下三选一按种子确定性随机、跳过界面。
3. **卡池**：奖励、商店、事件、药水与"随机生成一张牌"等效果只从**已选卡包**中出牌；变化（transform）也在卡包内进行（无色牌照旧变为无色牌）。
4. **卡面**：卡包里的牌在卡面上方显示**卡包名**；原版卡保留原角色卡框（设置"统一卡框"后使用卡包角色的卡框）。
5. **右上角卡包按钮**：悬停列出本局卡包，点击查看**本局牌池**（不含初始牌；按卡包 → 稀有度 → 名称排序）。
6. **图鉴**：卡包角色的筛选按钮显示其全部卡包卡牌（含原版卡）并在卡面上方显示包名；"卡包"排序按钮；搜索框可输入卡包名。
7. **设置 → 游戏设置 → 卡包大师**（可折叠，1 代 mod 设置 + 开发选项）：统一卡框 / 允许多个"无"槽位 / [开发] 解锁全部卡包 / [开发] 跳过选包。

## 演示角色的 11 个卡包

每个卡包 11-13 张原版卡，攻击/技能/能力各 ≥2，普通/罕见/稀有各 ≥2。

| 卡包 | 来源 | 卡包 | 来源 |
|---|---|---|---|
| 疼痛无视 | 铁甲·自伤 | 毒性爆发 | 静默·中毒 |
| 铜墙铁壁 | 铁甲·格挡 | 幻影之刃 | 静默·小刀 |
| 灾厄将至 | 亡灵契约师·灾厄 | 混沌之初 | 储君·无色/生成 |
| 骸骨之友 | 亡灵契约师·奥斯提 | 王国兵器 | 储君·铸造 |
| 星辰之力 | 储君·星辉 | 闪电风暴 / 冰霜堡垒 | 故障机器人·充能球 |

不属于任何卡包的牌（同 1 代基础牌）：4 打击 + 4 防御（自有副本）、痛击、中和（初始牌，古老牙齿可升华）；腐化、幽魂形态等先古牌（供尘封魔典）。默认配置：4 随机 + 3 三选一 = 7 包（1 代默认 7 包）。

## 给角色 mod 作者的 API

```csharp
[ModInitializer(nameof(Init))]
public static void Init()
{
    PackmasterApi.AddLoc("cards", "zhs", myCardsZhs);   // 免 pck 本地化（也可用 pck）
    PackmasterApi.RegisterCharacter(new PackCharacterRegistration
    {
        CharacterType = typeof(MyCharacter),
        Packs = new[]
        {
            new PackDefinition
            {
                Id = "my_pack",
                NameKey = "cards:MY_PACK_PREVIEW.title",
                DescriptionKey = "cards:MY_PACK_PREVIEW.description",
                Author = "Me",
                CardTypes = new[] { typeof(MyCard), typeof(Bloodletting) /* 原版卡可直接引用 */, ... },
                PreviewCardType = typeof(MyPackPreview),        // : PackPreviewCard，选包界面显示
                CoverCardType = typeof(MyRare),                 // 预览卡卡图（默认第一张稀有）
                Summary = new PackSummary { Offense = 4, Defense = 2, Support = 1, Frontload = 3, Scaling = 4,
                                            Tags = new[] { PackTags.Strength } },
            },
        },
        ExtraPoolCardTypes = new[] { typeof(MyStrike), typeof(MyDefend), typeof(MyAncient) }, // 不属于卡包的牌
        DefaultSlots = new[] { "random", "random", "random", "random", "choice", "choice", "choice" },
    });
}
```

- **卡包深度（重要）**：奖励、商店、"随机攻击/技能/能力"等效果只从已选卡包抽牌，卡池过浅可能导致无牌可抽而**闪退**。参照 1 代，每个卡包建议 **≥10 张**，**攻击/技能/能力各 ≥2**，**普通/罕见/稀有各 ≥2**；仅多人可用（`MultiplayerOnly`）的牌在单人中会消失，不计入。不满足时库会在日志中警告（`Pack '...' is thin`）。
- **先古牌**：尘封魔典会从角色卡池随机给一张非升华先古牌，卡池里没有会出错——请在 `ExtraPoolCardTypes` 里放至少一张先古牌。
- **原版/其他 mod 的卡**：直接写进 `CardTypes`，保留原卡池、卡框、卡图（1 代对原版卡同样如此）；同一张卡在一个角色里只能属于一个卡包。
- **卡池类**：`GenerateAllCards()` 返回 `PackRegistry.GetPoolCards(registration)`（只含你 mod 程序集里的卡：自有卡、初始牌副本、预览卡）。原版卡留在原卡池，由库在 `GetUnlockedCards` 中按本局卡包提供。
- **卡包解锁**：默认全部解锁；`registration.IsPackUnlocked = pack => ...` 自定义（任何 mod 可随时修改），`registration.UnlockedPacks` 查询。未解锁的卡包不会被提供。
- **库设置**（`PackmasterSettings`）：`OneFrameMode`、`AllowMultipleNone`、`UnlockAllPacks`、`AutoResolveChoices`、`HideSummaries`。
- **无美术角色**：继承 `RedirectedCharacterModel`（复用某个原版角色的视觉/音效资源）。
- **查询**：`PackState.Get(player)`（本局已选卡包）、`PackRegistry.GetPackOf(card, registration)`、`PackRegistry.GetPackCards(pack)`。

## 测试

端到端自动测试（像玩家一样操作：选人 → 出发 → 选包界面三轮选择 → 确认 → 涅奥；全程静音）：

```
SlayTheSpire2.exe --packmastertest --force-steam=off              # 窗口模式，额外截图到 user://packtest_*.png
SlayTheSpire2.exe --headless --packmastertest --force-steam=off   # 无头
grep "PackmasterLib-AutoTest" <日志>
```

最近一次结果：`RESULT: 927 checks, 0 failures`（autotest15.log）。覆盖：卡包深度与唯一性、原版卡保留原卡池、解析器不变量（60 种子 × 4 配置，1 代"提供过即移出卡池"）、卡包解锁 API 与开发设置、多个"无"规则、选人面板与设置页、**真实出发流程**（选包界面出现、悬停概要、三轮选择、确认、存档字段）、**涅奥保留原有奖励**、右上角按钮（位置、悬停、卡池视图打开/关闭）、局内卡池（60 张战斗奖励、商店、三种类型随机生成、变化）、卡面包名与统一卡框、存档往返、**中途存档重开选包界面**。

游戏内控制台（`~`）：`packtest resolve [seed]` / `packtest state` / `packgive <packId>` / `packtestreward [rounds]`。
