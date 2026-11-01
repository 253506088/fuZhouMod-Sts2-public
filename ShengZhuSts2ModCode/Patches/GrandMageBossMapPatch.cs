using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves.Runs;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Monsters.GrandMageDad;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Services;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Patches;

/// <summary>将追加 Boss 接入原生生成、保存、进入战斗和奖励继续流程。</summary>
public static class GrandMageBossMapPatch
{
    /// <summary>原版选择两位 Boss 后再配置老爹，保持原版 A10 的随机结果。</summary>
    [HarmonyPatch(typeof(RunManager), nameof(RunManager.GenerateRooms))]
    public static class RoomsPatch
    {
        /// <summary>配置将来第三章的 Boss 模型，供原生资源预加载使用。</summary>
        public static void Postfix(RunManager __instance) => GrandMageBossMapService.Configure(__instance.DebugOnlyGetState());
    }

    /// <summary>读档也补齐章节模型，不依赖仅在新局执行的 GenerateRooms。</summary>
    [HarmonyPatch(typeof(RunManager), nameof(RunManager.GenerateMap))]
    public static class PrepareMapPatch
    {
        /// <summary>在创建或恢复地图前准备第二 Boss。</summary>
        public static void Prefix(RunManager __instance) => GrandMageBossMapService.Configure(__instance.DebugOnlyGetState());
    }

    /// <summary>新生成地图追加最终节点。</summary>
    [HarmonyPatch(typeof(Hook), nameof(Hook.ModifyGeneratedMap))]
    public static class GeneratedPatch
    {
        /// <summary>在其他正常地图修改完成后追加 Boss。</summary>
        [HarmonyPriority(Priority.Last)]
        public static void Postfix(IRunState __0, int __2, ref ActMap __result) => __result = GrandMageBossMapService.Append(__0, __result, __2);
    }

    /// <summary>恢复地图以相同规则接上第三节点，不丢弃已访问坐标。</summary>
    [HarmonyPatch(typeof(Hook), nameof(Hook.ModifyGeneratedMapLate))]
    public static class RestoredPatch
    {
        /// <summary>在移除失效访问记录之前还原追加节点。</summary>
        [HarmonyPriority(Priority.Last)]
        public static void Postfix(IRunState __0, int __2, ref ActMap __result) => __result = GrandMageBossMapService.Append(__0, __result, __2);
    }

    /// <summary>第三节点放入原生存档网格，连接关系仍由原生序列化处理。</summary>
    [HarmonyPatch(typeof(SerializableActMap), nameof(SerializableActMap.FromActMap))]
    public static class SaveMapPatch
    {
        /// <summary>只添加不存在的第三节点，不扩展原生存档格式。</summary>
        public static void Postfix(ActMap map, SerializableActMap __result)
        {
            if (map is not GrandMageFinalActMap { ThirdBossMapPoint: { } third }) return;
            if (!__result.Points.Any(point => point.Coord == third.coord)) __result.Points.Add(SerializableMapPoint.FromMapPoint(third));
            __result.GridHeight = Math.Max(__result.GridHeight, third.coord.row + 1);
        }
    }

    /// <summary>原生非虚坐标检查也需要识别第三节点。</summary>
    [HarmonyPatch(typeof(ActMap), nameof(ActMap.HasPoint))]
    public static class HasPointPatch
    {
        /// <summary>其他坐标继续交给原版检查。</summary>
        public static bool Prefix(ActMap __instance, MapCoord coord, ref bool __result)
        {
            if (__instance is not GrandMageFinalActMap { ThirdBossMapPoint: { } third } || third.coord != coord) return true;
            __result = true;
            return false;
        }
    }

    /// <summary>兼容使用列、行直接查找地图的游戏逻辑。</summary>
    [HarmonyPatch(typeof(ActMap), nameof(ActMap.GetPoint), [typeof(int), typeof(int)])]
    public static class GetPointPatch
    {
        /// <summary>只截取第三节点，原版两个 Boss 和普通节点保持原样。</summary>
        public static bool Prefix(ActMap __instance, int col, int row, ref MapPoint? __result)
        {
            if (__instance is not GrandMageFinalActMap { ThirdBossMapPoint: { } third } || third.coord.col != col || third.coord.row != row) return true;
            __result = third;
            return false;
        }
    }

