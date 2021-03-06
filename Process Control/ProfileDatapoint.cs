using System;
using Microsoft.SPOT;

namespace ReflowOvenController.ProcessControl
{
    class ProfileDatapoint
    {
        //seconds,temperature,flags
        public TimeSpan TimeOffset;
        public float Temperature;
        public DatapointFlags Flags;

        public const int Stride = 9;

        [Flags]
        public enum DatapointFlags
        {
            None = 0,
            LerpFrom = 1,   // Lerp between this value and the next
            NoAbortDoorOpen = 2, // Opening the door will not cause an abort while this datapoint is active (e.g. cooling, "insert item now" stage, etc)
            WaitForTemperature = 4, // Pause the process timer until setpoint is reached
            Beep = 8, // Beep once when this datapoint becomes active
            InsertItemNotification = 16, // IfNoAbortDoorOpen is set, this selects between "insert item now" and "item ready" notifications
            Cooling = 32, // Say 'Cooling' instead of 'Baking'
            NextTemperature = 64 // Use the temperature setpoint from the next data point instead of this one
        }

        public ProfileDatapoint(TimeSpan TimePoint, float Temp)
        {
            TimeOffset = TimePoint;
            Temperature = Temp;
            Flags = DatapointFlags.None;
        }

        public ProfileDatapoint(TimeSpan TimePoint, float Temp, DatapointFlags Flags)
            : this(TimePoint, Temp)
        {
            this.Flags = Flags;
        }

        public ProfileDatapoint(int Seconds, float Temp, DatapointFlags Flags)
        {
            TimeOffset = new TimeSpan(0, 0, Seconds);
            Temperature = Temp;
            this.Flags = Flags;
        }

        public void ToBytes(byte[] OutputBuffer, int Offs) {
            Array.Copy(BitConverter.GetBytes(TimeOffset.Seconds + (TimeOffset.Minutes * 60) + (TimeOffset.Hours * 3600) + (TimeOffset.Days * 86400)), 0, OutputBuffer, Offs, 4);
            Array.Copy(BitConverter.GetBytes(Temperature), 0, OutputBuffer, Offs + 4, 4);
            OutputBuffer[Offs + 8] = (byte)Flags;
        }

        public ProfileDatapoint(byte[] Buffer, int Offs)
        {
            TimeOffset = new TimeSpan(0, 0, BitConverter.ToInt32(Buffer, Offs));
            Temperature = BitConverter.ToSingle(Buffer, Offs + 4);
            Flags = (DatapointFlags)Buffer[Offs + 8];
        }
    }
}
