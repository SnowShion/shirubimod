using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Extensions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using shirubimod.Scripts.Content.Powers;
using STS2RitsuLib.Interop.AutoRegistration;

namespace shirubimod.Scripts.Content.Cards;

public abstract class ShibiRewardCard : ShibiBasicCard
{
    protected ShibiRewardCard(int energyCost, CardType type, TargetType targetType, CardRarity rarity)
        : base(energyCost, type, targetType, rarity)
    {
    }

    protected async Task AttackOne(PlayerChoiceContext choiceContext, CardPlay cardPlay, decimal damage, int hits = 1)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await DamageCmd.Attack(damage).WithHitCount(hits).FromCard(this).Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);
    }

    protected async Task AttackAll(PlayerChoiceContext choiceContext, decimal damage, int hits = 1)
    {
        await DamageCmd.Attack(damage).WithHitCount(hits).FromCard(this)
            .TargetingAllOpponents(CombatState!)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);
    }

    protected async Task GainBlock(CardPlay cardPlay, decimal block)
    {
        await CreatureCmd.GainBlock(Owner.Creature, block, ValueProp.Move, cardPlay);
    }

    protected async Task AddTemporaryExhaustEtherealCardToHand<T>(bool upgraded = false) where T : CardModel
    {
        var card = CombatState!.CreateCard<T>(Owner);
        if (upgraded)
        {
            CardCmd.Upgrade(card);
        }

        card.AddKeyword(CardKeyword.Exhaust);
        card.AddKeyword(CardKeyword.Ethereal);
        await CardPileCmd.Add(card, PileType.Hand, CardPilePosition.Bottom, this);
    }

    protected async Task AddTemporaryCardToHand<T>(bool upgraded = false, int costChange = 0, params CardKeyword[] keywords) where T : CardModel
    {
        var card = CombatState!.CreateCard<T>(Owner);
        if (upgraded)
        {
            CardCmd.Upgrade(card);
        }

        foreach (var keyword in keywords)
        {
            card.AddKeyword(keyword);
        }

        if (costChange != 0 && !card.EnergyCost.CostsX)
        {
            card.EnergyCost.AddThisCombat(costChange, reduceOnly: costChange < 0);
            card.InvokeEnergyCostChanged();
        }

        await CardPileCmd.Add(card, PileType.Hand, CardPilePosition.Bottom, this);
    }

    protected async Task<IReadOnlyList<CardModel>> SelectHandCards(PlayerChoiceContext choiceContext, string promptKey, int min, int max, Func<CardModel, bool>? filter = null, bool cancelable = false)
    {
        return (await CardSelectCmd.FromHand(
            choiceContext,
            Owner,
            new CardSelectorPrefs(new LocString("cards", promptKey), min, max) { Cancelable = cancelable },
            card => card != this && (filter?.Invoke(card) ?? true),
            this)).ToList();
    }

    protected int ExhaustedManaCardsThisCombat()
    {
        return PileType.Exhaust.GetPile(Owner).Cards.OfType<ShibiBasicCard>().Count(card => card.ManaGain > 0);
    }
}

[RegisterCard(typeof(ShirubiCardPool), FullPublicEntry = "SHIBI_WEAKNESS_INSIGHT")]
public sealed class ShibiWeaknessInsight : ShibiRewardCard
{
    public ShibiWeaknessInsight() : base(1, CardType.Skill, TargetType.Self, CardRarity.Uncommon) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<ShibiMagicEyePower>(Owner.Creature, 1m, Owner.Creature, this);
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }
}

[RegisterCard(typeof(ShirubiCardPool), FullPublicEntry = "SHIBI_HAPPY_WATER")]
public sealed class ShibiHappyWater : ShibiRewardCard
{
    public override int ManaGain => 1;
    public ShibiHappyWater() : base(0, CardType.Skill, TargetType.Self, CardRarity.Common) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PlayerCmd.GainEnergy(1, Owner);
        if (IsUpgraded)
        {
            await CardPileCmd.Draw(choiceContext, 1, Owner);
        }
        await FinishManaCard(choiceContext, cardPlay);
    }
}

[RegisterCard(typeof(ShirubiCardPool), FullPublicEntry = "SHIBI_UNO_PLUS_TWO")]
public sealed class ShibiUnoPlusTwo : ShibiRewardCard
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [new CardsVar(2)];
    public ShibiUnoPlusTwo() : base(1, CardType.Skill, TargetType.Self, CardRarity.Common) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CardPileCmd.Draw(choiceContext, IsFullReleaseActive ? 4 : DynamicVars.Cards.IntValue, Owner);
        await ShibiMechanics.ConsumeFullReleaseUse(Owner, this);
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }
}

[RegisterCard(typeof(ShirubiCardPool), FullPublicEntry = "SHIBI_SHADOW_IMAGE")]
public sealed class ShibiShadowImage : ShibiRewardCard
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => IsUpgraded ? [CardKeyword.Retain] : [];
    public ShibiShadowImage() : base(2, CardType.Skill, TargetType.Self, CardRarity.Uncommon) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<DuplicationPower>(Owner.Creature, 1m, Owner.Creature, this);
    }

    protected override void OnUpgrade()
    {
        _ = Keywords;
        AddKeyword(CardKeyword.Retain);
    }
}

[RegisterCard(typeof(ShirubiCardPool), FullPublicEntry = "SHIBI_YOU_GIVE_LUDA")]
public sealed class ShibiYouGiveLuda : ShibiRewardCard
{
    public ShibiYouGiveLuda() : base(3, CardType.Skill, TargetType.Self, CardRarity.Rare) { }

    public override bool TryModifyEnergyCostInCombat(CardModel card, decimal originalCost, out decimal modifiedCost)
    {
        if (card == this && CombatState != null)
        {
            modifiedCost = originalCost - Owner.Creature.GetPowerAmount<ShibiMagicEyePower>();
            return true;
        }

        modifiedCost = originalCost;
        return false;
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<IntangiblePower>(Owner.Creature, 1m, Owner.Creature, this);
        if (IsUpgraded)
        {
            await PowerCmd.Apply<DexterityPower>(Owner.Creature, 1m, Owner.Creature, this);
        }
    }
}

