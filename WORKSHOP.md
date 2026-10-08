# Steam Workshop descriptions

BBCode for the two Workshop items, English and Chinese. Fill in the empty `[url=]` links once both
items are published. Upload `screenshots/*.jpg` as the item's preview images.

- PackmasterLib: <!-- workshop link: TODO -->
- VanillaPacks: <!-- workshop link: TODO -->

---

## PackmasterLib — English

Title: `PackmasterLib`

```bbcode
A library that brings the core of STS1's [b]The Packmaster[/b] to Slay the Spire 2: [b]card-pack characters[/b] whose card pool is assembled from packs drafted at the start of each run.

[pullquote]This is a library for mod authors. It does nothing on its own — install it together with a character mod that uses it.[/pullquote]

Want to try it? Subscribe to [url=][b]Vanilla Packs[/b][/url], a test character with 11 packs made of vanilla cards.

[h1]Features[/h1]
[list]
[*][b]Pack config on character select[/b]: All Packs mode, 3–10 slots, each Random / Choice of 3 / None / a specific pack. Saved per character.
[*][b]Pack draft before Neow[/b] (STS1 setup screen): current packs on top, choice of 3 below, pack summary with star ratings and tags on hover. Back / Esc hides the screen to look at Neow or the map. You can save & quit before confirming. Neow's rewards are untouched.
[*][b]Pack card pool[/b]: card rewards, shops, events, potions, "random card" effects and transforms only use the run's packs.
[*][b]Pack names on cards[/b], on the top edge of the card frame. Vanilla cards keep their original frame (optional "One frame for all").
[*][b]Packs button[/b] in the top bar: lists the run's packs; click to view the run's card pool.
[*][b]Card library[/b]: pack sort button, pack filter dropdown and search by pack name.
[*][b]Settings → General → Packmaster[/b]: One frame for all, multiple "None" slots, plus developer options.
[*]Dependency-free, save-safe, multiplayer-compatible, UI in all 16 game languages.
[/list]

[h1]For mod authors[/h1]
Download the source from [url=https://github.com/mojimoon/Sts2Packmaster]GitHub[/url] and read the documentation in [b]docs/en/[/b] (Chinese: [b]docs/zh/[/b]):
[list]
[*][b]Getting started[/b] — project setup, character, card pool, packs, localization, pack design rules.
[*][b]API reference[/b] — registration options, custom pack drawing, unlocks, shared packs, settings, queries.
[*][b]In-game behavior[/b] — draft rules, card pool, UI, saves, multiplayer, testing.
[/list]
Vanilla Packs in the same repository is a complete working example.

Add PackmasterLib as a dependency in your manifest ([b]"dependencies": [{ "id": "PackmasterLib", "min_version": "0.1.0" }][/b]) and as a Required Item of your Workshop item.

[h1]Compatibility[/h1]
Game version v0.111.0 or later. Only affects characters registered with the library.

[pullquote]Comments, bug reports and suggestions are welcome on GitHub![/pullquote]

GitHub repo: [url=https://github.com/mojimoon/Sts2Packmaster]mojimoon/Sts2Packmaster[/url]
```

## PackmasterLib — 中文

标题：`PackMasterLib / 卡包大师Lib`

```bbcode
复刻《杀戮尖塔》1 代 [b]卡包大师（The Packmaster）[/b] 核心玩法的杀戮尖塔2工具库：[b]卡包角色[/b]的卡池由每局开始时选出的卡包组成。

[pullquote]这是给 mod 作者使用的前置库，单独安装没有任何效果——请和使用它的角色 mod 一起安装。[/pullquote]

想体验一下？订阅 [url=][b]原版卡包[/b][/url]，一个由原版卡组成 11 个卡包的测试角色。

[h1]功能[/h1]
[list]
[*][b]选人页卡包配置[/b]：全卡包模式、3–10 个槽位，每个槽位可选 随机 / 三选一 / 无 / 指定卡包，按角色保存。
[*][b]涅奥之前选包[/b]（1 代选包界面）：上方为已选定卡包，下方三选一，悬停显示带星级评分和标签的卡包概要。返回按钮 / Esc 可收起界面查看涅奥或地图。确认前可以 SL。涅奥奖励不受影响。
[*][b]卡包卡池[/b]：卡牌奖励、商店、事件、药水、「随机一张牌」效果和变化只使用本局卡包。
[*][b]卡面包名[/b]：显示在卡框上边缘。原版卡保留原卡框（可选「统一卡框」）。
[*][b]右上角卡包按钮[/b]：列出本局卡包，点击查看本局牌池。
[*][b]图鉴[/b]：卡包排序按钮、卡包筛选下拉框，可搜索卡包名。
[*][b]设置 → 游戏设置 → 卡包大师[/b]：统一卡框、允许多个「无」槽位，以及开发选项。
[*]免依赖，支持S/L与多人模式，界面覆盖游戏全部 16 种语言。
[/list]

[h1]给 mod 作者[/h1]
请前往 [url=https://github.com/mojimoon/Sts2Packmaster]GitHub[/url] 下载源码，并阅读 [b]docs/zh/[/b] 中的文档（英文版：[b]docs/en/[/b]）：
[list]
[*][b]入门[/b]——项目配置、角色、卡池、卡包、本地化、卡包设计规则。
[*][b]API 参考[/b]——注册选项、自定义抽取算法、解锁、共享卡包、设置、查询。
[*][b]游戏内行为[/b]——候选规则、卡池、界面、存档、多人、测试。
[/list]
同一仓库中的原版卡包是一个完整可运行的示例。

在清单中把 PackmasterLib 声明为依赖（[b]"dependencies": [{ "id": "PackmasterLib", "min_version": "0.1.0" }][/b]），并在创意工坊物品中添加为「必需物品」。

[h1]兼容性[/h1]
游戏版本 v0.111.0 及以上。只影响注册到本库的角色。

[pullquote]欢迎在 GitHub 上留言、反馈 bug 和提出建议！[/pullquote]

GitHub 仓库：[url=https://github.com/mojimoon/Sts2Packmaster]mojimoon/Sts2Packmaster[/url]
```

