using Microsoft.SPOT;
using Microsoft.SPOT.Hardware;
using Microsoft.SPOT.Net.NetworkInformation;
using ReflowOvenController.HardwareDrivers;
using ReflowOvenController.ProcessControl;
using Rinsen.WebServer;
using Rinsen.WebServer.FileAndDirectoryServer;
using SDBrowser;
using SecretLabs.NETMF.Hardware.Netduino;
using System;
using System.IO;
using System.Threading;

namespace ReflowOvenController
{
    public class OvenController
    {
        public const float MaxBayTemperature = 50f;

        // Basic overview data
        public static float OvenTemperature { get; private set; }
        public static float BayTemperature { get; private set; }
        public static float LowerElementPower { get; set; }
        public static float UpperElementPower { get; set; }
        public static float TemperatureSetpoint { get; set; }
        public static float OvenFanSpeed { get; set; }
        public static float LcdOnBrightness { get; set; }
        public static float LcdDimBrightness { get; set; }
        public static uint FreeMem { get; private set; }
        public static bool ElementsEnabled { get; set; }
        public static bool DoorAjar { get; private set; }
        public static FaultCodes Faults { get; set; }
        public static bool LastDoorState;

        // Detailed sensor/interface access
        public static OvenKeypad Keypad { get; private set; }
        public static TemperatureSensor Sensor1 { get; private set; }
        public static TemperatureSensor Sensor2 { get; private set; }
        public static CPUMonitor CPULoad { get; private set; }
        public static Lcd LCD { get; private set; }
        public static ProfileController ProfileController { get; set; }
        public static MCP23017 PortExpander { get; private set; }
        public static DeltaPID Element1PID;
        public static DeltaPID Element2PID;

        // Internal stuff
        private static TemperatureSensor _Sensor1;
        private static TemperatureSensor _Sensor2;
        private static OvenKeypad _Keypad;
        private static Lcd _LCD;
        private static ZeroCrossingSSR _Element1;
        private static ZeroCrossingSSR _Element2;
        private static PWM _OvenFanPWM;
        private static UserInterface _Interface;
        private static OutputPort _ScanLED;
        private static MCP23017 _PortExpander;
        private static InputPort _VUSB;
        private static int _ThermoFailCounter;
        private static int _BayTempFailCounter;

        //Web GUI stuff here
        private static WebServer WebServer;

        // Debugging / File Management
        internal static EmbeddedFileHost BrowserHost;

        public void Scan()
        {
            // Update free-memory numbers and flip the scan LED status
            FreeMem = Debug.GC(false);
            _ScanLED.Write(!_ScanLED.Read());

            // Read door sensor
            DoorAjar = (((int)_PortExpander.GPIOA & 0x01) == 0x01);

            // Read thermocouple sensors
            _Sensor1.Read();
            _Sensor2.Read();

            // Handle sensor faults
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

            if ((Faults & (FaultCodes.Therm1Fail | FaultCodes.Therm2Fail | FaultCodes.TSense1Fail | FaultCodes.TSense2Fail)) == 0)
                _ThermoFailCounter = 0;

            // Based on fault codes, read oven temperature
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
                    _ThermoFailCounter = _ThermoFailCounter + 1;
                    if (_ThermoFailCounter > 5 && ProfileController.CurrentState == ProcessControl.ProfileController.ProcessState.Running)
                    {
                        ProfileController.Abort("Dual Thermocouple Failure");
                    }
                    break;
            }

            // Based on fault codes, read electronics bay temperature
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
                    _ThermoFailCounter = _ThermoFailCounter + 1;
                    if (_ThermoFailCounter > 5 && ProfileController.CurrentState == ProcessControl.ProfileController.ProcessState.Running)
                    {
                        ProfileController.Abort("SPI Bus Failure");
                    }
                    break;
            }

            _ThermoFailCounter = System.Math.Min(_ThermoFailCounter, 10);
            _Keypad.Scan();
            _Interface.Tick();
            ProfileController.Tick();

