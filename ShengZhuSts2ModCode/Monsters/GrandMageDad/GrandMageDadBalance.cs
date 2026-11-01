using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Cards;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards.Masks;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Helpers;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Relics;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Monsters.GrandMageDad;

/// <summary>老爹的构筑成长算法：普通牌不加难度，队伍平均评分只计算一次。</summary>
public static class GrandMageDadBalance
{
    /// <summary>根据已解锁的构筑能力计算单人成长评分。</summary>
    public static int CalculateScore(int talismans, int masks, int demonKinds, int awakenings, int upgradedCards)
    {
        return new GrandMageGrowthSource
        {
            Talismans = talismans, Masks = masks, DemonKinds = demonKinds,
            Awakenings = awakenings, UpgradedCards = upgradedCards
        }.Score;
    }

    /// <summary>取全队平均评分，交换玩家顺序不影响结果。</summary>
    public static int AverageScore(IEnumerable<int> scores)
    {
        int[] values = scores.Select(score => Math.Max(0, score)).ToArray();
        return values.Length == 0 ? 0 : values.Sum() / values.Length;
    }

    /// <summary>在已经由游戏放大的人数生命上按平均评分追加生命，不再限制成长上限。</summary>
    public static int ExtraHp(int nativeMaxHp, int averageScore)
    {
        return (int)((long)Math.Max(0, nativeMaxHp) * Math.Max(0, averageScore) / 600);
    }

    /// <summary>保留已有接口，按人数缩放后的生命成长计算全队共同的吟唱盾。</summary>
    public static int ChantBlock(int playerCount, int averageScore)
    {
        return ChantBlock(averageScore);
    }

    /// <summary>吟唱盾为100加生命成长的一半，小数舍去，不再另乘联机人数。</summary>
    public static int ChantBlock(int growthHp) => 100 + Math.Max(0, growthHp) / 2;

    /// <summary>A0至A9每10点生命成长换力量，A10每5点；除以开局人数后一次四舍五入。</summary>
    public static int CalculateStrength(int growthHp, int playerCount, int ascension)
        => (int)Math.Round(Math.Max(0, growthHp) / (ascension >= 10 ? 5m : 10m) / Math.Max(1, playerCount), MidpointRounding.AwayFromZero);

    /// <summary>本轮全来源生命伤害上限为256乘开局人数。</summary>
    public static int DamageCap(int playerCount) => 256 * Math.Clamp(playerCount, 1, 4);

    /// <summary>A0至A9剥夺每人两枚，A10剥夺每人三枚。</summary>
    public static int SealCount(int ascension) => ascension >= 10 ? 3 : 2;

    /// <summary>庇佑按开局参战人数共享，A0至A9每人3次，A10每人4次。</summary>
    public static int BlessingNegates(int playerCount, int ascension)
        => Math.Max(1, playerCount) * (ascension >= 10 ? 4 : 3);

    /// <summary>保留已有接口，超过本轮硬上限的生命伤害归零；查询不修改状态。</summary>
    public static decimal ReduceBurstDamage(decimal amount, int taken, int threshold)
    {
        if (amount <= 0 || threshold <= 0) return amount;
        return Math.Min(amount, Math.Max(0, threshold - taken));
    }

    /// <summary>从永久牌组和已有遗物采样，生成牌与临时能力不抬高评分。</summary>
    private static int ScorePlayer(Player player)
        => CaptureGrowthSource(player).Score;

    /// <summary>一次采样同时保存各项数量和实际分值，界面读取此快照而不影响游戏状态。</summary>
    private static GrandMageGrowthSource CaptureGrowthSource(Player player)
    {
        var cards = player.Deck.Cards;
        int masks = cards.Where(card => card is BaseMaskCard or TaLaMask).Select(card => card.Id).Distinct().Count();
        // 无尽黑暗觉醒会消耗十种面具牌，但这张牌仍能施加全部十种面具，不能把合成后的构筑误判为变弱。
        if (cards.Any(card => card is EndlessDarkness)) masks = 10;
        int awakenings = cards.Where(card => card is NothingLackingCard or EndlessDarkness or EightDemonPossessionCard)
            .Select(card => card.Id).Distinct().Count();
        var ownedDemonKinds = PanKuDemonQiHelper.CollectOwnedKinds(player, player.GetRelic<PanKuBox>());
        int demonKinds = ownedDemonKinds.Count;
        int upgrades = cards.Count(card => card.IsUpgraded && card.Rarity != CardRarity.Basic);
        var source = new GrandMageGrowthSource
        {
            OwnerNetId = player.NetId, Talismans = TalismanHelper.CountOwnedAnimalTalismans(player),
            Masks = masks, DemonKinds = demonKinds, Awakenings = awakenings, UpgradedCards = upgrades,
            Items = GrandMageGrowthCatalog.Capture(player, ownedDemonKinds)
        };
        MainFile.Logger.Info($"【老爹成长】玩家={player.NetId}，符咒={source.Talismans}，面具={masks}，魔气={demonKinds}，觉醒={awakenings}，升级牌={upgrades}，评分={source.Score}。");
        return source;
    }

    /// <summary>初始化当前遭遇的成长快照，避免多人倍率重复计算。</summary>
    internal static async Task Initialize(GrandMageDad monster)
    {
        var creature = monster.Creature;
        if (creature?.IsAlive != true || monster.CombatState?.Encounter is not GrandMageDadEncounter encounter)
            return;
        var state = encounter.State;
        encounter.ActiveMonster = monster;
        if (monster.GrowthAppliedToInstance) return;
        if (!state.GrowthInitialized)
        {
            state.PlayerCount = Math.Max(1, monster.CombatState.Players.Count);
            state.GrowthSources = monster.CombatState.Players.OrderBy(player => player.NetId).Select(CaptureGrowthSource).ToList();
            state.AverageGrowth = AverageScore(state.GrowthSources.Select(source => source.Score));
        }
        state.Ascension = monster.CombatState.RunState.AscensionLevel;
        state.BaseHp = monster.MinInitialHp;
        state.GrowthHp = ExtraHp(creature.MaxHp, state.AverageGrowth);
        state.GrowthStrength = CalculateStrength(state.GrowthHp, state.PlayerCount, state.Ascension);
        if (state.GrowthHp > 0) await CreatureCmd.GainMaxHp(creature, state.GrowthHp);
        state.InitialMaxHp = creature.MaxHp;
        state.GrowthInitialized = true;
        monster.GrowthAppliedToInstance = true;
        MainFile.Logger.Info($"【老爹成长】进阶={state.Ascension}，人数={state.PlayerCount}，平均评分={state.AverageGrowth}，追加生命={state.GrowthHp}，最终生命={state.InitialMaxHp}，力量={state.GrowthStrength}，力量除数={(state.Ascension >= 10 ? 5 : 10)}，吟唱盾={ChantBlock(state.GrowthHp)}，每轮承伤上限={DamageCap(state.PlayerCount)}。");
    }
}
