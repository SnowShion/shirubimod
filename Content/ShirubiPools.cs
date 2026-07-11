using System;
using System.Collections.Generic;
using Godot;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace shirubimod.Scripts.Content;

[RegisterSharedCardPool]
public sealed class ShirubiCardPool : TypeListCardPoolModel
{
    // 卡池名会影响部分资源路径和卡牌库分类显示。
    public override string Title => "Shirubi";

    // false 表示这是一个角色专属颜色卡池，不是无色卡池。
    public override bool IsColorless => false;

    // 暂时借用红色能量图标。后续有希比专属能量图标时，可改这里或覆盖图标路径。
    public override string EnergyColorName => "ironclad";

    // 牌库列表里的颜色。
    public override Color DeckEntryCardColor => new(0.95f, 0.45f, 0.95f);

    // 旧式卡牌枚举入口。现在卡牌使用 [RegisterCard(typeof(ShirubiCardPool))] 注册，
    // 所以这里保持空列表，避免重复注册同一张卡。
    [Obsolete]
    protected override IReadOnlyList<Type> CardTypes => [];
}

[RegisterSharedRelicPool]
public sealed class ShirubiRelicPool : TypeListRelicPoolModel
{
    // 遗物池暂时只需要能被 RitsuLib 识别。之后新增希比遗物时再注册到这个池。
    public override string EnergyColorName => "ironclad";

    // 遗物同样会用属性注册，这里保持空列表。
    [Obsolete]
    protected override IReadOnlyList<Type> RelicTypes => [];
}

[RegisterSharedPotionPool]
public sealed class ShirubiPotionPool : TypeListPotionPoolModel
{
    // 药水池预留。之后做希比专属药水时使用。
    public override string EnergyColorName => "ironclad";

    // 药水同样会用属性注册，这里保持空列表。
    [Obsolete]
    protected override IReadOnlyList<Type> PotionTypes => [];
}
