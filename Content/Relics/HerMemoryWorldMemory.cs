using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves.Runs;
using shirubimod.Scripts.Content.Cards;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace shirubimod.Scripts.Content.Relics;

// 只能通过希比专属涅奥选项获得的特殊遗物。
[RegisterRelic(typeof(ShirubiRelicPool), FullPublicEntry = "SHIBI_HER_MEMORY_WORLD_MEMORY")]
public sealed class HerMemoryWorldMemory : ModRelicTemplate
{
    private const string MemoryIconPath = "res://images/relics/The_Memory,The_world.png";
    private const int SecondDeathHeal = 77;

    private bool _hasSeenFirstDeath;
    private bool _wasUsed;

    public override RelicRarity Rarity => RelicRarity.Event;
    public override bool IsUsedUp => WasUsed;

    public override RelicAssetProfile AssetProfile => new(
        IconPath: MemoryIconPath,
        IconOutlinePath: MemoryIconPath,
        BigIconPath: MemoryIconPath);

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Cards", 7m),
        new HealVar(SecondDeathHeal)
    ];

    [SavedProperty]
    public bool HasSeenFirstDeath
    {
        get => _hasSeenFirstDeath;
        set => _hasSeenFirstDeath = value;
    }

    [SavedProperty]
    public bool WasUsed
    {
        get => _wasUsed;
        set
        {
            _wasUsed = value;
            if (value)
            {
                Status = RelicStatus.Disabled;
            }
        }
    }

    public override bool ShouldDieLate(Creature creature)
    {
        if (creature != Owner.Creature || WasUsed)
        {
            return true;
        }

        if (!HasSeenFirstDeath && !WasStarterDeathSaveUsed())
        {
            HasSeenFirstDeath = true;
            return true;
        }

        return false;
    }

    public override async Task AfterPreventingDeath(Creature creature)
    {
        WasUsed = true;
        await CreatureCmd.Heal(creature, SecondDeathHeal);
        await BecomeCompleteSelf();
    }

    private bool WasStarterDeathSaveUsed()
    {
        return Owner.Relics.OfType<GoldenButterflyKnot>().Any(relic => relic.WasUsed);
    }

    private async Task BecomeCompleteSelf()
    {
        await ReplacePermanentDeckWithMemoryCards();

        var combatState = Owner.Creature.CombatState;
        if (combatState == null)
        {
            return;
        }

        // 当前战斗也同步成“完整的她”的七张牌，避免当场继续抽到转化前的牌。
        foreach (var pileType in new[] { PileType.Hand, PileType.Draw, PileType.Discard, PileType.Play })
        {
            await CardPileCmd.RemoveFromCombat(pileType.GetPile(Owner).Cards.ToList(), skipVisuals: true);
        }

        await AddMemoryCardToCombat<ShibiPainfulPast>(combatState);
        await AddMemoryCardToCombat<ShibiBeginningOfStory>(combatState);
        await AddMemoryCardToCombat<ShibiTwistingJourney>(combatState);
        await AddMemoryCardToCombat<ShibiHappyEnding>(combatState);
        await AddMemoryCardToCombat<ShibiStartEndCompleteMe>(combatState);
        await AddMemoryCardToCombat<ShibiTerminusOfAll>(combatState);
        await AddMemoryCardToCombat<ShibiReshapeWorldWithMemory>(combatState);
    }

    private async Task ReplacePermanentDeckWithMemoryCards()
    {
        await CardPileCmd.RemoveFromDeck(PileType.Deck.GetPile(Owner).Cards.ToList(), showPreview: false);

        await AddMemoryCardToDeck<ShibiPainfulPast>();
        await AddMemoryCardToDeck<ShibiBeginningOfStory>();
        await AddMemoryCardToDeck<ShibiTwistingJourney>();
        await AddMemoryCardToDeck<ShibiHappyEnding>();
        await AddMemoryCardToDeck<ShibiStartEndCompleteMe>();
        await AddMemoryCardToDeck<ShibiTerminusOfAll>();
        await AddMemoryCardToDeck<ShibiReshapeWorldWithMemory>();
    }

    private async Task AddMemoryCardToDeck<T>() where T : CardModel
    {
        var card = Owner.RunState.CreateCard(ModelDb.Card<T>(), Owner);
        await CardPileCmd.Add(card, PileType.Deck, CardPilePosition.Bottom, this);
    }

    private async Task AddMemoryCardToCombat<T>(CombatState combatState) where T : CardModel
    {
        var card = combatState.CreateCard<T>(Owner);
        await CardPileCmd.AddGeneratedCardToCombat(card, PileType.Hand, addedByPlayer: true);
    }
}
