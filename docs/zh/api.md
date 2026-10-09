# API 参考

[English](../en/api.md) · [入门](getting-started.md) · [游戏内行为](behavior.md)

命名空间：`Sts2Packmaster.Lib.Api`（公开 API）、`Sts2Packmaster.Lib.Core`（注册表与本局状态查询）。以下成员均为 public，可在其他 mod 中安全调用。

## PackmasterApi

| 成员 | 说明 |
|---|---|
| `RegisterCharacter(PackCharacterRegistration)` | 注册卡包角色。在 `[ModInitializer]` 中调用。延迟解析：ModelDb 就绪后才解析卡牌类型。 |
| `AddLoc(table, entries)` | 添加英文（`eng`）本地化词条。 |
| `AddLoc(table, language, entries)` | 添加某种语言的词条，在该语言加载时合并进游戏的表。常用表：`cards`、`characters`、`gameplay_ui`、`card_library`、`card_selection`。 |
| `IsPackCharacter(CharacterModel)` | 该角色是否注册为卡包角色。 |
| `GetPacks(CharacterModel)` | 角色的卡包（非卡包角色返回空）。 |

## PackCharacterRegistration

| 属性 | 默认值 | 说明 |
|---|---|---|
| `CharacterType`（必填） | | 你的 `CharacterModel` 子类。 |
| `Packs`（必填） | | 角色的 `PackDefinition` 列表。 |
| `DefaultSlots`（必填） | | 玩家未配置时使用的槽位（多人模式始终使用）。3–10 个。取值：`"random"`、`"choice"`、`"none"` 或卡包 id（常量见 `PackSlotToken`）。 |
| `ExtraPoolCardTypes` | 空 | 角色卡池中不属于任何卡包的牌：初始牌和先古牌（至少一张非升华先古牌，供尘封魔典）。可以是自有或原版类型。 |
| `ChoiceSize` | 3 | 每轮候选卡包数。 |
| `Drawer` | `null` | 自定义 `IPackDrawer`；`null` = `WeightedPackDrawer`。可随时修改。 |
| `IsPackUnlocked` | `null` | 解锁规则 `Func<PackDefinition, bool>`；`null` = 全部解锁。可随时修改，任何 mod 都可以改。 |
| `UnlockedPacks` | | 只读：当前满足 `IsPackUnlocked` 的卡包（开启开发选项「解锁全部卡包」时为全部）。 |

未解锁的卡包不会被随机槽或三选一提供，不计入全卡包模式，也不出现在选人配置中。指定未解锁卡包的固定槽会被跳过。

## PackDefinition

| 属性 | 默认值 | 说明 |
|---|---|---|
| `Id`（必填） | | 写入存档和配置的稳定 id，每个角色内唯一。建议小写下划线。**发布后不要改名。** |
| `NameKey`（必填） | | 显示名称：`"表名:键"` 本地化键或纯文本。 |
| `DescriptionKey`（必填） | | 描述：`"表名:键"` 或纯文本。 |
| `CardTypes`（必填） | | 卡包的卡：自有、原版或其他 mod 的 `CardModel` 类型。 |
| `Author` | `""` | 显示在预览卡下方和提示框中。 |
| `CreditsKey` | `null` | 提示框中可选的致谢行（本地化键或文本）。 |
| `PreviewCardType` | `null` | 选包界面用的 `PackPreviewCard` 子类。没有则该卡包不会出现在三选一中。 |
| `CoverCardType` | 第一张稀有卡 | 预览卡显示其卡图的卡。 |
| `Weight` | 1.0 | 默认抽取算法的权重。`0` = 只在没有其他候选时才会被抽到。 |
| `Summary` | 空 | 提示框中显示的 `PackSummary`。 |

同一个 `PackDefinition` 实例可以放进多个角色的 `Packs`（共享卡包）。每个角色从自己的列表抽取；同一张卡可以在不同角色中属于不同卡包，但在一个角色中最多属于一个卡包。

## PackSummary 与 PackTags

`PackSummary { Offense, Defense, Support, Frontload, Scaling }` 是 0–5 星评分（1 代的卡包概要：伤害、防御、辅助、即效、成长），以及 `Tags`——每个标签可以是 `PackTags` 常量（库自带翻译）、`"表名:键"` 本地化键或纯文本。

`PackTags`：`Strength`、`Exhaust`、`Orbs`、`Discard`、`Debuffs`、`Attacks`、`Tokens`、`Powers`、`Block`、`Poison`、`Shivs`、`Doom`、`Summon`、`Stars`、`Forge`、`SelfDamage`、`Draw`、`Energy`、`Generation`（`PackTags.All` 列出全部）。

玩家可以在选包界面用「显示卡包评分」隐藏星级。

## 抽取算法：IPackDrawer

```csharp
public interface IPackDrawer
{
    PackDefinition DrawRandomSlot(PackDrawContext context);                              // 随机槽抽一个卡包
    IReadOnlyList<PackDefinition> DrawChoiceOffer(PackDrawContext context, int count);   // 三选一候选，顺序即从左到右
}
```

`PackDrawContext`：

| 属性 | 说明 |
|---|---|
| `Registration` | 角色。 |
| `Candidates` | 可以返回的卡包（已解锁、未在本局中、按候选规则排除了跳过的卡包）。 |
| `Selected` | 本局已有的卡包，按顺序。 |
| `Round` | 三选一轮次（从 0 开始）；随机槽为 `-1`。 |
| `Rng` | 唯一允许使用的随机源。抽取必须是确定性的，以保证存读档和多人一致。 |

