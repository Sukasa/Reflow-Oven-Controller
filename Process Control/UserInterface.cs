using System;
using Microsoft.SPOT;
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
                    MainMenu();
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
            }

            //RunTestHarness();

            if (OvenController.TotalSeconds(DateTime.Now - OvenController.Keypad.LastBeepTime) > 60 && (CurrentScreen == Screens.Home || CurrentScreen == Screens.SettingsAbout))
            {
                OvenController.LCD.LoadImage("Splash");
                CurrentScreen = Screens.Splash;
            }


        }

        #region Main Menu

        private void MainMenu()
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
                    // TODO Load SetProfiles Page
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
                    // TODO Load IP Address Screen
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
        }

        #endregion

        #region IP Address Screen

        #endregion

        #region Profile Screen

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

        private void RunTestHarness()
        {
            if (OvenController.Keypad.IsKeyPressed(OvenKeypad.Keys.Start))
            {
                OvenController.Keypad.Beep(OvenKeypad.BeepLength.Medium);
                OvenController.Keypad.LEDControl = OvenKeypad.LEDState.On;
                OvenController.Element1PID.Bias = 50f;
                OvenController.Element2PID.Bias = 50f;
                OvenController.OvenFanSpeed = 0.0f;
                OvenController.ElementsEnabled = true;
            }

            if (OvenController.Keypad.IsKeyPressed(OvenKeypad.Keys.Stop))
            {
                OvenController.Keypad.LEDControl = OvenKeypad.LEDState.Off;
                OvenController.ElementsEnabled = false;
                OvenController.OvenFanSpeed = 0.0f;
            }

            if (OvenController.DoorAjar && !OvenController.LastDoorState && OvenController.Keypad.LEDControl != OvenKeypad.LEDState.Off)
            {
                OvenController.Keypad.Beep(OvenKeypad.BeepLength.Long);
                OvenController.Keypad.LEDControl = OvenKeypad.LEDState.Off;
                OvenController.ElementsEnabled = false;
            }

            if (OvenController.Keypad.IsKeyPressed(OvenKeypad.Keys.Up))
            {
                OvenController.TemperatureSetpoint += 5f;
                if (OvenController.TemperatureSetpoint > 230f)
                    OvenController.TemperatureSetpoint = 230f;
            }

            if (OvenController.Keypad.IsKeyPressed(OvenKeypad.Keys.Bake))
            {
                OvenController.OvenFanSpeed = 0.0f;
            }

            if (OvenController.Keypad.IsKeyPressed(OvenKeypad.Keys.Broil))
            {
                OvenController.OvenFanSpeed = 0.25f;
            }

            if (OvenController.Keypad.IsKeyPressed(OvenKeypad.Keys.Toast))
            {
                OvenController.OvenFanSpeed = 0.5f;
            }

            if (OvenController.Keypad.IsKeyPressed(OvenKeypad.Keys.Warm))
            {
                OvenController.OvenFanSpeed = 0.75f;
            }

            if (OvenController.Keypad.IsKeyPressed(OvenKeypad.Keys.Temp))
            {
                OvenController.OvenFanSpeed = 1.0f;
            }

            if (OvenController.Keypad.IsKeyPressed(OvenKeypad.Keys.Down))
            {
                OvenController.TemperatureSetpoint -= 5f;
                if (OvenController.TemperatureSetpoint < 0f)
                    OvenController.TemperatureSetpoint = 0f;
            }
        }

        public UserInterface()
        {
            CurrentScreen = Screens.Splash;
            OvenController.LCD.LoadImage("Splash");
            OvenController.LCD.DrawText(10, 10, 300, "The quick brown fox is", 2);
            OvenController.LCD.DrawText(10, 30, 300, "rippin' the old man a new one~", 2);
        }
    }
}
