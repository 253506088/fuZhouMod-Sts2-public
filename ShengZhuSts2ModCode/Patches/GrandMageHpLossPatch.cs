using HarmonyLib;
using System.Reflection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Monsters.GrandMageDad;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers.GrandMageDad;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Patches;

/// <summary>在真实扣血入口落实老爹承伤额度，按实际生命差额记账，其他生物沿用原生逻辑。</summary>
[HarmonyPatch]
internal static class GrandMageHpLossPatch
{
    /// <summary>普通扣血和直接设置生命都在最终写入前限制，治疗照常执行。</summary>
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(Creature), nameof(Creature.LoseHpInternal));
        yield return AccessTools.Method(typeof(Creature), nameof(Creature.SetCurrentHpInternal));
    }

    /// <summary>在扣血前限制本轮剩余额度，预览不经过此入口。</summary>
    private static void Prefix(Creature __instance, MethodBase __originalMethod, ref decimal __0, out (GrandMageDamageCapPower? cap, int hp) __state)
    {
        var cap = __instance.Monster is GrandMageDad && __instance.IsAlive && CombatManager.Instance.IsInProgress
            ? __instance.GetPower<GrandMageDamageCapPower>() : null;
        __state = (cap, __instance.CurrentHp);
        if (cap == null) return;
        if (__originalMethod.Name == nameof(Creature.SetCurrentHpInternal))
        {
            if (__0 < __instance.CurrentHp) __0 = __instance.CurrentHp - cap.LimitLoss(__instance.CurrentHp - __0);
        }
        else __0 = cap.LimitLoss(Math.Max(0, __0));
    }

    /// <summary>立即记录真实损失，后续多段和反伤读取更新后的额度。</summary>
    private static void Postfix(Creature __instance, (GrandMageDamageCapPower? cap, int hp) __state)
        => __state.cap?.RecordLoss(__state.hp - __instance.CurrentHp);
}
