using System;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using shirubimod.Scripts.Content.Cards;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace shirubimod.Scripts.Content.Powers;

[RegisterPower]
public sealed class ShibiManaPower : ModPowerTemplate
{
    public override PowerAssetProfile AssetProfile => new(
        IconPath: "res://images/powers/shirubi_mana_power.png",
        BigIconPath: "res://images/powers/shirubi_mana_power.png");

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
}

[RegisterPower]
public sealed class ShibiFullReleasePower : ModPowerTemplate
{
    public override PowerAssetProfile AssetProfile => new(
        IconPath: "res://images/powers/shirubi_full_release_power.png",
        BigIconPath: "res://images/powers/shirubi_full_release_power.png");

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
}

[RegisterPower]
public sealed class ShibiToughnessPower : ModPowerTemplate
{
    public override PowerAssetProfile AssetProfile => new(
        IconPath: "res://images/powers/shirubi_toughness_power.png",
        BigIconPath: "res://images/powers/shirubi_toughness_power.png");

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
}

[RegisterPower]
public sealed class ShibiToughnessLockPower : ModPowerTemplate
{
    public override PowerAssetProfile AssetProfile => new(
        IconPath: "res://images/powers/shirubi_toughness_lock_power.png",
        BigIconPath: "res://images/powers/shirubi_toughness_lock_power.png");

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterTurnEnd(PlayerChoiceContext choiceContext, CombatSide side)
    {
        if (Owner.Side == side)
        {
            // The lock starts at 2 after a stun: one monster turn is spent stunned,
            // and one following monster turn is protected from being stunned again.
            await PowerCmd.Decrement(this);
        }
    }
}

[RegisterPower]
public sealed class ShibiMagicEyePower : ModPowerTemplate
{
    public override PowerAssetProfile AssetProfile => new(
        IconPath: "res://images/powers/shirubi_magic_eye_power.png",
        BigIconPath: "res://images/powers/shirubi_magic_eye_power.png");

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
}

[RegisterPower]
public sealed class ShibiNextTurnMagicEyePower : ModPowerTemplate
{
    public override PowerAssetProfile AssetProfile => new(
        IconPath: "res://images/powers/shirubi_magic_eye_power.png",
        BigIconPath: "res://images/powers/shirubi_magic_eye_power.png");

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player.Creature != Owner)
        {
            return;
        }

        await PowerCmd.Apply<ShibiMagicEyePower>(Owner, Amount, Owner, null);
        await PowerCmd.Remove(this);
    }
}

[RegisterPower]
public sealed class ShibiFullComboPower : ModPowerTemplate
{
    public override PowerAssetProfile AssetProfile => new(
        IconPath: "res://images/powers/shirubi_full_combo_power.png",
        BigIconPath: "res://images/powers/shirubi_full_combo_power.png");

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
}

[RegisterPower]
public sealed class ShibiManaResearchPower : ModPowerTemplate
{
    private sealed class Data
    {
        public int ManaCardsPlayed;
    }

    public override PowerAssetProfile AssetProfile => new(
        IconPath: "res://images/powers/shirubi_mana_power.png",
        BigIconPath: "res://images/powers/shirubi_mana_power.png");

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override object InitInternalData() => new Data();

    public override async Task AfterCardPlayed(PlayerChoiceContext context, CardPlay cardPlay)
    {
        if (cardPlay.Card.Owner.Creature != Owner || cardPlay.Card is not ShibiBasicCard { ManaGain: > 0 })
        {
            return;
        }

        var data = GetInternalData<Data>();
        data.ManaCardsPlayed++;
        while (data.ManaCardsPlayed >= 2)
        {
            data.ManaCardsPlayed -= 2;
            if (Owner.Player == null)
            {
                return;
            }

            await ShibiMechanics.GainMana(Owner.Player, Amount, null);
            await PowerCmd.Apply<StrengthPower>(Owner, Amount, Owner, null);
        }
    }

    public override async Task AfterTurnEnd(PlayerChoiceContext choiceContext, CombatSide side)
    {
        if (Owner.Side == side)
        {
            await PowerCmd.Remove(this);
        }
    }
}

[RegisterPower]
public sealed class ShibiRagingMagicPower : ModPowerTemplate
{
    public override PowerAssetProfile AssetProfile => new(
        IconPath: "res://images/powers/shirubi_full_release_power.png",
        BigIconPath: "res://images/powers/shirubi_full_release_power.png");

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override decimal ModifyDamageAdditive(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (dealer == Owner && cardSource is ShibiBasicCard { ManaGain: > 0, Type: CardType.Attack })
        {
            return Amount;
        }

        return 0m;
    }

