using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Models;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers.GrandMageDad;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Helpers;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Networking;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Relics;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Visuals;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Monsters.GrandMageDad;

/// <summary>临时封存符咒的战斗触发；保持遗物所有权与已有奖励不变。</summary>
public static class GrandMageDadSealService
{
    /// <summary>只在拥有存活老爹的当前战斗中读取封存，结束后自动失效。</summary>
    public static bool IsSealed(RelicModel? relic)
    {
        // 图鉴等界面会读取规范模型的状态，它们没有可访问的 Owner。
        if (relic?.IsMutable != true) return false;
        var combat = relic?.Owner?.Creature?.CombatState;
        return relic != null && combat?.Encounter is GrandMageDadEncounter encounter
            // IsOverOrEnding 会重新枚举结束钩子；此处只能读取状态位，否则击杀时会递归。
            && CombatManager.Instance.IsInProgress
            && combat.Enemies.Any(creature => creature.Monster is GrandMageDad && creature.IsAlive)
            && encounter.State.SealedTalismans.Any(seal => seal.OwnerNetId == relic.Owner.NetId
                && seal.RelicId == relic.Id.Entry && seal.RemainingTurns > 0);
    }

    /// <summary>每人封存两或三枚；先通过全队匹配尽量选不同类型，再补足重复和缺额。</summary>
    internal static async Task SealPlayers(GrandMageDad monster)
    {
        if (monster.Creature?.IsAlive != true || monster.CombatState?.Encounter is not GrandMageDadEncounter encounter) return;
        if (encounter.State.SealedTalismans.Count > 0 || monster.Creature.HasPower<GrandMageSealPower>()) return;
        int count = GrandMageDadBalance.SealCount(encounter.State.Ascension);
        var players = monster.CombatState.Players.Where(player => player.Creature.IsAlive).OrderBy(player => player.NetId).ToList();
        var candidates = players.Select(player => player.Relics.Where(relic => !relic.IsMelted && TalismanHelper.AnimalTalismanTypes.Contains(relic.GetType()))
            .DistinctBy(relic => relic.Id.Entry).OrderBy(relic => relic.Id.Entry, StringComparer.Ordinal).ToList()).ToList();
        var selected = SelectTalismans(candidates.Select(items => items.Select(item => item.Id.Entry).ToArray()).ToArray(), count);
        int missing = 0;
        var types = new HashSet<Type>();
        for (int index = 0; index < players.Count; index++)
        {
            missing += count - selected[index].Count;
            foreach (string id in selected[index])
            {
                var relic = candidates[index].Single(item => item.Id.Entry == id);
                types.Add(relic.GetType());
                encounter.State.SealedTalismans.Add(new GrandMageTalismanSeal { OwnerNetId = players[index].NetId, RelicId = id });
                MainFile.Logger.Info($"【老爹封存】玩家={players[index].NetId}，符咒={id}，期限=两个玩家回合；遗物仍归原玩家所有。");
            }
        }
        int duplicates = encounter.State.SealedTalismans.Count - types.Count;
        var power = await PowerCmd.Apply<GrandMageSealPower>(monster.Creature, 2, monster.Creature, null);
        if (power != null)
        {
            power.SetCompensation(duplicates, missing);
            await power.Configure(types, duplicates * 3 + missing * 2);
        }
        NotifyRelics(encounter);
        MainFile.Logger.Info($"【老爹封存】进阶={encounter.State.Ascension}，每人目标={count}，不同类型={types.Count}，重复={duplicates}，缺额={missing}，重复与缺额力量={duplicates * 3 + missing * 2}。");
    }

