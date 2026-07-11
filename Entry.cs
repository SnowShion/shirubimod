using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;
using STS2RitsuLib.Interop;
using shirubimod.Scripts.Content;
using System.Reflection;

namespace shirubimod.Scripts;

[ModInitializer(nameof(Init))]
public class Entry
{
    // 游戏加载 DLL 后会调用这个方法。所有“启动时注册/打补丁”的逻辑都放在这里。
    public static void Init()
    {
        // Harmony 会扫描当前程序集里所有带 [HarmonyPatch] 的类。
        // 目前本项目用它来给角色和卡牌文本提供本地化兜底。
        var harmony = new Harmony(ModInfo.Id);
        harmony.PatchAll();

        // 告诉 RitsuLib：这个 DLL 里有需要自动注册的内容。
        // 例如 [RegisterCharacter]、[RegisterCard]、[RegisterSharedCardPool] 等。
        ModTypeDiscoveryHub.RegisterModAssembly(ModInfo.Id, Assembly.GetExecutingAssembly());

        // 给希比的开局涅奥事件追加一个专属第 4 选项。
        ShirubiNeowOptions.Register();

        Log.Info("Shirubi mod initialized.");
    }
}