    public override bool TryModifyEnergyCostInCombat(CardModel card, decimal originalCost, out decimal modifiedCost)
    {
        if (card.Owner?.Creature == Owner && card is ShibiBasicCard { ManaGain: > 0, Type: CardType.Attack })
        {
            modifiedCost = originalCost + Math.Max(1, Amount / 4);
            return true;
        }

        modifiedCost = originalCost;
        return false;
    }

    public override async Task AfterTurnEnd(PlayerChoiceContext choiceContext, CombatSide side)
    {
        if (Owner.Side == side)
        {
            await PowerCmd.Remove(this);
        }
    }
}

[RegisterPower]
public sealed class ShibiFoxSpiritIntuitionPower : ModPowerTemplate
{
    private const decimal HitsRequired = 3m;

    public override PowerAssetProfile AssetProfile => new(
        IconPath: "res://images/powers/shirubi_magic_eye_power.png",
        BigIconPath: "res://images/powers/shirubi_magic_eye_power.png");

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterDamageReceived(PlayerChoiceContext choiceContext, Creature target, DamageResult result, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (target != Owner || dealer == null || !dealer.IsMonster || result.TotalDamage <= 0)
        {
            return;
        }

        await PowerCmd.ModifyAmount(this, -1m, Owner, null);
        if (Amount > 0)
        {
            return;
        }

        await PowerCmd.Apply<ShibiMagicEyePower>(Owner, 1m, Owner, null);
        await PowerCmd.SetAmount<ShibiFoxSpiritIntuitionPower>(Owner, HitsRequired, Owner, null);
    }
}

[RegisterPower]
public sealed class ShibiManaAbsorbPower : ModPowerTemplate
{
    public override PowerAssetProfile AssetProfile => new(
        IconPath: "res://images/powers/shirubi_mana_absorb_power.png",
        BigIconPath: "res://images/powers/shirubi_mana_absorb_power.png");

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
}

[RegisterPower]
public sealed class ShibiRoundFoxPrinciplePower : ModPowerTemplate
{
    public override PowerAssetProfile AssetProfile => new(
        IconPath: "res://images/powers/shirubi_matoka.png",
        BigIconPath: "res://images/powers/shirubi_matoka.png");

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player.Creature != Owner)
        {
            return;
        }

        await PowerCmd.ModifyAmount(this, -1m, Owner, null);
        if (Amount > 0)
        {
            return;
        }

        await PowerCmd.Apply<IntangiblePower>(Owner, 1m, Owner, null);
        await PowerCmd.SetAmount<ShibiRoundFoxPrinciplePower>(Owner, 5m, Owner, null);
    }

    public async Task AfterFullReleaseActivated(CardModel? source)
    {
        await PowerCmd.Apply<IntangiblePower>(Owner, 1m, Owner, source);
    }
}

[RegisterPower]
public sealed class ShibiTurnCardTypeCounterPower : ModPowerTemplate
{
    private sealed class Data
    {
        public int AttacksPlayed;
        public int SkillsPlayed;
    }

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    protected override bool IsVisibleInternal => false;
    protected override object InitInternalData() => new Data();

    public int AttacksPlayed => GetInternalData<Data>().AttacksPlayed;
    public int SkillsPlayed => GetInternalData<Data>().SkillsPlayed;

    public override Task AfterCardPlayed(PlayerChoiceContext context, CardPlay cardPlay)
    {
        if (Owner.Player == null || cardPlay.Card.Owner != Owner.Player)
        {
            return Task.CompletedTask;
        }

        var data = GetInternalData<Data>();
        if (cardPlay.Card.Type == CardType.Attack)
        {
            data.AttacksPlayed++;
        }
        else if (cardPlay.Card.Type == CardType.Skill)
        {
            data.SkillsPlayed++;
        }

        return Task.CompletedTask;
    }

    public override Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player.Creature == Owner)
        {
            var data = GetInternalData<Data>();
            data.AttacksPlayed = 0;
            data.SkillsPlayed = 0;
        }

        return Task.CompletedTask;
    }
}

