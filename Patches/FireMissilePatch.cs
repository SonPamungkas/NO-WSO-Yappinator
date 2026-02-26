using HarmonyLib;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace WSOYappinator.Patches
{
    [HarmonyPatch(typeof(MountedMissile), nameof(MountedMissile.Fire))]
    internal static class FireMissilePatch
    {
        private static void Postfix(MountedMissile __instance, Unit owner, Unit target, Vector3 inheritedVelocity,
                                    WeaponStation weaponStation, GlobalPosition aimpoint)
        {
            if (GameManager.IsLocalAircraft(owner as Aircraft)) Plugin.I.TriggerVoiceline(ResolveFireEvent(__instance));
        }

        private static readonly Dictionary<WeaponInfo, VoiceEvent> _fireEventCache = [];

        private static VoiceEvent ResolveFireEvent(MountedMissile mm)
        {
            WeaponInfo info = mm?.info;
            if (info == null) return VoiceEvent.fireMissile;
            if (_fireEventCache.TryGetValue(info, out VoiceEvent cached)) return cached;

            VoiceEvent result;
            if (info.nuclear) result = VoiceEvent.fireNuclear;
            else if (info.bomb || info.name.Contains("glide",StringComparison.OrdinalIgnoreCase)) result = VoiceEvent.fireBomb;
            else if (info.laserGuided) result = VoiceEvent.fireAGR;
            else
            {
                result = (info.weaponPrefab ? info.weaponPrefab.GetComponent<MissileSeeker>() : null) switch
                {
                    IRSeeker => VoiceEvent.fireFox2,
                    ARMSeeker => VoiceEvent.fireARM,
                    ARHSeeker => VoiceEvent.fireFox3,
                    OpticalSeeker => VoiceEvent.fireAGM,
                    OpticalSeekerCruiseMissile => VoiceEvent.fireCruise,
                    _ => VoiceEvent.fireMissile
                };
            }

            _fireEventCache[info] = result;
            return result;
        }

    }
}