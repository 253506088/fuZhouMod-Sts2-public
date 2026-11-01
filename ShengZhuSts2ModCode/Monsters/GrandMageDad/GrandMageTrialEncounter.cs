using MegaCrit.Sts2.Core.Rooms;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Monsters.GrandMageDad;

/// <summary>事件专用老爹遭遇：复用完整机制，以精英类型结算奖励，避免触发关底通关。</summary>
public class GrandMageTrialEncounter : GrandMageDadEncounter
{
    /// <summary>仅供正气试炼显式创建，不加入普通精英池。</summary>
    public GrandMageTrialEncounter() : base(RoomType.Elite)
    {
    }

    /// <summary>胜利后启用原生精英战奖励及事件提交的额外奖励。</summary>
    public override bool ShouldGiveRewards => true;
}
