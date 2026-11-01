using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Events;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Services;

/// <summary>探测仪的第三章试炼邀请，读取全队存档状态，不消耗随机数或依赖本地玩家。</summary>
public static class GrandMageTrialService
{
    /// <summary>将第三章首个可用问号事件替换为试炼；多枚探测仪处理同一事件时保持结果不变。</summary>
    public static EventModel ReplaceNextEvent(IRunState runState, EventModel currentEvent)
    {
        if (currentEvent is GrandMageTrialEvent || runState.CurrentMapPoint?.PointType != MapPointType.Unknown)
        {
            return currentEvent;
        }

        var trial = ModelDb.Event<GrandMageTrialEvent>();
        if (!trial.IsAllowed(runState))
        {
            return currentEvent;
        }

        MainFile.Logger.Info($"【正气试炼】第三章邀请：原事件={currentEvent.Id.Entry}，试炼={trial.Id.Entry}，队伍人数={runState.Players.Count}。");
        return trial;
    }
}
