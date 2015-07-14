using Microsoft.SPOT.Hardware;
using System;
using System.Threading;

namespace Reflow_Oven_Controller
{
    public class OvenKeypad
    {
        private TristatePort[] _Rows;
        private InputPort[] _Columns;
        public OutputPort _LED;
        public PWM Buzzer;
        private Thread _LEDThread;
        private DateTime _BeepTime;

        private bool _PlayingTune;
        private int _TunePtr;
        private double[] Frequencies = { 329.63, 349.23, 369.99, 392.00,
                                         415.30, 440.00, 466.16, 493.88, 523.25, 554.37,
                                         587.33, 622.25, 659.25, 698.46, 739.99, 783.99};
        private int TimeBase = 180;

        private byte[] Tune = {                   
         0x25, 0x48, 0x2A, 0x3C, 0x1D, 0x2C, 0x4A, 0x27, 0x33, 0x15, 0x27, 0x48, 0x25, 0x35, 0x14, 0x25, 0x47, 0x24, 0x40, 0x25, 0x48, 0x2A, 0x3C, 0x1D,
         0x2C, 0x4A, 0x27, 0x33, 0x15, 0x27, 0x38, 0x17, 0x25, 0x34, 0x12, 0x23, 0x65, 0x65, 0x6F, 0x3F, 0x1E, 0x2C, 0x4A, 0x27, 0x33, 0x15, 0x27, 0x48,
         0x25, 0x35, 0x14, 0x25, 0x47, 0x24, 0x60, 0x6F, 0x3F, 0x1E, 0x2C, 0x4A, 0x27, 0x33, 0x15, 0x27, 0x38, 0x17, 0x25, 0x34, 0x12, 0x23, 0x65, 0x45, 0xF1
                              };


        [Flags()]
        public enum Keys
        {
            None = 0,
            Any = 65535,
            Presets = 63,

            Stop = 512,
            Start = 256,
            Down = 128,
            Up = 64,
            Warm = 32,
            Temp = 16,
            Time = 8,
            Bake = 4,
            Broil = 2,
            Toast = 1
        }
        public enum LEDState
        {
            // Off and On are symbolic constants
            Off = 0,
            On = 1,

            // FastFlast and SlowFlash are millisecond timings
            FastFlash = 120,
            SlowFlash = 450
        }

        public Keys KeysDown { get; set; }
        public Keys KeysPressed { get; private set; }

        public bool IsKeyPressed(Keys Key)
        {
            return (KeysPressed & Key) != 0;
        }

        public bool IsKeyDown(Keys Key)
        {
            return (KeysDown & Key) == Key;
        }

        public LEDState LEDControl { get; set; }

        public DateTime LastBeepTime
        {
            get
            {
                return _BeepTime;
            }
        }

        public void Beep(BeepLength Length)
        {
            _BeepTime = DateTime.Now;
            if (Length == 0 || _PlayingTune)
                return;

            _BeepTimeLeft = Math.Max(_BeepTimeLeft, (int)Length);
            Buzzer.Start();
        }

        public enum BeepLength : int
        {
            Long = 900,
            Medium = 450,
            Short = 175
        }

        private int _BeepTimeLeft;
        private int _LEDTimeLeft;

        public void StartTune()
        {
            _PlayingTune = true;
            _TunePtr = -1;
            _BeepTimeLeft = 1;

            Buzzer.Stop();
        }