            // If the electronics bay overheats, perform emergency shutdown of oven
            if (BayTemperature > MaxBayTemperature)
            {
                _BayTempFailCounter = System.Math.Min(_BayTempFailCounter + 1, 10);
                if (_BayTempFailCounter > 3)
                {
                    if (ProfileController.CurrentState != ProcessControl.ProfileController.ProcessState.Aborted)
                    {
                        ProfileController.Abort("Aborted - System overheat");
                        ProfileController.CurrentState = ProcessControl.ProfileController.ProcessState.Aborted;
                        Keypad.LEDControl = OvenKeypad.LEDState.FastFlash;
                    }
                    OvenFanSpeed = 1.0f;
                    ElementsEnabled = false;
                }
            }
            else
            {
                _BayTempFailCounter = 0;
            }

            // Control oven fan power based on speed requested
            if (OvenFanSpeed >= 0.01f)
            {
                _PortExpander.GPIOA.SetBits(0x80);
            }
            else
            {
                _PortExpander.GPIOA.ClearBits(0x80);
            }
            _OvenFanPWM.DutyCycle = OvenFanSpeed;

            Element1PID.Setpoint = TemperatureSetpoint;
            Element2PID.Setpoint = TemperatureSetpoint;

            if (ElementsEnabled)
            {
                LowerElementPower = Element1PID.Value;
                UpperElementPower = Element2PID.Value;
            }
            else
            {
                UpperElementPower = 0f;
                LowerElementPower = 0f;
            }

            _Element1.PowerLevel = LowerElementPower / 100f;
            _Element2.PowerLevel = UpperElementPower / 100f;

