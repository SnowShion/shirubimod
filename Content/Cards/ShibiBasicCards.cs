using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;

namespace shirubimod.Scripts.Content.Cards;

public abstract class ShibiBasicCard : CardModel
{
    public virtual int ManaGain => 0;
    public virtual ShibiInterruptKind InterruptKind => ShibiInterruptKind.None;

    // 希比基础卡的共同基类。
    // energyCost 是费用，type 是攻击/技能/能力，targetType 是目标类型。
    protected ShibiBasicCard(int energyCost, CardType type, TargetType targetType, CardRarity rarity = CardRarity.Common)
        // The reward card file fills Common/Uncommon/Rare. Starter cards pass Basic
        // explicitly so they stay out of normal rewards but can still transform into
        // the reward pool.
        : base(energyCost, type, rarity, targetType)
    {
    }

    // 目前还没有卡图资源，统一使用游戏内置的缺失卡图占位。
    // 后续做好卡图后，可以删除这些 override，让游戏按卡牌 ID 去找真正的资源。
    public override string PortraitPath => MissingPortraitPath;
    public override string BetaPortraitPath => MissingPortraitPath;
    public override IEnumerable<string> AllPortraitPaths => [MissingPortraitPath];

    protected bool IsFullReleaseActive => ShibiMechanics.IsFullReleaseActive(Owner);

    protected async Task FinishManaCard(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (ManaGain <= 0)
        {
            return;
        }

        await ShibiMechanics.GainMana(Owner, ManaGain, this);
        await ShibiMechanics.ConsumeFullReleaseUse(Owner, this);
    }

    protected async Task HandleInterrupt(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await ShibiMechanics.HandleInterrupt(choiceContext, this, cardPlay.Target, InterruptKind);
    }

    public override async Task BeforeCombatStart()
    {
        await ShibiMechanics.EnsureToughnessForCombat(CombatState, this);
        await ShibiMechanics.EnsureShibiCombatTrackers(Owner, this);
    }

    public override async Task AfterCreatureAddedToCombat(Creature creature)
    {
        await ShibiMechanics.EnsureToughnessForCombat(CombatState, this);
    }
}

// 注册到希比卡池。FullPublicEntry 同时也是本地化 key 的前缀：
// SHIBI_STRIKE.title / SHIBI_STRIKE.description。
[RegisterCard(typeof(ShirubiCardPool), FullPublicEntry = "SHIBI_STRIKE")]
public sealed class ShibiStrike : ShibiBasicCard
{
    public override int ManaGain => 1;

    // Strike 标签让游戏把它识别为“打击”类基础攻击牌。
    protected override HashSet<CardTag> CanonicalTags => [CardTag.Strike];

    // DamageVar 会让描述里的 {Damage:diff()} 自动显示当前伤害和升级预览。
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(6m, ValueProp.Move)];

    public ShibiStrike()
        // 1 费攻击牌，目标为任意敌人。
        : base(1, CardType.Attack, TargetType.AnyEnemy, CardRarity.Basic)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);

        // 造成 DynamicVars.Damage.BaseValue 点攻击伤害，并播放斩击特效。
        var damage = DynamicVars.Damage.BaseValue + (IsFullReleaseActive ? 3m : 0m);
        await DamageCmd.Attack(damage).FromCard(this).Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);
        await FinishManaCard(choiceContext, cardPlay);
    }

    protected override void OnUpgrade()
    {
        // 升级后伤害 +3：6 -> 9。
        DynamicVars.Damage.UpgradeValueBy(3m);
    }
}

[RegisterCard(typeof(ShirubiCardPool), FullPublicEntry = "SHIBI_DEFEND")]
public sealed class ShibiDefend : ShibiBasicCard
{
    public override int ManaGain => 1;

    // 告诉游戏这张牌会获得格挡，用于 UI 提示和部分钩子判断。
    public override bool GainsBlock => true;

    // Defend 标签让游戏把它识别为“防御”类基础技能牌。
    protected override HashSet<CardTag> CanonicalTags => [CardTag.Defend];