---

## VanillaPacks — English

Title: `Vanilla Packs (Packmaster Demo)`

```bbcode
A [b]test / demo character[/b] for [url=][b]PackmasterLib[/b][/url]: [b]The Vanilla Slinger[/b], who builds a deck from card packs made of vanilla cards — like STS1's The Packmaster.

[pullquote]Requires [url=][b]PackmasterLib[/b][/url]. This mod mainly exists to test the library and to serve as an example for mod authors, and is not a custom playable character.[/pullquote]

[h1]How to play[/h1]
[list]
[*]Pick [b]The Vanilla Slinger[/b] on character select. Configure your packs in the panel at the top right: All Packs, or 3–10 slots of Random / Choice of 3 / None / a specific pack. Default: 4 random + 3 drafted packs.
[*]Before Neow, draft your packs. Hover a pack to see its ratings and tags.
[*]Card rewards, shops and random card effects only offer cards from your packs.
[*]Starting deck: 4 Strike, 4 Defend, Bash, Neutralize; relic: Burning Blood.
[/list]

[h1]The 11 packs[/h1]
Each pack has 11–13 vanilla cards, with at least 2 Attacks, Skills and Powers and at least 2 Commons, Uncommons and Rares.
[list]
[*][b]Pain Ignorance[/b] — Ironclad, self-damage
[*][b]Iron Wall[/b] — Ironclad, Block
[*][b]Doom Approaches[/b] — Necrobinder, Doom
[*][b]Bone Friends[/b] — Necrobinder, Osty
[*][b]Toxic Outbreak[/b] — Silent, Poison
[*][b]Phantom Blades[/b] — Silent, Shivs
[*][b]Primordial Chaos[/b] — Regent, card generation
[*][b]Kingdom's Arms[/b] — Regent, Forge
[*][b]Starlight[/b] — Regent, Stars
[*][b]Lightning Storm[/b] — Defect, Lightning
[*][b]Frost Fortress[/b] — Defect, Frost
[/list]

[h1]For mod authors[/h1]
The full source is in the Sts2Packmaster repository on GitHub see the [b]VanillaPacks/[/b] folder and the documentation in [b]docs/en/[/b]. It also contains console commands [b]packgive <packId>[/b] and [b]packtestreward[/b].

GitHub repo: [url=https://github.com/mojimoon/Sts2Packmaster]mojimoon/Sts2Packmaster[/url]
```

## VanillaPacks — 中文

标题：`原版卡包（卡包大师演示）`

```bbcode
[url=][b]PackMasterLib / 卡包大师Lib[/b][/url] 的[b]测试 / 演示角色[/b]：[b]原版卡包师[/b]，用原版卡组成的卡包构筑牌组——就像 1 代的卡包大师。

[pullquote]需要前置 [url=][b]PackMasterLib[/b][/url]。本 mod 主要用于测试工具库、作为示例，不是一个全新的可玩角色。[/pullquote]

[h1]玩法[/h1]
[list]
[*]在选人界面选择[b]原版卡包师[/b]，在右上角面板中配置卡包：全卡包，或 3–10 个 随机 / 三选一 / 无 / 指定卡包 的槽位。默认：4 个随机 + 3 个三选一。
[*]涅奥之前选择卡包，悬停可查看卡包的评分和标签。
[*]卡牌奖励、商店和随机卡效果只提供你的卡包中的卡。
[*]初始牌组：4 打击、4 防御、痛击、中和；遗物：燃烧之血。
[/list]

[h1]11 个卡包[/h1]
每个卡包 11–13 张原版卡，攻击/技能/能力各至少 2 张，普通/罕见/稀有各至少 2 张。
[list]
[*][b]疼痛无视[/b]——铁甲战士·自伤
[*][b]铜墙铁壁[/b]——铁甲战士·格挡
[*][b]灾厄将至[/b]——亡灵契约师·灾厄
[*][b]骸骨之友[/b]——亡灵契约师·奥斯提
[*][b]毒性爆发[/b]——静默猎手·中毒
[*][b]幻影之刃[/b]——静默猎手·小刀
[*][b]混沌之初[/b]——储君·生成卡牌
[*][b]王国兵器[/b]——储君·铸造
[*][b]星辰之力[/b]——储君·星辉
[*][b]闪电风暴[/b]——故障机器人·闪电
[*][b]冰霜堡垒[/b]——故障机器人·冰霜
[/list]

[h1]给 mod 作者[/h1]
完整源码在 GitHub 的 Sts2Packmaster 仓库中，见 [b]VanillaPacks/[/b] 目录和 [b]docs/zh/[/b] 中的文档。其中还包含控制台命令 [b]packgive <packId>[/b]、[b]packtestreward[/b]。

GitHub 仓库：[url=https://github.com/mojimoon/Sts2Packmaster]mojimoon/Sts2Packmaster[/url]
```
