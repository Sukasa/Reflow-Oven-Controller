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

        private ProfileDatapoint[] _Datapoints;
        private int _CurrentDatapoint;

        public enum ProcessState
        {
            Stopped,
            Running,
            Aborted,
            Finished
        }

        public ProfileController()
        {
            CurrentState = ProcessState.Stopped;
        }

        public void LoadProfile(string ProfileName)
        {
            byte[] Buffer = new byte[512];

            using (FileStream Stream = new FileStream("\\SD\\Oven\\Profile" + ProfileName, FileMode.Open))
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

        public void Abort()
        {
            CurrentState = ProcessState.Aborted;
            OvenController.ElementsEnabled = false;
            OvenController.Keypad.LEDControl = OvenKeypad.LEDState.Off;
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

                // Wait for door to be closed
                if (OvenController.DoorAjar && ((Datapoint.Flags & ProfileDatapoint.DatapointFlags.NoAbortDoorOpen) == ProfileDatapoint.DatapointFlags.NoAbortDoorOpen))
                {
                    ElapsedTime = Datapoint.TimeOffset;
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
                // Replace both of these stand-in numbers with proper values/constants later
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
