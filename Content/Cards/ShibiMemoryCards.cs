using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;

namespace shirubimod.Scripts.Content.Cards;

// “她的记忆，世界的记忆”触发后发放的专属 Token 卡。
// 这些卡不进入普通奖励池，只在遗物二次濒死效果中生成。
public abstract class ShibiMemoryCard : ShibiRewardCard
{
    protected ShibiMemoryCard(CardType type, TargetType targetType)
        : base(0, type, targetType, CardRarity.Event)
    {
    }

    public override bool CanBeGeneratedInCombat => false;
    public override bool CanBeGeneratedByModifiers => false;

    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Retain, CardKeyword.Eternal];
}

[RegisterCard(typeof(ShirubiCardPool), FullPublicEntry = "SHIBI_PAINFUL_PAST")]
public sealed class ShibiPainfulPast : ShibiMemoryCard
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("Amount", 77m)];

    public ShibiPainfulPast() : base(CardType.Skill, TargetType.AllEnemies) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        foreach (var enemy in CombatState!.Enemies.Where(enemy => enemy.IsMonster && !enemy.IsDead).ToList())
        {
            await PowerCmd.Apply<WeakPower>(enemy, DynamicVars["Amount"].BaseValue, Owner.Creature, this);
            await PowerCmd.Apply<VulnerablePower>(enemy, DynamicVars["Amount"].BaseValue, Owner.Creature, this);
            await PowerCmd.Apply<StrengthPower>(enemy, -DynamicVars["Amount"].BaseValue, Owner.Creature, this);
            await PowerCmd.Apply<DexterityPower>(enemy, -DynamicVars["Amount"].BaseValue, Owner.Creature, this);
        }
    }
}

[RegisterCard(typeof(ShirubiCardPool), FullPublicEntry = "SHIBI_BEGINNING_OF_STORY")]
public sealed class ShibiBeginningOfStory : ShibiMemoryCard
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("Amount", 77m)];

    public ShibiBeginningOfStory() : base(CardType.Skill, TargetType.Self) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<StrengthPower>(Owner.Creature, DynamicVars["Amount"].BaseValue, Owner.Creature, this);
        await PowerCmd.Apply<DexterityPower>(Owner.Creature, DynamicVars["Amount"].BaseValue, Owner.Creature, this);
    }
}

[RegisterCard(typeof(ShirubiCardPool), FullPublicEntry = "SHIBI_TWISTING_JOURNEY")]
public sealed class ShibiTwistingJourney : ShibiMemoryCard
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [new CardsVar(7)];

    public ShibiTwistingJourney() : base(CardType.Skill, TargetType.Self) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.IntValue, Owner);

        var copy = CombatState!.CreateCard<ShibiTwistingJourney>(Owner);
        await CardPileCmd.Add(copy, PileType.Hand, CardPilePosition.Bottom, this);
    }
}

[RegisterCard(typeof(ShirubiCardPool), FullPublicEntry = "SHIBI_HAPPY_ENDING")]
public sealed class ShibiHappyEnding : ShibiMemoryCard
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new HealVar(777m),
        new DynamicVar("Energy", 777m)
    ];

    public ShibiHappyEnding() : base(CardType.Skill, TargetType.Self) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CreatureCmd.Heal(Owner.Creature, DynamicVars.Heal.BaseValue);
        await PlayerCmd.GainEnergy(DynamicVars["Energy"].IntValue, Owner);
    }
}

[RegisterCard(typeof(ShirubiCardPool), FullPublicEntry = "SHIBI_START_END_COMPLETE_ME")]
public sealed class ShibiStartEndCompleteMe : ShibiMemoryCard
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("Amount", 777m)];

    public ShibiStartEndCompleteMe() : base(CardType.Skill, TargetType.Self) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<IntangiblePower>(Owner.Creature, DynamicVars["Amount"].BaseValue, Owner.Creature, this);
    }
}

[RegisterCard(typeof(ShirubiCardPool), FullPublicEntry = "SHIBI_TERMINUS_OF_ALL")]
public sealed class ShibiTerminusOfAll : ShibiMemoryCard
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(7777m, ValueProp.Move)];

    public ShibiTerminusOfAll() : base(CardType.Attack, TargetType.AllEnemies) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await AttackAll(choiceContext, DynamicVars.Damage.BaseValue);
    }
}

[RegisterCard(typeof(ShirubiCardPool), FullPublicEntry = "SHIBI_RESHAPE_WORLD_WITH_MEMORY")]
public sealed class ShibiReshapeWorldWithMemory : ShibiMemoryCard
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Retain, CardKeyword.Eternal, CardKeyword.Unplayable];

    public ShibiReshapeWorldWithMemory() : base(CardType.Skill, TargetType.Self) { }

    protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        return Task.CompletedTask;
    }
}