[RegisterCard(typeof(ShirubiCardPool), FullPublicEntry = "SHIBI_LIFE_LIGHT")]
public sealed class ShibiLifeLight : ShibiRewardCard
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("PlatedArmor", 4m)];
    public ShibiLifeLight() : base(1, CardType.Power, TargetType.Self, CardRarity.Uncommon) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var platedArmor = DynamicVars["PlatedArmor"].BaseValue + (IsFullReleaseActive ? 3m : 0m);
        await PowerCmd.Apply<ShibiPlatedArmorPower>(Owner.Creature, platedArmor, Owner.Creature, this);
        await ShibiMechanics.ConsumeFullReleaseUse(Owner, this);
    }

    protected override void OnUpgrade()
    {
        DynamicVars["PlatedArmor"].UpgradeValueBy(2m);
    }
}

[RegisterCard(typeof(ShirubiCardPool), FullPublicEntry = "SHIBI_METEOR")]
public sealed class ShibiMeteor : ShibiRewardCard
{
    public override int ManaGain => 1;
    public ShibiMeteor() : base(2, CardType.Skill, TargetType.AnyEnemy, CardRarity.Uncommon) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await PowerCmd.Apply<VulnerablePower>(cardPlay.Target, IsUpgraded ? 3m : 1m, Owner.Creature, this);
        await ShibiMechanics.RemoveToughnessLock(cardPlay.Target);
        await FinishManaCard(choiceContext, cardPlay);
    }
}

[RegisterCard(typeof(ShirubiCardPool), FullPublicEntry = "SHIBI_MY_TURN")]
public sealed class ShibiMyTurn : ShibiRewardCard
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    public ShibiMyTurn() : base(2, CardType.Skill, TargetType.Self, CardRarity.Rare) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (Owner.Creature.HasPower<ShibiStunnedEnemyThisTurnPower>())
        {
            await CardPileCmd.Draw(choiceContext, IsUpgraded ? 3 : 2, Owner);
            await PlayerCmd.GainEnergy(3, Owner);
        }
    }
}

[RegisterCard(typeof(ShirubiCardPool), FullPublicEntry = "SHIBI_STILL_MY_TURN")]
public sealed class ShibiStillMyTurn : ShibiRewardCard
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    public ShibiStillMyTurn() : base(3, CardType.Skill, TargetType.Self, CardRarity.Rare) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (Owner.Creature.HasPower<ShibiStunnedEnemyThisTurnPower>())
        {
            await ShibiMechanics.GainMana(Owner, IsUpgraded ? 17 : 12, this);
        }
    }
}

[RegisterCard(typeof(ShirubiCardPool), FullPublicEntry = "SHIBI_DIRECT_MAGIC_EYE")]
public sealed class ShibiDirectMagicEye : ShibiRewardCard
{
    public ShibiDirectMagicEye() : base(1, CardType.Skill, TargetType.AnyEnemy, CardRarity.Uncommon) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await PowerCmd.Apply<ShibiNextTurnMagicEyePower>(Owner.Creature, 1m, Owner.Creature, this);
        await PowerCmd.Apply<VulnerablePower>(cardPlay.Target, 1m, Owner.Creature, this);
        if (IsUpgraded)
        {
            await PowerCmd.Apply<WeakPower>(cardPlay.Target, 1m, Owner.Creature, this);
        }
    }
}

[RegisterCard(typeof(ShirubiCardPool), FullPublicEntry = "SHIBI_TACTICAL_RETREAT")]
public sealed class ShibiTacticalRetreat : ShibiRewardCard
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new BlockVar(3m, ValueProp.Move)];
    public override bool GainsBlock => true;
    public ShibiTacticalRetreat() : base(0, CardType.Skill, TargetType.Self, CardRarity.Uncommon) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await GainBlock(cardPlay, DynamicVars.Block.BaseValue);
        var handCards = PileType.Hand.GetPile(Owner).Cards
            .Where(card => card != this && !card.EnergyCost.CostsX && card.EnergyCost.GetWithModifiers(CostModifiers.All) > 0)
            .ToList();
        if (handCards.Count == 0)
        {
            return;
        }

        var selectedCard = (await CardSelectCmd.FromHand(
            choiceContext,
            Owner,
            new CardSelectorPrefs(new LocString("cards", "SHIBI_TACTICAL_RETREAT.selectionScreenPrompt"), 0, 1) { Cancelable = true },
            card => handCards.Contains(card),
            this)).FirstOrDefault();
        if (selectedCard != null)
        {
            selectedCard.EnergyCost.AddThisCombat(-1, reduceOnly: true);
            selectedCard.InvokeEnergyCostChanged();
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(4m);
    }
}

[RegisterCard(typeof(ShirubiCardPool), FullPublicEntry = "SHIBI_WHAT_TO_EAT")]
public sealed class ShibiWhatToEat : ShibiRewardCard
{
    public override bool GainsBlock => true;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new BlockVar(5m, ValueProp.Move)];
    public ShibiWhatToEat() : base(1, CardType.Skill, TargetType.Self, CardRarity.Common) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await GainBlock(cardPlay, DynamicVars.Block.BaseValue);
        await AddTemporaryExhaustEtherealCardToHand<ShibiYeahWhatToEat>(IsUpgraded);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(3m);
    }
}

[RegisterCard(typeof(ShirubiCardPool), FullPublicEntry = "SHIBI_YEAH_WHAT_TO_EAT")]
public sealed class ShibiYeahWhatToEat : ShibiRewardCard
{
    public override bool CanBeGeneratedInCombat => false;
    public override bool CanBeGeneratedByModifiers => false;
    public override bool GainsBlock => true;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new BlockVar(5m, ValueProp.Move)];
    public ShibiYeahWhatToEat() : base(1, CardType.Skill, TargetType.Self, CardRarity.Token) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await GainBlock(cardPlay, DynamicVars.Block.BaseValue);
        await AddTemporaryExhaustEtherealCardToHand<ShibiWhatToEat>(IsUpgraded);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(3m);
    }
}

