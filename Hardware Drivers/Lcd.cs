using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Microsoft.SPOT;
using Microsoft.SPOT.Hardware;
using SecretLabs.NETMF.Hardware;
using SecretLabs.NETMF.Hardware.Netduino;

namespace Reflow_Oven_Controller.Hardware_Drivers
{
    public class Lcd
    {
        private byte[] _Buffer;
        private SPI.Configuration _Device;
        private OutputPort _ChipSelect;
        private PWM _Backlight;

        public Lcd(Cpu.Pin ChipSelectPin, Cpu.PWMChannel BacklightPin)
        {
            _Device = SPIBus.Instance().CreateBusDevice(Pins.GPIO_NONE, false, 10000);
            _ChipSelect = new OutputPort(ChipSelectPin, true);
            _Backlight = new PWM(BacklightPin, 2000, 1.0, false);

            _Buffer = new byte[32768];
        }

        public float BacklightIntensity
        {
            get
            {
                return (float)_Backlight.DutyCycle;
            }
            set
            {
                _Backlight.DutyCycle = (double)value;
            }
        }


        public void DrawImage(string ImageFilename)
        {

        }

        public void DrawBox()
        {

        }



        public void DrawLine()
        {

        }

        private void SetWindow()
        {

        }

        public void InitLcd()
        {

        }

        public void SetPixel()
        {

        }

        private void LoadExecuteInitSequence()
        {
            using (FileStream Stream = new FileStream("\\SD\\Oven\\LCDInit", FileMode.Open))
            {
                int Length = Stream.Read(_Buffer, 0, 32768);
                SPIBus Bus = SPIBus.Instance();
                Bus.SelectDevice(_Device);

                for (int Ptr = 0; Ptr < Length; )
                {
                    // Get number of bytes in data portion of packet
                    int DataLen = _Buffer[Ptr];
                    Ptr++;

                    // Open packet
                    _ChipSelect.Write(false); // Active low

                    // Write command byte of packet
                    Bus.Write(_Buffer, Ptr, 1);
                    Ptr++;
                    
                    // Write data bytes of packet
                    Bus.Write(_Buffer, Ptr, DataLen);
                    Ptr += DataLen;

                    // Close packet
                    _ChipSelect.Write(true); // Active low
                }

            }

        }

        public void DrawText(string Text)
        {

        }
    }
}
