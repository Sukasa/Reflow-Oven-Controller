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

        public TimeSpan _CachedTSS;

        public TimeSpan TimeSinceStart
        {
            get
            {
                if (CurrentState == ProcessState.Running)
                    return _CachedTSS = (DateTime.Now - _DisplayStartTime);
                
                if (CurrentState == ProcessState.Aborted || CurrentState == ProcessState.Finished)
                    return _CachedTSS;

                return _CachedTSS = new TimeSpan(0);
            }
        }
        public ProcessState CurrentState { get; set; }
        public float TargetTemperature { get; private set; }
        public bool Hours { get; private set; }

        private DateTime _StartTime;
        private DateTime _DisplayStartTime;
        private string[] _ProfilePresets;
        private string[] _Profiles;
        private string AbortReason;
        private TimeSpan _TotalTime;

        // Currently-running profile
        private ProfileDatapoint[] _Datapoints;
        private int _CurrentDatapoint;

        public string LoadedProfile { get; private set; }

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
            LoadedProfile = ProfileName;
            CurrentState = ProcessState.NotStarted;

            byte[] Buffer = new byte[512];

            using (FileStream Stream = new FileStream("\\SD\\Oven\\Profiles\\" + ProfileName, FileMode.Open))
            {
                Stream.Read(Buffer, 0, 512);
            }

            // Set up the datapoint array
            _Datapoints = new ProfileDatapoint[Buffer[0]];
            int Ptr = 1;

            // Load datapoints from buffer
            for (int Datapoint = 0; Datapoint < Buffer[0]; Datapoint++)
            {
                _Datapoints[Datapoint] = new ProfileDatapoint(Buffer, Ptr);
                Ptr += ProfileDatapoint.Stride;
            }

            Hours = _Datapoints[_Datapoints.Length - 1].TimeOffset.Hours > 0;
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

            // Clear to start, so initialize all necessary values
            _StartTime = DateTime.Now;
            _DisplayStartTime = _StartTime;
            _CurrentDatapoint = 0;
            CurrentState = ProcessState.Running;

            // Configure oven
            OvenController.ElementsEnabled = true;
            OvenController.Keypad.LEDControl = OvenKeypad.LEDState.On;
            OvenController.Element1PID.Bias = 50f;
            OvenController.Element2PID.Bias = 50f;

            return true;
        }

        public void ParseProfile(string Filename)
        {
            ProfileDatapoint[] Points;
            int NumDatapoints;

            // Read in text data, convert to correct value types, and create datapoints
            using (FileStream FS = File.OpenRead(Filename))
            {
                using (TextReader TR = new StreamReader(FS))
                {
                    string Count = TR.ReadLine();
                    NumDatapoints = int.Parse(Count);
                    Points = new ProfileDatapoint[NumDatapoints];
                    for (int i = 0; i < NumDatapoints; i++)
                    {
                        string[] Data = TR.ReadLine().Split(',');
                        Points[i] = new ProfileDatapoint(int.Parse(Data[0]), (float)double.Parse(Data[1]), (ProfileDatapoint.DatapointFlags)int.Parse(Data[2]));
                    }
                }
            }

            // Delete originating profile source
            //File.Delete(Filename);

            // Now get new filename, and delete any pre-existing version
            Filename = "\\SD\\Oven\\Profiles\\" + Path.GetFileNameWithoutExtension(Filename);

            if (File.Exists(Filename))
                File.Delete(Filename);

            // Write converted profile to SD card
            using (FileStream FS = File.OpenWrite(Filename))
            {
                byte[] Buffer = new byte[NumDatapoints * ProfileDatapoint.Stride + 1];
                Buffer[0] = (byte)NumDatapoints;
                for (int i = 0; i < NumDatapoints; i++)
                {
                    Points[i].ToBytes(Buffer, i * ProfileDatapoint.Stride + 1);
                }
                FS.Write(Buffer, 0, Buffer.Length);
            }

            _Datapoints = null;
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
                    if ((CurrentDatapoint.Flags & ProfileDatapoint.DatapointFlags.InsertItemNotification) != 0)
                    {
                        return "Insert item now";
                    }
                    else if ((CurrentDatapoint.Flags & ProfileDatapoint.DatapointFlags.NoAbortDoorOpen) != 0)
                    {
                        return "Close door to continue";
                    }
                    else if ((CurrentDatapoint.Flags & ProfileDatapoint.DatapointFlags.WaitForTemperature) != 0)
                    {
                        return "Heating";
                    }
                    else if ((CurrentDatapoint.Flags & ProfileDatapoint.DatapointFlags.Cooling) != 0)
                    {
                        return "Cooling";
                    }
                    return "Baking";
            }
            return "Error";
        }

        public void Tick()
        {
            // Only tick the profile if it's actually running
            if (CurrentState != ProcessState.Running)
            {
                // If we finished, then opening the door should shut off the LED / fan
                if (OvenController.DoorAjar && CurrentState == ProcessState.Finished)
                {
                    OvenController.Keypad.LEDControl = OvenKeypad.LEDState.Off;
                    OvenController.OvenFanSpeed = 0f;
                }
                return;
            }

            // Graph Bay and Oven temperatures over time
            int X = (int)(((double)ElapsedTime.Ticks / _TotalTime.Ticks) * 297) + 11;
            int OvenY = 159 - (int)((OvenController.OvenTemperature / 250f) * 133);
            int BayY = 159 - (int)((OvenController.BayTemperature / 250f) * 133);

            OvenController.LCD.DrawBrush = OvenController.LCD.CreateBrush(0, 128, 255);
            OvenController.LCD.SetPixel(X, BayY);

            OvenController.LCD.DrawBrush = OvenController.LCD.CreateBrush(255, 128, 0);
            OvenController.LCD.SetPixel(X, OvenY);

            // Advance datapoints based on time
            if (ElapsedTime > _Datapoints[_CurrentDatapoint].TimeOffset)
            {
                _CurrentDatapoint++;

                if (_CurrentDatapoint >= _Datapoints.Length)
                {
                    // Finish Cycle
                    OvenController.Keypad.Beep(OvenKeypad.BeepLength.Long);

                    OvenController.ElementsEnabled = false;
                    CurrentState = ProcessState.Finished;
                    OvenController.Keypad.LEDControl = OvenKeypad.LEDState.SlowFlash;
                    return;
                }

                // If we should beep at this point, beep.
                if ((_Datapoints[_CurrentDatapoint].Flags & ProfileDatapoint.DatapointFlags.Beep) == ProfileDatapoint.DatapointFlags.Beep)
                {
                    OvenController.Keypad.Beep(OvenKeypad.BeepLength.Short);
                }

                Tick();
                return;
            }

            // Turn on the circulation fan during cooling segments
            if ((CurrentDatapoint.Flags & ProfileDatapoint.DatapointFlags.Cooling) != 0)
            {
                OvenController.OvenFanSpeed = 1.0f;
            }

            // Set the target temperature displayed to the user
            ProfileDatapoint Datapoint = _Datapoints[_CurrentDatapoint];
            TargetTemperature = _Datapoints[_CurrentDatapoint].Temperature;

            // Lerp the displayed target temperature between datapoints if the flag calls for it
            if (((Datapoint.Flags & ProfileDatapoint.DatapointFlags.LerpFrom) == ProfileDatapoint.DatapointFlags.LerpFrom) && _CurrentDatapoint >= 1)
            {
                long TimeOffs = _Datapoints[_CurrentDatapoint - 1].TimeOffset.Ticks;
                long TimeDivisor = (_Datapoints[_CurrentDatapoint].TimeOffset.Ticks - TimeOffs) / 10000;
                long TimeAt = (ElapsedTime.Ticks - TimeOffs) / 10000;

                float Lerp = (float)TimeAt / (float)TimeDivisor;

                TargetTemperature = _Datapoints[_CurrentDatapoint - 1].Temperature + (Lerp * (_Datapoints[_CurrentDatapoint].Temperature - _Datapoints[_CurrentDatapoint - 1].Temperature));
            }

            // Wait for temperature to be hit internally
            if ((Datapoint.Flags & ProfileDatapoint.DatapointFlags.WaitForTemperature) == ProfileDatapoint.DatapointFlags.WaitForTemperature)
            {
                if (OvenController.OvenTemperature < TargetTemperature)
                {
                    if (_CurrentDatapoint >= 1)
                    {
                        ElapsedTime = _Datapoints[_CurrentDatapoint - 1].TimeOffset;
                    }
                    else
                    {
                        ElapsedTime = new TimeSpan(0, 0, 0);
                    }
                }
                else
                {
                    _Datapoints[_CurrentDatapoint].Flags &= ~ProfileDatapoint.DatapointFlags.WaitForTemperature;
                }
            }

            // Wait for the door to open at least once, and also clear the 'insert item' screen
            if ((Datapoint.Flags & ProfileDatapoint.DatapointFlags.InsertItemNotification) == ProfileDatapoint.DatapointFlags.InsertItemNotification)
            {
                if (_CurrentDatapoint >= 1)
                {
                    ElapsedTime = _Datapoints[_CurrentDatapoint - 1].TimeOffset;
                }
                else
                {
                    ElapsedTime = new TimeSpan(0, 0, 0);
                }
                if (OvenController.DoorAjar)
                {
                    _Datapoints[_CurrentDatapoint].Flags &= ~ProfileDatapoint.DatapointFlags.InsertItemNotification;
                }
            }

            // Wait for door to be closed (or fault if we opened the door unexpectedly)
            if (OvenController.DoorAjar)
            {
                if ((Datapoint.Flags & ProfileDatapoint.DatapointFlags.NoAbortDoorOpen) == ProfileDatapoint.DatapointFlags.NoAbortDoorOpen)
                {
                    if (_CurrentDatapoint >= 1)
                    {
                        ElapsedTime = _Datapoints[_CurrentDatapoint - 1].TimeOffset;
                    }
                    else
                    {
                        ElapsedTime = new TimeSpan(0, 0, 0);
                    }
                }
                else
                {
                    Abort("Aborted - Door opened");
                }
            }

            // If door closed and we had set the no-abort flag, then clear the no-abort flag unless the Insert Item notification is still active (i.e. waiting for the door to open), or we're cooling
            if (!OvenController.DoorAjar &&
                (Datapoint.Flags & ProfileDatapoint.DatapointFlags.NoAbortDoorOpen) == ProfileDatapoint.DatapointFlags.NoAbortDoorOpen &&
                ((Datapoint.Flags & ProfileDatapoint.DatapointFlags.InsertItemNotification) == 0) &&
                ((Datapoint.Flags & ProfileDatapoint.DatapointFlags.Cooling) == 0))
            {
                _Datapoints[_CurrentDatapoint].Flags &= ~ProfileDatapoint.DatapointFlags.NoAbortDoorOpen;
            }

            if ((Datapoint.Flags & ProfileDatapoint.DatapointFlags.NoAbortDoorOpen) == ProfileDatapoint.DatapointFlags.NoAbortDoorOpen)
            {
                if (_CurrentDatapoint >= 1)
                {
                    ElapsedTime = _Datapoints[_CurrentDatapoint - 1].TimeOffset;
                }
                else
                {
                    ElapsedTime = new TimeSpan(0, 0, 0);
                }
            }

            // In order to make the rise nicer, don't lerp the temperature setpoint the PID loops run off
            if ((_Datapoints[_CurrentDatapoint].Flags & ProfileDatapoint.DatapointFlags.NextTemperature) != 0)
            {
                OvenController.TemperatureSetpoint = _Datapoints[_CurrentDatapoint + 1].Temperature;
            }
            else
            {
                OvenController.TemperatureSetpoint = _Datapoints[_CurrentDatapoint].Temperature;
            }
        }

        // Draw profile to display, assuming the base window is already drawn
        public void DrawProfile()
        {
            // Profile is just a series of lines, though whether to draw one angled line or a digital-ish line depends on the flag of each datapoint.

            _TotalTime = _Datapoints[_Datapoints.Length - 1].TimeOffset;
            int RedBrush = OvenController.LCD.CreateBrush(255, 0, 0);

            // Initialize datapoints for left edge of graph
            int XPrev = 11;
            int YPrev = 159 - (int)((_Datapoints[0].Temperature / 250f) * 133);

            for (int idx = 0; idx < _Datapoints.Length; idx++)
            {
                ProfileDatapoint Datapoint = _Datapoints[idx];

                // 297 is inner width of window
                // 11 is X position of first pixel column within window
                int XNext = (int)(((double)Datapoint.TimeOffset.Ticks / _TotalTime.Ticks) * 297) + 11;

                // 250 is max temperature
                // 159 is height of inner window
                int YNext = 159 - (int)((Datapoint.Temperature / 250f) * 133);

                if ((Datapoint.Flags & ProfileDatapoint.DatapointFlags.LerpFrom) == ProfileDatapoint.DatapointFlags.LerpFrom)
                {
                    // Interpolated point - draw diagonal line
                    OvenController.LCD.DrawLine(XPrev, YPrev, XNext, YNext, RedBrush);
                }
                else
                {
                    // Step point - draw straight lines (Horiz + Vert)
                    OvenController.LCD.DrawLine(XPrev, YPrev, XPrev, YNext, RedBrush);
                    OvenController.LCD.DrawLine(XPrev, YNext, XNext, YNext, RedBrush);
                }
                XPrev = XNext;
                YPrev = YNext;
            }

            // Draw "Thermal Limit" line for bay temperature
            YPrev = 159 - (int)((OvenController.MaxBayTemperature / 250f) * 133);
            OvenController.LCD.DrawLine(11, YPrev, 308, YPrev, OvenController.LCD.CreateBrush(32, 64, 128));

        }
    }
}
