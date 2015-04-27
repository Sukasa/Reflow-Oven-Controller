using System;
using System.Threading;
using Microsoft.SPOT;
using Microsoft.SPOT.Hardware;
using Microsoft.SPOT.Net.NetworkInformation;
using Reflow_Oven_Controller.Hardware_Drivers;
using Reflow_Oven_Controller.Process_Control;
using Rinsen.WebServer;
using Rinsen.WebServer.FileAndDirectoryServer;
using SecretLabs.NETMF.Hardware.Netduino;

namespace Reflow_Oven_Controller
{
    public class OvenController
    {
        // Basic overview data
        public static float OvenTemperature { get; private set; }
        public static float BayTemperature { get; private set; }
        public static float LowerElementPower { get; set; }
        public static float UpperElementPower { get; set; }
        public static float TemperatureSetpoint { get; set; }
        public static float OvenFanSpeed { get; set; }

        public static uint FreeMem { get; private set; }

        public static bool ElementsEnabled { get; set; }
        public static bool DoorAjar { get; private set; }

        public static FaultCodes Faults { get; set; }

        // Detailed sensor/interface access
        public static OvenKeypad Keypad { get; private set; }
        public static TemperatureSensor Sensor1 { get; private set; }
        public static TemperatureSensor Sensor2 { get; private set; }
        public static CPUMonitor CPULoad { get; private set; }
        public static Lcd LCD { get; private set; }
        public static ProfileController Profile { get; set; }
        public static MCP23017 PortExpander { get; private set; }

        public static OutputPort Test { get; set; }

        // Internal stuff
        private static TemperatureSensor _Sensor1;
        private static TemperatureSensor _Sensor2;
        private static OvenKeypad _Keypad;
        private static Lcd _LCD;
        private static ZeroCrossingSSR _Element1;
        private static ZeroCrossingSSR _Element2;
        private static ProfileController _Profile;
        private static PWM _OvenFanPWM;
        private static UserInterface _Interface;

        private static DeltaPID _Element1PID;
        private static DeltaPID _Element2PID;
        private static OutputPort _ScanLED;
        private static MCP23017 _PortExpander;

        //Web GUI stuff here
        private static WebServer WebServer;

        private static bool LastDoorState;

