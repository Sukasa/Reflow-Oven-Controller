using System;

using Microsoft.SPOT.Net.NetworkInformation;
using Microsoft.SPOT;
using System.Net;
using Reflow_Oven_Controller.Hardware_Drivers;

namespace Reflow_Oven_Controller.Process_Control
{
    class UserInterface
    {
        public Screens CurrentScreen = Screens.Splash;
        public int MainMenuChoice = 0;
        public int SettingsMenuChoice = 0;

        public enum Screens
        {
            Splash,
            Home,
            Settings,
            SettingsIp,
            SettingsAbout,
            Profiles,
            Bake
        }

        private OvenKeypad Keypad = OvenController.Keypad;
        private Lcd LCD = OvenController.LCD;

        public void Tick()
        {
            if (OvenController.DoorAjar)
            {
                OvenController.Keypad.Beep(0);
            }


            OvenController.LCD.BacklightIntensity = (OvenController.TotalSeconds(DateTime.Now - OvenController.Keypad.LastBeepTime) > 20) ? OvenController.LcdDimBrightness : OvenController.LcdOnBrightness;


            switch (CurrentScreen)
            {
                case Screens.Home:
                    MainMenuTick();
                    break;
                case Screens.Splash:
                    if (OvenController.Keypad.IsKeyPressed(OvenKeypad.Keys.Any))
                    {
                        if (CurrentScreen == Screens.Splash)
                        {
                            LoadMainMenu();
                            CurrentScreen = Screens.Home;
                        }
                    }
                    break;
                case Screens.Settings:
                    SettingsTick();
                    break;
                case Screens.SettingsAbout:
                    AboutScreenTick();
                    break;
                case Screens.SettingsIp:
                    IPAddressTick();
                    break;
                case Screens.Profiles:
                    ProfileScreenTick();
                    break;
            }


            if (OvenController.TotalSeconds(DateTime.Now - OvenController.Keypad.LastBeepTime) > 60 && (CurrentScreen == Screens.Home || CurrentScreen == Screens.SettingsAbout))
            {
                OvenController.LCD.LoadImage("Splash");
                CurrentScreen = Screens.Splash;
            }


        }

        #region Main Menu

        private void MainMenuTick()
        {
            if (Keypad.IsKeyPressed(OvenKeypad.Keys.Up | OvenKeypad.Keys.Down))
            {
                MainMenuChoice = 1 - MainMenuChoice;

                DrawBoxes(MainMenuChoice);
            }
            else if (Keypad.IsKeyPressed(OvenKeypad.Keys.Start))
            {
                if (MainMenuChoice == 0)
                {
                    LoadProfileScreen();
                }
                else
                {
                    LoadSettingsMenu();
                }
            }
            else if (Keypad.IsKeyPressed(OvenKeypad.Keys.Presets))
            {
                // TODO Start oven via preset
            }
        }

        private void LoadMainMenu()
        {
            OvenController.LCD.LoadImage("MainMenu");
            CurrentScreen = Screens.Home;

            DrawBoxes(MainMenuChoice);
        }

        #endregion

        #region Settings Menu

        private void LoadSettingsMenu()
        {
            LCD.LoadImage("Settings");
            DrawBoxes(SettingsMenuChoice);
            CurrentScreen = Screens.Settings;
        }

        private void SettingsTick()
        {
            if (Keypad.IsKeyPressed(OvenKeypad.Keys.Up | OvenKeypad.Keys.Down))
            {
                SettingsMenuChoice = 1 - SettingsMenuChoice;
                DrawBoxes(SettingsMenuChoice);
            }
            else if (Keypad.IsKeyPressed(OvenKeypad.Keys.Stop))
            {
                LoadMainMenu();
            }
            else if (Keypad.IsKeyPressed(OvenKeypad.Keys.Start))
            {
                if (SettingsMenuChoice == 0)
                {
                    LoadIPAddressScreen();
                }
                else
                {
                    LoadAboutScreen();
                }
            }
        }

        #endregion

        #region About Screen

        private void LoadAboutScreen()
        {
            LCD.LoadImage("AboutScreen");
            CurrentScreen = Screens.SettingsAbout;
        }

        private void AboutScreenTick()
        {
            if (Keypad.IsKeyPressed(OvenKeypad.Keys.Stop))
            {
                LoadSettingsMenu();
            }

            if (Keypad.IsKeyPressed(OvenKeypad.Keys.Temp))
            {
                throw new Exception("Greensleeves");
            }
        }

        #endregion

        #region IP Address Screen