[RegisterCard(typeof(ShirubiCardPool), FullPublicEntry = "SHIBI_DONE_AND_GONE")]
public sealed class ShibiDoneAndGone : ShibiRewardCard
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    public override bool GainsBlock => true;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new BlockVar(13m, ValueProp.Move)];
    public ShibiDoneAndGone() : base(1, CardType.Skill, TargetType.Self, CardRarity.Common) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await GainBlock(cardPlay, DynamicVars.Block.BaseValue);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(4m);
    }
}

[RegisterCard(typeof(ShirubiCardPool), FullPublicEntry = "SHIBI_BUT_THAT_IS_LOSER_THINKING")]
public sealed class ShibiButThatIsLoserThinking : ShibiRewardCard
{
    public ShibiButThatIsLoserThinking() : base(1, CardType.Skill, TargetType.Self, CardRarity.Common) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<StrengthPower>(Owner.Creature, 1m, Owner.Creature, this);
        await PowerCmd.Apply<VulnerablePower>(Owner.Creature, 1m, Owner.Creature, this);
        if (IsUpgraded)
        {
            await CardPileCmd.Draw(choiceContext, 1, Owner);
        }
    }
}

[RegisterCard(typeof(ShirubiCardPool), FullPublicEntry = "SHIBI_ISEKAI_FARMSTAY")]
public sealed class ShibiIsekaiFarmstay : ShibiRewardCard
{
    public override int ManaGain => 1;
    public override bool GainsBlock => true;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new BlockVar(8m, ValueProp.Move), new DynamicVar("FullReleaseBlock", 12m)];
    public ShibiIsekaiFarmstay() : base(2, CardType.Skill, TargetType.AnyEnemy, CardRarity.Common) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await GainBlock(cardPlay, IsFullReleaseActive ? DynamicVars["FullReleaseBlock"].BaseValue : DynamicVars.Block.BaseValue);
        await PowerCmd.Apply<WeakPower>(cardPlay.Target, 3m, Owner.Creature, this);
        await FinishManaCard(choiceContext, cardPlay);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(4m);
        DynamicVars["FullReleaseBlock"].UpgradeValueBy(4m);
    }
}

[RegisterCard(typeof(ShirubiCardPool), FullPublicEntry = "SHIBI_NINE_TAIL_FUNERAL")]
public sealed class ShibiNineTailFuneral : ShibiRewardCard
{
    public override int ManaGain => 3;
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    protected override bool HasEnergyCostX => true;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(9m, ValueProp.Move)];
    public ShibiNineTailFuneral() : base(0, CardType.Attack, TargetType.AnyEnemy, CardRarity.Common) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var hits = ResolveEnergyXValue() + (IsFullReleaseActive ? 1 : 0);
        if (hits > 0)
        {
            await AttackOne(choiceContext, cardPlay, DynamicVars.Damage.BaseValue, hits);
        }
        await FinishManaCard(choiceContext, cardPlay);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(3m);
    }
}

[RegisterCard(typeof(ShirubiCardPool), FullPublicEntry = "SHIBI_CURSE_POWER_RELEASE")]
public sealed class ShibiCursePowerRelease : ShibiRewardCard
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => IsUpgraded ? [CardKeyword.Retain] : [];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(4m, ValueProp.Move)];
    public ShibiCursePowerRelease() : base(0, CardType.Attack, TargetType.AnyEnemy, CardRarity.Common) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await AttackOne(choiceContext, cardPlay, DynamicVars.Damage.BaseValue);
        if (cardPlay.Target.HasPower<ShibiToughnessLockPower>())
        {
            await PlayerCmd.GainEnergy(1, Owner);
        }
    }

    protected override void OnUpgrade()
    {
        _ = Keywords;
        AddKeyword(CardKeyword.Retain);
    }
}

[RegisterCard(typeof(ShirubiCardPool), FullPublicEntry = "SHIBI_WAND_THOUSAND_ARRAY")]
public sealed class ShibiWandThousandArray : ShibiRewardCard
{
    public override int ManaGain => IsUpgraded ? 4 : 3;
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(12m, ValueProp.Move)];
    public ShibiWandThousandArray() : base(0, CardType.Attack, TargetType.AnyEnemy, CardRarity.Common) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await AttackOne(choiceContext, cardPlay, DynamicVars.Damage.BaseValue, IsFullReleaseActive ? 2 : 1);
        await FinishManaCard(choiceContext, cardPlay);
    }
}

[RegisterCard(typeof(ShirubiCardPool), FullPublicEntry = "SHIBI_FLUFFY_TAIL")]
public sealed class ShibiFluffyTail : ShibiRewardCard
{
    public override int ManaGain => IsUpgraded ? 3 : 2;
    public override ShibiInterruptKind InterruptKind => ShibiInterruptKind.Status;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(3m, ValueProp.Move)];
    public ShibiFluffyTail() : base(0, CardType.Attack, TargetType.AnyEnemy, CardRarity.Common) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await AttackOne(choiceContext, cardPlay, DynamicVars.Damage.BaseValue);
        await HandleInterrupt(choiceContext, cardPlay);
        if (IsFullReleaseActive)
        {
            await PowerCmd.Apply<ShibiMagicEyePower>(Owner.Creature, 1m, Owner.Creature, this);
        }
        await FinishManaCard(choiceContext, cardPlay);
    }
}

[RegisterCard(typeof(ShirubiCardPool), FullPublicEntry = "SHIBI_SKY_FALL")]
public sealed class ShibiSkyFall : ShibiRewardCard
{
    public override int ManaGain => IsUpgraded ? 5 : 3;
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(30m, ValueProp.Move)];
    public ShibiSkyFall() : base(3, CardType.Attack, TargetType.AnyEnemy, CardRarity.Common) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var selected = await SelectHandCards(choiceContext, "SHIBI_SKY_FALL.selectionScreenPrompt", 0, 1, null, cancelable: true);
        foreach (var card in selected)
        {
            await CardCmd.Exhaust(choiceContext, card);
        }

        await AttackOne(choiceContext, cardPlay, DynamicVars.Damage.BaseValue + (IsFullReleaseActive ? 17m : 0m));
        await FinishManaCard(choiceContext, cardPlay);
    }
}

