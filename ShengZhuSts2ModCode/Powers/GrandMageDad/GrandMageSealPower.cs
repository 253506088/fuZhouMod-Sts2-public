using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Helpers;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Monsters.GrandMageDad;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Relics;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers.GrandMageDad;

/// <summary>两回合封存带来的塔1加成；同类只生效一次，归还时撤回未消耗加成和借牌。</summary>
public class GrandMageSealPower : ShengZhuSts2ModPower
{
    private HashSet<Type> _types = [];
    private List<CardModel> _borrowedCards = [];
    private int _strengthBonus;
    private int _artifactBonus;
    private int _flightBonus;
    private int _sealedTurn;
    private CardModel? _lastBorrowedCard;
    private PowerModel? _strengthPower;
    private PowerModel? _artifactPower;
    private PowerModel? _flightPower;

    /// <summary>模型浅拷贝时隔离本批借牌和加成，避免不同战斗共用列表。</summary>
    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        _types = [];
        _borrowedCards = [];
        _strengthBonus = _artifactBonus = _flightBonus = _sealedTurn = 0;
        _lastBorrowedCard = null;
        _strengthPower = _artifactPower = _flightPower = null;
        DuplicateStrength = MissingStrength = 0;
    }

    /// <summary>展示本批不同类型以及重复、缺额产生的补偿，不在界面重新计算。</summary>
    internal IReadOnlySet<Type> SealedTypes => _types;
    /// <summary>本批重复符咒带来的力量。</summary>
    internal int DuplicateStrength { get; private set; }
    /// <summary>本批缺额带来的力量。</summary>
    internal int MissingStrength { get; private set; }
    /// <summary>当前借走的原牌，供情报展示归属与名称。</summary>
    internal IReadOnlyList<CardModel> BorrowedCards => _borrowedCards;

    /// <summary>保存匹配结果中的补偿来源，便于展示与排查。</summary>
    internal void SetCompensation(int duplicates, int missing)
    {
        DuplicateStrength = duplicates * 3;
        MissingStrength = missing * 2;
    }

    /// <summary>封存是老爹的临时增益。</summary>
    public override PowerType Type => PowerType.Buff;
    /// <summary>层数表示剩余玩家回合。</summary>
    public override PowerStackType StackType => PowerStackType.Counter;
    /// <summary>复用既有五行阵图标。</summary>
    public override string CustomPackedIconPath => "res://ShengZhuSts2Mod/images/powers/grand_mage_cycle.svg";
    /// <summary>大图复用既有五行阵图标。</summary>
    public override string CustomBigIconPath => CustomPackedIconPath;

    /// <summary>检查本批封存是否包含指定类型。</summary>
    internal bool Has<T>() => _types.Contains(typeof(T));
    /// <summary>兔符咒为每次普通攻击增加一段。</summary>
    internal int ExtraHits => Has<RabbitTalisman>() ? 1 : 0;
    /// <summary>蛇仅隐藏剥夺后的下次行动副意图。</summary>
    internal bool HideSecondary => Has<SnakeTalisman>() && Owner?.CombatState?.Encounter is GrandMageDadEncounter encounter
        && encounter.State.TurnIndex == _sealedTurn;

    /// <summary>按不同类型施加一次加成，重复每枚加3力量，缺额每枚加2力量。</summary>
    internal async Task Configure(HashSet<Type> types, int compensation)
    {
        if (Owner?.IsAlive != true || Owner.CombatState == null) return;
        _types = types;
        _sealedTurn = (Owner.CombatState.Encounter as GrandMageDadEncounter)?.State.TurnIndex ?? 0;
        _strengthBonus = compensation + (Has<OxTalisman>() ? 4 : 0);
        if (_strengthBonus > 0) _strengthPower = await PowerCmd.Apply<StrengthPower>(Owner, _strengthBonus, Owner, null);
        if (Has<TigerTalisman>())
        {
            _artifactBonus = 2;
            int existing = Owner.GetPower<ArtifactPower>()?.Amount ?? 0;
            _artifactPower = await PowerCmd.Apply<ArtifactPower>(Owner, 2, Owner, null);
            // 原生人工制品首次施加会另加人数，本次剥夺规定同种固定只加两层。
            _artifactPower?.SetAmount(existing + 2);
        }
        if (Has<RoosterTalisman>())
        {
            _flightBonus = 3;
            _flightPower = await PowerCmd.Apply<FlightAfuPower>(Owner, 3, Owner, null);
        }
        if (Has<DogTalisman>()) Owner.GetPower<GrandMageDamageCapPower>()?.SetPerHitCap(45);
        if (Has<SheepTalisman>())
            foreach (var player in Owner.CombatState.Players.Where(player => player.Creature.IsAlive).OrderBy(player => player.NetId))
                await PowerCmd.Apply<GrandMageSheepTaxPower>(player.Creature, 1, Owner, null);
        MainFile.Logger.Info($"【老爹封存加成】类型={string.Join("、", types.Select(type => type.Name))}，临时力量={_strengthBonus}，期限={Amount}玩家回合。");
    }

    /// <summary>记录本批人工制品与飞行的消耗，归还时不扣除其他来源层数。</summary>
    public override Task AfterPowerAmountChanged(PlayerChoiceContext choiceContext, PowerModel power, decimal amount, Creature? applier, CardModel? cardSource)
    {
        if (power.Owner == Owner && amount < 0)
        {
            if (power == _strengthPower) _strengthBonus = Math.Max(0, _strengthBonus + (int)amount);
            if (power == _artifactPower) _artifactBonus = Math.Max(0, _artifactBonus + (int)amount);
            if (power == _flightPower) _flightBonus = Math.Max(0, _flightBonus + (int)amount);
        }
        return Task.CompletedTask;
    }

    /// <summary>马按已损生命的八分之一回复；猴借走全队抽弃牌堆的一张非状态非诅咒牌。</summary>
    public override async Task BeforeSideTurnStart(PlayerChoiceContext choiceContext, CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (Owner?.IsAlive != true || Owner.CombatState == null || side != Owner.Side) return;
        if (Has<HorseTalisman>()) await CreatureCmd.Heal(Owner, (Owner.MaxHp - Owner.CurrentHp) / 8);
        if (!Has<MonkeyTalisman>()) return;
        await ReturnBorrowedCards();
        var players = combatState.Players.Where(player => player.Creature.IsAlive).OrderBy(player => player.NetId).ToList();
        var cards = players.SelectMany(player => player.PlayerCombatState?.AllPiles
            .Where(pile => pile.Type is PileType.Draw or PileType.Discard).SelectMany(pile => pile.Cards) ?? [])
            .Where(card => card.Type is not (CardType.Curse or CardType.Status)).ToList();
        // 沿用塔1的避免连续借同一张牌规则；有其他候选时跳过上一张原牌。
        if (cards.Count > 1) cards.Remove(_lastBorrowedCard!);
        var chosen = StableRandomHelper.PickByStableHash(players.FirstOrDefault(), cards, "GrandMageMonkey",
            card => $"{card.Owner.NetId}:{card.Id.Entry}:{cards.IndexOf(card)}", combatState.RoundNumber.ToString());
        if (chosen == null) { await CreatureCmd.Heal(Owner, 30); return; }
        _borrowedCards.Add(chosen);
        _lastBorrowedCard = chosen;
        // RemoveFromCombat 会注销原牌，之后无法归还；这里只临时取出牌堆并通知原生钩子。
        var oldPile = chosen.Pile!.Type;
        chosen.RemoveFromCurrentPile();
        await Hook.AfterCardChangedPiles(chosen.Owner.RunState, combatState, chosen, oldPile, null);
        MainFile.Logger.Info($"【老爹猴符咒】借走玩家={chosen.Owner.NetId}，卡牌={chosen.Id.Entry}；玩家回合结束归还弃牌堆。");
    }

    /// <summary>每次普通攻击完成后龙加入燃烧，猪对全队施加虚弱和易伤。</summary>
    internal async Task AfterAttack()
    {
        if (Owner?.IsAlive != true || Owner.CombatState == null) return;
        foreach (var player in Owner.CombatState.Players.Where(player => player.Creature.IsAlive).OrderBy(player => player.NetId))
        {
            if (Has<DragonTalisman>()) await CardPileCmdHelper.AddToCombatAndPreview<Burn>(player.Creature, PileType.Discard, 1, false);
            if (!Has<PigTalisman>()) continue;
            await PowerCmd.Apply<WeakPower>(player.Creature, 1, Owner, null);
            await PowerCmd.Apply<VulnerablePower>(player.Creature, 1, Owner, null);
        }
    }

    /// <summary>归还原来的战斗卡牌实例，不生成新牌，不改变永久牌组。</summary>
    internal async Task ReturnBorrowedCards()
    {
        var cards = _borrowedCards.ToArray();
        _borrowedCards.Clear();
        foreach (var card in cards)
            if (card.Owner?.Creature.CombatState != null && !card.HasBeenRemovedFromState)
                await CardPileCmd.Add(card, PileType.Discard);
    }

    /// <summary>两回合到期或战斗结束时归还借牌并撤回本批临时加成。</summary>
    public override async Task AfterRemoved(Creature oldOwner)
    {
        await ReturnBorrowedCards();
        oldOwner.GetPower<GrandMageDamageCapPower>()?.SetPerHitCap(0);
        if (oldOwner.IsAlive)
        {
            await RemoveBonus(oldOwner, _strengthPower, _strengthBonus);
            await RemoveBonus(oldOwner, _artifactPower, _artifactBonus);
            await RemoveBonus(oldOwner, _flightPower, _flightBonus);
        }
        foreach (var player in oldOwner.CombatState?.Players ?? [])
            await PowerCmd.Remove(player.Creature.GetPower<GrandMageSheepTaxPower>());
        _types.Clear();
        MainFile.Logger.Info("【老爹封存加成】本批临时加成全部到期，借牌与符咒已归还。");
    }

    /// <summary>只撤回本批尚存层数，其他来源加成保留。</summary>
    private static async Task RemoveBonus(Creature creature, PowerModel? power, int amount)
    {
        if (power == null || amount <= 0 || !creature.Powers.Contains(power)) return;
        // 到期撤回不是施加减益，不能再走人工制品的拦截；只扣本批尚存层数。
        power.SetAmount(power.Amount - Math.Min(amount, Math.Max(0, power.Amount)));
        if (power.ShouldRemoveDueToAmount()) await PowerCmd.Remove(power);
    }
}