        public void Scan()
        {
            FreeMem = Debug.GC(false);

            _ScanLED.Write(!_ScanLED.Read());

            DoorAjar = (((int)_PortExpander.GPIOA & 0x01) == 0x01);

            if (OvenFanSpeed >= 0.01f)
            {
                _PortExpander.GPIOA.SetBits(0x80);
            }
            else
            {
                _PortExpander.GPIOA.ClearBits(0x80);
            }
            _OvenFanPWM.DutyCycle = OvenFanSpeed;

            _Sensor1.Read();
            _Sensor2.Read();

            if (_Sensor1.Fault != TemperatureSensor.FaultCode.None)
            {
                Faults |= FaultCodes.Therm1Fail;
            }
            else
            {
                Faults &= ~FaultCodes.Therm1Fail;
            }

            if (_Sensor1.ColdTemp == 0 && _Sensor1.HotTemp == 0)
            {
                Faults |= FaultCodes.TSense1Fail | FaultCodes.Therm1Fail;
            }
            else
            {
                Faults &= ~FaultCodes.TSense1Fail;
            }

            if (_Sensor2.Fault != TemperatureSensor.FaultCode.None)
            {
                Faults |= FaultCodes.Therm2Fail;
            }
            else
            {
                Faults &= ~FaultCodes.Therm2Fail;
            }

            if (_Sensor2.ColdTemp == 0 && _Sensor2.HotTemp == 0)
            {
                Faults |= FaultCodes.TSense2Fail | FaultCodes.Therm2Fail;
            }
            else
            {
                Faults &= ~FaultCodes.TSense2Fail;
            }

            switch (Faults & (FaultCodes.Therm1Fail | FaultCodes.Therm2Fail))
            {
                case 0:
                    // Both sensors work
                    OvenTemperature = (_Sensor2.HotTemp + _Sensor1.HotTemp) / 2f;
                    break;
                case FaultCodes.Therm1Fail:
                    // Use sensor 2 only
                    OvenTemperature = _Sensor2.HotTemp;
                    break;
                case FaultCodes.Therm2Fail:
                    // Use sensor 1 only
                    OvenTemperature = _Sensor1.HotTemp;
                    break;
                default:
                    // Fail hard - no working temperature sensors
                    break;
            }

            switch (Faults & (FaultCodes.TSense1Fail | FaultCodes.TSense2Fail))
            {
                case 0:
                    // Both sensors work
                    BayTemperature = (_Sensor2.ColdTemp + _Sensor1.ColdTemp) / 2f;
                    break;
                case FaultCodes.TSense1Fail:
                    // Use sensor 2 only
                    BayTemperature = _Sensor2.ColdTemp;
                    break;
                case FaultCodes.TSense2Fail:
                    // Use sensor 1 only
                    BayTemperature = _Sensor1.ColdTemp;
                    break;
                default:
                    // Fail hard - no working temperature sensors
                    break;
            }

            // TODO Local user interface
            _Keypad.Scan();


            // TODO Process Control

            _Element1PID.Setpoint = TemperatureSetpoint - 2; // Run the resistive element at a lower setpoint due to thermal inertia
            _Element2PID.Setpoint = TemperatureSetpoint;

            if (ElementsEnabled)
            {
                LowerElementPower = _Element1PID.Value;
                UpperElementPower = _Element2PID.Value;
            }
            else
            {
                UpperElementPower = 0f;
                LowerElementPower = 0f;
            }

            _Element1.PowerLevel = LowerElementPower / 100f;
            _Element2.PowerLevel = UpperElementPower / 100f;

            _Interface.Tick();

            if (_Keypad.IsKeyPressed(OvenKeypad.Keys.Any))
            {
                _Keypad.Beep(OvenKeypad.BeepLength.Short);
            }

            if (_Keypad.IsKeyPressed(OvenKeypad.Keys.Start))
            {
                _Keypad.Beep(OvenKeypad.BeepLength.Medium);
                _Keypad.LEDControl = OvenKeypad.LEDState.On;
                _Element1PID.Bias = 50f;
                _Element2PID.Bias = 50f;
                OvenFanSpeed = 0.0f;
                ElementsEnabled = true;
            }

            if (_Keypad.IsKeyPressed(OvenKeypad.Keys.Stop))
            {
                _Keypad.LEDControl = OvenKeypad.LEDState.Off;
                ElementsEnabled = false;
                OvenFanSpeed = 0.0f;
            }

            if (DoorAjar && !LastDoorState && _Keypad.LEDControl != OvenKeypad.LEDState.Off)
            {
                _Keypad.Beep(OvenKeypad.BeepLength.Long);
                _Keypad.LEDControl = OvenKeypad.LEDState.Off;
                ElementsEnabled = false;
            }

            if (_Keypad.IsKeyPressed(OvenKeypad.Keys.Up))
            {
                TemperatureSetpoint += 5f;
                if (TemperatureSetpoint > 230f)
                    TemperatureSetpoint = 230f;
            }

            if (_Keypad.IsKeyPressed(OvenKeypad.Keys.Bake))
            {
                OvenFanSpeed = 0.0f;
            }

            if (_Keypad.IsKeyPressed(OvenKeypad.Keys.Broil))
            {
                OvenFanSpeed = 0.25f;
            }

            if (_Keypad.IsKeyPressed(OvenKeypad.Keys.Toast))
            {
                OvenFanSpeed = 0.5f;
            }

            if (_Keypad.IsKeyPressed(OvenKeypad.Keys.Warm))
            {
                OvenFanSpeed = 0.75f;
            }

            if (_Keypad.IsKeyPressed(OvenKeypad.Keys.Temp))
            {
                OvenFanSpeed = 1.0f;
            }

            if (_Keypad.IsKeyPressed(OvenKeypad.Keys.Down))
            {
                TemperatureSetpoint -= 5f;
                if (TemperatureSetpoint < 0f)
                    TemperatureSetpoint = 0f;
            }

            LastDoorState = DoorAjar;
        }

