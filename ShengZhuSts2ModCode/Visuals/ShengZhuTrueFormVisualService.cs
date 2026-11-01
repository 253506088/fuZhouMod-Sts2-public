using System.Reflection;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Entities.Cards;
using Godot;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.RestSite;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens.Shops;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Character;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Relics;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Services;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Visuals;

/// <summary>
/// 圣主真身视觉服务：根据鼠符咒持有状态切换石板外观和真身外观。
/// </summary>
public static class ShengZhuTrueFormVisualService
{
    private const string StatueNodeName = "%ShengZhuStatueVisual";
    private const string TrueFormNodeName = "%ShengZhuTrueFormVisual";
    private static readonly FieldInfo? MerchantPlayersField = typeof(NMerchantRoom).GetField("_players", BindingFlags.Instance | BindingFlags.NonPublic);

    /// <summary>
    /// 刷新指定玩家在战斗房间里的圣主外观。
    /// </summary>
    /// <param name="player">需要刷新的玩家。</param>
    public static void Refresh(Player? player)
    {
        if (player?.Character is not ShengZhu)
        {
            return;
        }

        NCreature? creatureNode = NCombatRoom.Instance?.GetCreatureNode(player.Creature);
        if (creatureNode == null)
        {
            return;
        }

        Refresh(creatureNode, ShengZhuFormService.IsHumanForm(player));
    }

    /// <summary>
    /// 刷新指定玩家在当前可见房间里的圣主外观。
    /// </summary>
    /// <param name="player">需要刷新的玩家。</param>
    public static void RefreshEverywhere(Player? player)
    {
        if (player?.Character is not ShengZhu)
        {
            return;
        }

        Refresh(player);
        RefreshRestSite(player);
        RefreshMerchant(player);
    }

    /// <summary>
    /// 刷新战斗房间里所有圣主玩家的真身显示状态。
    /// </summary>
    public static void RefreshAll()
    {
        if (NCombatRoom.Instance == null)
        {
            return;
        }

        foreach (NCreature creatureNode in NCombatRoom.Instance.CreatureNodes)
        {
            Player? player = creatureNode.Entity?.Player;
            if (player?.Character is ShengZhu)
            {
                Refresh(creatureNode, ShengZhuFormService.IsHumanForm(player));
            }
        }
    }

    /// <summary>
    /// 刷新单个战斗节点的真身显示状态。
    /// </summary>
    /// <param name="creatureNode">战斗内角色节点。</param>
    /// <param name="showTrueForm">是否显示真身。</param>
    public static void Refresh(NCreature? creatureNode, bool showTrueForm)
    {
        if (creatureNode?.Entity?.Player?.Character is not ShengZhu)
        {
            return;
        }

        RefreshVisualRoot(creatureNode.Visuals, showTrueForm);
    }

    /// <summary>
    /// 刷新休息处里的圣主外观。
    /// </summary>
    /// <param name="restSiteCharacter">休息处角色节点。</param>
    public static void Refresh(NRestSiteCharacter? restSiteCharacter)
    {
        Player? player = restSiteCharacter?.Player;
        if (player?.Character is not ShengZhu)
        {
            return;
        }

        RefreshVisualRoot(restSiteCharacter, ShengZhuFormService.IsHumanForm(player));
    }

    /// <summary>
    /// 刷新商店里的圣主外观。
    /// </summary>
    /// <param name="merchantCharacter">商店角色节点。</param>
    /// <param name="player">商店角色节点对应的玩家。</param>
    public static void Refresh(NMerchantCharacter? merchantCharacter, Player? player)
    {
        if (player?.Character is not ShengZhu)
        {
            return;
        }

        RefreshVisualRoot(merchantCharacter, ShengZhuFormService.IsHumanForm(player));
    }

    /// <summary>
    /// 刷新商店房间里的所有圣主玩家外观。
    /// </summary>
    /// <param name="merchantRoom">商店房间节点。</param>
    public static void Refresh(NMerchantRoom? merchantRoom)
    {
        if (merchantRoom == null)
        {
            return;
        }

        IReadOnlyList<Player>? players = GetMerchantPlayers(merchantRoom);
        if (players == null)
        {
            return;
        }

        for (int i = 0; i < players.Count && i < merchantRoom.PlayerVisuals.Count; i++)
        {
            Refresh(merchantRoom.PlayerVisuals[i], players[i]);
        }
    }

