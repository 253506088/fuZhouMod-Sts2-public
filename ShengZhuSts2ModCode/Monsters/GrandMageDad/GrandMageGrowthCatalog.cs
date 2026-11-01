using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards.Masks;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Helpers;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Relics;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Monsters.GrandMageDad;

/// <summary>记录计分涉及的具体卡牌、遗物和魔气名称来源；不改变原有五项评分规则。</summary>
internal static class GrandMageGrowthCatalog
{
    /// <summary>按固定顺序生成开局物品账目；同名重复牌合并数量，升级分另列。</summary>
    internal static List<GrandMageGrowthItem> Capture(Player player, IEnumerable<PanKuDemonQiKind> demonKinds)
    {
        var items = new List<GrandMageGrowthItem>();
        foreach (var group in player.Relics.GroupBy(relic => relic.Id.Entry).OrderBy(group => group.Key, StringComparer.Ordinal))
            items.Add(new() { Category = TalismanHelper.AnimalTalismanTypes.Contains(group.First().GetType()) ? "talismans" : "otherRelics", Table = "relics", Entry = group.Key, Count = group.Count() });

        bool endless = player.Deck.Cards.Any(card => card is EndlessDarkness);
        foreach (var group in player.Deck.Cards.GroupBy(card => card.Id.Entry).OrderBy(group => group.Key, StringComparer.Ordinal))
        {
            var card = group.First();
            if (card is BaseMaskCard or TaLaMask)
                items.Add(new() { Category = endless ? "coveredMasks" : "masks", Table = "cards", Entry = group.Key, Count = group.Count() });
            else if (card is not NothingLackingCard and not EndlessDarkness and not EightDemonPossessionCard)
                items.Add(new() { Category = PanKuDemonQiHelper.IsDemonQiCard(card) ? "demonCards" : "otherCards", Table = "cards", Entry = group.Key, Count = group.Count() });

            if (card is EndlessDarkness)
                items.Add(new() { Category = "allMasks", Table = "cards", Entry = group.Key, Count = group.Count() });
            if (card is NothingLackingCard or EndlessDarkness or EightDemonPossessionCard)
                items.Add(new() { Category = "awakenings", Table = "cards", Entry = group.Key, Count = group.Count() });
            int upgrades = group.Count(item => item.IsUpgraded && item.Rarity != CardRarity.Basic);
            if (upgrades > 0) items.Add(new() { Category = "upgrades", Table = "cards", Entry = group.Key, Count = upgrades });
            foreach (var enchanted in group.Where(item => PanKuDemonQiHelper.TryGetKindFromEnchantment(item.Enchantment, out _))
                .GroupBy(item => item.Enchantment!.Id.Entry).OrderBy(item => item.Key, StringComparer.Ordinal))
            {
                PanKuDemonQiHelper.TryGetKindFromEnchantment(enchanted.First().Enchantment, out var kind);
                items.Add(new() { Category = "demonEnchantments", Table = "cards", Entry = group.Key, Count = enchanted.Count(), DemonKind = kind.ToString() });
            }
        }
        foreach (var kind in demonKinds.OrderBy(kind => kind))
            items.Add(new() { Category = "demons", Table = "demon", Entry = kind.ToString(), Count = 1 });
        if (player.GetRelic<PanKuBox>() is { } box)
            foreach (var kind in box.GetStoredKinds().OrderBy(kind => kind))
                items.Add(new() { Category = "storedDemons", Table = "demon", Entry = kind.ToString(), Count = 1 });
        return items;
    }
}
