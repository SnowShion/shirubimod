using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Events;
using shirubimod.Scripts.Content.Relics;
using STS2RitsuLib;
using STS2RitsuLib.Scaffolding.Ancients.Options;

namespace shirubimod.Scripts.Content;

public static class ShirubiNeowOptions
{
    private const string OptionKey = "SHIRUBI_NEOW_MEMORY_OPTION";

    public static void Register()
    {
        RitsuLibFramework.RegisterAncientOption<Neow>(
            ModInfo.Id,
            ModAncientOptionRule.Single(CreateMemoryOption, IsShirubiRun, priority: 100));
    }

    private static bool IsShirubiRun(AncientEventModel ancient)
    {
        return ancient.Owner?.Character is ShirubiCharacter;
    }

    private static EventOption CreateMemoryOption(AncientEventModel ancient)
    {
        var relic = ModelDb.Relic<HerMemoryWorldMemory>().ToMutable();
        if (ancient.Owner != null)
        {
            relic.Owner = ancient.Owner;
        }

        return EventOption.FromRelic(
            relic,
            ancient,
            async () => await ChooseMemoryRelic(ancient, relic),
            OptionKey);
    }

    private static async Task ChooseMemoryRelic(AncientEventModel ancient, RelicModel relic)
    {
        var owner = ancient.Owner;
        if (owner == null)
        {
            return;
        }

        relic.Owner = owner;
        await RelicCmd.Obtain(relic, owner, -1);
        ancient.StartPreFinished();
    }
}