[RegisterCard(typeof(ShirubiCardPool), FullPublicEntry = "SHIBI_SUPER_MAGIC_CANNON")]
public sealed class ShibiSuperMagicCannon : ShibiRewardCard
{
    public override int ManaGain => 2;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(10m, ValueProp.Move)];
    public ShibiSuperMagicCannon() : base(1, CardType.Attack, TargetType.AnyEnemy, CardRarity.Common) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var selected = await SelectHandCards(choiceContext, "SHIBI_SUPER_MAGIC_CANNON.selectionScreenPrompt", 0, 1, null, cancelable: true);
        await CardCmd.Discard(choiceContext, selected);
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await AttackOne(choiceContext, cardPlay, DynamicVars.Damage.BaseValue + (IsFullReleaseActive ? 7m : 0m));
        if (cardPlay.Target.IsDead)
        {
            await CardPileCmd.Draw(choiceContext, 1, Owner);
        }
        await FinishManaCard(choiceContext, cardPlay);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(4m);
    }
}

[RegisterCard(typeof(ShirubiCardPool), FullPublicEntry = "SHIBI_FOX_SHOVEL")]
public sealed class ShibiFoxShovel : ShibiRewardCard
{
    public override ShibiInterruptKind InterruptKind => ShibiInterruptKind.Attack;
    public override IEnumerable<CardKeyword> CanonicalKeywords => IsUpgraded ? [CardKeyword.Retain] : [];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(5m, ValueProp.Move)];
    public ShibiFoxShovel() : base(1, CardType.Attack, TargetType.AnyEnemy, CardRarity.Common) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var stunnedBefore = ShibiMechanics.StunnedEnemiesThisTurn(Owner);
        await AttackOne(choiceContext, cardPlay, DynamicVars.Damage.BaseValue);
        await HandleInterrupt(choiceContext, cardPlay);
        if (ShibiMechanics.StunnedEnemiesThisTurn(Owner) > stunnedBefore)
        {
            await PlayerCmd.GainEnergy(1, Owner);
        }
    }

    protected override void OnUpgrade()
    {
        _ = Keywords;
        AddKeyword(CardKeyword.Retain);
    }
}

[RegisterCard(typeof(ShirubiCardPool), FullPublicEntry = "SHIBI_FOXTRAAK")]
public sealed class ShibiFoxtraak : ShibiRewardCard
{
    public ShibiFoxtraak() : base(1, CardType.Attack, TargetType.Self, CardRarity.Common) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        for (var i = 0; i < 3; i++)
        {
            await AddTemporaryCardToHand<ShibiDemonSlayingMagic>(IsUpgraded);
        }
    }
}

[RegisterCard(typeof(ShirubiCardPool), FullPublicEntry = "SHIBI_DEMON_SLAYING_MAGIC")]
public sealed class ShibiDemonSlayingMagic : ShibiRewardCard
{
    public override bool CanBeGeneratedInCombat => false;
    public override bool CanBeGeneratedByModifiers => false;
    public override int ManaGain => 2;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(4m, ValueProp.Move)];
    public ShibiDemonSlayingMagic() : base(0, CardType.Attack, TargetType.AnyEnemy, CardRarity.Token) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await AttackOne(choiceContext, cardPlay, DynamicVars.Damage.BaseValue, IsFullReleaseActive ? 2 : 1);
        await PowerCmd.Apply<ShibiDemonSlayingMagicCounterPower>(Owner.Creature, 1m, Owner.Creature, this);
        await FinishManaCard(choiceContext, cardPlay);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(3m);
    }
}

[RegisterCard(typeof(ShirubiCardPool), FullPublicEntry = "SHIBI_I_AM_MANAMIC")]
public sealed class ShibiIAmManamic : ShibiRewardCard
{
    public override int ManaGain => 2;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(6m, ValueProp.Move)];
    public ShibiIAmManamic() : base(2, CardType.Attack, TargetType.AllEnemies, CardRarity.Common) { }

    public override bool TryModifyEnergyCostInCombat(CardModel card, decimal originalCost, out decimal modifiedCost)
    {
        if (card == this && CombatState != null)
        {
            modifiedCost = originalCost - ExhaustedManaCardsThisCombat();
            return true;
        }

        modifiedCost = originalCost;
        return false;
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await AttackAll(choiceContext, DynamicVars.Damage.BaseValue, IsFullReleaseActive ? 2 : 1);
        await FinishManaCard(choiceContext, cardPlay);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(4m);
    }
}

[RegisterCard(typeof(ShirubiCardPool), FullPublicEntry = "SHIBI_SONIC_BLADE")]
public sealed class ShibiSonicBlade : ShibiRewardCard
{
    public override int ManaGain => 1;
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    public override bool GainsBlock => true;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(5m, ValueProp.Move), new BlockVar(5m, ValueProp.Move)];
    public ShibiSonicBlade() : base(1, CardType.Attack, TargetType.AnyEnemy, CardRarity.Common) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await AttackOne(choiceContext, cardPlay, DynamicVars.Damage.BaseValue + (IsFullReleaseActive ? 5m : 0m));
        await GainBlock(cardPlay, DynamicVars.Block.BaseValue + (IsFullReleaseActive ? 5m : 0m));
        await FinishManaCard(choiceContext, cardPlay);
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }
}

[RegisterCard(typeof(ShirubiCardPool), FullPublicEntry = "SHIBI_WHITE_FOX_STAR")]
public sealed class ShibiWhiteFoxStar : ShibiRewardCard
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => IsUpgraded ? [CardKeyword.Retain] : [];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(1m, ValueProp.Move)];
    public ShibiWhiteFoxStar() : base(1, CardType.Attack, TargetType.AnyEnemy, CardRarity.Common) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await ShibiMechanics.EnsureShibiCombatTrackers(Owner, this);
        var counter = Owner.Creature.GetPower<ShibiTurnCardTypeCounterPower>();
        var fullReleaseBonus = IsFullReleaseActive ? 1 : 0;
        var damage = DynamicVars.Damage.BaseValue + (counter?.AttacksPlayed ?? 0) + 1 + fullReleaseBonus;
        var hits = (counter?.SkillsPlayed ?? 0) + 1 + fullReleaseBonus;
        await AttackOne(choiceContext, cardPlay, damage, hits);
        await ShibiMechanics.ConsumeFullReleaseUse(Owner, this);
    }

    protected override void OnUpgrade()
    {
        _ = Keywords;
        AddKeyword(CardKeyword.Retain);
    }
}

