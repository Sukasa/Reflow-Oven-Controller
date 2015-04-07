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
        public static bool DoorAjar { get; private set; }
        public static float OvenTemperature { get; private set; }
        public static float BayTemperature { get; private set; }
        public static float LowerElementPower { get; set; }
        public static float UpperElementPower { get; set; }
        public static uint FreeMem { get; private set; }
        public static float TemperatureSetpoint { get; set; }
        public static bool ElementsEnabled { get; set; }

        // Detailed sensor/interface access
        public static OvenKeypad Keypad { get; private set; }
        public static TemperatureSensor Sensor1 { get; private set; }
        public static TemperatureSensor Sensor2 { get; private set; }
        public static CPUMonitor CPULoad { get; private set; }
        public static Lcd LCD { get; private set; }
        public static ProfileController Profile;

        // Internal stuff
        private static InputPort _DoorSwitch;
        private static TemperatureSensor _Sensor1;
        private static TemperatureSensor _Sensor2;
        private static OvenKeypad _Keypad;
        private static Lcd _LCD;
        private static ZeroCrossingSSR _Element1;
        private static ZeroCrossingSSR _Element2;
        private static ProfileController _Profile;


        private static DeltaPID _Element1PID;
        private static DeltaPID _Element2PID;
        private static OutputPort _ScanLED;

        //Web GUI stuff here
        private static WebServer WebServer;

        private static bool LastDoorState;

        public void Scan()
        {
            FreeMem = Debug.GC(false);

            _ScanLED.Write(!_ScanLED.Read());

            DoorAjar = _DoorSwitch.Read();
            _Sensor1.Read();
            _Sensor2.Read();

            //TODO handle sensor faults

            OvenTemperature = (_Sensor2.HotTemp + _Sensor1.HotTemp) / 2f;
            BayTemperature = (_Sensor2.ColdTemp + _Sensor1.ColdTemp) / 2f;


            // TODO Local user interface
            _Keypad.Scan();


            // TODO Process Control
            _Element1PID.Setpoint = TemperatureSetpoint - 5; // Run the resistive element at a lower setpoint due to thermal inertia
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
                ElementsEnabled = true;
            }

            if (_Keypad.IsKeyPressed(OvenKeypad.Keys.Stop))
            {
                _Keypad.LEDControl = OvenKeypad.LEDState.Off;
                ElementsEnabled = false;
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

            if (_Keypad.IsKeyPressed(OvenKeypad.Keys.Down))
            {
                TemperatureSetpoint -= 5f;
                if (TemperatureSetpoint < 30f)
                    TemperatureSetpoint = 30f;
            }

            LastDoorState = DoorAjar;
        }

        public void Init()
        {
            // Initialize I/O drivers and ports
            _DoorSwitch = new InputPort(Pins.GPIO_PIN_A3, true, Port.ResistorMode.PullUp);



            _Keypad = new OvenKeypad(Pins.GPIO_PIN_D8, Pins.GPIO_PIN_D1, Pins.GPIO_PIN_D2, Pins.GPIO_PIN_D3,
                                     Pins.GPIO_PIN_D4, Pins.GPIO_PIN_D5, Pins.GPIO_PIN_D6,
                                     Pins.GPIO_PIN_D7, PWMChannels.PWM_PIN_D9); // D7



            _Sensor1 = new TemperatureSensor(Pins.GPIO_PIN_A0);
            _Sensor2 = new TemperatureSensor(Pins.GPIO_PIN_A1);
            _LCD = new Lcd(Pins.GPIO_PIN_A2, PWMChannels.PWM_PIN_D10);
            _Profile = new ProfileController();

            Keypad = _Keypad;
            Sensor1 = _Sensor1;
            Sensor2 = _Sensor2;
            LCD = _LCD;
            Profile = _Profile;

            _Element1 = new ZeroCrossingSSR(Pins.GPIO_PIN_A4); //A4
            _Element2 = new ZeroCrossingSSR(Pins.GPIO_PIN_A5);

            CPULoad = new CPUMonitor();

            _Element1PID = new DeltaPID(() => OvenTemperature);
            _Element2PID = new DeltaPID(() => OvenTemperature);

            // Resistive Element (Bottom)
            _Element1PID.ProportionalBand = 40;
            _Element1PID.IntegralRate = 6;
            _Element1PID.IntegralResetBand = 18;
            _Element1PID.TargetHz = 10;
            _Element1PID.ReverseActing = true;
            _Element1PID.DerivativeTime = 19f;
            _Element1PID.DerivativeGain = 7f;
            _Element1PID.DerivativeBand = 95f;
            _Element1PID.ProportionalGain = 60f;

            // Quartz Element (Top)
            _Element2PID.ProportionalBand = 11;
            _Element2PID.IntegralRate = 8;
            _Element2PID.IntegralResetBand = 15;
            _Element2PID.TargetHz = 10;
            _Element2PID.ReverseActing = true;
            _Element2PID.DerivativeTime = 14f;
            _Element2PID.DerivativeGain = 8f;
            _Element2PID.DerivativeBand = 80f;
            _Element2PID.ProportionalGain = 60f;

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
