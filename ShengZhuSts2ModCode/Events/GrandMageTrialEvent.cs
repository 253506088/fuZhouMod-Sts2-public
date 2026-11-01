using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Runs;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Character;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Monsters.GrandMageDad;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Events;

/// <summary>第三章自愿挑战老爹的共享事件，入场和奖励均由原生事件同步器处理。</summary>
public class GrandMageTrialEvent : CustomEventModel
{
    /// <summary>全队投票决定挑战或离开，避免只有部分玩家进入战斗。</summary>
    public override bool IsShared => true;

    /// <summary>复用已有老爹画像作为邀请页插图。</summary>
    public override string? CustomInitialPortraitPath => $"{MainFile.ResPath}/images/monsters/grand_mage_dad/grand_mage_dad.png";

    /// <summary>老爹移至第三章末端；类型仅保留用于旧档和调试恢复。</summary>
    public override bool IsAllowed(IRunState runState)
    {
        return false;
    }

    /// <summary>提供挑战和无代价离开两个选项。</summary>
    protected override IReadOnlyList<EventOption> GenerateInitialOptions()
    {
        return
        [
            new EventOption(this, Challenge, $"{Id.Entry}.pages.INITIAL.options.CHALLENGE"),
            new EventOption(this, Leave, $"{Id.Entry}.pages.INITIAL.options.LEAVE")
        ];
    }

    /// <summary>各玩家副本提交本人奖励，由原生同步器收齐后只创建一场战斗。</summary>
    private Task Challenge()
    {
        // 正式版通过 EnteringEventCombat 事件同步入场，没有 beta 的 _combatSynchronizer 字段。
        if (Owner?.Creature.IsAlive != true || Owner.RunState.CurrentRoom is not MegaCrit.Sts2.Core.Rooms.EventRoom)
        {
            MainFile.Logger.Info($"【正气试炼】忽略无效入场请求：玩家={Owner?.NetId}，房间={Owner?.RunState.CurrentRoom?.GetType().Name}。");
            return Task.CompletedTask;
        }

        MainFile.Logger.Info($"【正气试炼】玩家接受挑战：玩家={Owner.NetId}，章节={Owner.RunState.CurrentActIndex + 1}，额外金币=100。");
        // 正式版只由本地事件实例创建房间，不会替我们合并队友的额外奖励。
        // 各端必须按同一顺序为全队构造奖励，否则网络奖励索引会指向不同的物品。
        var rewards = Owner.RunState.Players.OrderBy(player => player.NetId)
            .Select(player => (Reward)new GoldReward(100, player)).ToList();
        EnterCombatWithoutExitingEvent<GrandMageTrialEncounter>(rewards, shouldResumeAfterCombat: false);
        return Task.CompletedTask;
    }

    /// <summary>离开只结束事件，不扣资源、不发奖励，也不再次邀请。</summary>
    private Task Leave()
    {
        MainFile.Logger.Info($"【正气试炼】玩家选择离开：玩家={Owner?.NetId}。");
        SetEventFinished(L10NLookup($"{Id.Entry}.pages.LEFT.description"));
        return Task.CompletedTask;
    }
}
