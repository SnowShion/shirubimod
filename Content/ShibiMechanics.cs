using System;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Encounters;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Rooms;
using shirubimod.Scripts.Patches;
using shirubimod.Scripts.Content.Powers;
using shirubimod.Scripts.Content.Relics;

namespace shirubimod.Scripts.Content;

public enum ShibiInterruptKind
{
    None,
    Attack,
    Block,
    Status
}

public static class ShibiMechanics
{
    public const int ManaRequiredForFullRelease = 17;
    public const int FullReleaseNormalCardPlays = 7;
    public const int FullReleaseDeathSaveCardPlays = 99;
    public const int CardsDrawnOnFullRelease = 3;
    public const int EnergyGainedOnFullRelease = 3;

    public static bool IsFullReleaseActive(Player player) =>
        player.Creature.GetPowerAmount<ShibiFullReleasePower>() > 0;

    public static async Task GainMana(Player player, int amount, CardModel? source)
    {
        if (amount <= 0 || IsFullReleaseActive(player))
        {
            return;
        }

        if (player.Creature.HasPower<ShibiManaAbsorbPower>())
        {
            amount += player.Creature.GetPowerAmount<ShibiManaAbsorbPower>();
        }

        await PowerCmd.Apply<ShibiManaPower>(player.Creature, amount, player.Creature, source);
        await GoldenButterflyKnot.SyncForPlayer(player);
        ShibiCombatUiPatch.RefreshFullReleaseButton();
    }

    public static async Task ConsumeFullReleaseUse(Player player, CardModel? source)
    {
        var fullRelease = player.Creature.GetPower<ShibiFullReleasePower>();
        if (fullRelease == null)
        {
            return;
        }

        await PowerCmd.ModifyAmount(fullRelease, -1m, player.Creature, source);
        ShibiCombatUiPatch.RefreshFullReleaseButton();
    }

    public static async Task<bool> TryActivateFullRelease(PlayerChoiceContext choiceContext, Player player, CardModel source, int cardPlays)
    {
        var mana = player.Creature.GetPower<ShibiManaPower>();
        if (mana == null || mana.Amount < ManaRequiredForFullRelease)
        {
            return false;
        }

        await PowerCmd.ModifyAmount(mana, -ManaRequiredForFullRelease, player.Creature, source);
        await GoldenButterflyKnot.SyncForPlayer(player);
        await PowerCmd.Apply<ShibiFullReleasePower>(player.Creature, cardPlays, player.Creature, source);
        await NotifyFullReleaseActivated(player, source);
        await PlayerCmd.GainEnergy(EnergyGainedOnFullRelease, player);
        await DrawManaCardsFromDrawPile(player, CardsDrawnOnFullRelease, source);
        ShibiCombatUiPatch.RefreshFullReleaseButton();
        return true;
    }

    public static async Task<bool> TryActivateFullReleaseFromButton(Player player)
    {
        var mana = player.Creature.GetPower<ShibiManaPower>();
        if (mana == null || mana.Amount < ManaRequiredForFullRelease)
        {
            return false;
        }

        await PowerCmd.ModifyAmount(mana, -ManaRequiredForFullRelease, player.Creature, null);
        await GoldenButterflyKnot.SyncForPlayer(player);
        await PowerCmd.Apply<ShibiFullReleasePower>(player.Creature, FullReleaseNormalCardPlays, player.Creature, null);
        await NotifyFullReleaseActivated(player, null);
        await PlayerCmd.GainEnergy(EnergyGainedOnFullRelease, player);
        await DrawManaCardsFromDrawPile(player, CardsDrawnOnFullRelease, null);
        ShibiCombatUiPatch.RefreshFullReleaseButton();
        return true;
    }

    public static async Task HandleInterrupt(PlayerChoiceContext choiceContext, CardModel card, Creature? target, ShibiInterruptKind kind)
    {
        if (kind == ShibiInterruptKind.None || target == null || !target.IsMonster || target.IsDead)
        {
            return;
        }

        if (!DoesInterruptMatchIntent(target, kind))
        {
            return;
        }

        var toughness = await EnsureToughnessPower(target, card);
        if (toughness == null || target.HasPower<ShibiToughnessLockPower>())
        {
            return;
        }

        var toughnessDamage = 1m;
        var magicEye = card.Owner.Creature.GetPower<ShibiMagicEyePower>();
        if (magicEye != null && card.Type == CardType.Attack)
        {
            toughnessDamage += 1m;
            await PowerCmd.Decrement(magicEye);
        }

        await PowerCmd.ModifyAmount(toughness, -toughnessDamage, card.Owner.Creature, card);
        ShibiCombatUiPatch.UpdateToughnessBar(target);

        if (toughness.Amount > 0 || target.IsDead)
        {
            return;
        }

        await PowerCmd.Apply<ShibiToughnessLockPower>(target, 2m, card.Owner.Creature, card);
        await CreatureCmd.Stun(target);
        await PowerCmd.Apply<ShibiStunnedEnemyThisTurnPower>(card.Owner.Creature, 1m, card.Owner.Creature, card);
        await PowerCmd.Apply<ShibiStunnedEnemyThisCombatPower>(card.Owner.Creature, 1m, card.Owner.Creature, card);
        await GrantFullComboReward(choiceContext, card.Owner);
        await GrantStunReward(choiceContext, card.Owner, target);
        await ResetToughness(target, card);
    }

