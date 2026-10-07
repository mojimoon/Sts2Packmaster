# STS2 卡包大师工具库 + 演示角色

复刻《杀戮尖塔1》卡包大师（The Packmaster）核心体验的 **Slay the Spire 2** 模组套件。
**零第三方依赖**（不依赖 BaseLib / RitsuLib，只用游戏原生 Modding API + Harmony）。

| Mod | ID | 作用 |
|---|---|---|
| `PackmasterLib\` | `PackmasterLib` | 工具库：卡包注册 API、选人页右侧配置面板、开局选卡包（三选一）、奖励走卡池、图鉴按卡包排序/搜索、运行存档持久化；**库自带全部 16 种原版语言的 UI 词条** |
| `VanillaPacks\` | `VanillaPacks` | 演示角色 **Vanilla Slinger**（依赖上面的库）：27 张原版卡分成 4 个卡包 + 逻辑自动测试 |

对应游戏版本：**v0.111.0**（net9.0）。详细设计见 [DESIGN.md](DESIGN.md)。

## 构建 / 安装

```
powershell -ExecutionPolicy Bypass -File build-and-install.ps1
```

自动编译并部署到 `D:\SteamLibrary\steamapps\common\Slay the Spire 2\mods\`。
（首次启用 mod 需在游戏里确认"加载模组"弹窗并重启游戏。）

## 玩法（Vanilla Slinger）

1. 角色选择页选中 **原版卡包师 / Vanilla Slinger**（复用铁甲战士的立绘/图标）→ 右上角**卡包配置**面板（点标题折叠/展开）：
   - **全卡包**：跳过选择，开局直接获得全部（已解锁）卡包（对应 1 代 AllPacks）；
   - **卡包数量**：3-10 个槽位（7 个 = 1 代默认，不遮挡出发按钮）；
   - 每个槽位：**随机 / 三选一 / 无 / 指定卡包**；配置按角色自动保存（`user://PackmasterLib/config.json`）。
   **设置 → 游戏设置 → 卡包大师**（可折叠，对应 1 代 mod 设置 + 开发选项，`user://PackmasterLib/settings.json`）：
   - 允许多个"无"槽位（1 代 AllowMultipleNONE；关闭时多余的"无"按随机处理）；
   - [开发] 解锁全部卡包（忽略角色 mod 的解锁规则）；
   - [开发] 跳过选包："三选一"槽开局直接随机（仅单人）。
2. 开局后如果该存档已解锁 Neow（先古），选包界面会以 Neow 选项形式出现，**三选一**槽位逐轮弹出选包界面，预览卡上带作者写的**星级评分摘要**；
   若没有 Neow（未解锁纪元），剩余三选一槽会在第一次发卡奖励前自动随机补齐并记日志。
3. 之后所有战斗/商店/事件的卡牌奖励只从**已选卡包**抽取（其他来源的无色卡等仍正常出现）。
4. 图鉴：卡包角色的卡显示在该角色自己的池里；左侧有该角色的**池过滤按钮**；侧栏新增**"卡包"排序按钮**（按卡包 → 稀有度 → 名称分组）；**搜索框输入卡包名**可以过滤出整个卡包。

## 演示内容的 4 个卡包

| 卡包 | 内容 |
|---|---|
| 打击包 | 打击、双重打击、剑柄猛击、铁波浪、怒气、雷鸣一击、痛击 |
| 壁垒包 | 防御、耸肩、后空翻、武装、烈焰屏障、喘息、逃跑计划 |
| 奥能包 | 燃烧、破裂、战斗恍惚、放血、驾驭者、恶魔形态、腐化 |
| 诡计包 | 中和、筹备、杂技、专长、本能反应、头槌 |

卡为原版卡副本（原版卡类是 sealed），完整复制原版行为并借用原版卡图；预览卡/评分文本在 `VanillaPacks\src\VanillaPackLoc.cs`（由 `tools\gen_loc.py` 从原版本地化生成）。

## 给角色 mod 作者的 API

```csharp
[ModInitializer(nameof(Init))]
public static void Init()
{
    PackmasterApi.AddLoc("cards", "eng", myCardsEn);   // 免 pck 本地化（也可用 pck）
    PackmasterApi.AddLoc("cards", "zhs", myCardsZhs);
    PackmasterApi.RegisterCharacter(new PackCharacterRegistration {
        CharacterType = typeof(MyCharacter),           // 你的 CharacterModel 子类
        Packs = new[] { new PackDefinition { Id = "my_pack", ..., CardTypes = ..., PreviewCardType = ... } },
        DefaultSlots = new[] { "my_pack", "random", "choice" },
    });
}
```

- 卡池类：让 `GenerateAllCards()` 返回 `PackRegistry.GetPoolCards(registration)`，并 override `FilterThroughEpochs` 排除预览卡（见 `VanillaSlingerCardPool`）。
- 无美术可用 `RedirectedCharacterModel`（复用某个原版角色的全部视觉/sfx 资产路径）。
- 卡包解锁：默认全部解锁；`registration.IsPackUnlocked = pack => ...` 自定义（可随时由任意 mod 修改），`registration.UnlockedPacks` 查询。未解锁的包不会被随机/三选一/全卡包/配置面板提供。
- 库设置：`PackmasterSettings.AllowMultipleNone / UnlockAllPacks / AutoResolveChoices` 可读写。
- 卡牌用 `PackCardModel`（指定原版卡图来源）/ 预览卡用 `PackPreviewCard`。
- 卡框、能量图标、遗物/药水池是否与原版共享：全部由角色作者在自己的 `CardPoolModel`/`CharacterModel` 里配置，库不干预。

## 测试（卡包选择逻辑准确性）

**无头自动测试**（不需要操作 UI，自动开局、断言、自动退出）：

```
SlayTheSpire2.exe --headless --packmastertest --force-steam=off   # 去掉 --headless 则额外截图到 user://packtest_*.png；测试运行全程静音
grep "PackmasterLib-AutoTest" <日志>
```

最近一次结果：`RESULT: 1042 checks, 0 failures`（autotest14.log）。
覆盖：角色重定向资源全部存在、选人页真实点选角色、选人面板与设置页卡包区（12 行翻页控件、改动即写入配置）、ModelDb 注册与解锁、卡池归属、预览卡排除、50 组种子×5 种槽位配置的解析不变量（无重复/固定槽生效/合法包/三选一候选数）、真实开局（modifier 挂载、固定/随机/三选一解析）、30 张奖励卡全部属于已选卡包、未选三选一兜底、SavedProperty 存档往返、图鉴注入（9 个池过滤器/排序按钮/搜索注册/卡包分组排序）、**全部 16 种原版语言的词条注入与回退**。

**游戏内控制台**（mod 状态下按 `~`）：
- `packtest resolve [seed]` — 解析不变量断言
- `packtest state` — 本局卡包状态与池校验
- `packgive <strikes|defends|powers|tricks>` — 体验用：把整包卡加进牌库
- `packtestreward [rounds]` — 生成奖励并断言全部来自已选卡包