        public void Init()
        {
            // Initialize I/O drivers and ports
            _Keypad = new OvenKeypad(Pins.GPIO_PIN_D8, Pins.GPIO_PIN_D1, Pins.GPIO_PIN_D2, Pins.GPIO_PIN_D3,
                                     Pins.GPIO_PIN_D4, Pins.GPIO_PIN_D0, Pins.GPIO_PIN_D6,
                                     Pins.GPIO_PIN_D7, PWMChannels.PWM_PIN_D9);

            _Sensor1 = new TemperatureSensor(Pins.GPIO_PIN_A0);
            _Sensor2 = new TemperatureSensor(Pins.GPIO_PIN_A1);

            _Profile = new ProfileController();

            OutputPort.ReservePin(Pins.GPIO_PIN_D13, false);
            Test = new OutputPort(Pins.GPIO_PIN_D13, false);
            OutputPort.ReservePin(Pins.GPIO_PIN_D13, false);

            Keypad = _Keypad;
            Sensor1 = _Sensor1;
            Sensor2 = _Sensor2;
            Profile = _Profile;
            
            _PortExpander = new MCP23017();
            _PortExpander.GPIOA.EnablePullups(0x7F);
            _PortExpander.GPIOA.SetOutputs(0x80);

            PortExpander = _PortExpander;

            _LCD = new Lcd(Pins.GPIO_PIN_A2, Pins.GPIO_PIN_A3, PWMChannels.PWM_PIN_D10);
            LCD = _LCD;
            _LCD.Init();
            _Interface = new UserInterface();
            
            _OvenFanPWM = new PWM(PWMChannels.PWM_PIN_D5, 20000, 0.0, false);
            _OvenFanPWM.Start();

            _Element1 = new ZeroCrossingSSR(Pins.GPIO_PIN_A4);
            _Element2 = new ZeroCrossingSSR(Pins.GPIO_PIN_A5);

            CPULoad = new CPUMonitor();

            _Element1PID = new DeltaPID(() => OvenTemperature);
            _Element2PID = new DeltaPID(() => OvenTemperature);

            // Resistive Element (Bottom)
            _Element1PID.ProportionalBand = 15;
            _Element1PID.IntegralRate = 7;
            _Element1PID.IntegralResetBand = 15;
            _Element1PID.TargetHz = 10;
            _Element1PID.ReverseActing = true;
            _Element1PID.DerivativeTime = 19f;
            _Element1PID.DerivativeGain = 6f;
            _Element1PID.DerivativeBand = 95f;
            _Element1PID.ProportionalGain = 60f;

            // Quartz Element (Top)
            _Element2PID.ProportionalBand = 10;
            _Element2PID.IntegralRate = 8;
            _Element2PID.IntegralResetBand = 11;
            _Element2PID.TargetHz = 10;
            _Element2PID.ReverseActing = true;
            _Element2PID.DerivativeTime = 14f;
            _Element2PID.DerivativeGain = 6f;
            _Element2PID.DerivativeBand = 80f;
            _Element2PID.ProportionalGain = 55f;

            Thread.CurrentThread.Priority = ThreadPriority.AboveNormal; // Kick the main control thread to high priority, as we do all hardware I/O and "heavy lifting" on this thread

            if (InternetConnectionAvailable())
            {
                WebServer = new WebServer();
                WebServer.SetFileAndDirectoryService(new FileAndDirectoryService());
                WebServer.StartServer(80);

                try
                {
                    Ntp.UpdateTimeFromNtpServer("pool.ntp.org", -8);
                }
                catch
                {
                    // Do nothing if no time server available
                    Debug.Print("Unable to get NTP time");
                }
            }
            else
            {
                Faults |= FaultCodes.NoNetConnection;
            }


            _ScanLED = new OutputPort(Pins.ONBOARD_LED, false);
        }

        public static void Main()
        {
            OvenController Controller = new OvenController();
            Controller.Init();

            Debug.Print("Started");

            while (true)
            {
                Controller.Scan();
                Thread.Sleep(30);
            }
        }

        public static bool InternetConnectionAvailable()
        {
            foreach (NetworkInterface Interface in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (Interface.GatewayAddress != "0.0.0.0")
                {
                    return true;
                }
            }
            return false;
        }

    }
}
