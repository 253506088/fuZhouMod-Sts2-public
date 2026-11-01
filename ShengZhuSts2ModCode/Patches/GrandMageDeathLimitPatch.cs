using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Monsters.GrandMageDad;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers.GrandMageDad;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Patches;

/// <summary>直接死亡命令被承伤额度挡住后，仍有生命的老爹不能被移出战斗。</summary>
[HarmonyPatch(typeof(Hook), nameof(Hook.ShouldDie))]
internal static class GrandMageDeathLimitPatch
{
    /// <summary>生命已经归零时保留原生死亡流程，只保护被额度截断后仍存活的老爹。</summary>
    private static bool Prefix(Creature creature, ref bool __result, ref AbstractModel? preventer)
    {
        if (creature?.Monster is not GrandMageDad || !creature.IsAlive || !CombatManager.Instance.IsInProgress
            || creature.GetPower<GrandMageDamageCapPower>() is not { } cap) return true;
        preventer = cap;
        __result = false;
        return false;
    }
}
