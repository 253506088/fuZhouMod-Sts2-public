using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.ValueProps;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers.Talismans;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Relics;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Monsters.GrandMageDad;

/// <summary>终极封印直接斩杀全队；先检查戌狗和狗符咒，再交给原生单次死亡与复活流程。</summary>
internal static class GrandMageExecutionService
{
    /// <summary>每人只结算一次死亡，格挡和无实体不阻挡；原生流程只选择一个死亡阻止者。</summary>
    internal static async Task Execute(GrandMageDad dad)
    {
        if (dad.Creature?.IsAlive != true || dad.CombatState == null) return;
        var context = new BlockingPlayerChoiceContext();
        var killed = new List<Creature>();
        foreach (var player in dad.CombatState.Players.Where(player => player.Creature.IsAlive).OrderBy(player => player.NetId).ToList())
        {
            var creature = player.Creature;
            decimal hp = creature.CurrentHp;
            var cardPower = creature.GetPower<DogTalismanCardPower>();
            decimal loss = cardPower?.ModifyHpLostBeforeOsty(creature, hp, ValueProp.Unblockable | ValueProp.Unpowered, dad.Creature, null) ?? hp;
            if (loss < hp && cardPower != null)
            {
                await CreatureCmd.SetCurrentHp(creature, hp - loss);
                await cardPower.AfterDamageReceived(context, creature, new DamageResult(creature, ValueProp.Unpowered), ValueProp.Unpowered, dad.Creature, null);
                MainFile.Logger.Info($"【老爹斩杀】玩家={player.NetId}，戌狗免死一次，其他复活资源未消耗。");
                continue;
            }
            var dog = player.GetRelic<DogTalisman>();
            loss = dog != null && !GrandMageDadSealService.IsSealed(dog)
                ? dog.ModifyHpLostBeforeOsty(creature, hp, ValueProp.Unblockable | ValueProp.Unpowered, dad.Creature, null) : hp;
            if (loss < hp && dog != null)
            {
                await dog.AfterDamageReceived(context, creature, new DamageResult(creature, ValueProp.Unpowered), ValueProp.Unpowered, dad.Creature, null);
                MainFile.Logger.Info($"【老爹斩杀】玩家={player.NetId}，狗符咒免死一次，其他复活资源未消耗。");
                continue;
            }
            killed.Add(creature);
        }
        // false 保留蜥蜴尾巴、瓶装精灵及其他 Mod 的原生死亡阻止钩子，不能强制死亡。
        await CreatureCmd.Kill(killed, false);
        MainFile.Logger.Info($"【老爹斩杀】原生死亡流程人数={killed.Count}，每人仅结算一次。");
    }
}
