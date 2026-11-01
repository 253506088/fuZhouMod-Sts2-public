using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.ValueProps;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards.Curses.GrandMageDad;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Helpers;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers.GrandMageDad;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Monsters.GrandMageDad;

/// <summary>老爹的公开行动循环：有限压制、符咒封存、可打断吟唱与真实硬直。</summary>
internal static class GrandMageDadMoveService
{
    private static readonly Type[] CurseTypes = [typeof(CurseBlack), typeof(CurseJackie), typeof(CurseJade), typeof(CurseTohru)];

    /// <summary>构建固定行动与破阵分支，不依赖本地随机决定意图。</summary>
    public static MonsterMoveStateMachine CreateStateMachine(MonsterModel monster)
    {
        ArgumentNullException.ThrowIfNull(monster);
        var dad = (GrandMageDad)monster;
        var thunder = new MoveState(GrandMageDadMoveIds.ThunderCombo,
            targets => AttackPhase(dad, targets, 9, 3, 1), new MultiAttackIntent(9, () => 3 + ExtraHits(dad)), new DebuffIntent(false));
        var suppression = new MoveState(GrandMageDadMoveIds.SuppressionSeal,
            targets => AttackPhase(dad, targets, 32, 1, 2), new MultiAttackIntent(32, () => 1 + ExtraHits(dad)), new DebuffIntent(true), new DefendIntent());
        var pursuit = new MoveState(GrandMageDadMoveIds.TalismanPursuit,
            targets => AttackPhase(dad, targets, 20, 1, 3), new MultiAttackIntent(20, () => 1 + ExtraHits(dad)), new DebuffIntent(false));
        var bagua = new MoveState(GrandMageDadMoveIds.BaguaBarrage,
            targets => AttackPhase(dad, targets, 10, 4, 4), new MultiAttackIntent(10, () => 4 + ExtraHits(dad)), new DebuffIntent(false));
        var chant = new MoveState(GrandMageDadMoveIds.UltimateSealChant,
            _ => BeginChant(dad), new DefendIntent(), new BuffIntent());
        var execute = new MoveState(GrandMageDadMoveIds.UltimateSealExecute,
            targets => ResolveChant(dad, targets), new GrandMageExecutionIntent());
        var stagger = new MoveState(GrandMageDadMoveIds.Stagger,
            targets => ResolveChant(dad, targets), new StunIntent());
        thunder.FollowUpState = suppression;
        suppression.FollowUpState = pursuit;
        pursuit.FollowUpState = bagua;
        bagua.FollowUpState = chant;
        chant.FollowUpState = execute;
        execute.FollowUpState = thunder;
        stagger.FollowUpState = thunder;
        return new MonsterMoveStateMachine([thunder, suppression, pursuit, bagua, chant, execute, stagger], thunder);
    }

    /// <summary>进入房间后完成成长和核心能力初始化。</summary>
    public static async Task EnsureOpeningPowers(MonsterModel monster)
    {
        if (monster is not GrandMageDad dad || dad.Creature?.IsAlive != true) return;
        await GrandMageDadBalance.Initialize(dad);
        if (dad.CombatState?.Encounter is not GrandMageDadEncounter encounter) return;
        var creature = dad.Creature;
        if (!creature.HasPower<GrandMageDamageCapPower>())
            await PowerCmd.Apply<GrandMageDamageCapPower>(creature, GrandMageDadBalance.DamageCap(encounter.State.PlayerCount), creature, null);
        if (!creature.HasPower<GrandMageBlessingPower>())
            await PowerCmd.Apply<GrandMageBlessingPower>(creature, GrandMageDadBalance.BlessingNegates(encounter.State.PlayerCount, encounter.State.Ascension), creature, null);
        if (encounter.State.GrowthStrength > 0 && !creature.HasPower<GrandMageCounterPouncePower>())
        {
            var power = await PowerCmd.Apply<GrandMageCounterPouncePower>(creature, encounter.State.GrowthStrength, creature, null);
            power?.Configure(encounter.State.GrowthHp, encounter.State.PlayerCount);
            await PowerCmd.Apply<StrengthPower>(creature, encounter.State.GrowthStrength, creature, null);
        }
        await ApplyStage(dad, 0);
    }

