using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Context;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Monsters.GrandMageDad;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers.GrandMageDad;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Services;

/// <summary>将老爹的已结算状态转换为玩家可读说明；不修改状态，不调用随机或伤害命令。</summary>
internal static class GrandMageBattleInfoService
{
    /// <summary>使用统一文案格式化数字与规则。</summary>
    private static string Text(string key, params object[] values)
        => string.Format(GmUiText.Get("dad.info." + key), values);

    /// <summary>构建常驻提示，优先告诉玩家下一招、自己的应对方式和成长结果。</summary>
    internal static string BuildSummary(GrandMageDad dad, GrandMageDadState state)
    {
        Player? player = dad.CombatState.Players.FirstOrDefault(LocalContext.IsMe);
        return Text("next", DescribeMove(dad.NextMove.Id, state)) + "\n" + Text("summaryDamageHint") + "\n"
            + Text("current", DescribeCounter(dad, player)) + "\n"
            + (state.SealedTalismans.Count == 0 ? Text("noSeals") : Text("sealSummary", state.SealedTalismans.Count)) + "\n"
            + GrandMageGrowthInfoService.Summary(dad, state) + "\n" + GrandMageGrowthInfoService.SourceNames(state);
    }

    /// <summary>根据玩家实际拥有的阵法与剩余计数说明应对，免疫或清除后不冒充仍受压制。</summary>
    internal static string DescribeCounter(GrandMageDad dad, Player? player)
    {
        if (dad.CombatState.Encounter is not GrandMageDadEncounter encounter) return Text("free");
        if (dad.NextMove.Id == GrandMageDadMoveIds.Stagger) return Text("stagger");
        if (encounter.State.IsChanting) return Text("chant", dad.Creature.Block);
        var power = player?.Creature.GetPower<GrandMageCyclePower>();
        return power?.Amount switch
        {
            1 => Text("metal", power.RemainingMetalAttacks),
            2 => Text(power.WoodResolved ? "woodDone" : "wood"),
            3 => Text("water", power.RemainingWaterCards),
            4 => Text("fire", power.FirePercent),
            _ => Text("free")
        };
    }

    /// <summary>按真实下一招编号显示完整副效果，避免把上一招的副意图当成预告。</summary>
    internal static string DescribeMove(string id, GrandMageDadState state)
    {
        return id switch
        {
            GrandMageDadMoveIds.ThunderCombo => Text("move.thunder"),
            GrandMageDadMoveIds.SuppressionSeal => Text("move.seal", (state.PlayerCount > 1 ? 20 : 16) * state.PlayerCount),
            GrandMageDadMoveIds.TalismanPursuit => Text("move.pursuit", GrandMageDadBalance.SealCount(state.Ascension)),
            GrandMageDadMoveIds.BaguaBarrage => Text("move.bagua"),
            GrandMageDadMoveIds.UltimateSealChant => Text("move.chant", GrandMageDadBalance.ChantBlock(state.GrowthHp)),
            GrandMageDadMoveIds.UltimateSealExecute => Text("move.execute"),
            GrandMageDadMoveIds.Stagger => Text("move.stagger"),
            _ => Text("move.unknown")
        };
    }

    /// <summary>打开指定说明页，三页共用相同的实时数据入口。</summary>
    internal static string BuildDetails(GrandMageDad dad, GrandMageDadState state, int page)
        => page switch { 0 => BuildGrowth(dad, state), 1 => BuildCycle(dad, state), _ => BuildRules(dad, state) };

    /// <summary>展示各玩家的开局计分、全队平均以及最终生命力量护盾的来源。</summary>
    private static string BuildGrowth(GrandMageDad dad, GrandMageDadState state)
    {
        var sections = new List<string> { GrandMageGrowthInfoService.Result(dad, state), Text("growthIntro") };
        foreach (var source in state.GrowthSources)
        {
            sections.Add(Text("growthPlayer", PlayerName(dad, source.OwnerNetId), source.RawScore, source.Score) + "\n"
                + Text("growthItems", source.Talismans, source.TalismanScore, source.Masks, source.MaskScore,
                    source.DemonKinds, source.DemonScore, source.Awakenings, source.AwakeningScore,
                    source.UpgradedCards, source.UpgradeScore) + "\n" + GrandMageGrowthInfoService.Items(source));
        }
        sections.Add(state.GrowthSources.Count == 0 ? Text("legacyGrowth")
            : Text("average", string.Join(" + ", state.GrowthSources.Select(source => source.Score)), state.PlayerCount, state.AverageGrowth));
        sections.Add(Text("growthRules"));
        return string.Join("\n\n", sections);
    }

    /// <summary>展示固定六步及当前所在位置，失败与成功分支均明确说明。</summary>
    private static string BuildCycle(GrandMageDad dad, GrandMageDadState state)
    {
        string[] moves = [GrandMageDadMoveIds.ThunderCombo, GrandMageDadMoveIds.SuppressionSeal,
            GrandMageDadMoveIds.TalismanPursuit, GrandMageDadMoveIds.BaguaBarrage,
            GrandMageDadMoveIds.UltimateSealChant, GrandMageDadMoveIds.UltimateSealExecute, GrandMageDadMoveIds.Stagger];
        return Text("cycleIntro") + "\n\n" + Text("damageHint") + "\n\n"
            + Text("cycleCurrent", DescribeMove(dad.NextMove.Id, state)) + "\n\n"
            + string.Join("\n", moves.Select(id => DescribeMove(id, state)));
    }

    /// <summary>展示全队阵法实际状态、封存归属、破封进度与所有机制规则。</summary>
    private static string BuildRules(GrandMageDad dad, GrandMageDadState state)
    {
        int cap = GrandMageDadBalance.DamageCap(state.PlayerCount);
        var sections = new List<string>
        {
            string.Join("\n", dad.CombatState.Players.Where(player => player.Creature.IsAlive)
                .Select(player => PlayerName(dad, player.NetId) + "：" + DescribeCounter(dad, player))),
            Text("progress", state.DamageTakenThisTurn, Math.Max(0, cap - state.DamageTakenThisTurn)),
            Text("sealsTitle") + "\n" + DescribeSeals(dad, state),
            Text("capRules", state.PlayerCount, cap),
            Text("blessingRules", dad.Creature.GetPower<GrandMageBlessingPower>()?.Amount ?? 0,
                GrandMageDadBalance.BlessingNegates(state.PlayerCount, state.Ascension), state.PlayerCount, state.Ascension),
            Text("sealRules"), GrandMageSealInfoService.Rules(), Text("formationRules"), Text("curseRules")
        };
        return string.Join("\n\n", sections);
    }

    /// <summary>按归属显示每枚封存符咒及自然到期倒计时。</summary>
    internal static string DescribeSeals(GrandMageDad dad, GrandMageDadState state)
    {
        return GrandMageSealInfoService.Current(dad, state);
    }

    /// <summary>用队伍顺序标识玩家，只有本地标注使用当前视角，不参与任何战斗结算。</summary>
    private static string PlayerName(GrandMageDad dad, ulong ownerId)
    {
        int index = dad.CombatState.Players.ToList().FindIndex(player => player.NetId == ownerId);
        var player = index >= 0 ? dad.CombatState.Players[index] : null;
        return Text("player", index + 1) + (player != null && LocalContext.IsMe(player) ? Text("you") : string.Empty);
    }

}
