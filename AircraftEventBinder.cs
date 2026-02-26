using System;
using System.Collections.Generic;

namespace WSOYappinator
{
    public sealed class AircraftEventBinder(Action<VoiceEvent, string> onEvent, Action<string> onInfo, Func<bool> verbose) : IDisposable
    {
        private readonly Action<VoiceEvent, string> _emit = onEvent ?? throw new ArgumentNullException(nameof(onEvent));
        private readonly Action<string> _info = onInfo ?? (_ => { });
        private readonly Func<bool> _verbose = verbose ?? (() => false);

        private Aircraft _ac;
        private readonly List<Action> _unsubs = new();

        public void Bind(Aircraft aircraft)
        {
            if (aircraft != null && !aircraft) aircraft = null;
            if (_ac != null && !_ac) _ac = null;
            if (ReferenceEquals(_ac, aircraft)) return;

            Unbind();
            _ac = aircraft;

            if (_ac == null) { _info("No local aircraft"); return; }

            Subscribe(_ac);
            _info($"Bound aircraft: {_ac.unitName}");
            _emit(VoiceEvent.Spawn, null);
        }

        public void Unbind()
        {
            for (int i = _unsubs.Count - 1; i >= 0; --i)
            {
                try { _unsubs[i](); } catch { }
            }
            _unsubs.Clear();
            _ac = null;
        }

        public void Dispose() => Unbind();

