using MegaCrit.Sts2.Core.Localization;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Helpers;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Monsters.GrandMageDad;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Services;

/// <summary>把成长展示成完整生命账单与具体物品账目，所有数量均来自开局快照。</summary>
internal static class GrandMageGrowthInfoService
{
    /// <summary>统一从中英文表取文案。</summary>
    private static string Text(string key, params object[] values)
        => string.Format(GmUiText.Get("dad.info." + key), values);

    /// <summary>常驻区直接给出基础、人数和构筑三部分，不让玩家猜最大生命的来历。</summary>
    internal static string Summary(GrandMageDad dad, GrandMageDadState state)
    {
        int baseHp = state.BaseHp > 0 ? state.BaseHp : dad.MinInitialHp;
        int partyHp = state.InitialMaxHp - state.GrowthHp;
        return Text("hpBill", state.InitialMaxHp, baseHp, state.PlayerCount, partyHp - baseHp, state.GrowthHp)
            + "\n" + Text("strengthBill", state.GrowthStrength, state.GrowthHp, state.Ascension >= 10 ? 5 : 10, state.PlayerCount);
    }

    /// <summary>将实际加分物品的名称放到常驻区，详细数量与规则在展开页查看。</summary>
    internal static string SourceNames(GrandMageDadState state)
    {
        string[] names = state.GrowthSources.SelectMany(source => source.Items.Where(item => CountsForGrowth(source, item)))
            .Select(Name).Distinct().ToArray();
        string reason = names.Length == 0 ? Text(state.AverageGrowth == 0 ? "zeroGrowth" : "legacyNames")
            : string.Join("、", names.Take(3)) + (names.Length > 3 ? Text("moreItems", names.Length) : string.Empty);
        return Text("namedSourceHint", reason);
    }

    /// <summary>优先解释最终血量和力量，然后展示从总分到血量的完整换算。</summary>
    internal static string Result(GrandMageDad dad, GrandMageDadState state)
    {
        int partyHp = state.InitialMaxHp - state.GrowthHp;
        int baseHp = state.BaseHp > 0 ? state.BaseHp : dad.MinInitialHp;
        return Summary(dad, state) + "\n" + Text(state.GrowthStrength == 0 ? "noStrengthReason" : "hasStrengthReason",
                state.GrowthHp, state.GrowthStrength, state.Ascension >= 10 ? 5 : 10, state.PlayerCount)
            + "\n\n" + Text("hpCalculation", baseHp, state.PlayerCount, partyHp - baseHp, partyHp,
                state.AverageGrowth, partyHp * state.AverageGrowth / 600m, state.GrowthHp, state.InitialMaxHp)
            + "\n" + Text("shieldCalculation", state.GrowthHp, GrandMageDadBalance.ChantBlock(state.GrowthHp));
    }

    /// <summary>按计分项列出真实物品名称、数量、单价与本项总分；共同上限单独说明。</summary>
    internal static string Items(GrandMageGrowthSource source)
    {
        if (source.Items.Count == 0) return Text("legacyNames");
        var lines = new List<string>();
        string[] categories = ["talismans", "masks", "allMasks", "coveredMasks", "demons", "demonCards", "demonEnchantments", "storedDemons", "awakenings", "upgrades", "otherRelics", "otherCards"];
        foreach (string category in categories)
        {
            var items = source.Items.Where(item => item.Category == category).ToList();
            if (items.Count == 0) continue;
            lines.AddRange(items.Select(item => Text("item." + category, Name(item), item.Count, item.Count * 2,
                Enum.TryParse<PanKuDemonQiKind>(item.DemonKind, out var kind) ? PanKuDemonQiHelper.GetDisplayName(kind) : string.Empty)));
            if (category == "talismans") lines.Add(Text("talismanSubtotal", source.Talismans, source.TalismanScore));
            if (category == "upgrades") lines.Add(Text("upgradeSubtotal", source.UpgradedCards, source.UpgradeScore));
        }
        return string.Join("\n", lines);
    }

    /// <summary>只选出实际有正分的物品类别，免费符咒和不计分遗物不会被误当成强化来源。</summary>
    private static bool CountsForGrowth(GrandMageGrowthSource source, GrandMageGrowthItem item)
        => item.Category switch
        {
            "talismans" => source.TalismanScore > 0,
            "masks" or "allMasks" => source.MaskScore > 0,
            "demons" => source.DemonScore > 0,
            "awakenings" => source.AwakeningScore > 0,
            "upgrades" => source.UpgradeScore > 0,
            _ => false
        };

    /// <summary>从模型本地化表解析名称，切换语言不会改变存档或计分顺序。</summary>
    private static string Name(GrandMageGrowthItem item)
        => item.Table == "demon" && Enum.TryParse<PanKuDemonQiKind>(item.Entry, out var kind)
            ? PanKuDemonQiHelper.GetDisplayName(kind)
            : new LocString(item.Table, item.Entry + ".title").GetFormattedText();
}
