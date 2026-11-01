using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers.GrandMageDad;

/// <summary>羊符咒剥夺后使各玩家下一张技能费用加一，与布莱克诅咒分开计数和撤回。</summary>
public class GrandMageSheepTaxPower : ShengZhuSts2ModPower
{
    /// <summary>技能税可以被免疫或移除。</summary>
    public override PowerType Type => PowerType.Debuff;
    /// <summary>计数器表示待支付技能税。</summary>
    public override PowerStackType StackType => PowerStackType.Counter;
    /// <summary>技能税复用五行阵图标。</summary>
    public override string CustomPackedIconPath => "res://ShengZhuSts2Mod/images/powers/grand_mage_cycle.svg";
    /// <summary>大图复用五行阵图标。</summary>
    public override string CustomBigIconPath => CustomPackedIconPath;
    /// <summary>动态计算本人技能费用，避免改写卡牌基础费用。</summary>
    public override bool TryModifyEnergyCostInCombat(CardModel card, decimal originalCost, out decimal modifiedCost)
    {
        bool applies = Owner?.Player != null && card.Owner == Owner.Player && card.Type == CardType.Skill && originalCost >= 0 && Amount > 0;
        modifiedCost = applies ? originalCost + Amount : originalCost;
        return applies;
    }
    /// <summary>本人打出第一张技能后税款消失。</summary>
    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (Owner?.Player != null && cardPlay.Card.Owner == Owner.Player && cardPlay.Card.Type == CardType.Skill)
            await PowerCmd.Remove(this);
    }
}
