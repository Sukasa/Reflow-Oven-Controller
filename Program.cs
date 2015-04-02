using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Microsoft.SPOT;
using Microsoft.SPOT.Hardware;
using SecretLabs.NETMF.Hardware;
using SecretLabs.NETMF.Hardware.Netduino;
using Rinsen.WebServer;
using Rinsen.WebServer.FileAndDirectoryServer;

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

        // Detailed sensor/interface access
        public static OvenKeypad Keypad { get; private set; }
        public static TemperatureSensor Sensor1 { get; private set; }
        public static TemperatureSensor Sensor2 { get; private set; }
        public static CPUMonitor CPULoad {get; private set;}

        // Internal stuff
        private static InputPort _DoorSwitch;
        private static TemperatureSensor _Sensor1;
        private static TemperatureSensor _Sensor2;
        private static OvenKeypad _Keypad;
        private static ZeroCrossingSSR _Element1;
        private static ZeroCrossingSSR _Element2;
        
        private static DeltaPID _Element1PID;
        private static DeltaPID _Element2PID;
        private static OutputPort _ScanLED;

        //Web GUI stuff here
        private static WebServer WebServer;

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
            
            // TODO Heating element control


            // TODO Process presets

            
            // TODO Process Control
            LowerElementPower = _Element1PID.Value;
            //UpperElementPower = _Element2PID.Value;


            _Element1.PowerLevel = LowerElementPower / 100f;
            _Element2.PowerLevel = UpperElementPower / 100f;

            // TODO Web GUI
        }

        public void Init()
        {
            // Initialize I/O drivers and ports
            _DoorSwitch = new InputPort(Pins.GPIO_PIN_A3, true, Port.ResistorMode.PullUp);



            _Keypad = new OvenKeypad(Pins.GPIO_PIN_D0, Pins.GPIO_PIN_D1, Pins.GPIO_PIN_D2, Pins.GPIO_PIN_D3,
                                     Pins.GPIO_PIN_D4, Pins.GPIO_PIN_D5, Pins.GPIO_PIN_D6,
                                     Pins.GPIO_PIN_D7, Pins.GPIO_PIN_D8);
            


            _Sensor1 = new TemperatureSensor(Pins.GPIO_PIN_A0);
            _Sensor2 = new TemperatureSensor(Pins.GPIO_PIN_A1);
            // _Lcd = New LCD(Pins.GPIO_PIN_A2);

            Keypad = _Keypad;
            Sensor1 = _Sensor1;
            Sensor2 = _Sensor2;

            _Element1 = new ZeroCrossingSSR(Pins.GPIO_PIN_A4);
            _Element2 = new ZeroCrossingSSR(Pins.GPIO_PIN_A5);

            CPULoad = new CPUMonitor();

            _Element1PID = new DeltaPID(() => OvenTemperature);
            //_Element2PID = new DeltaPID(() => OvenTemperature);

            _Element1PID.ProportionalBand = 5;
            _Element1PID.IntegralRate = 50;
            _Element1PID.IntegralResetBand = 3;
            _Element1PID.TargetHz = 10;
            _Element1PID.ReverseActing = true;

            //_Element2PID.ProportionalBand = 3;
            //_Element2PID.IntegralRate = 70;
            //_Element2PID.IntegralResetBand = 2;
            //_Element2PID.TargetHz = 10;

            Thread.CurrentThread.Priority = ThreadPriority.AboveNormal; // Kick the main control thread to high priority, as we do all hardware I/O and "heavy lifting" on this thread

            WebServer = new WebServer();
            WebServer.SetFileAndDirectoryService(new FileAndDirectoryService());
            WebServer.StartServer(80);

            _ScanLED = new OutputPort(Pins.ONBOARD_LED, false);
        }

        public static void Main()
        {
            // write your code here
            OvenController Controller = new OvenController();
            Controller.Init();


            Debug.Print("Started");

            while (true)
            {
                Controller.Scan();
                Thread.Sleep(30);
            }
        }

    }
}
