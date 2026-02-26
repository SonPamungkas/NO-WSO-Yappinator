using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace WSOYappinator.Patches
{
    [HarmonyPatch]
    internal static class CountermeasureStation_Fire_Patch
    {
        static MethodBase TargetMethod() => AccessTools.Method(AccessTools.Inner(typeof(CountermeasureManager), "CountermeasureStation"), "Fire", [typeof(Aircraft)]);
        static void Postfix(CountermeasureManager __instance, Aircraft aircraft)
        {
            if (!GameManager.IsLocalAircraft(aircraft)) return;

            int ammo = Traverse.Create(__instance).Field("ammo").GetValue<int>();
            if (ammo is > 1 and <= 5) Plugin.I.TriggerVoiceline(VoiceEvent.noFlares, 10f);
        }
    }
}