        private void LoadIPAddressScreen()
        {
            LCD.LoadImage("IPAddress");
            CurrentScreen = Screens.SettingsIp;

            foreach (NetworkInterface Interface in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (Interface.GatewayAddress != "0.0.0.0")
                {
                    LCD.DrawText(65, 106, 220, Interface.IPAddress, 4);
                    return;
                }
            }

            LCD.DrawText(65, 106, 220, "Not Available", 4);
        }

        private void IPAddressTick()
        {
            if (Keypad.IsKeyPressed(OvenKeypad.Keys.Stop))
            {
                LoadSettingsMenu();
            }

        }

        #endregion

        #region Profile Screen

        public int ProfileScroll;
        public int ProfileSelection;

        public void LoadProfileScreen()
        {
            RedrawProfileScreen();
            DrawProfileBoxes();
            CurrentScreen = Screens.Profiles;
        }

        public void RedrawProfileScreen()
        {
            LCD.LoadImage("Presets");

            // Draw entry 1
            LCD.DrawText(12, 31, 200, OvenController.ProfileController.Profiles[ProfileScroll], 3);

            // Draw entry 2
            LCD.DrawText(12, 92, 200, OvenController.ProfileController.Profiles[ProfileScroll + 1], 3);

            // Draw entry 3
            LCD.DrawText(12, 153, 200, OvenController.ProfileController.Profiles[ProfileScroll + 2], 3);
        }

        public void DrawProfileBoxes()
        {
            int ActiveBox = ProfileSelection - ProfileScroll;

            for (int Box = 0; Box < 3; Box++)
            {
                if (Box == ActiveBox)
                    LCD.DrawBrush = LCD.CreateBrush(255, 0, 0);
                else
                    LCD.DrawBrush = LCD.CreateBrush(0, 0, 0);

                LCD.DrawBox(7, 22 + 61 * Box, 242, 50);
            }
            LCD.DrawBrush = LCD.CreateBrush(0, 0, 0);
        }

        public void ProfileScreenTick()
        {
            if (Keypad.IsKeyPressed(OvenKeypad.Keys.Down))
            {
                // Move selector down
                if (ProfileSelection < OvenController.ProfileController.Profiles.Length - 1)
                {
                    if (ProfileSelection - ProfileScroll == 2)
                    {
                        ProfileScroll = System.Math.Min(ProfileScroll + 3, OvenController.ProfileController.Profiles.Length - 3);
                        RedrawProfileScreen();
                    }
                    ProfileSelection = System.Math.Min(ProfileSelection + 1, OvenController.ProfileController.Profiles.Length - 1);
                    DrawProfileBoxes();
                }
            }
            else if (Keypad.IsKeyPressed(OvenKeypad.Keys.Up))
            {
                // Move selector up
                if (ProfileSelection != 0)
                {
                    if (ProfileSelection - ProfileScroll == 0)
                    {
                        ProfileScroll = System.Math.Max(ProfileScroll - 3, 0);
                        RedrawProfileScreen();
                    }
                    ProfileSelection = System.Math.Max(ProfileSelection - 1, 0);
                    DrawProfileBoxes();
                }
            }
            else if (Keypad.IsKeyPressed(OvenKeypad.Keys.Presets))
            {
                // Assign current profile to a preset
                OvenController.ProfileController.SetProfilePreset(OvenController.ProfileController.Profiles[ProfileSelection], PresetToSlot((int)(Keypad.KeysPressed & OvenKeypad.Keys.Presets)));
            }
            else if (Keypad.IsKeyPressed(OvenKeypad.Keys.Stop))
            {
                LoadMainMenu();
                return;
            }
            else if (Keypad.IsKeyPressed(OvenKeypad.Keys.Start))
            {
                // Select current preset for bake
                OvenController.ProfileController.LoadProfile("");
                // LoadBakeScreen();
            }
        }

        private int PresetToSlot(int Preset)
        {
            int T = (int)(System.Math.Log((double)Preset) / System.Math.Log(2.0));
            return T;
        }

        #endregion

        #region Bake Screen

        #endregion


        private void DrawBoxes(int MenuChoice)
        {
            if (MenuChoice == 0)
            {
                LCD.DrawBrush = LCD.CreateBrush(255, 0, 0);
            }
            else
            {
                LCD.DrawBrush = LCD.CreateBrush(0, 0, 0);
            }

            LCD.DrawBox(39, 75, 242, 50);

            if (MenuChoice == 1)
            {
                LCD.DrawBrush = LCD.CreateBrush(255, 0, 0);
            }
            else
            {
                LCD.DrawBrush = LCD.CreateBrush(0, 0, 0);
            }

            LCD.DrawBox(39, 128, 242, 50);
        }

        public UserInterface()
        {
            CurrentScreen = Screens.Splash;
            OvenController.LCD.LoadImage("Splash");
        }
    }
}

