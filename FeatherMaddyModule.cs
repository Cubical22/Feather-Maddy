using Microsoft.Xna.Framework;
using System;

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

        private int featherCount = 1;
        private readonly int maxFeatherCount = 1;

        public override void Load()
        {
            On.Celeste.Player.DashBegin += FeatherDashStart;
            On.Celeste.Player.RefillDash += ReffilFeatherCountOnFloor;
            On.Celeste.Player.StarFlyEnd += DecreaseDashOnStarFlyEnd;
            On.Celeste.Player.Update += UpdateHairColor;
            On.Celeste.Refill.OnPlayer += RefillFeatherCountAddition;
        }

        private void RefillFeatherCountAddition(On.Celeste.Refill.orig_OnPlayer orig, Refill self, Player player)
        {
            featherCount = maxFeatherCount;
            orig(self, player);
        }

        private void UpdateHairColor(On.Celeste.Player.orig_Update orig, Player self)
        {
            orig(self);
            if (featherCount == 0)
            {
                self.OverrideHairColor = Color.Gold;
            } else
            {
                self.OverrideHairColor = null;
            }
        }

        private void FeatherDashStart(On.Celeste.Player.orig_DashBegin orig, Player self)
        {
            if (featherCount != 0)
            {
                self.StartStarFly();
                featherCount--;
            }

            orig(self);
        }

        private bool ReffilFeatherCountOnFloor(On.Celeste.Player.orig_RefillDash orig, Player self)
        {
            featherCount = maxFeatherCount;
            bool result = orig(self);
            return result;
        }

        private void DecreaseDashOnStarFlyEnd(On.Celeste.Player.orig_StarFlyEnd orig, Player self)
        {
            self.Dashes--;
            featherCount--;

            orig(self);
        }

        public override void Unload()
        {
            On.Celeste.Player.DashBegin -= FeatherDashStart;
            On.Celeste.Player.RefillDash -= ReffilFeatherCountOnFloor;
            On.Celeste.Player.StarFlyEnd -= DecreaseDashOnStarFlyEnd;
            On.Celeste.Player.Update -= UpdateHairColor;
            On.Celeste.Refill.OnPlayer -= RefillFeatherCountAddition;
        }
    }
}