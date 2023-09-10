namespace Celeste.Mod.FeatherMaddy
{
    public class FeatherMaddyModuleSettings : EverestModuleSettings
    {
        [SettingName("Feather Fly")] 
        [SettingSubText("this option allows you to use the feather fly ability, over the normal dash.")]
        public bool FeatherFly { get; set; } = true;

        [SettingName("Feather Shine")]
        [SettingSubText("this will make the game a little harder.\n" +
                        "hold (Down + Grab) to simply light up the room you are in, using your Feather Shine ability")]
        public bool DarkRooms { get; set; } = false;
    }
}