    /// <summary>执行攻击主意图与可读的副效果，最后为玩家下一回合设置阵法。</summary>
    private static async Task AttackPhase(GrandMageDad dad, IReadOnlyList<Creature> targets, int damage, int hits, int stage)
    {
        if (!CanAct(dad, out GrandMageDadState state)) return;
        state.TurnIndex++;
        GrandMageDadVisualService.Play(dad, stage switch { 1 => "thunder", 2 => "seal", 3 => "talisman", _ => "barrage" });
        if (stage == 1) await AddCurses(dad, false);
        if (stage == 2)
        {
            await CreatureCmd.GainBlock(dad.Creature, (state.PlayerCount > 1 ? 20 : 16) * state.PlayerCount, ValueProp.Unpowered, null);
            await CreatureCmd.Heal(dad.Creature, (dad.Creature.MaxHp - dad.Creature.CurrentHp) / 8);
            await PowerCmd.Apply<ArtifactPower>(dad.Creature, 1, dad.Creature, null);
        }
        if (stage == 3)
        {
            await GrandMageDadSealService.SealPlayers(dad);
            await AddCurses(dad, true);
        }
        await AttackAllPlayers(dad, targets, damage, hits + ExtraHits(dad));
        if (dad.Creature.GetPower<GrandMageSealPower>() is { } sealPower) await sealPower.AfterAttack();
        if (!dad.Creature.IsAlive) return;
        if (stage == 2)
        {
            foreach (Player player in LivingPlayers(dad))
            {
                await PowerCmd.Apply<WeakPower>(player.Creature, 1, dad.Creature, null);
                await PowerCmd.Apply<VulnerablePower>(player.Creature, 1, dad.Creature, null);
            }
        }
        await ApplyStage(dad, stage);
        state.SecondaryIntentCode = stage switch
        {
            1 => GrandMageDadMoveIds.SecondaryCurseJackie,
            2 => GrandMageDadMoveIds.SecondaryDefendArtifact,
            3 => GrandMageDadMoveIds.SecondarySealTalisman,
            _ => GrandMageDadMoveIds.SecondaryNone
        };
        MainFile.Logger.Info($"【老爹行动】轮次={state.TurnIndex}，阵法={stage}，攻击={damage}×{hits}，封存数={state.SealedTalismans.Count}。");
    }

    /// <summary>清掉其他阵法，给予完整的破盾窗口；护盾只按人数放大一次。</summary>
    private static async Task BeginChant(GrandMageDad dad)
    {
        if (!CanAct(dad, out GrandMageDadState state)) return;
        await ApplyStage(dad, 0);
        state.TurnIndex++;
        state.CycleStage = 5;
        state.SecondaryIntentCode = GrandMageDadMoveIds.SecondaryNone;
        state.IsChanting = true;
        state.ChantShieldAmount = GrandMageDadBalance.ChantBlock(state.GrowthHp);
        if (dad.Creature.Block > 0) await CreatureCmd.LoseBlock(dad.Creature, dad.Creature.Block);
        await PowerCmd.Apply<GrandMageChantShieldPower>(dad.Creature, state.ChantShieldAmount, dad.Creature, null);
        await CreatureCmd.GainBlock(dad.Creature, state.ChantShieldAmount, ValueProp.Unpowered, null);
        GrandMageDadVisualService.Play(dad, "chant");
        MainFile.Logger.Info($"【老爹吟唱】护盾={dad.Creature.Block}，人数={state.PlayerCount}；下回合前破盾可解除封存并造成硬直。");
    }

    /// <summary>未破盾时全队各结算一次斩杀，允许一次复活；破盾则硬直。</summary>
    private static async Task ResolveChant(GrandMageDad dad, IReadOnlyList<Creature> targets)
    {
        if (!CanAct(dad, out GrandMageDadState state)) return;
        state.TurnIndex++;
        bool execute = state.IsChanting && dad.Creature.Block > 0;
        state.IsChanting = false;
        state.ChantShieldAmount = 0;
        state.CycleStage = 0;
        await PowerCmd.Remove(dad.Creature.GetPower<GrandMageChantShieldPower>());
        if (execute)
        {
            GrandMageDadVisualService.Play(dad, "release");
            await GrandMageExecutionService.Execute(dad);
        }
        else
        {
            GrandMageDadVisualService.Play(dad, "stagger");
            if (dad.CombatState.Encounter is GrandMageDadEncounter encounter)
                await GrandMageDadSealService.ReleaseAll(encounter, "破阵硬直");
        }
        if (dad.Creature.IsAlive && dad.Creature.Block > 0)
            await CreatureCmd.LoseBlock(dad.Creature, dad.Creature.Block);
        MainFile.Logger.Info($"【老爹吟唱】结果={(execute ? "未破盾，全队斩杀各结算一次" : "破阵成功，硬直一回合")}。");
    }

