using Microsoft.SPOT.Hardware;
using SecretLabs.NETMF.Hardware.Netduino;
using System;
using System.IO;
using System.Threading;

namespace Reflow_Oven_Controller.Hardware_Drivers
{
    public class Lcd
    {
        protected byte[] _Buffer;

        protected SPI.Configuration _Device;
        protected OutputPort _ChipSelect;
        protected OutputPort _DataCommand;
        protected PWM _Backlight;
        protected byte[] _FontData;

        public int DrawBrush { get; set; }
        public int Fillbrush { get; set; }
        public int WindowStride;

        protected const byte WriteMem = 0x2C;
        protected const byte NoOp = 0x00;

        public Lcd(Cpu.Pin ChipSelectPin, Cpu.Pin DataCommandPin, Cpu.PWMChannel BacklightPin)
        {
            _Device = SPIBus.Instance().CreateBusDevice(Pins.GPIO_NONE, false, 10600);

            _ChipSelect = new OutputPort(ChipSelectPin, true);
            _DataCommand = new OutputPort(DataCommandPin, true);
            _Backlight = new PWM(BacklightPin, 2000, 1.0f, false);

            _Buffer = new byte[32768];

            _FontData = new byte[1536];
            using (FileStream FS = System.IO.File.OpenRead("\\SD\\Oven\\FontBase.bin"))
            {
                FS.Read(_FontData, 0, _FontData.Length);
            }
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

        public void LoadImage(string ImageFilename)
        {
            LoadImage(ImageFilename, 0, 0, 320, 240);
        }

        public void LoadImage(string ImageFilename, int X, int Y, int Width, int Height, bool SkipWrite = false)
        {
            SPIBus Bus = SPIBus.Instance();
            Bus.SelectDevice(_Device);

            SetWindow(X, Y, Width, Height);

            if (SkipWrite && (Width * Height * 3) > 32768) // If we are skipping the writes and can't fit everything into the buffer...
                return;

            ThreadPriority Prior = Thread.CurrentThread.Priority;
            Thread.CurrentThread.Priority = ThreadPriority.AboveNormal;

            if (!SkipWrite)
            {
                WriteCommand(WriteMem); // 2C == Memory Write
                _ChipSelect.Write(true);

                _DataCommand.Write(true); // Sending data and not command
                _ChipSelect.Write(true);
            }

            int Offset = 0;
            int Read = 0;

            using (FileStream FS = System.IO.File.OpenRead("\\SD\\Oven\\Images\\" + ImageFilename))
            {
                while ((Read = FS.Read(_Buffer, 0, _Buffer.Length)) > 0)
                {
                    if (!SkipWrite)
                    {
                        _ChipSelect.Write(false);
                        Bus.Write(_Buffer, 0, Read);
                        _ChipSelect.Write(true);
                    }

                    Offset += Read;
                }
            }

            if (!SkipWrite)
            {
                _ChipSelect.Write(true);

                WriteCommand(NoOp); // NOP, Ends write
                _ChipSelect.Write(true);
            }
            Thread.CurrentThread.Priority = Prior;
        }

        public void DrawFillBox(int X, int Y, int Width, int Height)
        {
            // Draw outline
            DrawBox(X, Y, Width, Height);

            // Draw infill
            FillBox(X + 3, Y + 3, Width - 6, Height - 6);
        }

        public void DrawBox(int X, int Y, int Width, int Height)
        {
            int BufferSize = Width * Height * 3;

            for (int i = 0; i < Math.Max(Width, Height) * 9; i+= 3)
            {
                _Buffer[i] = (byte)(DrawBrush >> 16);
                _Buffer[i + 1] = (byte)((DrawBrush >> 8) & 0xff);
                _Buffer[i + 2] = (byte)(DrawBrush & 0xff);
            }

            
            // Top border
            SetWindow(X, Y, Width, 3);
            DrawBuffer(Width * 9);

            // Left border
            SetWindow(X, Y, 3, Height);
            DrawBuffer(Height * 9);
            
            // Right border
            SetWindow(X + Width - 3, Y, 3, Height);
            DrawBuffer(Height * 9);
            
            // Bottom border
            SetWindow(X, Y + Height - 3, Width, 3);
            DrawBuffer(Width * 9);
        }

        public void FillBox(int X, int Y, int Width, int Height)
        {
            int NumBytes = Width * Height * 3;
            int UseBufferSize = Math.Min((_Buffer.Length / 3) * 3, NumBytes);

            SetWindow(X, Y, Width, Height);
            int Passes = (NumBytes + _Buffer.Length) / _Buffer.Length;

            for (int i = 0; i < UseBufferSize; i += 3)
            {
                _Buffer[i] = (byte)(Fillbrush >> 16);
                _Buffer[i + 1] = (byte)((Fillbrush >> 8) & 0xff);
                _Buffer[i + 2] = (byte)(Fillbrush & 0xff);
            }

            WriteCommand(WriteMem);
            _ChipSelect.Write(true);

            while (NumBytes > 0)
            {
                int NumDrawn = Math.Min(NumBytes, UseBufferSize);
                WriteData(NumDrawn, 0);
                NumBytes -= NumDrawn;
            }

            WriteCommand(00);
            _ChipSelect.Write(true);

        }

        public int CreateBrush(byte Red, byte Green, byte Blue)
        {

            return (Red << 16) | (Green << 8) | Blue;
        }

        public void DrawLine(int X1, int Y1, int X2, int Y2, int Brush)
        {

            // If necessary, swap points to make it left->right
            if (X1 > X2)
            {
                int T = X1;
                X1 = X2;
                X2 = T;

                T = Y1;
                Y1 = Y2;
                Y2 = T;
            }

            int DeltaX = X2 - X1;
            int DeltaY = Y2 - Y1;
            int MinY = Math.Min(Y1, Y2);
            int BufferSize = (DeltaX + 1) * (Math.Abs(DeltaY) + 1) * 3;

            float Error = 0.0f;
            float DError = DeltaX != 0 ? (float)Math.Abs((float)DeltaY / (float)DeltaX) : Math.Abs(DeltaY);


            SetWindow(X1, Math.Min(Y1, Y2), DeltaX + 1, Math.Abs(DeltaY) + 1);

            // Read in window
            Read(BufferSize, 0);

            int XEnd = Math.Max(X1, X2);
            int Y = Y1;

            SetBufferPixel(0, Y - MinY, Brush);

            for (int X = Math.Min(X1, X2); X <= XEnd; X++)
            {
                Error += DError;
                if (Math.Abs(Error) <= 0.5)
                {
                    SetBufferPixel(X - X1, Y - MinY, Brush);
                }
                while ((Math.Abs(Error) > 0.5))
                {
                    SetBufferPixel(X - X1, Y - MinY, Brush);
                    Y += Math.Sign(DeltaY);
                    Error -= Math.Sign(Error);
                }
            }

            // Write out Data
            DrawBuffer(BufferSize);
        }

        public void SetBufferPixel(int X, int Y, int Color)
        {
            int Ptr = ((X * WindowStride) + Y) * 3;

            _Buffer[Ptr] = (byte)(Color >> 16);
            _Buffer[Ptr + 1] = (byte)((Color >> 8) & 0xff);
            _Buffer[Ptr + 2] = (byte)(Color & 0xff);
        }

        public void SetWindow(int X, int Y, int Width, int Height)
        {
            WindowStride = Height;
            byte[] Data = new byte[] { (byte)(Y >> 8), (byte)(Y & 0xff), (byte)((Y + Height - 1) >> 8), (byte)((Y + Height - 1) & 0xff) };

            WriteCommand(0x2A); // Column addr set
            _ChipSelect.Write(true);
            WriteData(Data);

            Data = new byte[] { (byte)(X >> 8), (byte)(X & 0xff), (byte)((X + Width - 1) >> 8), (byte)((X + Width - 1) & 0xff) };

            WriteCommand(0x2B); // Row addr set
            _ChipSelect.Write(true);
            WriteData(Data);
            WriteCommand(00);
            _ChipSelect.Write(true);
        }

        public void DrawBuffer(int Bytes, int Offset = 0)
        {
            WriteCommand(WriteMem);
            _ChipSelect.Write(true);
            WriteData(Bytes, Offset);
            WriteCommand(00);
            _ChipSelect.Write(true);
        }

        private void WriteCommand(byte Command)
        {
            _DataCommand.Write(false);
            _ChipSelect.Write(false);

            SPIBus Bus = SPIBus.Instance();
            Bus.SelectDevice(_Device);
            Bus.Write(new byte[] { Command });
        }

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
            if (Bytes + Offset > _Buffer.Length - 1)
                return;

            WriteCommand(0x2E); // 0x2E == Read Memory

            _DataCommand.Write(true); // RX'ing data and not command

            // Now read data out from VRAM
            SPIBus Bus = SPIBus.Instance();

            Bus.Write(_Buffer, 0, 1);
            Bus.ReadWrite(_Buffer, _Buffer, 0, Bytes, Offset, Bytes, 0);

            // End data transfer
            WriteCommand(00);
            _ChipSelect.Write(true);
        }

