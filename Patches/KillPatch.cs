using HarmonyLib;
using NuclearOption.Networking;

namespace WSOYappinator.Patches
{
    [HarmonyPatch(typeof(FactionHQ), nameof(FactionHQ.ReportKillAction))]
    internal static class KillPatch
    {
        private static void Postfix(Player player, Unit target, float factor)
        {
            if (!GameManager.IsLocalPlayer(player)) return;

            Plugin.instance.TriggerVoiceline(target switch
            {
                GroundVehicle => VoiceEvent.killGround,
                Building => VoiceEvent.killBuilding,
                Aircraft => VoiceEvent.killAircraft,
                Missile => VoiceEvent.killMissile,
                Ship => VoiceEvent.killShip,
                _ => VoiceEvent.killGeneric
            });
        } 
    }
}