    /// <summary>自由选路效果只影响普通路线，Boss 链始终按顺序继续。</summary>
    [HarmonyPatch(typeof(MapTravel), nameof(MapTravel.GetTravelablePointsFrom))]
    public static class BossTravelPatch
    {
        /// <summary>额外 Boss 不在普通网格里，因此直接返回已保存的 Boss 子节点。</summary>
        public static void Postfix(IRunState runState, MapPoint currentPoint, ref IEnumerable<MapPoint> __result)
        {
            if (runState.Map is GrandMageFinalActMap && currentPoint.PointType == MapPointType.Boss)
                __result = currentPoint.Children;
        }
    }

    /// <summary>根据真实地图坐标选择老爹，不更换原版前两位 Boss。</summary>
    [HarmonyPatch(typeof(ActModel), nameof(ActModel.PullNextEncounter))]
    public static class EncounterPatch
    {
        /// <summary>原生进入地图仍负责模型克隆、联机和战斗保存。</summary>
        public static bool Prefix(ActModel __instance, RoomType roomType, ref EncounterModel __result)
        {
            RunState? run = RunManager.Instance.DebugOnlyGetState();
            if (roomType != RoomType.Boss || !ReferenceEquals(run?.Act, __instance) || !GrandMageBossMapService.IsFinalPoint(run, run?.CurrentMapCoord)) return true;
            __result = ModelDb.Encounter<GrandMageDadEncounter>();
            return false;
        }
    }

    /// <summary>追加老爹以后，中间 Boss 不能冻结通关用时。</summary>
    [HarmonyPatch(typeof(RunManager), nameof(RunManager.WinTime), MethodType.Setter)]
    public static class WinTimePatch
    {
        /// <summary>零值重置与最后一战仍使用原版计时。</summary>
        public static bool Prefix(RunManager __instance, long value)
            => value <= 0 || !GrandMageBossMapService.HasFollowingBoss(__instance.DebugOnlyGetState());
    }

    /// <summary>原版计时只识别前两个 Boss；第三位老爹获胜时也必须在保存战后存档前记录。</summary>
    [HarmonyPatch(typeof(CombatRoom), nameof(CombatRoom.MarkPreFinished))]
    public static class FinalWinTimePatch
    {
        /// <summary>只处理当前最终 Boss 的胜利，恢复已有计时的存档时不重新计时。</summary>
        public static void Postfix(CombatRoom __instance)
        {
            var manager = RunManager.Instance;
            RunState? run = manager.DebugOnlyGetState();
            if (run == null || !ReferenceEquals(run.CurrentRoom, __instance) || !__instance.IsPreFinished
                || __instance.Encounter is not GrandMageDadEncounter || __instance.RoomType != RoomType.Boss
                || !GrandMageBossMapService.IsFinalPoint(run, run.CurrentMapCoord) || manager.WinTime > 0) return;
            manager.WinTime = manager.RunTime;
        }
    }

    /// <summary>中间 Boss 的奖励继续复用原生返回地图命令。</summary>
    [HarmonyPatch(typeof(NRewardsScreen), "OnProceedButtonPressed")]
    public static class ProceedPatch
    {
        /// <summary>不跳过奖励、不直接传送；让玩家在原生地图投票选择下一战。</summary>
        public static bool Prefix(NRewardsScreen __instance)
        {
            if (!GodotObject.IsInstanceValid(__instance) || !(bool)AccessTools.Field(typeof(NRewardsScreen), "_isTerminal").GetValue(__instance)!
                || !GrandMageBossMapService.HasFollowingBoss(RunManager.Instance.DebugOnlyGetState())) return true;
            TaskHelper.RunSafely(RunManager.Instance.ProceedFromTerminalRewardsScreen());
            return false;
        }
    }
}