    // BlockVar 对应描述里的 {Block:diff()}。
    protected override IEnumerable<DynamicVar> CanonicalVars => [new BlockVar(5m, ValueProp.Move)];

    public ShibiDefend()
        // 1 费技能牌，目标为自己。
        : base(1, CardType.Skill, TargetType.Self, CardRarity.Basic)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 给玩家角色获得当前 BlockVar 数值的格挡。
        var block = DynamicVars.Block.BaseValue + (IsFullReleaseActive ? 3m : 0m);
        await CreatureCmd.GainBlock(Owner.Creature, block, DynamicVars.Block.Props, cardPlay);
        await FinishManaCard(choiceContext, cardPlay);
    }

    protected override void OnUpgrade()
    {
        // 升级后格挡 +3：5 -> 8。
        DynamicVars.Block.UpgradeValueBy(3m);
    }
}

[RegisterCard(typeof(ShirubiCardPool), FullPublicEntry = "SHIBI_NEEDLE_THRUST")]
public sealed class ShibiNeedleThrust : ShibiBasicCard
{
    public override int ManaGain => 2;
    public override ShibiInterruptKind InterruptKind => ShibiInterruptKind.Attack;

    // 这张牌有两个动态数值：伤害和抽牌数。
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(4m, ValueProp.Move),
        new CardsVar(1)
    ];

    public ShibiNeedleThrust()
        // 1 费攻击牌，目标为任意敌人。
        : base(1, CardType.Attack, TargetType.AnyEnemy, CardRarity.Basic)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);

        // 先造成伤害。
        var damage = DynamicVars.Damage.BaseValue + (IsFullReleaseActive ? 3m : 0m);
        var cards = DynamicVars.Cards.IntValue + (IsFullReleaseActive ? 1 : 0);

        await DamageCmd.Attack(damage).FromCard(this).Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);
        await HandleInterrupt(choiceContext, cardPlay);

        // 再抽 CardsVar 指定数量的牌。
        await CardPileCmd.Draw(choiceContext, cards, Owner);
        await FinishManaCard(choiceContext, cardPlay);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(3m);
    }
}

[RegisterCard(typeof(ShirubiCardPool), FullPublicEntry = "SHIBI_GUARDED_STEP")]
public sealed class ShibiGuardedStep : ShibiBasicCard
{
    public override int ManaGain => 1;
    public override bool GainsBlock => true;

    // 这张牌有两个动态数值：格挡和抽牌数。
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new BlockVar(3m, ValueProp.Move),
        new CardsVar(1)
    ];

    public ShibiGuardedStep()
        // 1 费技能牌，目标为自己。
        : base(1, CardType.Skill, TargetType.Self, CardRarity.Basic)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 先获得格挡，再抽牌。
        var block = DynamicVars.Block.BaseValue + (IsFullReleaseActive ? 3m : 0m);
        var cards = DynamicVars.Cards.IntValue + (IsFullReleaseActive ? 1 : 0);

        await CreatureCmd.GainBlock(Owner.Creature, block, DynamicVars.Block.Props, cardPlay);
        await CardPileCmd.Draw(choiceContext, cards, Owner);
        await FinishManaCard(choiceContext, cardPlay);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(3m);
    }
}

[RegisterCard(typeof(ShirubiCardPool), FullPublicEntry = "SHIBI_BRIGHT_CUT")]
public sealed class ShibiBrightCut : ShibiBasicCard
{
    public override ShibiInterruptKind InterruptKind => ShibiInterruptKind.Block;

    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(3m, ValueProp.Move)];

    public ShibiBrightCut()
        : base(2, CardType.Attack, TargetType.AnyEnemy, CardRarity.Common)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);

        var damage = DynamicVars.Damage.BaseValue + (IsFullReleaseActive ? 2m : 0m);
        await DamageCmd.Attack(damage).FromCard(this).Targeting(cardPlay.Target)
            .WithHitCount(3)
            .WithHitFx("vfx/vfx_heavy_blunt")
            .Execute(choiceContext);
        await HandleInterrupt(choiceContext, cardPlay);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(1m);
    }
}

