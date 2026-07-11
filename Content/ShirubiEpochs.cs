using MegaCrit.Sts2.Core.Nodes.Screens.Timeline;
using MegaCrit.Sts2.Core.Timeline;
using STS2RitsuLib.Interop.AutoRegistration;

namespace shirubimod.Scripts.Content;

public abstract class ShirubiEpochBase : EpochModel
{
    public override EpochEra Era => EpochEra.Blight1;
    public override string StoryId => "Shirubi";
    public override bool IsArtPlaceholder => true;
    public override string UnlockText => "希比的临时时间线占位。";
    public override void QueueUnlocks()
    {
        // Placeholder epoch: the mod currently unlocks no timeline-gated content.
    }
}

[RegisterEpoch]
public sealed class Shirubi2Epoch : ShirubiEpochBase
{
    public override string Id => "SHIRUBIMOD_CHARACTER_SHIRUBI_CHARACTER2_EPOCH";
    public override int EraPosition => 20;
}

[RegisterEpoch]
public sealed class Shirubi3Epoch : ShirubiEpochBase
{
    public override string Id => "SHIRUBIMOD_CHARACTER_SHIRUBI_CHARACTER3_EPOCH";
    public override int EraPosition => 21;
}

[RegisterEpoch]
public sealed class Shirubi4Epoch : ShirubiEpochBase
{
    public override string Id => "SHIRUBIMOD_CHARACTER_SHIRUBI_CHARACTER4_EPOCH";
    public override int EraPosition => 22;
}

[RegisterEpoch]
public sealed class Shirubi5Epoch : ShirubiEpochBase
{
    public override string Id => "SHIRUBIMOD_CHARACTER_SHIRUBI_CHARACTER5_EPOCH";
    public override int EraPosition => 23;
}

[RegisterEpoch]
public sealed class Shirubi6Epoch : ShirubiEpochBase
{
    public override string Id => "SHIRUBIMOD_CHARACTER_SHIRUBI_CHARACTER6_EPOCH";
    public override int EraPosition => 24;
}

[RegisterEpoch]
public sealed class Shirubi7Epoch : ShirubiEpochBase
{
    public override string Id => "SHIRUBIMOD_CHARACTER_SHIRUBI_CHARACTER7_EPOCH";
    public override int EraPosition => 25;
}
