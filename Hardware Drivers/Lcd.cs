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
        private OutputPort _ClockPort;

        public Lcd(Cpu.Pin ChipSelectPin, Cpu.Pin DataCommandPin, Cpu.PWMChannel BacklightPin)
        {
            _Device = SPIBus.Instance().CreateBusDevice(Pins.GPIO_NONE, false, 10000);

            _ChipSelect = new OutputPort(ChipSelectPin, true);
            _DataCommand = new OutputPort(DataCommandPin, true);
            _Backlight = new PWM(BacklightPin, 2000, 1.0, true);
            OutputPort.ReservePin(Pins.GPIO_PIN_D13, false);
            _ClockPort = new OutputPort(Pins.GPIO_PIN_D13, false);
            OutputPort.ReservePin(Pins.GPIO_PIN_D13, false);
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

        public ushort DrawBrush { get; set; }
        public ushort Fillbrush { get; set; }

        public void LoadImage(string ImageFilename)
        {
            LoadImage(ImageFilename, 0, 0, 320, 240);
        }

        public void LoadImage(string ImageFilename, int X, int Y, int Width, int Height)
        {
            SPIBus Bus = SPIBus.Instance();
            Bus.SelectDevice(_Device);

            SetWindow(Y, X, Y + Height - 1, X + Width - 1);

            LoadImageFragment(ImageFilename);
        }

        private void LoadImageFragment(string ImageFilename)
        {
            SPIBus Bus = SPIBus.Instance();
            Bus.SelectDevice(_Device);

            WriteCommand(0x2C); // 2C == Memory Write

            int Offset = 0;
            int Read = 0;

            using (FileStream FS = System.IO.File.OpenRead("\\SD\\Oven\\Images\\" + ImageFilename))
            {
                while ((Read = FS.Read(_Buffer, 0, _Buffer.Length)) > 0)
                {
                    WriteData(Read, 0);
                    Offset += Read;
                }
            }

            WriteCommand(0x00); // NOP, Ends write
            _ChipSelect.Write(true);
        }

        public void DrawBox(int X, int Y, int Width, int Height)
        {
            // Draw horizontal lines
            DrawLine(X, Y, X + Width, Y, DrawBrush);
            DrawLine(X, Y + Height, X + Width, Y + Height, DrawBrush);

            // Draw vertical lines
            DrawLine(X, Y, X, Y + Height, DrawBrush);
            DrawLine(X + Width, Y, X + Width, Y + Height, DrawBrush);

            // Draw infill

            // Number of rows we can fill per write
            int NumRows = (_Buffer.Length / ((Width - 2) * 2));

            SetWindow(X + 1, Y + 1, Width - 2, Height - 2);
            int Passes = 0;

            for (; Passes < _Buffer.Length; Passes += 2)
            {
                _Buffer[Passes] = (byte)((Fillbrush & 0xff00) >> 8);
                _Buffer[Passes + 1] = (byte)(Fillbrush & 0xff);
            }

            int PassesNeeded = (Height - 2) / NumRows;
            int RowsLeftOver = (Height - 2) - (PassesNeeded * NumRows);

            for (Passes = 0; Passes < PassesNeeded; Passes++)
            {
                WriteData(NumRows * Width * 2, 0);
            }

            WriteData(RowsLeftOver * Width * 2, 0);
        }

        public ushort CreateBrush(byte Red, byte Green, byte Blue)
        {
            return (ushort)(((int)Red & 0xF8) << 8 | ((int)Green & 0xFC) << 3 | ((int)Blue & 0xF8) >> 3);
        }

        public void DrawLine(int X1, int Y1, int X2, int Y2, ushort Brush)
        {

        }

        public void SetWindow(int X, int Y, int Width, int Height)
        {
            byte[] Data = new byte[] { (byte)(X >> 8), (byte)(X & 0xff), (byte)((X + Width - 1) >> 8), (byte)((X + Width - 1) & 0xff) };

            WriteCommand(0x2A); // Column addr set
            WriteData(Data);

            Data = new byte[] { (byte)(Y >> 8), (byte)(Y & 0xff), (byte)((Y + Height - 1) >> 8), (byte)((Y + Height - 1) & 0xff) };

            WriteCommand(0x2B); // Row addr set
            WriteData(Data);
            WriteCommand(00);
            _ChipSelect.Write(true);
        }

        public byte[] GetFunctionBuffer()
        {
            return _Buffer;
        }

        private void WriteCommand(byte Command)
        {
            _DataCommand.Write(false);
            _ChipSelect.Write(false);

            SPIBus Bus = SPIBus.Instance();
            Bus.Write(new byte[] { Command });

            _ChipSelect.Write(true); // CS is active low
        }

        /// <summary>
        ///     Write <paramref name="Bytes"/> number of bytes to the Lcd, starting at <paramref name="Offset"/> in the function buffer
        /// </summary>
        /// <param name="Bytes"></param>
        /// <param name="Offset"></param>
        public void WriteData(int Bytes, int Offset = 0)
        {
            // Don't write past the end of the array
            if (Bytes + Offset > _Buffer.Length)
                return;

            SPIBus Bus = SPIBus.Instance();

            // Prep for data writes
            _DataCommand.Write(true); // Sending data and not command
            _ChipSelect.Write(false);

            Bus.Write(_Buffer, Offset, Bytes);

            _ChipSelect.Write(true);
        }

        public void WriteData(byte[] Data)
        {
            SPIBus Bus = SPIBus.Instance();

            _DataCommand.Write(true); // Sending data and not command
            _ChipSelect.Write(false);

            Bus.Write(Data, 0, Data.Length);

            _ChipSelect.Write(true);
        }

        public void Read(int Bytes, int Offset = 0)
        {
            if (Bytes + Offset > _Buffer.Length)
            {
                // Error out - reading past end of buffer
            }
        }

        public void Init()
        {

            OvenController.PortExpander.GPIOA.SetOutputs(0x02);

            OvenController.PortExpander.GPIOA.SetBits(0x02);
            Thread.Sleep(5);
            OvenController.PortExpander.GPIOA.ClearBits(0x02);
            Thread.Sleep(20);
            OvenController.PortExpander.GPIOA.SetBits(0x02);
            Thread.Sleep(150);

            SPIBus Bus = SPIBus.Instance();
            Bus.SelectDevice(_Device);

            LoadExecuteInitSequence();
            _Backlight.Start();
            _Backlight.DutyCycle = 0.85f;

            Fillbrush = CreateBrush(255, 255, 0);
            DrawBrush = CreateBrush(0, 0, 0);

        }

        public void SetPixel()
        {

        }

        private void LoadExecuteInitSequence()
        {
            using (FileStream Stream = new FileStream("\\SD\\Oven\\LCDInit.bin", FileMode.Open))
            {
                int Length = Stream.Read(_Buffer, 0, _Buffer.Length);
                SPIBus Bus = SPIBus.Instance();
                Bus.SelectDevice(_Device);

                for (int Ptr = 0; Ptr < Length; )
                {
                    // Get number of bytes in data portion of packet
                    int DataLen = _Buffer[Ptr];

                    // 0xFF means sleep, 0 params
                    if (DataLen == 255)
                    {
                        DataLen = 0;
                        Thread.Sleep(140);
                    }
                    Ptr++;

                    // Open packet
                    _DataCommand.Write(false);
                    _ChipSelect.Write(false); // Active low
                    // Write command byte of packet
                    Bus.Write(_Buffer, Ptr, 1);
                    _ChipSelect.Write(true); // Active low
                    Ptr++;

                    for (; DataLen > 0; Ptr++, DataLen--)
                    {
                        WriteData(1, Ptr);
                    }
                    //WriteData(DataLen, Ptr);
                    //Ptr += DataLen;
                }

            }

        }

        public void DrawText(string Text)
        {

        }
    }
}
