using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using shirubimod.Scripts.Content.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace shirubimod.Scripts.Content.Relics;

// 可正常掉落和在商店出现的希比遗物：每回合稳定提供魔眼。
[RegisterRelic(typeof(ShirubiRelicPool), FullPublicEntry = "SHIBI_SHION_HAIRPIN")]
public sealed class ShionHairpin : ModRelicTemplate
{
    private const int MagicEyePerTurn = 1;

    public override RelicRarity Rarity => RelicRarity.Common;

    public override RelicAssetProfile AssetProfile => new(
        IconPath: "res://images/relics/Shion_knot.png",
        IconOutlinePath: "res://images/relics/Shion_knot.png",
        BigIconPath: "res://images/relics/Shion_knot.png");

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("MagicEye", MagicEyePerTurn)
    ];

    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
    [
        HoverTipFactory.FromPower<ShibiMagicEyePower>()
    ];

    public override async Task AfterSideTurnStart(CombatSide side, CombatState combatState)
    {
        if (side != Owner.Creature.Side)
        {
            return;
        }

        await PowerCmd.Apply<ShibiMagicEyePower>(Owner.Creature, MagicEyePerTurn, Owner.Creature, null);
    }
}
