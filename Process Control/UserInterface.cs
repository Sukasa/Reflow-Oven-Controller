using System;
using Microsoft.SPOT;

namespace Reflow_Oven_Controller.Process_Control
{
    class UserInterface
    {
        public Screens CurrentScreen = Screens.Splash;

        public enum Screens
        {
            Init,
            Splash,
            Home,
            Settings,
            SettingsIp,
            SettingsAbout,
            Profiles,
            Bake
        }

        public void Tick()
        {

            // Called once per scan
        }

        public UserInterface()
        {
            CurrentScreen = Screens.Splash;
            OvenController.LCD.LoadImage("TestLogo");
        }
    }
}
