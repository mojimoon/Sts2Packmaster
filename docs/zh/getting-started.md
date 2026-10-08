# 入门：制作第一个卡包角色

[English](../en/getting-started.md) · [API 参考](api.md) · [游戏内行为](behavior.md)

本文以仓库中的演示 mod `VanillaPacks` 为例，讲解如何用 **PackmasterLib** 制作卡包角色。
下面的代码都摘自 `VanillaPacks/src/`，建议对照阅读。

## 1. 准备

- 杀戮尖塔2（v0.111.0 及以上）和 .NET 9 SDK。
- 本仓库源码（GitHub 上 clone 或下载 zip）。`PackmasterLib.dll` 可从源码编译，也可以从已安装的 PackmasterLib mod 文件夹中复制。
- 基础的塔2 mod 知识：mod 是 `<游戏目录>/mods/<ModId>/` 下的文件夹，包含 `<ModId>.json`（清单）和 `<ModId>.dll`，入口为标记了 `[ModInitializer]` 的静态类。

PackmasterLib 只使用游戏自带的 Modding API 和 Harmony，没有其他依赖。

## 2. 项目配置

仓库的 `Directory.Build.props` 从游戏目录引用 `sts2.dll`、`0Harmony.dll`、`GodotSharp.dll`。设置一次游戏目录：

```
dotnet build -p:Sts2Dir="C:\Program Files (x86)\Steam\steamapps\common\Slay the Spire 2"
```

或设置环境变量 `STS2_DIR`。`build-and-install.ps1` 会编译两个 mod 并复制到 `<游戏目录>/mods/`（参数 `-GameDir`、`-Dotnet`，或环境变量 `STS2_DIR` / `STS2_DOTNET`）。

在你的 mod 中引用 PackmasterLib，**不要复制到输出目录**（游戏会加载 PackmasterLib mod 文件夹里的那份）：

```xml
<!-- 在本仓库内 -->
<ProjectReference Include="..\PackmasterLib\PackmasterLib.csproj">
  <Private>false</Private>
</ProjectReference>

<!-- 或在独立仓库中 -->
<Reference Include="PackmasterLib">
  <HintPath>path\to\PackmasterLib.dll</HintPath>
  <Private>false</Private>
</Reference>
```

在清单中声明依赖，确保游戏先加载 PackmasterLib：

```json
{
  "id": "MyPackCharacter",
  "name": "My Pack Character",
  "author": "Me",
  "description": "...",
  "version": "0.1.0",
  "min_game_version": "0.111.0",
  "has_pck": false,
  "has_dll": true,
  "dependencies": [
    { "id": "PackmasterLib", "min_version": "0.1.0" }
  ],
  "affects_gameplay": true
}
```

发布到创意工坊时，还要把 PackmasterLib 添加为物品的「必需物品」。

## 3. 角色

卡包角色就是你程序集里一个普通的 `CharacterModel` 子类（ModelDb 会自动注册）。如果还没有美术，可以继承 `RedirectedCharacterModel`，复用某个原版角色的立绘、图标、地图标记和音效：

```csharp
public sealed class VanillaSlinger : RedirectedCharacterModel
{
    protected override string AssetSourceEntry => "ironclad";   // 复用铁甲战士的资源

    public override Color NameColor => new Color("8a6f4d");
    public override CharacterGender Gender => CharacterGender.Neutral;
    protected override CharacterModel? UnlocksAfterRunAs => null;
    public override int StartingHp => 72;
    public override int StartingGold => 99;

    public override CardPoolModel CardPool => ModelDb.CardPool<VanillaSlingerCardPool>();
    public override RelicPoolModel RelicPool => ModelDb.RelicPool<IroncladRelicPool>();
    public override PotionPoolModel PotionPool => ModelDb.PotionPool<IroncladPotionPool>();

    public override IEnumerable<CardModel> StartingDeck => new List<CardModel>
    {
        ModelDb.Card<PackStrike>(), /* ... */ ModelDb.Card<Bash>(), ModelDb.Card<Neutralize>(),
    };
    public override IReadOnlyList<RelicModel> StartingRelics => new List<RelicModel> { ModelDb.Relic<BurningBlood>() };
    // ……其余抽象成员（动画延迟、地图颜色、建筑师特效）
}
```

