using System;
using System.IO;

namespace Reflow_Oven_Controller.Process_Control
{
    public class ProfileController
    {
        public TimeSpan ElapsedTime
        {
            get
            {
                if (CurrentState == ProcessState.Running)
                    return DateTime.Now - _StartTime;
                return new TimeSpan(0);
            }
            set
            {
                _StartTime = DateTime.Now - value;
            }
        }
        public ProcessState CurrentState { get; set; }
        private DateTime _StartTime;
        private string[] _ProfilePresets;
        private string[] _Profiles;
        private string AbortReason;

        // Currently-running profile
        private ProfileDatapoint[] _Datapoints;
        private int _CurrentDatapoint;

        private ProfileDatapoint CurrentDatapoint
        {
            get
            {
                return _Datapoints[_CurrentDatapoint];
            }
        }

        public enum ProcessState
        {
            NotStarted,
            Running,
            Aborted,
            Finished
        }

        public string[] Profiles
        {
            get
            {
                return _Profiles;
            }
        }


        public ProfileController()
        {
            CurrentState = ProcessState.NotStarted;



            // Scan for new profiles that need to be parsed from text into binary data
            _Profiles = Directory.GetFiles(@"SD\Oven\NewProfiles");

            foreach (string Filename in _Profiles)
            {
                // Open each file and convert it
                ParseProfile(Filename);
            }



            _Profiles = Directory.GetFiles(@"SD\Oven\Profiles");

            for (int X = _Profiles.Length - 1; X >= 0; X--)
            {
                _Profiles[X] = Path.GetFileName(_Profiles[X]);
            }

            if (File.Exists(@"SD\Oven\Presets"))
            {
                using (FileStream FS = File.OpenRead(@"SD\Oven\Presets"))
                {
                    using (StreamReader SR = new StreamReader(FS))
                    {
                        _ProfilePresets = SR.ReadToEnd().Split('\n');
                        for (int x = 0; x < _ProfilePresets.Length; x++)
                        {
                            _ProfilePresets[x] = _ProfilePresets[x].TrimEnd('\r');
                        }
                    }
                }
            }
            else
            {
                _ProfilePresets = new string[] { "", "", "", "", "", "" };
            }


        }

        public void SetProfilePreset(string Preset, int Slot)
        {
            if (Slot < 0 || Slot >= 6)
                return;

            _ProfilePresets[Slot] = Preset;
            if (File.Exists(@"SD\Oven\Presets"))
                File.Delete(@"SD\Oven\Presets");

            using (FileStream FS = File.Open(@"SD\Oven\Presets", FileMode.CreateNew))
            {
                using (StreamWriter SR = new StreamWriter(FS))
                {
                    SR.WriteLine(_ProfilePresets[0]);
                    SR.WriteLine(_ProfilePresets[1]);
                    SR.WriteLine(_ProfilePresets[2]);
                    SR.WriteLine(_ProfilePresets[3]);
                    SR.WriteLine(_ProfilePresets[4]);
                    SR.Write(_ProfilePresets[5]);
                }
            }
        }

        public void LoadProfile(string ProfileName)
        {
            byte[] Buffer = new byte[512];

            using (FileStream Stream = new FileStream("\\SD\\Oven\\Profiles\\" + ProfileName, FileMode.Open))
            {
                Stream.Read(Buffer, 0, 512);
            }
            _Datapoints = new ProfileDatapoint[Buffer[0]];
            int Ptr = 1;

            for (int Datapoint = 0; Datapoint < Buffer[0]; Datapoint++)
            {
                _Datapoints[Datapoint] = new ProfileDatapoint(Buffer, Ptr);
                Ptr += ProfileDatapoint.Stride;
            }
        }

        public void Abort(string Reason = "Aborted")
        {
            CurrentState = ProcessState.Aborted;
            OvenController.ElementsEnabled = false;
            OvenController.Keypad.LEDControl = OvenKeypad.LEDState.Off;
            OvenController.Keypad.Beep(OvenKeypad.BeepLength.Long);
            AbortReason = Reason;
        }

        public bool Start()
        {
            // Verify that conditions are ready to start, that the door is closed, etc

            if (_Datapoints == null)
                return false;

            if (OvenController.DoorAjar)
                return false;

            // Clear to start
            _StartTime = DateTime.Now;
            OvenController.ElementsEnabled = true;
            CurrentState = ProcessState.Running;
            OvenController.Keypad.LEDControl = OvenKeypad.LEDState.On;
            return true;
        }

        public void ParseProfile(string Filename)
        {

        }

        public string Status()
        {
            switch (CurrentState)
            {
                case ProcessState.Aborted:
                    return AbortReason;
                case ProcessState.Finished:
                    return "Complete";
                case ProcessState.NotStarted:
                    return "Press Start";
                case ProcessState.Running:
                    if ((CurrentDatapoint.Flags & ProfileDatapoint.DatapointFlags.WaitForTemperature) != 0)
                    {
                        return "Preheat";
                    }
                    else if ((CurrentDatapoint.Flags & ProfileDatapoint.DatapointFlags.InsertItemNotification) != 0)
                    {
                        return "Insert board now";
                    }
                    else if ((CurrentDatapoint.Flags & ProfileDatapoint.DatapointFlags.NoAbortDoorOpen) != 0 && OvenController.DoorAjar)
                    {
                        return "Close door to continue";
                    }
                    else if ((CurrentDatapoint.Flags & ProfileDatapoint.DatapointFlags.Cooling) != 0 && OvenController.DoorAjar)
                    {
                        return "Cooling board";
                    }
                    return "Baking";
            }
            return "Error";
        }