[RegisterCard(typeof(ShirubiCardPool), FullPublicEntry = "SHIBI_KUNLAN_ICE_FLAME")]
public sealed class ShibiKunlanIceFlame : ShibiRewardCard
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Ethereal];
    public ShibiKunlanIceFlame() : base(1, CardType.Skill, TargetType.Self, CardRarity.Common) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var selected = await SelectHandCards(choiceContext, "SHIBI_KUNLAN_ICE_FLAME.selectionScreenPrompt", 0, 1, null, cancelable: true);
        if (selected.Count == 0)
        {
            return;
        }

        await CardCmd.Exhaust(choiceContext, selected[0]);
        await PlayerCmd.GainEnergy(1, Owner);
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }
}

[RegisterCard(typeof(ShirubiCardPool), FullPublicEntry = "SHIBI_GOLDEN_EARS_DOUBLE_SPEED")]
public sealed class ShibiGoldenEarsDoubleSpeed : ShibiRewardCard
{
    public ShibiGoldenEarsDoubleSpeed() : base(1, CardType.Skill, TargetType.Self, CardRarity.Common) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var selected = await SelectHandCards(choiceContext, "SHIBI_GOLDEN_EARS_DOUBLE_SPEED.selectionScreenPrompt", 0, 2, null, cancelable: true);
        await CardCmd.Discard(choiceContext, selected);
        await ShibiMechanics.DrawManaCardsFromDrawPile(Owner, 1, this);
    }
}

[RegisterCard(typeof(ShirubiCardPool), FullPublicEntry = "SHIBI_FOX_BREATH")]
public sealed class ShibiFoxBreath : ShibiRewardCard
{
    public override int ManaGain => 1;
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new CardsVar(2)];
    public ShibiFoxBreath() : base(1, CardType.Skill, TargetType.Self, CardRarity.Common) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var selected = await SelectHandCards(choiceContext, "SHIBI_FOX_BREATH.selectionScreenPrompt", 0, 1, null, cancelable: true);
        foreach (var card in selected)
        {
            await CardCmd.Exhaust(choiceContext, card);
        }

        await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.IntValue, Owner);
        await FinishManaCard(choiceContext, cardPlay);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Cards.UpgradeValueBy(1m);
    }
}

[RegisterCard(typeof(ShirubiCardPool), FullPublicEntry = "SHIBI_APPLE_STAND")]
public sealed class ShibiAppleStand : ShibiRewardCard
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    public ShibiAppleStand() : base(0, CardType.Skill, TargetType.Self, CardRarity.Common) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<ShibiAppleStandPower>(Owner.Creature, IsUpgraded ? 3m : 4m, Owner.Creature, this);
    }
}

[RegisterCard(typeof(ShirubiCardPool), FullPublicEntry = "SHIBI_WOULD_YOU_LIKE_FOX")]
public sealed class ShibiWouldYouLikeFox : ShibiRewardCard
{
    public ShibiWouldYouLikeFox() : base(2, CardType.Skill, TargetType.Self, CardRarity.Uncommon) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<BufferPower>(Owner.Creature, 1m, Owner.Creature, this);
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }
}

[RegisterCard(typeof(ShirubiCardPool), FullPublicEntry = "SHIBI_SLEEP_WELL")]
public sealed class ShibiSleepWell : ShibiRewardCard
{
    public override int ManaGain => 2;
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    public ShibiSleepWell() : base(1, CardType.Skill, TargetType.Self, CardRarity.Common) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<RegenPower>(Owner.Creature, IsUpgraded ? 4m : 3m, Owner.Creature, this);
        await FinishManaCard(choiceContext, cardPlay);
        PlayerCmd.EndTurn(Owner, canBackOut: false);
    }
}

[RegisterCard(typeof(ShirubiCardPool), FullPublicEntry = "SHIBI_NO_FOX_NO_LIFE")]
public sealed class ShibiNoFoxNoLife : ShibiRewardCard
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    public ShibiNoFoxNoLife() : base(1, CardType.Skill, TargetType.Self, CardRarity.Common) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await AddTemporaryCardToHand<ShibiHitter>(costChange: -1);
        if (IsUpgraded)
        {
            await PowerCmd.Apply<ShibiMagicEyePower>(Owner.Creature, 1m, Owner.Creature, this);
        }
    }
}

[RegisterCard(typeof(ShirubiCardPool), FullPublicEntry = "SHIBI_STEINS_GATE_CHOICE")]
public sealed class ShibiSteinsGateChoice : ShibiRewardCard
{
    public ShibiSteinsGateChoice() : base(1, CardType.Skill, TargetType.Self, CardRarity.Common) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var handCards = PileType.Hand.GetPile(Owner).Cards.Where(card => card != this).ToList();
        var toDiscard = new List<CardModel>();
        while (handCards.Count > 0 && toDiscard.Count < 2)
        {
            var card = Owner.RunState.Rng.CombatCardSelection.NextItem(handCards);
            if (card == null)
            {
                break;
            }

            handCards.Remove(card);
            toDiscard.Add(card);
        }
        await CardCmd.Discard(choiceContext, toDiscard);

        foreach (var card in PileType.Discard.GetPile(Owner).Cards.Take(2).ToList())
        {
            await CardPileCmd.Add(card, PileType.Hand, CardPilePosition.Bottom, this);
        }

        foreach (var card in PileType.Draw.GetPile(Owner).Cards.Take(2).ToList())
        {
            await CardPileCmd.Add(card, PileType.Discard, CardPilePosition.Bottom, this);
        }
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }
}