角色还需要 `characters` 表的本地化（名称、描述、代词、台词……），游戏会读取的完整键列表见 `VanillaPacksEntry.cs` 中的 `CharacterEn`。

## 4. 卡池

卡池的 `GenerateAllCards()` **只能返回你自己程序集里的卡**——库会替你算好：

```csharp
public sealed class VanillaSlingerCardPool : CardPoolModel
{
    public override string Title => "vanilla_slinger";
    public override string EnergyColorName => "colorless";
    public override string CardFrameMaterialPath => "card_frame_colorless";
    public override Color DeckEntryCardColor => new Color("8a6f4d");
    public override bool IsColorless => false;

    protected override CardModel[] GenerateAllCards() =>
        VanillaPacksEntry.Registration == null
            ? Array.Empty<CardModel>()
            : PackRegistry.GetPoolCards(VanillaPacksEntry.Registration).ToArray();

    // 卡包预览卡不进入已解锁卡池
    protected override IEnumerable<CardModel> FilterThroughEpochs(UnlockState unlockState, IEnumerable<CardModel> cards) =>
        cards.Where(c => !PackRegistry.IsPreviewCard(c));
}
```

原因：游戏把第一个包含某张卡的卡池视为这张卡的卡池（决定卡框、能量图标、图鉴分类）。所以卡包里的原版卡或其他 mod 的卡会留在原卡池、保留原卡框；PackmasterLib 在运行时按本局卡包把它们提供给你的角色。卡池的卡框（`CardFrameMaterialPath`）就是「统一卡框」设置应用到所有卡包卡上的卡框。

## 5. 卡牌

卡包里的卡可以是：

- **自己的卡**——普通的 `CardModel` 子类。
- **原版或其他 mod 的卡**——直接引用类型（`typeof(Bloodletting)`），保留原卡池、卡框和卡图，与 1 代卡包大师对原版卡的处理一致。
- **使用原版卡图的复制卡**——原版卡类是 sealed 的，需要把效果复制到 `PackCardModel` 子类中，并指向原卡图。演示角色的初始牌就是这样做的：

```csharp
public sealed class PackStrike : PackCardModel
{
    protected override string SourcePoolTitle => "ironclad";
    protected override string SourcePortraitEntry => "strike_ironclad";
    protected override HashSet<CardTag> CanonicalTags => new() { CardTag.Strike };
    protected override IEnumerable<DynamicVar> CanonicalVars => new List<DynamicVar> { new DamageVar(6m, ValueProp.Move) };

    public PackStrike() : base(1, CardType.Attack, CardRarity.Basic, TargetType.AnyEnemy) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay).Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_slash").Execute(choiceContext);
    }

    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(3m);
}
```

**不属于任何卡包的牌**（1 代的基础牌）放进 `ExtraPoolCardTypes`：初始牌组的牌，以及**至少一张非升华的先古牌**。尘封魔典会从角色卡池随机给一张先古牌，卡池里没有就会出错。基础/先古稀有度保证这些牌不会出现在普通奖励中。

## 6. 卡包预览卡

每个卡包需要一张预览卡用于选包界面，空子类即可：

```csharp
public sealed class PainPackPreview : PackPreviewCard { }
```

预览卡的标题和描述就是卡包名和卡包描述；卡图来自卡包的封面卡（`CoverCardType`，默认为卡包中第一张稀有卡）。模型 id 由类名生成（`PainPackPreview` → `PAIN_PACK_PREVIEW`），所以本地化键是 `cards` 表中的 `PAIN_PACK_PREVIEW.title` / `PAIN_PACK_PREVIEW.description`。没有预览卡的卡包仍可用于指定槽和随机槽，但不会出现在三选一中。

## 7. 注册

在 mod 入口中注册：

