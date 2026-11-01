using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Rooms;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Monsters.GrandMageDad;

/// <summary>
/// 大法师老爹 Boss 本体：负责基础生命、行动状态机和后续战斗钩子入口。
/// </summary>
public class GrandMageDad : CustomMonsterModel
{
    private const int BaseHp = 600;
    private bool _openingReady;

    /// <summary>同一怪物实例只增加一次成长生命；读档重建的实例需要重新应用快照。</summary>
    internal bool GrowthAppliedToInstance { get; set; }

    /// <summary>供视觉安全读取的已绑定生物；图鉴规范模型没有生物，不能访问原生 Creature getter。</summary>
    internal Creature? VisualCreature { get; private set; }

    /// <summary>新怪物实例清理旧实例的初始化标记和视觉引用。</summary>
    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        _openingReady = false;
        GrowthAppliedToInstance = false;
        VisualCreature = null;
    }

    /// <summary>提供有效预加载资源，避免引擎寻找不存在的原版怪物场景。</summary>
    public override string CustomVisualPath => "res://ShengZhuSts2Mod/scenes/grand_mage_dad/grand_mage_dad.tscn";

    /// <summary>
    /// 大法师老爹单人基础最小生命；多人缩放交给塔2原生逻辑处理。
    /// </summary>
    public override int MinInitialHp => BaseHp;

    /// <summary>
    /// 大法师老爹单人基础最大生命；进入战斗后再按平均构筑评分追加成长。
    /// </summary>
    public override int MaxInitialHp => BaseHp;

    /// <summary>
    /// 生成固定行动循环和破阵硬直分支。
    /// </summary>
    /// <returns>老爹行动状态机。</returns>
    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        return GrandMageDadMoveService.CreateStateMachine(this);
    }

    /// <summary>
    /// 创建头部、衣袍、手臂和法阵组成的原生节点动画。
    /// </summary>
    /// <returns>大法师老爹战斗视觉节点。</returns>
    public override NCreatureVisuals? CreateCustomVisuals()
    {
        return GrandMageDadVisualService.CreateVisuals(this);
    }

    /// <summary>
    /// 老爹进房后立刻挂载核心能力，避免要等到第一回合出手才出现 Buff。
    /// </summary>
    /// <returns>异步任务。</returns>
    public override async Task AfterAddedToRoom()
    {
        if (_openingReady) return;
        await base.AfterAddedToRoom();
        VisualCreature = Creature;
        await GrandMageDadMoveService.EnsureOpeningPowers(this);
        _openingReady = Creature?.IsAlive == true;
    }

    /// <summary>每个玩家阵营回合结束时，推进封存期限一次。</summary>
    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (side == CombatSide.Player && Creature?.IsAlive == true && CombatState?.Encounter is GrandMageDadEncounter encounter)
            await GrandMageDadSealService.AdvancePlayerTurn(encounter);
    }

    /// <summary>老爹死亡立即释放临时封存，死亡动画不参与游戏状态判断。</summary>
    public override async Task AfterDeath(PlayerChoiceContext choiceContext, Creature creature, bool wasRemovalPrevented, float deathAnimLength)
    {
        if (creature == Creature && !wasRemovalPrevented && CombatState?.Encounter is GrandMageDadEncounter encounter)
        {
            await GrandMageDadSealService.ReleaseAll(encounter, "老爹死亡");
            encounter.State.IsChanting = false;
            GrandMageDadVisualService.Play(this, "death");
        }
    }

    /// <summary>受击只播放短暂反馈，不覆盖刚刚触发的破阵硬直。</summary>
    public override Task AfterDamageReceived(PlayerChoiceContext choiceContext, Creature target, DamageResult result, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (target == Creature && Creature.IsAlive && result.TotalDamage > 0 && NextMove.Id != GrandMageDadMoveIds.Stagger)
            GrandMageDadVisualService.Play(this, "hurt");
        return Task.CompletedTask;
    }

    /// <summary>离开战斗时清空临时封存和吟唱，避免外部开发入口复用遭遇时遗留。</summary>
    public override async Task AfterCombatEnd(CombatRoom room)
    {
        if (CombatState?.Encounter is GrandMageDadEncounter encounter)
        {
            await GrandMageDadSealService.ReleaseAll(encounter, "战斗结束");
            encounter.State.IsChanting = false;
            encounter.State.CycleStage = 0;
        }
        VisualCreature = null;
    }
}