        private void Subscribe(Aircraft ac)
        {
            static T Safe<T>(Func<T> f, T fallback = default)
            {
                try { return f(); } catch { return fallback; }
            }

            void AddUnsub(Action a) => _unsubs.Add(a);

            // Aircraft-level events
            void OnEject() => _emit(VoiceEvent.Eject, null);
            void OnTouchdown() => _emit(VoiceEvent.Touchdown, null);
            void OnJam(Unit.JamEventArgs _) => _emit(VoiceEvent.beingJammed, null);
            void OnRearm(RearmEventArgs _) => _emit(VoiceEvent.Rearm, null);
            void OnSortieSuccess(float v) => _emit(VoiceEvent.onSortieSuccess, $"value={v:0.00}");

            void OnGear(Aircraft.OnSetGear e)
            {
                if (e.gearState == LandingGear.GearState.LockedRetracted) _emit(VoiceEvent.GearUp, null);
                else if (e.gearState == LandingGear.GearState.LockedExtended) _emit(VoiceEvent.GearDown, null);
            }

            void OnAssist(Aircraft.OnFlightAssistToggle e)
                => _emit(e.enabled ? VoiceEvent.FlightAssistOn : VoiceEvent.FlightAssistOff, null);

            void OnRadar(Aircraft.OnRadarWarning e)
            {
                try
                {
                    if (!ac.KnownRadarWarning(e.emitter)) _emit(VoiceEvent.radarPingNew, e.emitter ? e.emitter.name : "emitter=null");
                    if (e.isTarget) _emit(VoiceEvent.radarPingLocked, e.emitter ? e.emitter.name : "emitter=null");
                }
                catch { }
            }

            ac.onEject += OnEject; AddUnsub(() => { if (ac) ac.onEject -= OnEject; });
            ac.OnTouchdown += OnTouchdown; AddUnsub(() => { if (ac) ac.OnTouchdown -= OnTouchdown; });
            ac.onJam += OnJam; AddUnsub(() => { if (ac) ac.onJam -= OnJam; });
            ac.OnRearm += OnRearm; AddUnsub(() => { if (ac) ac.OnRearm -= OnRearm; });
            ac.onSortieSuccessful += OnSortieSuccess; AddUnsub(() => { if (ac) ac.onSortieSuccessful -= OnSortieSuccess; });
            ac.onSetGear += OnGear; AddUnsub(() => { if (ac) ac.onSetGear -= OnGear; });
            ac.onSetFlightAssist += OnAssist; AddUnsub(() => { if (ac) ac.onSetFlightAssist -= OnAssist; });
            ac.onRadarWarning += OnRadar; AddUnsub(() => { if (ac) ac.onRadarWarning -= OnRadar; });

            // ControlsFilter (auto-hover)
            var cf = Safe(() => ac.GetControlsFilter());
            if (cf != null)
            {
                void OnAutoHover()
                {
                    bool enabled = Safe(() => cf.IsAutoHoverEnabled());
                    _emit(enabled ? VoiceEvent.AutohoverOn : VoiceEvent.AutohoverOff, null);
                }

                cf.OnSetAutoHover += OnAutoHover;
                AddUnsub(() => { if (cf) cf.OnSetAutoHover -= OnAutoHover; });
            }

            // Missile warning system
            var mws = Safe(() => ac.GetMissileWarningSystem());
            if (mws != null)
            {
                void OnWarn(MissileWarning.OnMissileWarning e)
                {
                    var m = e.missile;
                    var evt =
                        m && m.TryGetComponent<SARHSeeker>(out _) ? VoiceEvent.RwrOnFox1 :
                        m && m.TryGetComponent<IRSeeker>(out _) ? VoiceEvent.RwrOnFox2 :
                        m && m.TryGetComponent<ARHSeeker>(out _) ? VoiceEvent.RwrOnFox3 :
                        VoiceEvent.RwrOn;

                    _emit(evt, m ? m.name : "missile=null");
                }

                void OffWarn(MissileWarning.OffMissileWarning e)
                {
                    var m = e.missile;
                    var evt =
                        m && m.TryGetComponent<SARHSeeker>(out _) ? VoiceEvent.RwrOffFox1 :
                        m && m.TryGetComponent<IRSeeker>(out _) ? VoiceEvent.RwrOffFox2 :
                        m && m.TryGetComponent<ARHSeeker>(out _) ? VoiceEvent.RwrOffFox3 :
                        VoiceEvent.RwrOff;

                    _emit(evt, m ? m.name : "missile=null");
                }

                mws.onMissileWarning += OnWarn;
                mws.offMissileWarning += OffWarn;
                AddUnsub(() => { if (mws) { mws.onMissileWarning -= OnWarn; mws.offMissileWarning -= OffWarn; } });

                _info("Subscribed MWS (on/off)");
            }
            else _info("MissileWarningSystem missing");

            // Parts damage/engine/detach
            UnitPart[] parts = Safe(() => ac.GetAllParts()?.ToArray()) ?? Array.Empty<UnitPart>();
            foreach (var part in parts)
            {
                if (!part) continue;

                void OnDamage(UnitPart.OnApplyDamage e)
                {
                    if (!_verbose()) return;
                    float total = e.impactDamage + e.pierceDamage + e.fireDamage + e.blastDamage;
                    _emit(VoiceEvent.takeDamage, $"dmg={total:0.0}");
                }

                void OnDetached(UnitPart p)
                {
                    if (ReferenceEquals(p, part)) _emit(VoiceEvent.partDetach, part.name);
                }

                part.onApplyDamage += OnDamage;
                part.onPartDetached += OnDetached;

                IEngine engine = null;
                try { part.TryGetComponent(out engine); } catch { }

                void OnEngineDisable() => _emit(VoiceEvent.engineLost, part.name);
                void OnEngineDamage() { if (_verbose()) _emit(VoiceEvent.engineDamage, part.name); }

                if (engine != null)
                {
                    engine.OnEngineDisable += OnEngineDisable;
                    engine.OnEngineDamage += OnEngineDamage;
                }

                AddUnsub(() =>
                {
                    if (!part) return;
                    part.onApplyDamage -= OnDamage;
                    part.onPartDetached -= OnDetached;
                    if (engine != null)
                    {
                        engine.OnEngineDisable -= OnEngineDisable;
                        engine.OnEngineDamage -= OnEngineDamage;
                    }
                });
            }
        }
    }
}