```csharp
[ModInitializer(nameof(Init))]
public static class MyEntry
{
    public static PackCharacterRegistration? Registration { get; private set; }

    public static void Init()
    {
        PackmasterApi.AddLoc("cards", "eng", MyLoc.CardsEn);
        PackmasterApi.AddLoc("cards", "zhs", MyLoc.CardsZhs);
        PackmasterApi.AddLoc("characters", "eng", MyLoc.CharacterEn);

        Registration = new PackCharacterRegistration
        {
            CharacterType = typeof(VanillaSlinger),
            Packs = new List<PackDefinition>
            {
                new PackDefinition
                {
                    Id = "pain",
                    NameKey = "cards:PAIN_PACK_PREVIEW.title",
                    DescriptionKey = "cards:PAIN_PACK_PREVIEW.description",
                    Author = "MegaCrit",
                    PreviewCardType = typeof(PainPackPreview),
                    CoverCardType = typeof(CrimsonMantle),
                    CardTypes = new[]
                    {
                        typeof(Breakthrough), typeof(BloodWall), typeof(Hemokinesis), typeof(Spite),
                        typeof(Bloodletting), typeof(Rupture), typeof(Inferno), typeof(TearAsunder),
                        typeof(Offering), typeof(Brand), typeof(CrimsonMantle),
                    },
                    Summary = new PackSummary
                    {
                        Offense = 4, Defense = 1, Support = 2, Frontload = 3, Scaling = 4,
                        Tags = new[] { PackTags.SelfDamage, PackTags.Strength },
                    },
                },
                // ……更多卡包
            },
            ExtraPoolCardTypes = new[] { typeof(PackStrike), typeof(PackDefend), typeof(Bash), typeof(Neutralize),
                                         typeof(Corruption), typeof(WraithForm) /* 先古牌 */ },
            DefaultSlots = new[] { "random", "random", "random", "random", "choice", "choice", "choice" },
        };
        PackmasterApi.RegisterCharacter(Registration);
    }
}
```

把注册对象保存在静态属性中：卡池的 `GenerateAllCards()` 需要用到它。

## 8. 本地化

`PackmasterApi.AddLoc(table, language, entries)` 无需 `.pck` 即可把词条合并进游戏的本地化表。语言代码与游戏一致：`eng`、`zhs`、`zht`、`jpn`、`kor`、`deu`、`fra`、`spa`、`esp`、`ita`、`pol`、`ptb`、`rus`、`tha`、`tur`、`ind`。

- 卡牌和角色文本由游戏从**当前语言**的表中读取。没有翻译的语言请也添加英文词条，否则这些语言的玩家会看到原始键名。演示 mod 遍历了全部 16 种语言（`VanillaPacksEntry.cs` 中的 `VanillaLanguages`）。
- 由库解析的卡包名、描述和标签会自动回退到 `eng`。
- `NameKey`、`DescriptionKey`、`CreditsKey` 和自定义标签使用 `"表名:键"` 格式；不含冒号的文本按原样显示。如果你自带含本地化表的 `.pck`，同样的键也能用。

PackmasterLib 自己的界面文本已覆盖全部 16 种语言。

## 9. 卡包设计规则（重要）

奖励、商店和「随机获得一张攻击牌」之类的效果**只从已选卡包中抽牌**。卡池过浅可能导致无牌可抽而闪退。请遵循 1 代卡包大师的规范：

- 每个卡包 **≥ 10 张**；
- **攻击 ≥ 2、技能 ≥ 2、能力 ≥ 2**；
- **普通 ≥ 2、罕见 ≥ 2、稀有 ≥ 2**；
- 仅多人可用（`MultiplayerOnly`）的牌在单人模式中会消失，不计入；
- 同一张卡在一个角色中**最多属于一个卡包**。

不满足时库会在日志中输出 `Pack '<id>' is thin`——第一次启动后请检查游戏日志（`%APPDATA%\SlayTheSpire2\logs\godot.log`）。

## 10. 测试

1. 编译安装，启动游戏，启用两个 mod 后重启。
2. 选择你的角色：选人界面右上角出现卡包配置面板。
3. 出发：涅奥之前弹出选包界面。
4. 局内打开控制台（`~`）：`packtest state` 输出并校验本局卡包状态；`packtest resolve [seed]` 检查所有已注册角色的槽位解析。

玩家看到的行为见[游戏内行为](behavior.md)，所有选项见 [API 参考](api.md)。
