using System;
using Microsoft.SPOT;

namespace Reflow_Oven_Controller.Process_Control
{
    struct ProfileDatapoint
    {
        public TimeSpan TimeOffset;
        public float Temperature;

        public ProfileDatapoint(TimeSpan TimePoint, float Temp)
        {
            TimeOffset = TimePoint;
            Temperature = Temp;
        }

        public void ToBytes(byte[] OutputBuffer, int Offs) {
            Array.Copy(BitConverter.GetBytes((int)(TimeOffset.Ticks / 10000L)), 0, OutputBuffer, Offs, 4);
            Array.Copy(BitConverter.GetBytes(Temperature), 0, OutputBuffer, Offs + 4, 4);
        }

        public ProfileDatapoint(byte[] Buffer, int Offs)
        {
            TimeOffset = new TimeSpan(((long)BitConverter.ToInt32(Buffer, Offs) * 10000L));
            Temperature = BitConverter.ToSingle(Buffer, Offs + 4);
        }
    }
}
