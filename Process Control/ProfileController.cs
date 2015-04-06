using System;
using System.IO;
using Microsoft.SPOT;

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
                return new TimeSpan(0, 0, 0);
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
        }

        public bool Start()
        {
            // Verify that conditions are ready to start, that the door is closed, etc

            _StartTime = DateTime.Now;
            OvenController.ElementsEnabled = true;
            CurrentState = ProcessState.Running;
            return true;
        }
        
        public void Tick()
        {
            if (CurrentState != ProcessState.Running)
                return;

            if (_CurrentDatapoint >= _Datapoints.Length)
            {
                // Finish Cycle


                OvenController.ElementsEnabled = false;
                CurrentState = ProcessState.Finished;
                return;
            }

            if (ElapsedTime > _Datapoints[_CurrentDatapoint].TimeOffset)
            {
                _CurrentDatapoint++;
                Tick();
                return;
            }


            float TargetTemperature = _Datapoints[_CurrentDatapoint].Temperature;

            long TimeOffs =  _Datapoints[_CurrentDatapoint].TimeOffset.Ticks;
            long TimeDivisor = (_Datapoints[_CurrentDatapoint].TimeOffset.Ticks - TimeOffs) / 1000;
            long TimeAt = (ElapsedTime.Ticks - TimeOffs) / 1000;

            float Lerp = (float)TimeOffs / (float)TimeDivisor;

            TargetTemperature += Lerp * (float)(_Datapoints[_CurrentDatapoint - 1].Temperature - TargetTemperature);

            OvenController.TemperatureSetpoint = TargetTemperature;
        }

        // Draw profile to display, assuming the base window is already drawn and selected
        public void DrawProfile()
        {

        }
    }
}
