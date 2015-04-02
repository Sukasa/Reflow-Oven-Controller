using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Microsoft.SPOT;
using Microsoft.SPOT.Hardware;
using SecretLabs.NETMF.Hardware;
using SecretLabs.NETMF.Hardware.Netduino;

namespace Reflow_Oven_Controller
{
    public class OvenKeypad
    {
        private TristatePort[] _Rows;
        private InputPort[] _Columns;
        private OutputPort _LED;
        private OutputPort _Buzzer;
        private Thread _LEDThread;

        [Flags()]
        public enum Keys
        {
            None = 0,
            Bake = 1,
            Broil = 2,
            Toast = 4,
            Warm = 8,
            Temperature = 16,
            Time = 32,
            Up = 64,
            Down = 128,
            Stop = 256,
            Start = 512
        }
        public enum LEDState {
            Off = 0,
            Flashing = 1,
            On = 2
        }

        public Keys KeysPressed { get; private set; }
        public bool IsKeyPressed(Keys Key)
        {
            return (KeysPressed & Key) == Key;
        }

        public LEDState LEDControl { get; set; }
        public bool Buzzer
        {
            get
            {
                return _Buzzer.Read();
            }
            set
            {
                _Buzzer.Write(value);
            }
        }

        public void LEDThread() {
            while (true) {
                Thread.Sleep(310);

                switch (LEDControl)
                {
                    case LEDState.On:
                        _LED.Write(true);
                        break;
                    case LEDState.Flashing:
                        _LED.Write(!_LED.Read());
                        break;
                    default:
                        _LED.Write(false);
                        break;
                }
            }
        }

        public OvenKeypad(Cpu.Pin RowPin1, Cpu.Pin RowPin2, Cpu.Pin RowPin3, Cpu.Pin RowPin4,
                          Cpu.Pin ColumnPin1, Cpu.Pin ColumnPin2, Cpu.Pin ColumnPin3,
                          Cpu.Pin LEDPin, Cpu.Pin BuzzerPin)
        {
            _Rows = new TristatePort[4];
            _Columns = new InputPort[3];

            _Rows[0] = new TristatePort(RowPin1, false, false, Port.ResistorMode.PullDown);
            _Rows[1] = new TristatePort(RowPin2, false, false, Port.ResistorMode.PullDown);
            _Rows[2] = new TristatePort(RowPin3, false, false, Port.ResistorMode.PullDown);
            _Rows[3] = new TristatePort(RowPin4, false, false, Port.ResistorMode.PullDown);

            for (int Row = 0; Row < 4; Row++ )
                if (_Rows[Row].Active)   // This if statement works around a firmware design choice to THROW AN EXCEPTION if you try to set active to the value
                    _Rows[Row].Active = false; // it already is.  What kind of design choice is that?!

            _Columns[0] = new InputPort(ColumnPin1, false, Port.ResistorMode.Disabled);
            _Columns[1] = new InputPort(ColumnPin2, false, Port.ResistorMode.Disabled);
            _Columns[2] = new InputPort(ColumnPin3, false, Port.ResistorMode.Disabled);
            
            _LED = new OutputPort(LEDPin, true);
            _Buzzer = new OutputPort(BuzzerPin, false);

            _LEDThread = new Thread(LEDThread);
            _LEDThread.Start();
            LEDControl = LEDState.Flashing;
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
                        Flag <<= 1;
                    }
                }
                _Rows[Row].Write(false);
                _Rows[Row].Active = false;
            }
            if (NumKeys < 3) // Anything more than 3 is potentially misreading, so don't store
            {   // A misread of 3 buttons as more will show up as 4 or more here.  Thus, reading 3 buttons can only happen if it's not a misread.
                KeysPressed = Buffer;
            }
        }
    }
}