[RegisterCard(typeof(ShirubiCardPool), FullPublicEntry = "SHIBI_BUTTERFLY_GUARD")]
public sealed class ShibiButterflyGuard : ShibiBasicCard
{
    public override int ManaGain => 2;
    public override bool GainsBlock => true;

    // 2 费大防御，当前基础格挡为 8。
    protected override IEnumerable<DynamicVar> CanonicalVars => [new BlockVar(8m, ValueProp.Move)];

    public ShibiButterflyGuard()
        // 2 费技能牌，目标为自己。
        : base(2, CardType.Skill, TargetType.Self, CardRarity.Basic)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 获得当前 BlockVar 数值的格挡。
        var block = DynamicVars.Block.BaseValue + (IsFullReleaseActive ? 5m : 0m);
        await CreatureCmd.GainBlock(Owner.Creature, block, DynamicVars.Block.Props, cardPlay);
        await FinishManaCard(choiceContext, cardPlay);
    }

    protected override void OnUpgrade()
    {
        // 升级后格挡 +4：8 -> 12。
        DynamicVars.Block.UpgradeValueBy(4m);
    }
}

[RegisterCard(typeof(ShirubiCardPool), FullPublicEntry = "SHIBI_QUICK_STUDY")]
public sealed class ShibiQuickStudy : ShibiBasicCard
{
    public override int ManaGain => 2;

    // CardsVar 同时用于抽牌数量和弃牌数量。
    protected override IEnumerable<DynamicVar> CanonicalVars => [new CardsVar(1)];

    public ShibiQuickStudy()
        // 0 费技能牌，目标为自己。
        : base(0, CardType.Skill, TargetType.Self, CardRarity.Common)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var cardCount = DynamicVars.Cards.IntValue + (IsFullReleaseActive ? 1 : 0);

        // 先抽牌，再打开弃牌选择界面。
        await CardPileCmd.Draw(choiceContext, cardCount, Owner);
        await CardCmd.Discard(choiceContext, await CardSelectCmd.FromHandForDiscard(
            choiceContext,
            Owner,
            new CardSelectorPrefs(CardSelectorPrefs.DiscardSelectionPrompt, cardCount),
            null,
            this));
        await FinishManaCard(choiceContext, cardPlay);
    }

    protected override void OnUpgrade()
    {
        // 升级后抽弃数量 +1：抽 1 弃 1 -> 抽 2 弃 2。
        DynamicVars.Cards.UpgradeValueBy(1m);
    }
}

[RegisterCard(typeof(ShirubiCardPool), FullPublicEntry = "SHIBI_DOUBLE_FLICKER")]
public sealed class ShibiDoubleFlicker : ShibiBasicCard
{
    public override int ManaGain => 1;
    public override ShibiInterruptKind InterruptKind => ShibiInterruptKind.Status;

    // 每段伤害为 3，实际会执行两次攻击。
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(3m, ValueProp.Move)];

    public ShibiDoubleFlicker()
        // 1 费攻击牌，目标为任意敌人。
        : base(1, CardType.Attack, TargetType.AnyEnemy, CardRarity.Common)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);

        // 连续执行两次攻击命令，所以会触发两次伤害结算。
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCard(this).Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCard(this).Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);
        if (IsFullReleaseActive)
        {
            await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCard(this).Targeting(cardPlay.Target)
                .WithHitFx("vfx/vfx_attack_slash")
                .Execute(choiceContext);
        }
        await HandleInterrupt(choiceContext, cardPlay);
        await FinishManaCard(choiceContext, cardPlay);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(2m);
    }
}

[RegisterCard(typeof(ShirubiCardPool), FullPublicEntry = "SHIBI_FULL_RELEASE")]
public sealed class ShibiFullRelease : ShibiBasicCard
{
    public override bool CanBeGeneratedInCombat => false;
    public override bool CanBeGeneratedByModifiers => false;

    public ShibiFullRelease()
        : base(0, CardType.Skill, TargetType.Self, CardRarity.Token)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await ShibiMechanics.TryActivateFullRelease(
            choiceContext,
            Owner,
            this,
            ShibiMechanics.FullReleaseNormalCardPlays);
    }
}
