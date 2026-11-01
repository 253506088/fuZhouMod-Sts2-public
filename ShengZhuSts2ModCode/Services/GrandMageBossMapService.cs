using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Character;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Monsters.GrandMageDad;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Services;

/// <summary>第三章最终老爹的模型和地图规则；不使用本地玩家、系统时间或额外随机数。</summary>
public static class GrandMageBossMapService
{
    /// <summary>沿用圣主角色内容的队伍范围，只影响第三张地图。</summary>
    public static bool Applies(IRunState? run, int actIndex)
        => run != null && actIndex == 2 && run.Acts.Count > 2 && run.Players.Any(player => player.Character is ShengZhu);

    /// <summary>A0～9 使用原生第二 Boss 槽位；A10 保留原生第二 Boss，老爹使用额外槽位。</summary>
    public static void Configure(RunState? run)
    {
        if (!Applies(run, 2)) return;
        ActModel act = run!.Acts[2];
        if (run.AscensionLevel < 10)
        {
            if (act.SecondBossEncounter is not GrandMageDadEncounter)
                act.SetSecondBossEncounter(ModelDb.Encounter<GrandMageDadEncounter>());
        }
        else if (act.SecondBossEncounter == null || act.SecondBossEncounter is GrandMageDadEncounter)
        {
            // 兼容旧存档或调试构图缺少第二 Boss 的情况；通常原版 GenerateRooms 已选好。
            EncounterModel? second = act.AllBossEncounters.Where(encounter => encounter.Id != act.BossEncounter.Id && encounter is not GrandMageDadEncounter)
                .OrderBy(encounter => encounter.Id.Entry, StringComparer.Ordinal).FirstOrDefault();
            if (second != null) act.SetSecondBossEncounter(second);
        }
    }

    /// <summary>新地图和保存地图走同一追加逻辑，普通路线和种子不改变。</summary>
    public static ActMap Append(IRunState run, ActMap map, int actIndex)
    {
        if (!Applies(run, actIndex) || map is GrandMageFinalActMap || map.BossMapPoint.coord.row <= 0 || map.GetColumnCount() <= 0)
            return map;
        var result = new GrandMageFinalActMap(map, run.AscensionLevel >= 10);
        MainFile.Logger.Info($"【老爹终局地图】第三章追加完成：进阶={run.AscensionLevel}，Boss数={(result.ThirdBossMapPoint == null ? 2 : 3)}，最后节点={result.FinalBossPoint.coord}。");
        return result;
    }

    /// <summary>判断一个坐标是否对应本次地图追加的老爹。</summary>
    public static bool IsFinalPoint(IRunState? run, MapCoord? coord)
        => run?.Map is GrandMageFinalActMap map && coord == map.FinalBossPoint.coord;

    /// <summary>当前已打完的 Boss 后面还有老爹时，奖励继续按钮应返回地图。</summary>
    public static bool HasFollowingBoss(IRunState? run)
        => run?.Map is GrandMageFinalActMap && run.CurrentMapPoint?.PointType == MapPointType.Boss
            && run.CurrentMapPoint.Children.Any(point => point.PointType == MapPointType.Boss);
}