    /// <summary>稳定的增广匹配先最大化全队不同符咒数；每人剩余名额再用自己的未选符咒补足。</summary>
    internal static List<string>[] SelectTalismans(string[][] candidates, int count)
    {
        var slots = Enumerable.Range(0, candidates.Length).SelectMany(player => Enumerable.Repeat(player, count)).ToArray();
        var owners = new Dictionary<string, int>(StringComparer.Ordinal);
        // 每个类型只能占一个匹配名额；挪动已选类型可为符咒较少的玩家腾出名额。
        // 为当前名额寻找一种尚未使用、或可以让出原名额的符咒。
        bool Match(int slot, HashSet<string> visited)
        {
            foreach (string id in candidates[slots[slot]])
            {
                if (!visited.Add(id)) continue;
                if (!owners.TryGetValue(id, out int previous) || Match(previous, visited))
                {
                    owners[id] = slot;
                    return true;
                }
            }
            return false;
        }
        for (int slot = 0; slot < slots.Length; slot++) Match(slot, new HashSet<string>(StringComparer.Ordinal));
        var result = candidates.Select(_ => new List<string>()).ToArray();
        foreach (var pair in owners.OrderBy(pair => pair.Key, StringComparer.Ordinal)) result[slots[pair.Value]].Add(pair.Key);
        for (int player = 0; player < candidates.Length; player++)
            foreach (string id in candidates[player])
                if (result[player].Count < count && !result[player].Contains(id)) result[player].Add(id);
        return result;
    }

    /// <summary>每个玩家阵营回合只递减一次期限。</summary>
    internal static async Task AdvancePlayerTurn(GrandMageDadEncounter encounter)
    {
        foreach (var seal in encounter.State.SealedTalismans) seal.RemainingTurns--;
        int released = encounter.State.SealedTalismans.RemoveAll(seal => seal.RemainingTurns <= 0);
        var dad = encounter.ActiveMonster?.VisualCreature;
        if (dad?.GetPower<GrandMageSealPower>() is { } power)
        {
            await power.ReturnBorrowedCards();
            if (power.Amount <= 1) await PowerCmd.Remove(power);
            else await PowerCmd.ModifyAmount(power, -1, dad, null);
        }
        if (released > 0) MainFile.Logger.Info($"【老爹封存】自然到期解除={released}枚。");
        NotifyRelics(encounter);
    }

    /// <summary>破阵、老爹死亡或战斗结束时解除所有玩家的封存。</summary>
    internal static async Task ReleaseAll(GrandMageDadEncounter encounter, string reason)
    {
        int count = encounter.State.SealedTalismans.Count;
        encounter.State.SealedTalismans.Clear();
        var dad = encounter.ActiveMonster?.VisualCreature;
        if (dad?.GetPower<GrandMageSealPower>() is { } power) await PowerCmd.Remove(power);
        NotifyRelics(encounter);
        if (count > 0) MainFile.Logger.Info($"【老爹封存】原因={reason}，解除={count}枚，所有权未发生变更。");
    }

    /// <summary>归还与倒计时都刷新已有遗物格，状态读取不改变原有冷却或激发信息。</summary>
    private static void NotifyRelics(GrandMageDadEncounter encounter)
    {
        foreach (var player in encounter.ActiveMonster?.VisualCreature?.CombatState?.Players ?? [])
            foreach (var relic in player.Relics.Where(relic => TalismanHelper.AnimalTalismanTypes.Contains(relic.GetType())))
                GrandMageSealRelicDisplay.Notify(relic);
    }

    /// <summary>统一检查本地请求、动作队列与旧网络消息的实际执行。</summary>
    internal static bool BlocksInteraction(Player player, RelicInteractionType interaction, TalismanRelicType target)
    {
        // 只约束战斗交互，休息处取消确认等协议消息必须始终放行。
        if (interaction is not (RelicInteractionType.SetActivation or RelicInteractionType.RatTransform
            or RelicInteractionType.RatFormSwitch or RelicInteractionType.HorseRemoveDebuff
            or RelicInteractionType.MonkeyCombatTransform or RelicInteractionType.RabbitRefreshTarget)) return false;
        RelicModel? relic = target == TalismanRelicType.None ? null
            : player.Relics.FirstOrDefault(item => TalismanRelicTypeHelper.FromRelic(item) == target);
        bool blocked = IsSealed(relic)
            || (interaction == RelicInteractionType.RabbitRefreshTarget && IsSealed(player.GetRelic<RabbitTalisman>()));
        if (blocked) MainFile.Logger.Info($"【老爹封存】阻止临时封存中的交互：玩家={player.NetId}，交互={interaction}，目标={target}。");
        return blocked;
    }
}