        public void Init()
        {

            OvenController.PortExpander.GPIOA.SetOutputs(0x02);

            // Reset the LCD controller
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

            Fillbrush = CreateBrush(255, 255, 0);
            DrawBrush = CreateBrush(0, 0, 0);

        }

        public void SetPixel(int X, int Y)
        {
            //Select device
            SPIBus Bus = SPIBus.Instance();
            Bus.SelectDevice(_Device);

            // Set window to single pixel + load buffer
            SetWindow(X, Y, 1, 1);
            SetBufferPixel(0, 0, DrawBrush);

            // Write pixel data
            WriteCommand(WriteMem);
            _ChipSelect.Write(true);
            WriteData(3, 0);
            WriteCommand(NoOp);
            _ChipSelect.Write(true);

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
                }

            }

        }

        public int MeasureTextWidth(string Text, int SizeMultiplier)
        {
            int Width = 0;
            for (int i = 0; i < Text.Length; i++)
            {
                char Character = Text[i];
                Width += _FontData[(Character << 1)] * SizeMultiplier;
                Width++;
            }
            return --Width;
        }

        public void DrawText(int X, int Y, int MaxWidth, string Text, int SizeMultiplier, int OffsetX = 0, int OffsetY = 0, bool SkipRead = false, bool SkipWrite = false)
        {
            SizeMultiplier = Math.Min(SizeMultiplier, 4);
            if (SizeMultiplier < 0)
                return;

            MaxWidth = Math.Min(MaxWidth, 320 - X);
            int Height = 11 * SizeMultiplier + OffsetY;
            int BufferSize = MaxWidth * Height * 3;

            // Read in window
            if (!SkipRead)
            {
                SetWindow(X, Y, MaxWidth, Height);
                Read(BufferSize, 0);
            }
            int DrawX = OffsetX;

            for (int i = 0; i < Text.Length; i++)
            {
                char Character = Text[i];
                int CharacterWidth = _FontData[(Character << 1)];
                if (DrawX + CharacterWidth > MaxWidth)
                    break;

                int DataOffset = 512 + (_FontData[(Character << 1) + 1] << 2);
                int Bits = 0;

                for (; CharacterWidth > 0; --CharacterWidth)
                {
                    for (int DrawY = OffsetY; DrawY < Height; DrawY += SizeMultiplier)
                    {
                        // Draw pixel of character to scale
                        if ((_FontData[DataOffset] & (1 << Bits)) != 0)
                            for (int dX = 0; dX < SizeMultiplier; dX++)
                                for (int dY = 0; dY < SizeMultiplier; dY++)
                                    SetBufferPixel(DrawX + dX, DrawY + dY, DrawBrush);

                        if (++Bits == 8)
                        {
                            Bits = 0;
                            ++DataOffset;
                        }
                    }
                    DrawX += SizeMultiplier;
                }
                DrawX++;
            }

            if (!SkipWrite)
            {
                // SetWindow() again in case we never read.
                SetWindow(X, Y, MaxWidth, Height);

                // Write out data
                WriteCommand(WriteMem);
                _ChipSelect.Write(true);
                WriteData(BufferSize, 0);
                WriteCommand(NoOp);
                _ChipSelect.Write(true);
            }
        }
    }
}