    /// <summary>打破吟唱后立即公开硬直意图，避免画面仍显示重击。</summary>
    internal static async Task InterruptChant(GrandMageDad dad)
    {
        if (!CanAct(dad, out GrandMageDadState state) || !state.IsChanting) return;
        state.IsChanting = false;
        state.ChantShieldAmount = 0;
        if (dad.CombatState.Encounter is GrandMageDadEncounter encounter)
            await GrandMageDadSealService.ReleaseAll(encounter, "打破吟唱护盾");
        if (dad.NextMove.Id == GrandMageDadMoveIds.UltimateSealExecute
            && dad.MoveStateMachine?.States.TryGetValue(GrandMageDadMoveIds.Stagger, out var stagger) == true
            && stagger is MoveState move)
            dad.SetMoveImmediate(move, true);
        GrandMageDadVisualService.Play(dad, "stagger");
        MainFile.Logger.Info("【老爹破阵】吟唱已打断，切换为硬直意图并解除全队封存。");
    }

    /// <summary>替换每名玩家自己的压制能力，不叠加上一阶段计数。</summary>
    private static async Task ApplyStage(GrandMageDad dad, int stage)
    {
        if (!CanAct(dad, out GrandMageDadState state)) return;
        state.CycleStage = stage;
        foreach (Player player in LivingPlayers(dad))
        {
            await PowerCmd.Remove(player.Creature.GetPower<GrandMageCyclePower>());
            if (stage > 0) await PowerCmd.Apply<GrandMageCyclePower>(player.Creature, stage, dad.Creature, null);
        }
    }

    /// <summary>为存活玩家加入一张诅咒；随机池在队伍内轮换，耗尽后才重复。</summary>
    private static async Task AddCurses(GrandMageDad dad, bool random)
    {
        List<Type> remaining = CurseTypes.ToList();
        foreach (Player player in LivingPlayers(dad))
        {
            if (remaining.Count == 0) remaining = CurseTypes.ToList();
            Type selected = random
                ? StableRandomHelper.PickByStableHash(player, remaining, "GrandMageCurse", type => type.Name, dad.CombatState.RoundNumber.ToString()) ?? typeof(CurseJackie)
                : typeof(CurseJackie);
            remaining.Remove(selected);
            CardModel? prototype = ModelDb.AllCards.FirstOrDefault(card => card.GetType() == selected);
            if (prototype == null) continue;
            // 限制当前战斗牌堆中的老爹诅咒总量，长局不会被无限塞满。
            int existing = player.PlayerCombatState?.AllPiles.SelectMany(pile => pile.Cards).Count(card => card is GrandMageDadCurseCard) ?? 0;
            if (existing >= 4) continue;
            var curse = dad.CombatState.CreateCard(prototype, player);
            await CardPileCmdHelper.AddGeneratedCardToCombat(curse, PileType.Discard, false);
        }
    }

    /// <summary>按稳定玩家顺序逐段攻击，反伤杀死老爹或目标后停止后续段数。</summary>
    private static async Task AttackAllPlayers(GrandMageDad dad, IReadOnlyList<Creature> targets, int damage, int hits)
    {
        var context = new BlockingPlayerChoiceContext();
        for (int hit = 0; hit < hits && dad.Creature?.IsAlive == true; hit++)
        {
            var living = targets.Where(target => target.IsAlive).OrderBy(target => target.Player?.NetId ?? 0).ToList();
            if (living.Count == 0) break;
            await DealDamage(context, living, damage, ValueProp.Move, dad.Creature);
        }
    }

    /// <summary>隔离正式版与 beta 的伤害命令签名差异，其他机制共用同一逻辑。</summary>
    internal static Task DealDamage(PlayerChoiceContext context, IReadOnlyList<Creature> targets, decimal damage, ValueProp props, Creature? source)
    {
        return CreatureCmd.Damage(context, targets, damage, props, source, null);
    }

    /// <summary>获取按网络编号排序的存活玩家。</summary>
    private static IEnumerable<Player> LivingPlayers(GrandMageDad dad) => dad.CombatState.Players
        .Where(player => player.Creature.IsAlive).OrderBy(player => player.NetId);

    /// <summary>实际攻击与头顶意图共用兔符咒的额外段数。</summary>
    private static int ExtraHits(GrandMageDad dad) => dad.Creature?.GetPower<GrandMageSealPower>()?.ExtraHits ?? 0;

    /// <summary>动作执行前确认当前怪物和遭遇仍然有效。</summary>
    private static bool CanAct(GrandMageDad dad, out GrandMageDadState state)
    {
        state = null!;
        if (dad.Creature?.IsAlive != true || dad.CombatState?.Encounter is not GrandMageDadEncounter encounter) return false;
        state = encounter.State;
        return true;
    }
}
