using System;
using System.Collections.Generic;
using BepInEx.Logging;
using UnityEngine;

namespace WSOYappinator
{
    public sealed class VoicelineDirector
    {
        private struct Gate { public bool fired; public int fails; public float nextRollAt; }

        private readonly System.Random _rnd = new();
        private readonly VoicelineCatalog _catalog;
        private readonly AudioPlayer _player;
        private readonly Func<VoiceEvent, int> _pri;
        private readonly Func<float> _minCd;
        private readonly Func<int> _baseChance;
        private readonly ManualLogSource _log;
        private readonly Func<bool> _verbose;
        private float _cooldownUntil;
        private int _cooldownPriority;
        private readonly Dictionary<VoiceEvent, float> _suppressUntil = new();
        private readonly Dictionary<VoiceEvent, Gate> _gate = new();
        public float LastPlayedAt;
        public VoicelineDirector(
            VoicelineCatalog catalog,
            AudioPlayer player,
            Func<VoiceEvent, int> getPriority,
            Func<float> minCooldownSeconds,
            Func<int> baseChancePercent,
            Func<bool> verbose,
            ManualLogSource log)
        {
            _catalog = catalog;
            _player = player;
            _pri = getPriority ?? (_ => 0);
            _minCd = minCooldownSeconds ?? (() => 0f);
            _baseChance = baseChancePercent ?? (() => 100);
            _verbose = verbose ?? (() => false);
            _log = log;
            LastPlayedAt = Time.time;
        }

        public void Reset(bool stopAudio)
        {
            if (stopAudio) _player?.StopImmediate();
            _cooldownUntil = 0f;
            _cooldownPriority = 0;
            _suppressUntil.Clear();
            _gate.Clear();
            LastPlayedAt = Time.time;
        }

        public void ResetTransient()
        {
            _suppressUntil.Clear();
            _gate.Clear();
        }

        public void TryGated(VoiceEvent evt, int basePct = 5, int stepPct = 5, int maxPct = 50, float rollCooldownSeconds = 5f)
        {
            Gate st = _gate.TryGetValue(evt, out Gate s) ? s : default;
            if (st.fired) return;

            float now = Time.time;
            if (now < st.nextRollAt) return;

            int chance = Mathf.Clamp(basePct + st.fails * stepPct, 0, maxPct);
            if (_rnd.Next(100) < chance)
            {
                if (TryPlay(evt))
                {
                    st.fired = true;
                    st.nextRollAt = float.PositiveInfinity;
                }
                else st.nextRollAt = now + rollCooldownSeconds;
            }
            else
            {
                st.fails++;
                st.nextRollAt = now + rollCooldownSeconds;
            }

            _gate[evt] = st;
        }
            
        public bool TryPlay(VoiceEvent requested, float suppressTime = 0.5f)
        {
            if (_catalog == null || _player == null) return false;

            void V(string msg, bool play = false)
            {
                if (!_verbose())
                {
                    return;
                }
                if (play) _log.LogWarning(msg);
                else _log.LogDebug(msg);
            }

            float now = Time.time;
            V($"try evt={requested} t={now:0.00}");
            if (now >= _cooldownUntil) _cooldownPriority = 0;

            VoiceEvent effective = requested;

            if (requested != VoiceEvent.RareClip &&
                _catalog.HasClips(VoiceEvent.RareClip) &&
                _pri(requested) <= _pri(VoiceEvent.RareClip) && 
                _rnd.NextDouble() < (Plugin.I.rareClipFrequency.Value / 100f))
            {
                effective = VoiceEvent.RareClip;
            }

            int pri = _pri(effective);
            bool preempt = now < _cooldownUntil && pri > _cooldownPriority;

            if (!preempt && now < _cooldownUntil)
            {
                V($"block cooldown evt={requested} eff={effective} pri={pri} until={_cooldownUntil:0.00}");
                return false;
            }
            if (IsSuppressed(requested, now) || IsSuppressed(effective, now))
            {
                V($"block suppress evt={requested} eff={effective}");
                return false;
            }
            if (!preempt)
            {
                int chance = Mathf.Clamp(_baseChance() + pri, 0, 100);
                int roll = _rnd.Next(100);
                if (roll >= chance)
                {
                    V($"block chance evt={requested} eff={effective} roll={roll} chance={chance}");
                    return false;
                }
            }

            if (!TryResolveKey(effective, out VoiceEvent key))
            {
                V($"block noclips evt={requested} eff={effective}");
                return false;
            }

            if (IsSuppressed(key, now))
            {
                V($"block suppress key={key} (from {requested})");
                return false;
            }

            AudioClip clip = _catalog.RequestClip(key);
            if (clip == null)
            {
                V($"block nullclip key={key} (from {requested})");
                return false;
            }
            if (requested is VoiceEvent.beingJammed) suppressTime = 20f;
            float until = now + suppressTime;
            _suppressUntil[requested] = until;
            _suppressUntil[effective] = until;
            _suppressUntil[key] = until;

            _player.TryPlay(clip);
            LastPlayedAt = Time.time;
            V($"play evt={requested} key={key} clip={clip.name} len={clip.length:0.00}");

            float cd = Mathf.Max(_minCd(), clip.length);
            _cooldownUntil = now + cd;
            _cooldownPriority = Mathf.Max(_cooldownPriority, pri);

            return true;
        }

        private bool IsSuppressed(VoiceEvent evt, float now)
            => _suppressUntil.TryGetValue(evt, out float u) && now < u;

        private bool TryResolveKey(VoiceEvent evt, out VoiceEvent key)
        {
            if (_catalog.HasClips(evt)) { key = evt; return true; }

            if (VoiceEventMaps.Fallbacks.TryGetValue(evt, out VoiceEvent[] chain))
            {
                for (int i = 0; i < chain.Length; i++)
                {
                    var fb = chain[i];
                    if (_catalog.HasClips(fb)) { key = fb; return true; }
                }
            }

            key = evt;
            return false;
        }
    }
}
