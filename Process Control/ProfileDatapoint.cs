using System;
using Microsoft.SPOT;

namespace Reflow_Oven_Controller
{
    struct ProfileDatapoint
    {
        public int TimeOffsetMillis;
        public float Temperature;

        public ProfileDatapoint(int TimeOffset, float Temp)
        {
            TimeOffsetMillis = TimeOffset;
            Temperature = Temp;
        }

        public void ToBytes(byte[] OutputBuffer, int Offs) {
            Array.Copy(BitConverter.GetBytes(TimeOffsetMillis), 0, OutputBuffer, Offs, 4);
            Array.Copy(BitConverter.GetBytes(Temperature), 0, OutputBuffer, Offs + 4, 4);
        }

        public ProfileDatapoint(byte[] Buffer, int Offs)
        {
            TimeOffsetMillis = BitConverter.ToInt32(Buffer, Offs);
            Temperature = BitConverter.ToSingle(Buffer, Offs + 4);
        }
    }
}