[RegisterPower]
public sealed class ShibiDemonSlayingMagicCounterPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    protected override bool IsVisibleInternal => false;

    public override bool TryModifyEnergyCostInCombat(CardModel card, decimal originalCost, out decimal modifiedCost)
    {
        if (card.Owner?.Creature == Owner && card is ShibiDemonSlayingMagic)
        {
            modifiedCost = originalCost + Amount;
            return true;
        }

        modifiedCost = originalCost;
        return false;
    }

    public override async Task AfterTurnEnd(PlayerChoiceContext choiceContext, CombatSide side)
    {
        if (Owner.Side == side)
        {
            await PowerCmd.Remove(this);
        }
    }
}

[RegisterPower]
public sealed class ShibiNextTurnDrawPower : ModPowerTemplate
{
    public override PowerAssetProfile AssetProfile => new(
        IconPath: "res://images/powers/shirubi_magic_eye_power.png",
        BigIconPath: "res://images/powers/shirubi_magic_eye_power.png");

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player.Creature != Owner)
        {
            return;
        }

        await CardPileCmd.Draw(choiceContext, Amount, player);
        await PowerCmd.Remove(this);
    }
}

[RegisterPower]
public sealed class ShibiAppleStandPower : ModPowerTemplate
{
    private sealed class Data
    {
        public int CardsPlayed;
    }

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    protected override bool IsVisibleInternal => false;
    protected override object InitInternalData() => new Data();

    public override async Task AfterCardPlayed(PlayerChoiceContext context, CardPlay cardPlay)
    {
        if (cardPlay.Card.Owner.Creature != Owner)
        {
            return;
        }

        var data = GetInternalData<Data>();
        data.CardsPlayed++;
        var threshold = Math.Max(1, Amount);
        while (data.CardsPlayed >= threshold)
        {
            data.CardsPlayed -= (int)threshold;
            await PowerCmd.Apply<ShibiNextTurnDrawPower>(Owner, 1m, Owner, null);
        }
    }

    public override async Task AfterTurnEnd(PlayerChoiceContext choiceContext, CombatSide side)
    {
        if (Owner.Side == side)
        {
            await PowerCmd.Remove(this);
        }
    }
}

[RegisterPower]
public sealed class ShibiStunnedEnemyThisTurnPower : ModPowerTemplate
{
    public override PowerAssetProfile AssetProfile => new(
        IconPath: "res://images/powers/shirubi_toughness_lock_power.png",
        BigIconPath: "res://images/powers/shirubi_toughness_lock_power.png");

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterTurnEnd(PlayerChoiceContext choiceContext, CombatSide side)
    {
        if (Owner.Side == side)
        {
            await PowerCmd.Remove(this);
        }
    }
}

[RegisterPower]
public sealed class ShibiStunnedEnemyThisCombatPower : ModPowerTemplate
{
    public override PowerAssetProfile AssetProfile => new(
        IconPath: "res://images/powers/shirubi_toughness_lock_power.png",
        BigIconPath: "res://images/powers/shirubi_toughness_lock_power.png");

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override bool IsVisibleInternal => false;
}

[RegisterPower]
public sealed class ShibiPlatedArmorPower : ModPowerTemplate
{
    public override PowerAssetProfile AssetProfile => new(
        IconPath: "res://images/powers/shirubi_hardening_pattern_power.png",
        BigIconPath: "res://images/powers/shirubi_hardening_pattern_power.png");

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player.Creature != Owner)
        {
            return;
        }

        await CreatureCmd.GainBlock(Owner, Amount, ValueProp.Unpowered, null);
        await PowerCmd.Decrement(this);
    }
}

[RegisterPower]
public sealed class ShibiHardeningPatternPower : ModPowerTemplate
{
    public override PowerAssetProfile AssetProfile => new(
        IconPath: "res://images/powers/shirubi_hardening_pattern_power.png",
        BigIconPath: "res://images/powers/shirubi_hardening_pattern_power.png");

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, MegaCrit.Sts2.Core.Entities.Players.Player player)
    {
        if (player.Creature != Owner)
        {
            return;
        }

        var mana = Owner.GetPowerAmount<ShibiManaPower>();
        await CreatureCmd.GainBlock(Owner, Amount + mana / 3, ValueProp.Unpowered, null);
    }
}

[RegisterPower]
public sealed class ShibiHeroicSealPower : ModPowerTemplate
{
    public override PowerAssetProfile AssetProfile => new(
        IconPath: "res://images/powers/shirubi_heroic_seal_power.png",
        BigIconPath: "res://images/powers/shirubi_heroic_seal_power.png");

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, MegaCrit.Sts2.Core.Entities.Players.Player player)
    {
        if (player.Creature == Owner)
        {
            await PowerCmd.Apply<StrengthPower>(Owner, Amount, Owner, null);
        }
    }
}
