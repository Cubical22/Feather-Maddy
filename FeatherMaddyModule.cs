using System;
using System.Collections;
using System.Linq;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.FeatherMaddy
{
    public class FeatherMaddyModule : EverestModule
    {
        public static FeatherMaddyModule Instance { get; private set; }

        public override Type SettingsType => typeof(FeatherMaddyModuleSettings);
        public static FeatherMaddyModuleSettings Settings => (FeatherMaddyModuleSettings)Instance._Settings;

        public override Type SessionType => typeof(FeatherMaddyModuleSession);
        public static FeatherMaddyModuleSession Session => (FeatherMaddyModuleSession)Instance._Session;

        public FeatherMaddyModule()
        {
            Instance = this;
#if DEBUG
            // debug builds use verbose logging
            Logger.SetLogLevel(nameof(FeatherMaddyModule), LogLevel.Verbose);
#else
            // release builds use info logging to reduce spam in log files
            Logger.SetLogLevel(nameof(FeatherMaddyModule), LogLevel.Info);
#endif
        }

        private int _featherCount = 1;
        private const int MaxFeatherCount = 1;

        // these values are used to make the feather shine mechanic
        private float _featherLightingProgress = 1f;
        private const float SceneLightingMaxAmount = 1f;
        private const float SceneLightingMinAmount = 0.2f;
        private const float LightChangeSpeedMultiplier = 1f;
        private bool _stillCastingFeather = false; // this var is used to stop the feather spawning loop

        private float? _levelStartingAlpha;
        private Level _currentLevel;

        private SoundSource _currentFeatherPlaySoundSource = null;
        
        private Vector2 _playerFxPosition = Vector2.Zero;
        private float _animationTime = 0f;

        public override void Load()
        {
            On.Celeste.Level.LoadLevel += LevelLoadingConfig;
            On.Celeste.Level.TransitionTo += TransitionLightingSet;

            On.Celeste.Player.DashBegin += FeatherDashStart;
            On.Celeste.Player.RefillDash += RefillFeatherCountOnFloor;
            On.Celeste.Player.StarFlyEnd += DecreaseDashOnStarFlyEnd;
            On.Celeste.Player.Update += UpdateHairColor;
            On.Celeste.Player.IntroRespawnBegin += RefillFeatherOnRespawn;
            On.Celeste.Player.Render += DrawPlayerEffects;
            
            On.Celeste.Refill.OnPlayer += RefillFeatherCountAddition;
        }

        private void DrawPlayerEffects(On.Celeste.Player.orig_Render orig, Player self)
        {
            var offsetUnit = self.StateMachine.State is 5 or 2 or 4 ? 8f : 6f; // handling if the player
            // is using boosters or not
            _playerFxPosition = self.Position - Vector2.UnitY * offsetUnit;
            // this is used to make the growing effect on featherShine
            var subScale = 0f;
            if (Settings.DarkRooms)
            {
                subScale = (SceneLightingMinAmount - _featherLightingProgress) /
                                 (SceneLightingMaxAmount - SceneLightingMinAmount) * 7f;
            }

            if (Settings.PlayerEffects)
            {
                var topCircleOffset = (float)Math.Sin(_animationTime) * 3f;
                Draw.Circle(_playerFxPosition
                    , 39f + topCircleOffset - subScale, Color.Goldenrod, 1f, 10);
                var middleCircleOffset = (float)Math.Sin(_animationTime + 0.7f) * 3f;
                Draw.Circle(_playerFxPosition
                    , 28f + middleCircleOffset - subScale, Color.Gold, 2f, 10);
                var bottomCircleOffset = (float)Math.Sin(_animationTime + 1.3f) * 3f;
                Draw.Circle(_playerFxPosition, 15f + bottomCircleOffset - subScale, 
                    Color.Lerp(Color.Gold, Color.BlueViolet, 0.3f), 3f, 10);

                Draw.Line(_playerFxPosition,
                    _playerFxPosition +
                    new Vector2((float)Math.Cos(_animationTime + Math.PI * 2 / 3) * 
                                (39f + topCircleOffset - subScale),
                        (float)Math.Sin(_animationTime + Math.PI * 2 / 3) * 
                                (39f + topCircleOffset - subScale)),
                    Color.Goldenrod);
                Draw.Line(_playerFxPosition,
                    _playerFxPosition +
                    new Vector2((float)Math.Cos(_animationTime) * (28f + middleCircleOffset - subScale),
                        (float)Math.Sin(_animationTime) * (28f + middleCircleOffset - subScale)),
                    Color.Gold);
                Draw.Line(_playerFxPosition,
                    _playerFxPosition +
                    new Vector2((float)Math.Cos(_animationTime + Math.PI * 4 / 3) *
                                (15f + bottomCircleOffset - subScale),
                        (float)Math.Sin(_animationTime + Math.PI * 4 / 3) * 
                                (15f + bottomCircleOffset - subScale)),
                    Color.Lerp(Color.Gold, Color.BlueViolet, 0.3f));
            }
            
            Logger.Log(nameof(FeatherMaddyModule), _playerFxPosition.ToString());
            orig(self);
        }

        private void RefillFeatherOnRespawn(On.Celeste.Player.orig_IntroRespawnBegin orig,Player self)
        {
            orig(self);
            _featherCount = MaxFeatherCount;
        }

        private void TransitionLightingSet(On.Celeste.Level.orig_TransitionTo orig, Level self, LevelData next, Vector2 direction)
        {
            if (_levelStartingAlpha != null && !Settings.DarkRooms)
            {
                self.Lighting.Alpha = (float)_levelStartingAlpha;
            }
            
            // handling in game darkrooms
            if (self.DarkRoom)
            {
                self.Lighting.Alpha = self.Session.DarkRoomAlpha;
            }
            
            orig(self, next, direction);

            Logger.Log(nameof(FeatherMaddyModule), "a transition happened");
        }

        private void LevelLoadingConfig(On.Celeste.Level.orig_LoadLevel orig, Level self, Player.IntroTypes introType,
            bool fromLoader)
        {
            orig(self, introType, fromLoader);
            if (Settings.DarkRooms)
                self.Lighting.Alpha = SceneLightingMaxAmount;
            else if (_currentLevel != self || _currentLevel == null)
            {
                _currentLevel = self;
                _levelStartingAlpha = self.Lighting.Alpha;
            }
        }

        private void RefillFeatherCountAddition(On.Celeste.Refill.orig_OnPlayer orig, Refill self, Player player)
        {
            _featherCount = MaxFeatherCount;
            orig(self, player);
        }

        private void UpdateHairColor(On.Celeste.Player.orig_Update orig, Player self)
        {
            orig(self);

            _animationTime += Engine.DeltaTime;
            if (Settings.FeatherFly)
            {
                if (_featherCount == 0)
                {
                    self.OverrideHairColor = Color.Gold;
                }
                else
                {
                    self.OverrideHairColor = null;
                }
            }

            // this section is used to make the duck light system work
            Scene scene = Engine.Scene;
            if (scene == null || scene.GetType() != typeof(Level)) return;
            Level lvl = scene as Level;
            
            if (lvl == null) return;

            _levelStartingAlpha ??= lvl.BaseLightingAlpha;

            if (Settings.DarkRooms)
            {
                if (self.Ducking && Input.GrabCheck)
                {
                    // update the delay time amount
                    _featherLightingProgress -= Engine.DeltaTime * LightChangeSpeedMultiplier;
                    
                    // this section is used to spawn the particles used on this ability
                    _stillCastingFeather = true;
                    
                    self.Add(new Coroutine(FeatherParticleSpawn(lvl, self)));
                    self.OverrideHairColor = Color.Gold;

                    _currentFeatherPlaySoundSource ??= new SoundSource();

                    if (!_currentFeatherPlaySoundSource.Playing)
                        _currentFeatherPlaySoundSource.Play("event:/game/06_reflection/feather_state_loop");
                }
                else
                {
                    _featherLightingProgress += Engine.DeltaTime * LightChangeSpeedMultiplier;

                    _stillCastingFeather = false;

                    if (_currentFeatherPlaySoundSource is { Playing: true })
                    {
                        _currentFeatherPlaySoundSource.Stop();

                        if (!Settings.FeatherFly) // this is just used to make the hair color go back to default
                        { // after using the feather shine ability
                            self.OverrideHairColor = null;
                        }
                    }
                }


                // implementing the lighting effect based on the var that was defined
                lvl.BaseLightingAlpha = SceneLightingMaxAmount;

                /* the old way
                 _featherLightingProgress = _featherLightingProgress switch
                {
                    < SceneLightingMinAmount => SceneLightingMinAmount,
                    > SceneLightingMaxAmount => SceneLightingMaxAmount,
                    _ => _featherLightingProgress
                };
                */ 
                
                // the new way
                _featherLightingProgress = Math.Min(_featherLightingProgress, SceneLightingMaxAmount);
                _featherLightingProgress = Math.Max(_featherLightingProgress, SceneLightingMinAmount);

                lvl.Lighting.Alpha = _featherLightingProgress;
            }
            else
            {
                if (_levelStartingAlpha == null) return;
                // lvl.Lighting.Alpha = (float)_levelStartingAlpha;
                lvl.BaseLightingAlpha = (float)_levelStartingAlpha;
            }
        }

        private IEnumerator FeatherParticleSpawn(Level lvl, Player self)
        {
            while (_stillCastingFeather)
            {
                lvl.ParticlesFG.Emit(FlyFeather.P_Respawn, 4, 
                    self.Position - Vector2.UnitY * 5f, Vector2.One * 10f);

                yield return 50;
            }
        }

        private void FeatherDashStart(On.Celeste.Player.orig_DashBegin orig, Player self)
        {
            if (_featherCount != 0)
            {
                if (Settings.FeatherFly)
                {
                    self.StartStarFly();
                    Audio.Play("event:/game/06_reflection/feather_bubble_get",
                        self.Position);
                    if (Settings.PlayerEffects)
                    {
                        if (self.Scene is Level lvl)
                            lvl.ParticlesFG.Emit(FlyFeather.P_Collect, 10, _playerFxPosition, Vector2.One * 6f);
                    }
                }
                _featherCount--;
            }

            orig(self);
        }

        private bool RefillFeatherCountOnFloor(On.Celeste.Player.orig_RefillDash orig, Player self)
        {
            _featherCount = MaxFeatherCount;
            bool result = orig(self);
            return result;
        }

        private void DecreaseDashOnStarFlyEnd(On.Celeste.Player.orig_StarFlyEnd orig, Player self)
        {
            if (Settings.FeatherFly)
                self.Dashes--;
            _featherCount--;

            orig(self);
        }

        public override void Unload()
        {
            On.Celeste.Level.LoadLevel -= LevelLoadingConfig;
            On.Celeste.Level.TransitionTo -= TransitionLightingSet;

            On.Celeste.Player.DashBegin -= FeatherDashStart;
            On.Celeste.Player.RefillDash -= RefillFeatherCountOnFloor;
            On.Celeste.Player.StarFlyEnd -= DecreaseDashOnStarFlyEnd;
            On.Celeste.Player.Update -= UpdateHairColor;
            On.Celeste.Player.IntroRespawnBegin -= RefillFeatherOnRespawn;
            
            On.Celeste.Refill.OnPlayer -= RefillFeatherCountAddition;
        }
    }
}