    /// <summary>
    /// 判断节点是否是圣主自定义石板/真身展示节点。
    /// </summary>
    /// <param name="node">待检查节点。</param>
    /// <returns>同时存在石板和真身节点时返回 true。</returns>
    public static bool HasShengZhuVisuals(Node? node)
    {
        return ShengZhuPresentation.Find(node, "ShengZhuStatueVisual") != null
               && ShengZhuPresentation.Find(node, "ShengZhuTrueFormVisual") != null;
    }

    /// <summary>
    /// 刷新当前休息处里指定玩家的外观。
    /// </summary>
    /// <param name="player">需要刷新的玩家。</param>
    private static void RefreshRestSite(Player player)
    {
        NRestSiteRoom? restSiteRoom = NRestSiteRoom.Instance;
        if (restSiteRoom == null)
        {
            return;
        }

        foreach (NRestSiteCharacter restSiteCharacter in restSiteRoom.Characters)
        {
            if (ReferenceEquals(restSiteCharacter.Player, player))
            {
                Refresh(restSiteCharacter);
            }
        }
    }

    /// <summary>
    /// 刷新当前商店里指定玩家的外观。
    /// </summary>
    /// <param name="player">需要刷新的玩家。</param>
    private static void RefreshMerchant(Player player)
    {
        NMerchantRoom? merchantRoom = NMerchantRoom.Instance;
        if (merchantRoom == null)
        {
            return;
        }

        IReadOnlyList<Player>? players = GetMerchantPlayers(merchantRoom);
        if (players == null)
        {
            return;
        }

        for (int i = 0; i < players.Count && i < merchantRoom.PlayerVisuals.Count; i++)
        {
            if (ReferenceEquals(players[i], player))
            {
                Refresh(merchantRoom.PlayerVisuals[i], player);
            }
        }
    }

    /// <summary>
    /// 读取商店房间内部的玩家顺序，用来匹配商店角色节点。
    /// </summary>
    /// <param name="merchantRoom">商店房间节点。</param>
    /// <returns>商店房间里的玩家列表。</returns>
    private static IReadOnlyList<Player>? GetMerchantPlayers(NMerchantRoom merchantRoom)
    {
        IReadOnlyList<Player>? players = MerchantPlayersField?.GetValue(merchantRoom) as IReadOnlyList<Player>;
        if (players == null)
        {
            MainFile.Logger.Info("【圣主真身】未找到商店玩家列表，跳过商店外观刷新。");
        }

        return players;
    }

    /// <summary>
    /// 按鼠符咒状态切换石板和真身两个节点。
    /// </summary>
    /// <param name="visualRoot">包含石板和真身节点的根节点。</param>
    /// <param name="showTrueForm">是否显示真身。</param>
    private static void RefreshVisualRoot(Node? visualRoot, bool showTrueForm)
    {
        ShengZhuPresentation.Refresh(visualRoot, showTrueForm);
    }

    /// <summary>将动作路由到所属玩家的有效战斗节点。</summary>
    public static void Play(Player? player, string pose)
    {
        if (player?.Character is not ShengZhu) return;
        NCombatRoom? room = NCombatRoom.Instance;
        if (room == null || !GodotObject.IsInstanceValid(room)) return;
        NCreature? node = room.GetCreatureNode(player.Creature);
        if (node == null || !GodotObject.IsInstanceValid(node)) return;
        ShengZhuPresentation.Refresh(node.Visuals, ShengZhuFormService.IsHumanForm(player));
        ShengZhuPresentation.Play(node.Visuals, pose);
    }

    /// <summary>按明确卡牌来源区分电眼、龙焰和普通出牌，未知技能使用聚气动作。</summary>
    public static void PlayCard(CardModel card)
    {
        string id = card.Id.Entry;
        string pose = id is "LASER_EYES" or "PIG_TALISMAN_CARD" ? "laser"
            : id == "DRAGON_TALISMAN_CARD" ? "flame"
            : card.Type == CardType.Power ? "buff"
            : card.Type == CardType.Attack ? (id.Contains("AFU") || id.Contains("AXE") ? "heavy" : "attack")
            : card.DynamicVars.ContainsKey("Block") || id.Contains("DEFEND") || id.Contains("SHIELD") ? "defend" : "cast";
        Play(card.Owner, pose);
    }

    /// <summary>战斗获胜后让仍存活的圣主短暂收势；即将离场时不会额外阻塞奖励。</summary>
    public static void PlayVictory()
    {
        NCombatRoom? room = NCombatRoom.Instance;
        if (room == null || !GodotObject.IsInstanceValid(room)) return;
        foreach (NCreature node in room.CreatureNodes)
        {
            if (node.Entity?.Player?.Character is ShengZhu && node.Entity.IsAlive)
                ShengZhuPresentation.Play(node.Visuals, "victory");
        }
    }
}
