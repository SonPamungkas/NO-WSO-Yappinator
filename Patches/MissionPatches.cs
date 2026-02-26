using System;
using System.Collections.Generic;
using System.Text;
using HarmonyLib;
using NuclearOption.SavedMission.ObjectiveV2;

namespace WSOYappinator.Patches
{
    internal class MissionPatches
    {

        [HarmonyPatch(typeof(GameManager), nameof(GameManager.FinishGame))]
        internal static class GameManager_FinishGame_Patch
        {
            private static void Postfix(GameManager __instance, GameResolution resolution)
            {
                if (resolution is GameResolution.Ongoing) 
                    return;
                else if (resolution is GameResolution.Victory)
                    Plugin.I.TriggerVoiceline(VoiceEvent.OutcomeVictory);
                else if (resolution is GameResolution.Defeat)
                    Plugin.I.TriggerVoiceline(VoiceEvent.OutcomeDefeat);
                Plugin.seenNukes = false;
                Plugin.reachedTactical = false;
                Plugin.reachedStrategic = false;
            }
        }
        [HarmonyPatch(typeof(FactionHQ), nameof(FactionHQ.SetScore))]
        internal static class FactionHQ_SetScore_Patch
        {
            private static void Postfix(FactionHQ __instance, float score)
            {
                if (!Plugin.reachedStrategic && NetworkSceneSingleton<MissionManager>.i.currentEscalation >= NetworkSceneSingleton<MissionManager>.i.strategicThreshold)
                {
                    Plugin.I.TriggerVoiceline(VoiceEvent.EscalationStrategic);
                    Plugin.reachedStrategic = true;
                }
                else if (!Plugin.reachedTactical && NetworkSceneSingleton<MissionManager>.i.currentEscalation >= NetworkSceneSingleton<MissionManager>.i.tacticalThreshold)
                {
                    Plugin.I.TriggerVoiceline(VoiceEvent.EscalationTactical);
                    Plugin.reachedTactical = true;
                }
            }
        }

        [HarmonyPatch(typeof(Missile), nameof(Missile.UserCode_RpcDetonate_897349600))]
        static class Missile_Detonate_Patch
        {

            static void Postfix(Missile __instance, bool armed)
            {
                if (armed && !Plugin.seenNukes && __instance.GetWeaponInfo().nuclear)
                {
                    Plugin.seenNukes = true;
                    if (GameManager.IsLocalHQ(__instance.NetworkHQ))
                        Plugin.I.TriggerVoiceline(VoiceEvent.FirstNukeFriendly);
                    else
                        Plugin.I.TriggerVoiceline(VoiceEvent.FirstNukeHostile);
                }
            }
        }
    }
    /*
    [HarmonyPatch(typeof(Outcome), nameof(Outcome.Complete))]
    internal static class Outcome_Complete_Patch
    {
        private static void Postfix(Outcome __instance, Objective completedObjective)
        {
            if (completedObjective.SavedObjective.Hidden) return; //hidden objectives
            
            GameManager.GetLocalFaction(out Faction localFaction);
            if (localFaction.factionName != completedObjective.SavedObjective.Faction) 
            { 
                Plugin.I.Log.LogInfo($"[OUTCOME EVENT] {localFaction.factionName} != {completedObjective.SavedObjective.Faction}"); 
                return; 
            }
            
            
            if (resolution is GameResolution.Defeat) 
            { 
                Plugin.I.TriggerVoiceline(VoiceEvent.onGLOC);
            }
        }
    }
    */
}