库会清理返回值：丢弃不在 `Candidates` 中的和重复的卡包，缺少的用默认算法补足。可以继承 `WeightedPackDrawer`（方法均为 virtual），或调用静态方法 `WeightedPackDrawer.Draw(candidates, count, rng)` 复用按权重不放回抽样。

示例——每轮最左边的卡包保底出现：

```csharp
sealed class PityDrawer : WeightedPackDrawer
{
    public override IReadOnlyList<PackDefinition> DrawChoiceOffer(PackDrawContext ctx, int count)
    {
        var first = ctx.Candidates.FirstOrDefault(p => p.Id == "my_signature_pack");
        if (first == null) return base.DrawChoiceOffer(ctx, count);
        var rest = Draw(ctx.Candidates.Where(p => p != first).ToList(), count - 1, ctx.Rng);
        return new[] { first }.Concat(rest).ToList();
    }
}

registration.Drawer = new PityDrawer();
```

## 卡牌与角色基类

| 类 | 说明 |
|---|---|
| `PackPreviewCard` | 卡包预览卡基类，空子类即可。标题/描述来自本地化（`cards` 表中的 `<ID>.title` / `<ID>.description`），卡图来自封面卡。不在图鉴中显示，不会被生成，也不能打出。 |
| `PackCardModel` | 复用原版卡图的自有卡基类：重写 `SourcePoolTitle`（如 `"ironclad"`）和 `SourcePortraitEntry`（如 `"strike_ironclad"`）。 |
| `RedirectedCharacterModel` | 无美术角色基类：重写 `AssetSourceEntry`（如 `"ironclad"`），复用该角色的图标、选人立绘、地图标记、视觉和音效，以及先古之民对话和攻击建筑师的特效（没有建筑师对话的角色会在通关时崩溃）。 |

## PackmasterSettings

静态属性，保存在 `user://PackmasterLib/settings.json`（`%APPDATA%\SlayTheSpire2\PackmasterLib\settings.json`）。在「设置 → 游戏设置 → 卡包大师」中显示，`HideSummaries` 除外（在选包界面切换）。

| 设置 | 默认 | 说明 |
|---|---|---|
| `OneFrameMode` | 关 | 1 代「统一卡框」：卡包卡使用卡包角色的卡框和能量图标。 |
| `AllowMultipleNone` | 关 | 1 代「允许多次选择无」。关闭时只有第一个「无」生效，其余改为随机。 |
| `HideSummaries` | 关 | 在卡包提示框中隐藏星级评分。 |
| `UnlockAllPacks` | 关 | 开发：忽略 `IsPackUnlocked`。 |
| `AutoResolveChoices` | 关 | 开发：三选一随机决定，跳过选包界面。 |
| `ExcludeOnlyLastRound` | 关 | 开发：宽松候选规则（见[游戏内行为](behavior.md#候选规则)）。 |

## 查询

`PackRegistry`（`Sts2Packmaster.Lib.Core`）：

| 成员 | 说明 |
|---|---|
| `Registrations` | 所有已注册角色。 |
| `GetRegistration(CharacterModel?)` | 角色的注册信息，或 `null`。 |
| `GetPackCharacters()` | 所有卡包角色的 `CharacterModel`。 |
| `GetPoolCards(registration)` | 供 `GenerateAllCards()` 使用的自有程序集卡牌。 |
| `GetCharacterCards(registration)` | 角色的全部卡牌：所有卡包加额外卡（不含预览卡）。 |
| `GetExtraCards(registration)` | `ExtraPoolCardTypes` 的卡。 |
| `GetPackCards(pack)` | 卡包的卡牌（canonical）。 |
| `GetPackOf(card, registration)` | 该角色中某张卡所属的卡包。 |
| `GetPreviewCard(pack)`、`IsPreviewCard(card)`、`GetPreviewPack(card)` | 预览卡查询。 |
| `GetPackName(pack)` | 本地化后的卡包名。 |
| `ResolveLocKey("表名:键")` | 解析库格式的本地化键（先查注入的词条，再查游戏的表）。 |

`PackState`——每个玩家的本局卡包：

| 成员 | 说明 |
|---|---|
| `PackState.Get(player)` | `PlayerPackState`，非卡包角色为 `null`。 |
| `PackState.NeedsSetup(player)` | 选包界面尚未确认。 |
| `PackState.GetSelectedCardIds(player)` | 本局卡包中所有卡的 id。 |
| `PlayerPackState.Selected` | 本局卡包，按顺序。 |
| `PlayerPackState.ChoicesLeft` | 剩余三选一轮数。 |
| `PlayerPackState.SetupDone` | 是否已确认。 |
| `PlayerPackState.CurrentOffer()` | 当前轮的候选。 |
| `PlayerPackState.SelectedCards()` | 已选卡包的卡牌。 |

请把 `PlayerPackState` 当作只读；选包只能通过选包界面进行。

`PackDisplayContext.For(card)` 返回屏幕上某张卡适用的卡包角色（卡牌所有者、图鉴中选中的卡包角色筛选，或本地玩家）。

## 控制台命令

| 命令 | 所属 mod | 说明 |
|---|---|---|
| `packtest resolve [seed]` | PackmasterLib | 检查所有已注册角色的槽位解析不变量。 |
| `packtest state` | PackmasterLib | 输出并校验本局卡包状态。 |
| `packgive <packId>` | VanillaPacks | 把某个卡包的全部卡加入牌组。 |
| `packtestreward [rounds]` | VanillaPacks | 生成卡牌奖励，检查其中只有已选卡包的卡。 |