[RegisterCard(typeof(ShirubiCardPool), FullPublicEntry = "SHIBI_FOXMIZAWA_SYNDROME")]
public sealed class ShibiFoxmizawaSyndrome : ShibiRewardCard
{
    public ShibiFoxmizawaSyndrome() : base(1, CardType.Skill, TargetType.AnyEnemy, CardRarity.Common) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await PowerCmd.Apply<WeakPower>(cardPlay.Target, IsUpgraded ? 3m : 2m, Owner.Creature, this);
        var enemyCount = CombatState!.Enemies.Count(enemy => enemy.IsMonster && !enemy.IsDead);
        await PowerCmd.Apply<StrengthPower>(cardPlay.Target, -enemyCount, Owner.Creature, this);
    }
}

[RegisterCard(typeof(ShirubiCardPool), FullPublicEntry = "SHIBI_SEVENFOLD_MAGIC_CIRCLE")]
public sealed class ShibiSevenfoldMagicCircle : ShibiRewardCard
{
    public override int ManaGain => 3;
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    public override bool GainsBlock => true;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new BlockVar(7m, ValueProp.Move)];
    public ShibiSevenfoldMagicCircle() : base(0, CardType.Skill, TargetType.Self, CardRarity.Common) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await GainBlock(cardPlay, DynamicVars.Block.BaseValue);
        await FinishManaCard(choiceContext, cardPlay);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(5m);
    }
}

[RegisterCard(typeof(ShirubiCardPool), FullPublicEntry = "SHIBI_ROUND_FOX_PRINCIPLE")]
public sealed class ShibiRoundFoxPrinciple : ShibiRewardCard
{
    public ShibiRoundFoxPrinciple() : base(2, CardType.Power, TargetType.Self, CardRarity.Common) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<ShibiRoundFoxPrinciplePower>(Owner.Creature, 5m, Owner.Creature, this);
    }
}

[RegisterCard(typeof(ShirubiCardPool), FullPublicEntry = "SHIBI_MANA_RESEARCH")]
public sealed class ShibiManaResearch : ShibiRewardCard
{
    public ShibiManaResearch() : base(1, CardType.Skill, TargetType.Self, CardRarity.Uncommon) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<ShibiManaResearchPower>(Owner.Creature, 1m, Owner.Creature, this);
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }
}

[RegisterCard(typeof(ShirubiCardPool), FullPublicEntry = "SHIBI_RAGING_MAGIC")]
public sealed class ShibiRagingMagic : ShibiRewardCard
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => IsUpgraded ? [CardKeyword.Retain] : [];
    public ShibiRagingMagic() : base(1, CardType.Skill, TargetType.Self, CardRarity.Rare) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<ShibiRagingMagicPower>(Owner.Creature, 4m, Owner.Creature, this);
    }

    protected override void OnUpgrade()
    {
        _ = Keywords;
        AddKeyword(CardKeyword.Retain);
    }
}

[RegisterCard(typeof(ShirubiCardPool), FullPublicEntry = "SHIBI_FLAME_SWORD_DANCE")]
public sealed class ShibiFlameSwordDance : ShibiRewardCard
{
    public override int ManaGain => 1;
    public override ShibiInterruptKind InterruptKind => ShibiInterruptKind.Status;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(7m, ValueProp.Move)];
    public ShibiFlameSwordDance() : base(3, CardType.Attack, TargetType.AnyEnemy, CardRarity.Uncommon) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await AttackOne(choiceContext, cardPlay, IsFullReleaseActive ? 12m + (DynamicVars.Damage.BaseValue - 7m) : DynamicVars.Damage.BaseValue, 3);
        await HandleInterrupt(choiceContext, cardPlay);
        await FinishManaCard(choiceContext, cardPlay);
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }
}

[RegisterCard(typeof(ShirubiCardPool), FullPublicEntry = "SHIBI_SWORD_OF_BENEVOLENCE")]
public sealed class ShibiSwordOfBenevolence : ShibiRewardCard
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(5m, ValueProp.Move)];
    public ShibiSwordOfBenevolence() : base(1, CardType.Attack, TargetType.AnyEnemy, CardRarity.Common) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await AttackOne(choiceContext, cardPlay, DynamicVars.Damage.BaseValue);
        await PowerCmd.Apply<WeakPower>(cardPlay.Target, 2m, Owner.Creature, this);
        await AddTemporaryExhaustEtherealCardToHand<ShibiSwordOfJustice>(IsUpgraded);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(4m);
    }
}

[RegisterCard(typeof(ShirubiCardPool), FullPublicEntry = "SHIBI_SWORD_OF_JUSTICE")]
public sealed class ShibiSwordOfJustice : ShibiRewardCard
{
    public override bool CanBeGeneratedInCombat => false;
    public override bool CanBeGeneratedByModifiers => false;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(5m, ValueProp.Move)];
    public ShibiSwordOfJustice() : base(1, CardType.Attack, TargetType.AnyEnemy, CardRarity.Token) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await AttackOne(choiceContext, cardPlay, DynamicVars.Damage.BaseValue);
        await PowerCmd.Apply<VulnerablePower>(cardPlay.Target, 2m, Owner.Creature, this);
        await AddTemporaryExhaustEtherealCardToHand<ShibiSwordOfBenevolence>(IsUpgraded);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(4m);
    }
}

[RegisterCard(typeof(ShirubiCardPool), FullPublicEntry = "SHIBI_PUMPKIN_PIE")]
public sealed class ShibiPumpkinPie : ShibiRewardCard
{
    public override int ManaGain => IsUpgraded ? 7 : 5;
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(1m, ValueProp.Move)];
    public ShibiPumpkinPie() : base(1, CardType.Attack, TargetType.AnyEnemy, CardRarity.Common) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await AttackOne(choiceContext, cardPlay, DynamicVars.Damage.BaseValue);
        await FinishManaCard(choiceContext, cardPlay);
    }
}

[RegisterCard(typeof(ShirubiCardPool), FullPublicEntry = "SHIBI_IAIDO_PURPLE_LIGHTNING")]
public sealed class ShibiIaidoPurpleLightning : ShibiRewardCard
{
    public override ShibiInterruptKind InterruptKind => ShibiInterruptKind.Block;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(10m, ValueProp.Move)];
    public ShibiIaidoPurpleLightning() : base(1, CardType.Attack, TargetType.AnyEnemy, CardRarity.Common) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await AttackOne(choiceContext, cardPlay, DynamicVars.Damage.BaseValue);
        await HandleInterrupt(choiceContext, cardPlay);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(6m);
    }
}

