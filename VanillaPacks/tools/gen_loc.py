import json

mapping = {
 "pack_strike": "strike_ironclad",
 "pack_twin_strike": "twin_strike",
 "pack_pommel_strike": "pommel_strike",
 "pack_iron_wave": "iron_wave",
 "pack_anger": "anger",
 "pack_thunderclap": "thunderclap",
 "pack_bash": "bash",
 "pack_defend": "defend_ironclad",
 "pack_shrug_it_off": "shrug_it_off",
 "pack_backflip": "backflip",
 "pack_armaments": "armaments",
 "pack_flame_barrier": "flame_barrier",
 "pack_second_wind": "second_wind",
 "pack_escape_plan": "escape_plan",
 "pack_inflame": "inflame",
 "pack_rupture": "rupture",
 "pack_battle_trance": "battle_trance",
 "pack_bloodletting": "bloodletting",
 "pack_juggernaut": "juggernaut",
 "pack_demon_form": "demon_form",
 "pack_corruption": "corruption",
 "pack_neutralize": "neutralize",
 "pack_prepared": "prepared",
 "pack_acrobatics": "acrobatics",
 "pack_expertise": "expertise",
 "pack_reflex": "reflex",
 "pack_headbutt": "headbutt",
}

preview_loc = {
 "eng": {
  "pack_strikes_preview.title": "Strikes Pack",
  "pack_strikes_preview.description": "Offense ****   Defense **   Scaling ***\nAttack cards: strikes, multi-hits and vulnerable setup.",
  "pack_defends_preview.title": "Bulwark Pack",
  "pack_defends_preview.description": "Defense *****   Offense **   Scaling **\nBlock tools, armor synergy and exhaust value.",
  "pack_powers_preview.title": "Arcana Pack",
  "pack_powers_preview.description": "Scaling *****   Offense *   Defense *\nPowers and form cards for long fights.",
  "pack_tricks_preview.title": "Tricks Pack",
  "pack_tricks_preview.description": "Flexibility ****\nCheap skills: draw, discard, weaken and recycle.",
 },
 "zhs": {
  "pack_strikes_preview.title": "打击包",
  "pack_strikes_preview.description": "进攻 ★★★★☆  防御 ★★☆☆☆  成长 ★★★☆☆\n以攻击卡为主：打击、多重打击与易伤铺场。",
  "pack_defends_preview.title": "壁垒包",
  "pack_defends_preview.description": "防御 ★★★★★  进攻 ★★☆☆☆  成长 ★★☆☆☆\n格挡、护甲联动与消耗收益。",
  "pack_powers_preview.title": "奥能包",
  "pack_powers_preview.description": "成长 ★★★★★  进攻 ★☆☆☆☆  防御 ★☆☆☆☆\n力量与形态能力，后期核心。",
  "pack_tricks_preview.title": "诡计包",
  "pack_tricks_preview.description": "灵活 ★★★★☆\n过牌、弃牌、削弱与循环利用的廉价技能。",
 },
}

out = {}
for lang, path in (("eng", r"D:\dev\mod\SlayTheSpire2\localization\eng\cards.json"), ("zhs", r"D:\dev\mod\SlayTheSpire2\localization\zhs\cards.json")):
    table = json.load(open(path, encoding="utf-8"))
    d = {}
    for ours, src in mapping.items():
        for suffix in (".title", ".description"):
            key = src.upper() + suffix
            if key not in table:
                print("MISSING", lang, key)
                continue
            d[ours.upper() + suffix] = table[key]
    d.update({k.split(".")[0].upper() + "." + k.split(".")[1]: v for k, v in preview_loc[lang].items()})
    out[lang] = d

def esc(s):
    return s.replace("\\", "\\\\").replace('"', '\\"').replace("\n", "\\n")

def cs_dict(name, data):
    lines = [f"\tpublic static readonly IReadOnlyDictionary<string, string> {name} = new Dictionary<string, string>", "\t{"]
    for k, v in data.items():
        lines.append(f'\t\t["{k}"] = "{esc(v)}",')
    lines.append("\t};")
    return "\n".join(lines)

cs = ('using System.Collections.Generic;\n\nnamespace Sts2Packmaster.VanillaPacks;\n\n'
      '/// <summary>Generated from the vanilla localization tables (tools/gen_loc.py).</summary>\n'
      'public static class VanillaPackLoc\n{\n'
      + cs_dict("CardsEn", out["eng"]) + "\n\n" + cs_dict("CardsZhs", out["zhs"]) + "\n}\n")
open("VanillaPackLoc.cs", "w", encoding="utf-8", newline="\n").write(cs)
print("entries:", {k: len(v) for k, v in out.items()})