        public void KeypadThread()
        {
            _LEDTimeLeft = 310;
            Thread.CurrentThread.Priority = ThreadPriority.Highest;
            int SleepTime;

            while (true)
            {



                if (_BeepTimeLeft <= 0)
                {
                    SleepTime = _LEDTimeLeft;
                }
                else
                {
                    SleepTime = Math.Min(_LEDTimeLeft, _BeepTimeLeft);
                }

                SleepTime = Math.Min(15, SleepTime);

                Thread.Sleep(SleepTime);

                _LEDTimeLeft -= SleepTime;
                if (_LEDTimeLeft <= 0)
                {
                    _LEDTimeLeft = 310;
                    switch (LEDControl)
                    {
                        case LEDState.On:
                            _LED.Write(true);
                            break;
                        case LEDState.Off:
                            _LED.Write(false);
                            break;
                        default:
                            _LEDTimeLeft = (int)LEDControl;
                            _LED.Write(!_LED.Read());
                            break;
                    }
                }


                if (_BeepTimeLeft > 0)
                {
                    _BeepTimeLeft -= SleepTime;
                    if (_BeepTimeLeft <= 0)
                    {
                        if (_PlayingTune)
                        {
                            _TunePtr++;
                            if (_TunePtr >= Tune.Length)
                                _TunePtr = 0;

                            if ((Tune[_TunePtr] & 0xf) != 1)
                            {
                                Buzzer.Frequency = Frequencies[(Tune[_TunePtr] & 0xf)];
                                Buzzer.Start();
                            }
                            else
                            {
                                Buzzer.Stop();
                            }
                            _BeepTimeLeft = TimeBase * (Tune[_TunePtr] >> 4);
                        }
                        else
                        {
                            _BeepTimeLeft = 0;
                            Buzzer.Stop();
                        }
                    }
                    else if (_BeepTimeLeft <= 30 && _PlayingTune)
                    {
                        Buzzer.Stop();
                    }
                }

            }
        }

        public OvenKeypad(Cpu.Pin RowPin1, Cpu.Pin RowPin2, Cpu.Pin RowPin3, Cpu.Pin RowPin4,
                          Cpu.Pin ColumnPin1, Cpu.Pin ColumnPin2, Cpu.Pin ColumnPin3,
                          Cpu.Pin LEDPin, Cpu.PWMChannel BuzzerPin)
        {
            _Rows = new TristatePort[4];
            _Columns = new InputPort[3];

            _Rows[0] = new TristatePort(RowPin1, false, false, Port.ResistorMode.Disabled);
            _Rows[1] = new TristatePort(RowPin2, false, false, Port.ResistorMode.Disabled);
            _Rows[2] = new TristatePort(RowPin3, false, false, Port.ResistorMode.Disabled);
            _Rows[3] = new TristatePort(RowPin4, false, false, Port.ResistorMode.Disabled);

            for (int Row = 0; Row < 4; Row++)
                if (_Rows[Row].Active)   // This if statement works around a firmware design choice to THROW AN EXCEPTION if you try to set active to the value
                    _Rows[Row].Active = false; // it already is.  What kind of design choice is that?!

            _Columns[0] = new InputPort(ColumnPin1, false, Port.ResistorMode.PullDown);
            _Columns[1] = new InputPort(ColumnPin2, false, Port.ResistorMode.PullDown);
            _Columns[2] = new InputPort(ColumnPin3, false, Port.ResistorMode.PullDown);

            _LED = new OutputPort(LEDPin, true);
            Buzzer = new PWM(BuzzerPin, 2300.0, 0.5, false);

            _LEDThread = new Thread(KeypadThread);
            _LEDThread.Start();
            LEDControl = LEDState.Off;
        }

        public void Scan()
        {
            Keys Buffer = Keys.None;
            int NumKeys = 0;
            int Flag = 1;

            for (int Row = 0; Row < 4; Row++)
            {
                _Rows[Row].Active = true;
                _Rows[Row].Write(true);

                for (int Column = 0; Column < 3; Column++)
                {
                    if (_Columns[Column].Read())
                    {
                        Buffer |= (Keys)Flag;
                    }
                    Flag <<= 1;
                }
                _Rows[Row].Write(false);
                _Rows[Row].Active = false;
            }
            if (NumKeys < 4) // Anything more than 3 is potentially misreading, so don't store
            {   // A misread of 3 buttons as more will show up as 4 or more here.  Thus, reading 3 buttons can only happen if it's not a misread.
                KeysPressed = (Buffer ^ KeysDown) & Buffer;
                KeysDown = Buffer;
                if (KeysPressed != Keys.None)
                {
                    Beep(BeepLength.Short);
                }
            }
        }
    }
}