            LastDoorState = DoorAjar;
        }

        public void Init()
        {
            // Initialize the VUSB detection pin (determines if USB is connected or not) - somewhat of a 'hidden' pin, hence the 0x09
            _VUSB = new InputPort((Cpu.Pin)0x09, false, Port.ResistorMode.PullDown);

            // Initialize I/O drivers and ports
            _Keypad = new OvenKeypad(Pins.GPIO_PIN_D8, Pins.GPIO_PIN_D1, Pins.GPIO_PIN_D2, Pins.GPIO_PIN_D3,
                                     Pins.GPIO_PIN_D4, Pins.GPIO_PIN_D0, Pins.GPIO_PIN_D6,
                                     Pins.GPIO_PIN_D7, PWMChannels.PWM_PIN_D9);

            _Sensor1 = new TemperatureSensor(Pins.GPIO_PIN_A0);
            _Sensor2 = new TemperatureSensor(Pins.GPIO_PIN_A1);

            Keypad = _Keypad;
            Sensor1 = _Sensor1;
            Sensor2 = _Sensor2;

            ProfileController = new ProfileController();

            _PortExpander = new MCP23017();
            _PortExpander.GPIOA.EnablePullups(0x7F);
            _PortExpander.GPIOA.SetOutputs(0x80);

            PortExpander = _PortExpander;

            _LCD = new Lcd(Pins.GPIO_PIN_A2, Pins.GPIO_PIN_A3, PWMChannels.PWM_PIN_D10);
            LCD = _LCD;
            _LCD.Init();

            _OvenFanPWM = new PWM(PWMChannels.PWM_PIN_D5, 20000, 0.0, false);
            _OvenFanPWM.Start();

            _Element1 = new ZeroCrossingSSR(Pins.GPIO_PIN_A4);
            _Element2 = new ZeroCrossingSSR(Pins.GPIO_PIN_A5);

            CPULoad = new CPUMonitor();

            DeltaPID.AllocatePIDs(2);
            DeltaPID.TargetHz = 10;

            Element1PID = DeltaPID.GetPID(0);
            Element2PID = DeltaPID.GetPID(1);

            GetCurrent TempGetter = () => OvenTemperature;
            Element1PID.SetGetter(TempGetter);
            Element2PID.SetGetter(TempGetter);

            // Resistive Element (Bottom)
            Element1PID.ProportionalBand = 10;
            Element1PID.IntegralRate = 9;
            Element1PID.IntegralResetBand = 15;
            Element1PID.ReverseActing = true;
            Element1PID.DerivativeTime = 19f;
            Element1PID.DerivativeGain = 2f;
            Element1PID.DerivativeBand = 65f;
            Element1PID.ProportionalGain = 60f;

            // Quartz Element (Top)
            Element2PID.ProportionalBand = 5;
            Element2PID.IntegralRate = 11;
            Element2PID.IntegralResetBand = 11;
            Element2PID.ReverseActing = true;
            Element2PID.DerivativeTime = 10f;
            Element2PID.DerivativeGain = 3f;
            Element2PID.DerivativeBand = 60f;
            Element2PID.ProportionalGain = 60f;

            DeltaPID.StartAll();

            Thread.CurrentThread.Priority = ThreadPriority.AboveNormal; // Kick the main control thread to high priority, as we do all hardware I/O and "heavy lifting" on this thread

            Keypad.Beep(OvenKeypad.BeepLength.Short);

            if (InternetConnectionAvailable())
            {
                try
                {
                    Ntp.UpdateTimeFromNtpServer("pool.ntp.org", -8);
                }
                catch
                {
                    // Do nothing if no time server available
                    Debug.Print("Unable to get NTP time");
                }

                WebServer = new WebServer();
                WebServer.SetFileAndDirectoryService(new FileAndDirectoryService());
                WebServer.StartServer(80);

                BrowserHost = new EmbeddedFileHost();
                BrowserHost.Status = "Normal";
                BrowserHost.FriendlyName = "Reflow Oven";
                BrowserHost.RootDir = "\\SD\\";
                BrowserHost.Init();
            }
            else
            {
                Faults |= FaultCodes.NoNetConnection;
            }

            _Interface = new UserInterface();
            _ScanLED = new OutputPort(Pins.ONBOARD_LED, false);

            LcdOnBrightness = 1.0f;
            LcdDimBrightness = 0.05f;
        }

        public static void Main()
        {
            OvenController Controller = new OvenController();

            try
            {
                Controller.Init();
                while (true)
                {
                    Controller.Scan();
                    Thread.Sleep(30);
                }
            }
            catch (Exception ex)
            {
                // Any hardware that *can* be controlled should be defaulted to failsafe configuration
                try
                {
                    _Element1.PowerLevel = 0;
                    _Element2.PowerLevel = 0;
                }
                catch
                {
                    // Swallow error and keep going
                }

                try
                {
                    Keypad.Buzzer.Stop();
                }
                catch
                {
                    // Swallow error and keep going
                }

                try
                {
                    _PortExpander.GPIOA.SetBits(0x80);
                    _OvenFanPWM.DutyCycle = 1.0f;
                }
                catch
                {
                    // Swallow error and keep going
                }

                try
                {
                    BrowserHost.Status = "System crash";
                }
                catch
                {
                    // Swallow error and keep going
                }

                try
                {
                    using (FileStream FS = File.OpenWrite("SD\\Error.txt"))
                    {
                        using (TextWriter TW = new StreamWriter(FS))
                        {
                            TW.WriteLine("Crash at " + DateTime.Now.ToString());
                            TW.WriteLine(ex.Message);
                            TW.WriteLine("Exception type is " + ex.GetType().ToString());
                            TW.WriteLine(ex.StackTrace);
                            while (ex.InnerException != null)
                            {
                                TW.WriteLine("Inner Exception:");
                                ex = ex.InnerException;
                                TW.WriteLine(ex.Message);
                                TW.WriteLine(ex.StackTrace);
                            }
                            TW.Flush();
                        }
                    }
                }
                catch
                {
                    // Swallow error and keep going
                }

                // If we're running on USB power, alert the debugger to the error, or just give up at this point if no _VUSB object
                if (_VUSB == null || _VUSB.Read())
                    throw;

                if (Keypad != null)
                    Keypad.StartTune();

                while (true)
                {
                    Thread.Sleep(1000);
                }
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

        public static int TotalSeconds(TimeSpan Span)
        {
            return Span.Seconds + (Span.Minutes * 60) + (Span.Hours * 3600) + (Span.Days * 86400);
        }

    }
}