[RegisterCard(typeof(ShirubiCardPool), FullPublicEntry = "SHIBI_HEAVEN_EARTH_FLASH")]
public sealed class ShibiHeavenEarthFlash : ShibiRewardCard
{
    public override ShibiInterruptKind InterruptKind => ShibiInterruptKind.Attack;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(16m, ValueProp.Move)];
    public ShibiHeavenEarthFlash() : base(2, CardType.Attack, TargetType.AnyEnemy, CardRarity.Uncommon) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await AttackOne(choiceContext, cardPlay, DynamicVars.Damage.BaseValue);
        await HandleInterrupt(choiceContext, cardPlay);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(8m);
    }
}

[RegisterCard(typeof(ShirubiCardPool), FullPublicEntry = "SHIBI_INSTANT_STRIKE")]
public sealed class ShibiInstantStrike : ShibiRewardCard
{
    public override ShibiInterruptKind InterruptKind => ShibiInterruptKind.Block;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(5m, ValueProp.Move)];
    public ShibiInstantStrike() : base(1, CardType.Attack, TargetType.AnyEnemy, CardRarity.Common) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await AttackOne(choiceContext, cardPlay, DynamicVars.Damage.BaseValue);
        await HandleInterrupt(choiceContext, cardPlay);
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }
}

[RegisterCard(typeof(ShirubiCardPool), FullPublicEntry = "SHIBI_CURSE_KILLING_BLADE")]
public sealed class ShibiCurseKillingBlade : ShibiRewardCard
{
    public override ShibiInterruptKind InterruptKind => ShibiInterruptKind.Status;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(5m, ValueProp.Move)];
    public ShibiCurseKillingBlade() : base(1, CardType.Attack, TargetType.AnyEnemy, CardRarity.Common) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await AttackOne(choiceContext, cardPlay, DynamicVars.Damage.BaseValue);
        if (IsUpgraded)
        {
            await PowerCmd.Apply<VulnerablePower>(cardPlay.Target, 1m, Owner.Creature, this);
        }
        await HandleInterrupt(choiceContext, cardPlay);
    }
}

[RegisterCard(typeof(ShirubiCardPool), FullPublicEntry = "SHIBI_HITTER")]
public sealed class ShibiHitter : ShibiRewardCard
{
    public override ShibiInterruptKind InterruptKind => ShibiInterruptKind.Attack;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(5m, ValueProp.Move)];
    public ShibiHitter() : base(1, CardType.Attack, TargetType.AnyEnemy, CardRarity.Common) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await AttackOne(choiceContext, cardPlay, DynamicVars.Damage.BaseValue);
        await HandleInterrupt(choiceContext, cardPlay);
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }
}

[RegisterCard(typeof(ShirubiCardPool), FullPublicEntry = "SHIBI_DIMENSION_SLASH")]
public sealed class ShibiDimensionSlash : ShibiRewardCard
{
    public override int ManaGain => 1;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(12m, ValueProp.Move)];
    public ShibiDimensionSlash() : base(3, CardType.Attack, TargetType.AllEnemies, CardRarity.Uncommon) { }

    public override bool TryModifyEnergyCostInCombat(CardModel card, decimal originalCost, out decimal modifiedCost)
    {
        if (card == this && CombatState != null)
        {
            modifiedCost = originalCost - ShibiMechanics.StunnedEnemiesThisCombat(Owner);
            return true;
        }

        modifiedCost = originalCost;
        return false;
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await AttackAll(choiceContext, DynamicVars.Damage.BaseValue);
        await FinishManaCard(choiceContext, cardPlay);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(5m);
    }
}

[RegisterCard(typeof(ShirubiCardPool), FullPublicEntry = "SHIBI_SKY_BREAKING_SLASH")]
public sealed class ShibiSkyBreakingSlash : ShibiRewardCard
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(4m, ValueProp.Move), new BlockVar(4m, ValueProp.Move)];
    public override bool GainsBlock => true;
    public ShibiSkyBreakingSlash() : base(0, CardType.Attack, TargetType.AnyEnemy, CardRarity.Common) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await AttackOne(choiceContext, cardPlay, DynamicVars.Damage.BaseValue);
        if (IsUpgraded)
        {
            await PowerCmd.Apply<WeakPower>(cardPlay.Target, 1m, Owner.Creature, this);
        }
        await GainBlock(cardPlay, DynamicVars.Block.BaseValue);
    }
}

[RegisterCard(typeof(ShirubiCardPool), FullPublicEntry = "SHIBI_EXPLOSIVE_FIREBALL")]
public sealed class ShibiExplosiveFireball : ShibiRewardCard
{
    public override int ManaGain => 2;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(2m, ValueProp.Move), new DynamicVar("FullReleaseDamage", 4m)];
    public ShibiExplosiveFireball() : base(1, CardType.Attack, TargetType.AnyEnemy, CardRarity.Common) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (IsFullReleaseActive)
        {
            await AttackAll(choiceContext, DynamicVars["FullReleaseDamage"].BaseValue, 3);
        }
        else
        {
            await AttackOne(choiceContext, cardPlay, DynamicVars.Damage.BaseValue, 3);
        }
        await FinishManaCard(choiceContext, cardPlay);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(3m);
        DynamicVars["FullReleaseDamage"].UpgradeValueBy(3m);
    }
}

[RegisterCard(typeof(ShirubiCardPool), FullPublicEntry = "SHIBI_FLAME_IMPACT")]
public sealed class ShibiFlameImpact : ShibiRewardCard
{
    public override int ManaGain => 2;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(4m, ValueProp.Move)];
    public ShibiFlameImpact() : base(1, CardType.Attack, TargetType.AnyEnemy, CardRarity.Common) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await AttackOne(choiceContext, cardPlay, IsFullReleaseActive ? 7m + (DynamicVars.Damage.BaseValue - 4m) : DynamicVars.Damage.BaseValue, IsUpgraded ? 3 : 2);
        await FinishManaCard(choiceContext, cardPlay);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(0m);
    }
}

