using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.API.Server;
using Vintagestory.API.Config;
using Vintagestory.API.Common;

namespace UnusedWorkItemRefund;

public class UnusedWorkItemRefundModSystem : ModSystem
{
    public const string ModId = "unusedworkitemrefund";
    public static Harmony? HarmonyInstance;
    
    // Called on server and client
    // Useful for registering block/entity classes on both sides
    public override void Start(ICoreAPI api)
    {
        Mod.Logger.Notification("About to patch Work Item To Ingot");

        if (HarmonyInstance == null)
        {
            HarmonyInstance = new Harmony(ModId);
            HarmonyInstance.PatchAll();
        }
        
    }
    
    public override void Dispose()
    {
        HarmonyInstance.UnpatchAll(ModId);
        
        HarmonyInstance = null;
        base.Dispose();
    }
}