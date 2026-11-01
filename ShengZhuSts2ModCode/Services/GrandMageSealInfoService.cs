using MegaCrit.Sts2.Core.Models;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Helpers;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Monsters.GrandMageDad;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers.GrandMageDad;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Services;

/// <summary>将封印归属、倒计时和塔1增益映射转成统一文案，所有入口只读战斗状态。</summary>
internal static class GrandMageSealInfoService
{
    /// <summary>统一格式化当前语言的封印提示。</summary>
    internal static string Text(string key, params object[] values)
        => string.Format(GmUiText.Get("dad.info." + key), values);

    /// <summary>返回当前遗物剩余封印回合，非当前战斗或已解除时返回零。</summary>
    internal static int Remaining(RelicModel relic)
        => GrandMageDadSealService.IsSealed(relic) && relic.Owner.Creature.CombatState?.Encounter is GrandMageDadEncounter encounter
            ? encounter.State.SealedTalismans.FirstOrDefault(seal => seal.OwnerNetId == relic.Owner.NetId && seal.RelicId == relic.Id.Entry)?.RemainingTurns ?? 0 : 0;

    /// <summary>类型名只用于稳定查键；显示名称和说明均由本地化入口提供。</summary>
    internal static string Benefit(Type type) => Text("sealEffect." + type.Name);

    /// <summary>遗物悬停说明包含剩余回合和老爹获得的效果。</summary>
    internal static string RelicHint(RelicModel relic)
        => Text("sealHover", Remaining(relic), Benefit(relic.GetType()));

    /// <summary>当前封印逐枚列出玩家、遗物、回合及对应效果，并单列重复与缺额补偿。</summary>
    internal static string Current(GrandMageDad dad, GrandMageDadState state)
    {
        var lines = new List<string>();
        foreach (var seal in state.SealedTalismans.OrderBy(seal => seal.OwnerNetId).ThenBy(seal => seal.RelicId, StringComparer.Ordinal))
        {
            var player = dad.CombatState.Players.FirstOrDefault(player => player.NetId == seal.OwnerNetId);
            var relic = player?.Relics.FirstOrDefault(relic => relic.Id.Entry == seal.RelicId);
            if (relic == null) continue;
            int index = dad.CombatState.Players.ToList().IndexOf(player!) + 1;
            lines.Add(Text("sealEntry", index, relic.Title.GetFormattedText(), seal.RemainingTurns, Benefit(relic.GetType())));
        }
        if (dad.Creature.GetPower<GrandMageSealPower>() is { } power)
        {
            if (power.DuplicateStrength + power.MissingStrength > 0)
                lines.Add(Text("sealCompensation", power.DuplicateStrength, power.MissingStrength, power.Amount));
            foreach (var card in power.BorrowedCards)
                lines.Add(Text("sealBorrowed", dad.CombatState.Players.ToList().IndexOf(card.Owner) + 1, card.Title));
        }
        return lines.Count == 0 ? Text("noSeals") : string.Join("\n", lines);
    }

    /// <summary>完整十二符咒遗物对照表，鼠符咒明确没有额外增益。</summary>
    internal static string Rules() => Text("sealBonusTitle") + "\n" + string.Join("\n", TalismanHelper.AnimalTalismanTypes.Select(type =>
        ModelDb.AllRelics.First(relic => relic.GetType() == type).Title.GetFormattedText() + "：" + Benefit(type)));
}
