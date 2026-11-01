using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.Nodes.Combat;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Monsters.GrandMageDad;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers.GrandMageDad;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Patches;

/// <summary>蛇符咒只隐藏老爹副意图的画面，主攻击与游戏状态照常结算。</summary>
[HarmonyPatch(typeof(NIntent), nameof(NIntent.UpdateIntent))]
internal static class GrandMageSealedIntentPatch
{
    /// <summary>非攻击副意图显示为隐藏图标，处决意图仍明确公开。</summary>
    private static void Prefix(ref AbstractIntent intent, Creature owner)
    {
        if (owner.Monster is GrandMageDad && owner.GetPower<GrandMageSealPower>()?.HideSecondary == true
            && intent is not (AttackIntent or DeathBlowIntent)) intent = new HiddenIntent();
    }
}
