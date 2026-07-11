using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Saves.Runs;
using shirubimod.Scripts.Content.Powers;
using shirubimod.Scripts.Patches;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace shirubimod.Scripts.Content.Relics;

// 希比的初始遗物：提供每回合玛娜、根据当前玛娜提供成长，并拥有一次濒死救场。
[RegisterRelic(typeof(ShirubiRelicPool), FullPublicEntry = "SHIBI_GOLDEN_BUTTERFLY_KNOT")]
public sealed class GoldenButterflyKnot : ModRelicTemplate
{
    private const int ManaPerTurn = 3;
    private const int ManaPerBuff = 15;
    private const int StrengthAndDexterityPerChunk = 1;
    private const int DeathSaveHealPercent = 50;

    private static readonly List<WeakReference<GoldenButterflyKnot>> ActiveInstances = [];

    private bool _wasUsed;
    private int _appliedBuffChunks;
    private CombatState? _lastCombatState;

    public override RelicRarity Rarity => RelicRarity.Starter;

    // 濒死效果触发过后，让遗物显示为已使用状态，和原版蜥蜴尾巴类似。
    public override bool IsUsedUp => WasUsed;

    public override RelicAssetProfile AssetProfile => new(
        IconPath: "res://images/relics/golden_butterfly_knot.png",
        IconOutlinePath: "res://images/relics/golden_butterfly_knot_outline.png",
        BigIconPath: "res://images/relics/golden_butterfly_knot.png");

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Mana", ManaPerTurn),
        new DynamicVar("ManaPerBuff", ManaPerBuff),
        new PowerVar<StrengthPower>("Strength", StrengthAndDexterityPerChunk),
        new PowerVar<DexterityPower>("Dexterity", StrengthAndDexterityPerChunk),
        new DynamicVar("FullRelease", ShibiMechanics.FullReleaseDeathSaveCardPlays),
        new HealVar(DeathSaveHealPercent)
    ];

    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
    [
        HoverTipFactory.FromPower<ShibiManaPower>(),
        HoverTipFactory.FromPower<ShibiFullReleasePower>(),
        HoverTipFactory.FromPower<StrengthPower>(),
        HoverTipFactory.FromPower<DexterityPower>()
    ];

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

    public override async Task AfterSideTurnStart(CombatSide side, CombatState combatState)
    {
        if (side != Owner.Creature.Side)
        {
            return;
        }

        RegisterActiveInstance(this);
        if (!ReferenceEquals(_lastCombatState, combatState))
        {
            _lastCombatState = combatState;
            _appliedBuffChunks = 0;
        }

        await ShibiMechanics.GainMana(Owner, ManaPerTurn, null);
        await SyncManaBuffs();
    }

    public override bool ShouldDieLate(Creature creature)
    {
        if (creature != Owner.Creature || WasUsed)
        {
            return true;
        }

        return false;
    }

    public override async Task AfterPreventingDeath(Creature creature)
    {
        WasUsed = true;

        var healAmount = Math.Max(1m, creature.MaxHp * (DeathSaveHealPercent / 100m));
        await CreatureCmd.Heal(creature, healAmount);
        await PowerCmd.Apply<ShibiFullReleasePower>(
            creature,
            ShibiMechanics.FullReleaseDeathSaveCardPlays,
            creature,
            null);

        ShibiCombatUiPatch.RefreshFullReleaseButton();
    }

    public static async Task SyncForPlayer(Player player)
    {
        for (var i = ActiveInstances.Count - 1; i >= 0; i--)
        {
            if (!ActiveInstances[i].TryGetTarget(out var relic))
            {
                ActiveInstances.RemoveAt(i);
                continue;
            }

            if (relic.Owner == player)
            {
                await relic.SyncManaBuffs();
            }
        }
    }

    private static void RegisterActiveInstance(GoldenButterflyKnot relic)
    {
        foreach (var weakReference in ActiveInstances)
        {
            if (weakReference.TryGetTarget(out var activeRelic) && ReferenceEquals(activeRelic, relic))
            {
                return;
            }
        }

        ActiveInstances.Add(new WeakReference<GoldenButterflyKnot>(relic));
    }

    private async Task SyncManaBuffs()
    {
        var currentMana = (int)Owner.Creature.GetPowerAmount<ShibiManaPower>();
        var targetChunks = Math.Max(0, currentMana / ManaPerBuff);
        var chunkDelta = targetChunks - _appliedBuffChunks;
        if (chunkDelta == 0)
        {
            return;
        }

        var buffDelta = chunkDelta * StrengthAndDexterityPerChunk;
        if (buffDelta > 0)
        {
            await PowerCmd.Apply<StrengthPower>(Owner.Creature, buffDelta, Owner.Creature, null);
            await PowerCmd.Apply<DexterityPower>(Owner.Creature, buffDelta, Owner.Creature, null);
        }
        else
        {
            var strength = Owner.Creature.GetPower<StrengthPower>();
            if (strength != null)
            {
                await PowerCmd.ModifyAmount(strength, buffDelta, Owner.Creature, null);
            }

            var dexterity = Owner.Creature.GetPower<DexterityPower>();
            if (dexterity != null)
            {
                await PowerCmd.ModifyAmount(dexterity, buffDelta, Owner.Creature, null);
            }
        }

        _appliedBuffChunks = targetChunks;
    }
}
