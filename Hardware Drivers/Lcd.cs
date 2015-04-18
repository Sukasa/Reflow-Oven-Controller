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
        private OutputPort _DataCommand;
        private PWM _Backlight;

        public Lcd(Cpu.Pin ChipSelectPin, Cpu.Pin DataCommandPin, Cpu.PWMChannel BacklightPin)
        {
            _Device = SPIBus.Instance().CreateBusDevice(Pins.GPIO_NONE, false, 10000);
            _ChipSelect = new OutputPort(ChipSelectPin, true);
            _DataCommand = new OutputPort(DataCommandPin, true);
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

        public ushort CreateBrush(byte Red, byte Blue, byte Green)
        {

            return 0;
        }

        public void DrawLine(int X1, int Y1, int X2, int Y2, ushort Brush)
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
            using (FileStream Stream = new FileStream("\\SD\\Oven\\LCDInit.bin", FileMode.Open))
            {
                int Length = Stream.Read(_Buffer, 0, 32768);
                SPIBus Bus = SPIBus.Instance();
                Bus.SelectDevice(_Device);

                // CHANGE: Reset CS between all bytes
                // CHANGE: Set D/C (A2) *low* for commands, *high* for data

                for (int Ptr = 0; Ptr < Length; )
                {
                    // Get number of bytes in data portion of packet
                    int DataLen = _Buffer[Ptr];

                    // 0xFF means sleep, 0 params
                    if (DataLen == 255)
                    {
                        DataLen = 0;
                        Thread.Sleep(120);
                    }
                    Ptr++;

                    // Open packet
                    _ChipSelect.Write(false); // Active low
                    _DataCommand.Write(false);

                    // Write command byte of packet
                    Bus.Write(_Buffer, Ptr, 1);
                    Ptr++;

                    // Prep for data writes
                    _ChipSelect.Write(true); // Active low
                    _DataCommand.Write(true);

                    // Write data bytes of packet
                    for (int DPtr = Ptr; DataLen > 0; DPtr++, Ptr++, DataLen--)
                    {
                        _ChipSelect.Write(false); // Active low
                        Bus.Write(_Buffer, DPtr, 1);
                        _ChipSelect.Write(true); // Active low
                    }
                }

            }

        }

        public void DrawText(string Text)
        {

        }
    }
}
