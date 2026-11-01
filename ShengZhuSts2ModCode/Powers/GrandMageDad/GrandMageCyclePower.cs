using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Monsters.GrandMageDad;
using DadMonster = ShengZhuSts2Mod.ShengZhuSts2ModCode.Monsters.GrandMageDad.GrandMageDad;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers.GrandMageDad;

/// <summary>五行阵的玩家级短时压制；计数只属于持有者，不响应队友出牌。</summary>
public class GrandMageCyclePower : ShengZhuSts2ModPower
{
    private int _cardsPlayed;
    private int _attacksPlayed;
    private bool _woodResolved;

    /// <summary>只读展示金阵剩余削弱次数，不推进出牌计数。</summary>
    internal int RemainingMetalAttacks => Math.Max(0, 2 - _attacksPlayed);

    /// <summary>只读展示水阵剩余反震次数。</summary>
    internal int RemainingWaterCards => Math.Max(0, 3 - _cardsPlayed);

    /// <summary>只读展示下一张牌的火阵伤害与格挡倍率。</summary>
    internal int FirePercent => Math.Max(60, 100 - _cardsPlayed * 10);

    /// <summary>只读展示木阵是否已经结算，避免回合末仍提示尚未选择。</summary>
    internal bool WoodResolved => _woodResolved;

    /// <summary>使用专用五行阵图标，避免新能力落到默认占位图片。</summary>
    public override string CustomPackedIconPath => "res://ShengZhuSts2Mod/images/powers/grand_mage_cycle.svg";

    /// <summary>提示框复用可缩放的五行阵图标。</summary>
    public override string CustomBigIconPath => CustomPackedIconPath;

    /// <summary>五行压制可以被正常的减益免疫和清除手段应对。</summary>
    public override PowerType Type => PowerType.Debuff;

    /// <summary>用一到四标记金木水火，土阵由老爹的吟唱盾展示。</summary>
    public override PowerStackType StackType => PowerStackType.Counter;

    /// <summary>只在持有者自己的回合重置出牌计数，额外回合也能重新应对。</summary>
    public override Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, MegaCrit.Sts2.Core.Entities.Players.Player player)
    {
        if (player.Creature == Owner) { _cardsPlayed = 0; _attacksPlayed = 0; _woodResolved = false; }
        return Task.CompletedTask;
    }

    /// <summary>所有修正只在当前有存活老爹的战斗内成立。</summary>
    private bool IsActive => Owner?.IsAlive == true
        && Owner.CombatState?.Enemies.Any(creature => creature.Monster is DadMonster && creature.IsAlive) == true;

    /// <summary>金阵只削弱持有者本回合前两张攻击，不永久改卡。</summary>
    public override decimal ModifyDamageAdditive(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        return IsActive && Amount == 1 && _attacksPlayed < 2 && dealer == Owner
            && cardSource?.Owner?.Creature == Owner && cardSource.Type == CardType.Attack ? -2m : 0m;
    }

    /// <summary>火阵的伤害衰减最多四成，后续出牌仍有收益。</summary>
    public override decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        return IsActive && Amount == 4 && dealer == Owner && cardSource?.Owner?.Creature == Owner
            ? Math.Max(0.6m, 1m - _cardsPlayed * 0.1m) : 1m;
    }

    /// <summary>火阵只修正持有者卡牌提供的格挡，保底保留六成。</summary>
    public override decimal ModifyBlockMultiplicative(Creature target, decimal block, ValueProp props, CardModel? cardSource, CardPlay? cardPlay)
    {
        return IsActive && Amount == 4 && target == Owner && cardSource?.Owner?.Creature == Owner
            ? Math.Max(0.6m, 1m - _cardsPlayed * 0.1m) : 1m;
    }

    /// <summary>更新持有者自己的出牌计数，水阵反震最多触发三次。</summary>
    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (!IsActive || cardPlay.Card.Owner?.Creature != Owner) return;
        _cardsPlayed++;
        if (cardPlay.Card.Type == CardType.Attack) _attacksPlayed++;
        if (Amount == 3 && _cardsPlayed <= 3)
            await GrandMageDadMoveService.DealDamage(choiceContext, [Owner], 2, ValueProp.Unpowered, FindDad()?.Creature);
    }

    /// <summary>木阵允许选择消耗一张手牌；不消耗则承受四点可格挡伤害。</summary>
    public override async Task BeforeSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (!IsActive || Amount != 2 || _woodResolved || side != CombatSide.Player || !participants.Contains(Owner)) return;
        _woodResolved = true;
        var player = Owner.Player;
        if (player?.PlayerCombatState == null) return;
        CardModel? selected = null;
        if (player.PlayerCombatState.Hand.Cards.Count > 0)
        {
            var prefs = new CardSelectorPrefs(new LocString("powers", Id.Entry + ".selectionPrompt"), 0, 1) { Cancelable = true };
            selected = (await CardSelectCmd.FromHand(choiceContext, player, prefs, null!, null!)).FirstOrDefault();
        }
        if (!IsActive) return;
        if (selected?.Owner == player && selected.Pile?.Type == PileType.Hand)
            await CardCmd.Exhaust(choiceContext, selected);
        else
            await GrandMageDadMoveService.DealDamage(choiceContext, [Owner], 4, ValueProp.Unpowered, FindDad()?.Creature);
    }

    /// <summary>读取同一战斗中的老爹作为伤害来源，不使用本地玩家视角。</summary>
    private DadMonster? FindDad() => Owner?.CombatState?.Enemies
        .Select(creature => creature.Monster).OfType<DadMonster>().FirstOrDefault(monster => monster.Creature.IsAlive);
}