        public void Tick()
        {
            if (CurrentState != ProcessState.Running)
                return;

            if (_CurrentDatapoint >= _Datapoints.Length)
            {
                // Finish Cycle
                OvenController.Keypad.Beep(OvenKeypad.BeepLength.Long);

                OvenController.ElementsEnabled = false;
                CurrentState = ProcessState.Finished;
                OvenController.Keypad.LEDControl = OvenKeypad.LEDState.FastFlash;
                return;
            }

            // Advance datapoints based on time
            if (ElapsedTime > _Datapoints[_CurrentDatapoint].TimeOffset)
            {
                _CurrentDatapoint++;

                // If we should beep at this point, beep.
                if ((_Datapoints[_CurrentDatapoint].Flags & ProfileDatapoint.DatapointFlags.Beep) == ProfileDatapoint.DatapointFlags.Beep)
                {
                    OvenController.Keypad.Beep(OvenKeypad.BeepLength.Short);
                }

                Tick();
                return;
            }

            ProfileDatapoint Datapoint = _Datapoints[_CurrentDatapoint];
            float TargetTemperature = _Datapoints[_CurrentDatapoint].Temperature;

            // Lerp the temperature setpoint between datapoints if the flag calls for it
            if ((Datapoint.Flags & ProfileDatapoint.DatapointFlags.LerpFrom) == ProfileDatapoint.DatapointFlags.LerpFrom)
            {
                long TimeOffs = _Datapoints[_CurrentDatapoint].TimeOffset.Ticks;
                long TimeDivisor = (_Datapoints[_CurrentDatapoint].TimeOffset.Ticks - TimeOffs) / 10000;
                long TimeAt = (ElapsedTime.Ticks - TimeOffs) / 10000;

                float Lerp = (float)TimeOffs / (float)TimeDivisor;

                TargetTemperature += Lerp * (float)(_Datapoints[_CurrentDatapoint - 1].Temperature - TargetTemperature);
            }

            // Wait for temperature to be hit internally - and depending on flags, wait for the user to put something into the oven as well
            if ((Datapoint.Flags & ProfileDatapoint.DatapointFlags.WaitForTemperature) == ProfileDatapoint.DatapointFlags.WaitForTemperature)
            {
                if (OvenController.OvenTemperature < TargetTemperature)
                    ElapsedTime = Datapoint.TimeOffset;

                // Wait for the door to open at least once, and also clear the 'insert item' screen
                if ((Datapoint.Flags & ProfileDatapoint.DatapointFlags.InsertItemNotification) == ProfileDatapoint.DatapointFlags.InsertItemNotification)
                {
                    ElapsedTime = Datapoint.TimeOffset;
                    if (OvenController.DoorAjar)
                    {
                        Datapoint.Flags &= ~ProfileDatapoint.DatapointFlags.InsertItemNotification;
                    }
                }

                // Wait for door to be closed (or fault if we opened the door unexpectedly)
                if (OvenController.DoorAjar)
                {
                    if ((Datapoint.Flags & ProfileDatapoint.DatapointFlags.NoAbortDoorOpen) == ProfileDatapoint.DatapointFlags.NoAbortDoorOpen)
                    {
                        ElapsedTime = Datapoint.TimeOffset;
                    }
                    else
                    {
                        Abort("Aborted - Door opened");
                    }
                }
            }

            OvenController.TemperatureSetpoint = TargetTemperature;
        }

        // Draw profile to display, assuming the base window is already drawn and selected
        public void DrawProfile()
        {
            // Profile is just a series of lines, though whether to draw one angled line or a digital-ish line depends on the flag of each datapoint.

            TimeSpan TotalTime = _Datapoints[_Datapoints.Length - 1].TimeOffset;
            int RedBrush = OvenController.LCD.CreateBrush(255, 0, 0);

            for (int idx = 0; idx < _Datapoints.Length - 1; idx++)
            {
                ProfileDatapoint Datapoint = _Datapoints[idx];
                ProfileDatapoint NextDatapoint = _Datapoints[idx + 1];

                // 300 is inner width of window
                // 20 is X position of first pixel column within window
                // TODO Replace both of these stand-in numbers with proper values/constants later
                int XStart = ((int)((double)Datapoint.TimeOffset.Ticks / TotalTime.Ticks) * 300) + 20;
                int XEnd = ((int)((double)NextDatapoint.TimeOffset.Ticks / TotalTime.Ticks) * 300) + 20;

                // 250 is max temperature
                // 5 is Y value of upper row of window
                // 200 is height of inner window
                // Replace all of these stand-in numbers with proper values/constants later
                int YStart = 200 - ((int)(Datapoint.Temperature / 250f) * 200) + 5;
                int YEnd = 200 - ((int)(NextDatapoint.Temperature / 250f) * 200) + 5;

                if ((Datapoint.Flags & ProfileDatapoint.DatapointFlags.LerpFrom) == ProfileDatapoint.DatapointFlags.LerpFrom)
                {
                    // Interpolated point - draw diagonal line
                    OvenController.LCD.DrawLine(XStart, YStart, XEnd, YEnd, RedBrush);
                }
                else
                {
                    // Step point - draw straight lines (Horiz + Vert)
                    OvenController.LCD.DrawLine(XStart, YStart, XEnd, YStart, RedBrush);
                    OvenController.LCD.DrawLine(XEnd, YStart, XEnd, YEnd, RedBrush);
                }
            }

        }
    }
}