[RegisterCard(typeof(ShirubiCardPool), FullPublicEntry = "SHIBI_ICE_BLOOM")]
public sealed class ShibiIceBloom : ShibiRewardCard
{
    public override int ManaGain => 2;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(3m, ValueProp.Move), new BlockVar(0m, ValueProp.Move)];
    public override bool GainsBlock => true;
    public ShibiIceBloom() : base(1, CardType.Attack, TargetType.AllEnemies, CardRarity.Common) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await AttackAll(choiceContext, DynamicVars.Damage.BaseValue);
        if (IsUpgraded)
        {
            await GainBlock(cardPlay, DynamicVars.Block.BaseValue);
        }
        if (IsFullReleaseActive)
        {
            foreach (var enemy in CombatState!.Enemies.Where(enemy => enemy.IsMonster && !enemy.IsDead).ToList())
            {
                await ShibiMechanics.DealToughnessDamage(choiceContext, Owner, enemy, 1m, this);
            }
        }
        await FinishManaCard(choiceContext, cardPlay);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(3m);
    }
}

[RegisterCard(typeof(ShirubiCardPool), FullPublicEntry = "SHIBI_EXTREME_COLD_ICE_SPIKE")]
public sealed class ShibiExtremeColdIceSpike : ShibiRewardCard
{
    public override int ManaGain => 2;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(1m, ValueProp.Move)];
    public ShibiExtremeColdIceSpike() : base(2, CardType.Attack, TargetType.AnyEnemy, CardRarity.Common) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        if (IsFullReleaseActive)
        {
            await AttackAll(choiceContext, DynamicVars.Damage.BaseValue, 7);
        }
        else
        {
            await AttackOne(choiceContext, cardPlay, DynamicVars.Damage.BaseValue, 4);
        }

        if (IsUpgraded)
        {
            await PowerCmd.Apply<WeakPower>(cardPlay.Target, 1m, Owner.Creature, this);
        }
        await FinishManaCard(choiceContext, cardPlay);
    }
}

[RegisterCard(typeof(ShirubiCardPool), FullPublicEntry = "SHIBI_ANNIHILATION_BLACK_HOLE")]
public sealed class ShibiAnnihilationBlackHole : ShibiRewardCard
{
    public override int ManaGain => 3;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(7m, ValueProp.Move), new DynamicVar("BaseDamage", 2m)];
    public ShibiAnnihilationBlackHole() : base(2, CardType.Attack, TargetType.AnyEnemy, CardRarity.Rare) { }

    public override bool TryModifyEnergyCostInCombat(CardModel card, decimal originalCost, out decimal modifiedCost)
    {
        if (card == this && CombatState != null && IsFullReleaseActive)
        {
            modifiedCost = originalCost + 1m;
            return true;
        }

        modifiedCost = originalCost;
        return false;
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        if (IsFullReleaseActive)
        {
            await AttackAll(choiceContext, DynamicVars.Damage.BaseValue, 7);
        }
        else
        {
            await AttackOne(choiceContext, cardPlay, DynamicVars["BaseDamage"].BaseValue, 5);
        }

        if (IsUpgraded)
        {
            await PowerCmd.Apply<WeakPower>(cardPlay.Target, 1m, Owner.Creature, this);
        }
        await FinishManaCard(choiceContext, cardPlay);
    }
}

[RegisterCard(typeof(ShirubiCardPool), FullPublicEntry = "SHIBI_FULL_COMBO")]
public sealed class ShibiFullCombo : ShibiRewardCard
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => IsUpgraded ? [CardKeyword.Retain] : [];
    public ShibiFullCombo() : base(3, CardType.Power, TargetType.Self, CardRarity.Rare) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<ShibiFullComboPower>(Owner.Creature, 1m, Owner.Creature, this);
    }

    protected override void OnUpgrade()
    {
        _ = Keywords;
        AddKeyword(CardKeyword.Retain);
    }
}

[RegisterCard(typeof(ShirubiCardPool), FullPublicEntry = "SHIBI_FOX_SPIRIT_INTUITION")]
public sealed class ShibiFoxSpiritIntuition : ShibiRewardCard
{
    public ShibiFoxSpiritIntuition() : base(1, CardType.Power, TargetType.Self, CardRarity.Rare) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<ShibiFoxSpiritIntuitionPower>(Owner.Creature, IsUpgraded ? 2m : 3m, Owner.Creature, this);
    }
}

[RegisterCard(typeof(ShirubiCardPool), FullPublicEntry = "SHIBI_HARDENING_PATTERN")]
public sealed class ShibiHardeningPattern : ShibiRewardCard
{
    public ShibiHardeningPattern() : base(2, CardType.Power, TargetType.Self, CardRarity.Uncommon) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<ShibiHardeningPatternPower>(Owner.Creature, IsUpgraded ? 5m : 3m, Owner.Creature, this);
    }
}

[RegisterCard(typeof(ShirubiCardPool), FullPublicEntry = "SHIBI_HEROIC_SEAL")]
public sealed class ShibiHeroicSeal : ShibiRewardCard
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => IsUpgraded ? [CardKeyword.Retain] : [];
    public ShibiHeroicSeal() : base(3, CardType.Power, TargetType.Self, CardRarity.Rare) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<ShibiHeroicSealPower>(Owner.Creature, IsFullReleaseActive ? 2m : 1m, Owner.Creature, this);
        await ShibiMechanics.ConsumeFullReleaseUse(Owner, this);
    }

    protected override void OnUpgrade()
    {
        _ = Keywords;
        AddKeyword(CardKeyword.Retain);
    }
}

[RegisterCard(typeof(ShirubiCardPool), FullPublicEntry = "SHIBI_MANA_ABSORB")]
public sealed class ShibiManaAbsorb : ShibiRewardCard
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => IsUpgraded ? [CardKeyword.Innate] : [];
    public ShibiManaAbsorb() : base(2, CardType.Power, TargetType.Self, CardRarity.Rare) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<ShibiManaAbsorbPower>(Owner.Creature, 1m, Owner.Creature, this);
    }

    protected override void OnUpgrade()
    {
        _ = Keywords;
        AddKeyword(CardKeyword.Innate);
    }
}
