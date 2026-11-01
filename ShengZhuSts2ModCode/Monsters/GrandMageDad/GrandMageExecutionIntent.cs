using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Monsters.GrandMageDad;

/// <summary>用原生斩杀图标明确展示直接死亡，避免显示可格挡的伤害数字。</summary>
internal sealed class GrandMageExecutionIntent : DeathBlowIntent
{
    /// <summary>复用死亡打击图标；零仅满足原生构造接口，不作为伤害结算。</summary>
    internal GrandMageExecutionIntent() : base(() => 0) { }

    /// <summary>头顶展示本地化斩杀文字。</summary>
    public override LocString GetIntentLabel(IEnumerable<Creature> targets, Creature owner)
        => new("monsters", "SHENGZHUSTS2MOD-GRAND_MAGE_DAD.executionLabel");
}