    public static async Task DealToughnessDamage(PlayerChoiceContext choiceContext, Player player, Creature target, decimal amount, CardModel source)
    {
        if (amount <= 0 || !target.IsMonster || target.IsDead || target.HasPower<ShibiToughnessLockPower>())
        {
            return;
        }

        var toughness = await EnsureToughnessPower(target, source);
        if (toughness == null)
        {
            return;
        }

        await PowerCmd.ModifyAmount(toughness, -amount, player.Creature, source);
        ShibiCombatUiPatch.UpdateToughnessBar(target);
        if (toughness.Amount > 0 || target.IsDead)
        {
            return;
        }

        await PowerCmd.Apply<ShibiToughnessLockPower>(target, 2m, player.Creature, source);
        await CreatureCmd.Stun(target);
        await PowerCmd.Apply<ShibiStunnedEnemyThisTurnPower>(player.Creature, 1m, player.Creature, source);
        await PowerCmd.Apply<ShibiStunnedEnemyThisCombatPower>(player.Creature, 1m, player.Creature, source);
        await GrantFullComboReward(choiceContext, player);
        await GrantStunReward(choiceContext, player, target);
        await ResetToughness(target, source);
    }

    public static int StunnedEnemiesThisTurn(Player player) =>
        Math.Max(0, player.Creature.GetPowerAmount<ShibiStunnedEnemyThisTurnPower>());

    public static int StunnedEnemiesThisCombat(Player player) =>
        Math.Max(0, player.Creature.GetPowerAmount<ShibiStunnedEnemyThisCombatPower>());

    public static async Task EnsureToughnessForCombat(CombatState? combatState, CardModel? source)
    {
        if (combatState == null)
        {
            return;
        }

        foreach (var enemy in combatState.Enemies.Where(e => e.IsMonster && !e.IsDead))
        {
            await EnsureToughnessPower(enemy, source);
        }
    }

    public static async Task EnsureShibiCombatTrackers(Player player, CardModel? source)
    {
        if (!player.Creature.HasPower<ShibiTurnCardTypeCounterPower>())
        {
            await PowerCmd.Apply<ShibiTurnCardTypeCounterPower>(player.Creature, 1m, player.Creature, source);
        }
    }

    public static async Task NotifyFullReleaseActivated(Player player, CardModel? source)
    {
        var roundFoxPrinciple = player.Creature.GetPower<ShibiRoundFoxPrinciplePower>();
        if (roundFoxPrinciple != null)
        {
            await roundFoxPrinciple.AfterFullReleaseActivated(source);
        }
    }

    public static async Task DrawManaCardsFromDrawPile(Player player, int count, AbstractModel? source)
    {
        var drawPile = PileType.Draw.GetPile(player);
        var manaCards = drawPile.Cards
            .OfType<Cards.ShibiBasicCard>()
            .Where(card => card.ManaGain > 0)
            .Cast<CardModel>()
            .Take(count)
            .ToList();

        foreach (var card in manaCards)
        {
            await CardPileCmd.Add(card, PileType.Hand, CardPilePosition.Top, source);
        }
    }

    private static bool DoesInterruptMatchIntent(Creature target, ShibiInterruptKind kind)
    {
        var intentTypes = target.Monster?.NextMove.Intents.Select(intent => intent.IntentType).ToList();
        if (intentTypes == null || intentTypes.Count == 0)
        {
            return false;
        }

        return kind switch
        {
            ShibiInterruptKind.Attack => intentTypes.Any(type => type is IntentType.Attack or IntentType.DeathBlow),
            ShibiInterruptKind.Block => intentTypes.Any(type => type is IntentType.Defend),
            ShibiInterruptKind.Status => intentTypes.Any(type => type is not IntentType.Attack and not IntentType.DeathBlow and not IntentType.Defend and not IntentType.Sleep and not IntentType.Unknown),
            _ => false
        };
    }

    public static async Task RemoveToughnessLock(Creature target)
    {
        await PowerCmd.Remove<ShibiToughnessLockPower>(target);
    }

    private static async Task<ShibiToughnessPower?> EnsureToughnessPower(Creature target, CardModel? source)
    {
        var current = target.GetPower<ShibiToughnessPower>();
        if (current != null)
        {
            return current;
        }

        var applied = await PowerCmd.Apply<ShibiToughnessPower>(target, GetMaxToughness(target), source?.Owner.Creature, source);
        ShibiCombatUiPatch.UpdateToughnessBar(target);
        return applied;
    }

    private static async Task ResetToughness(Creature target, CardModel source)
    {
        await PowerCmd.SetAmount<ShibiToughnessPower>(target, GetMaxToughness(target), source.Owner.Creature, source);
        ShibiCombatUiPatch.UpdateToughnessBar(target);
    }

    private static async Task GrantStunReward(PlayerChoiceContext choiceContext, Player player, Creature target)
    {
        var roomType = target.CombatState?.Encounter?.RoomType ?? RoomType.Monster;
        var energy = roomType switch
        {
            RoomType.Boss => 2,
            _ => 1
        };
        var cards = roomType switch
        {
            RoomType.Boss => 3,
            RoomType.Elite => 2,
            _ => 1
        };

        await PlayerCmd.GainEnergy(energy, player);
        await CardPileCmd.Draw(choiceContext, cards, player);
    }

    private static async Task GrantFullComboReward(PlayerChoiceContext choiceContext, Player player)
    {
        if (!player.Creature.HasPower<ShibiFullComboPower>())
        {
            return;
        }

        var amount = Math.Max(1, player.Creature.GetPowerAmount<ShibiFullComboPower>());
        await PlayerCmd.GainEnergy(amount, player);
        await CardPileCmd.Draw(choiceContext, amount, player);
    }

    public static int GetMaxToughness(Creature target)
    {
        var roomType = target.CombatState?.Encounter?.RoomType ?? RoomType.Monster;
        return roomType switch
        {
            RoomType.Boss => 4,
            RoomType.Elite => 3,
            _ => 2
        };
    }
}
