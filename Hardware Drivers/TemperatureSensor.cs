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
    /// <summary>
    ///     MAX31855 Thermocouple IC driver
    /// </summary>
    public class TemperatureSensor
    {
        private SPI.Configuration DeviceConfig;

        public float HotTemp {get; private set;}
        public float ColdTemp { get; private set; }
        public FaultCode Fault { get; private set; }
        public bool IsFaulted { get; private set; }
        
        [Flags()] public enum FaultCode
        {
            None = 0,
            ShortCircuitHigh = 4, // These numbers match the MAX31855 datasheet
            ShortCircuitLow = 2,
            OpenCircuit = 1
        }

        public TemperatureSensor(Cpu.Pin ChipSelectPin)
        {
            DeviceConfig = SPIBus.Instance().CreateBusDevice(ChipSelectPin);
        }
        
        public bool Read()
        {
            SPIBus.Instance().SelectDevice(DeviceConfig);
            byte[] Data = SPIBus.Instance().Read(4);
            short Working;

            // Thermocouple temperature data
            Working = (short)((Data[0] << 8) | Data[1]);

            // Fault bit
            if ((Working & 0x1) != 0)
            {
                IsFaulted = true;
            }
            else
            {
                // Temperature - 14 bits, signed, 0.25 degree C resolution
                HotTemp = ((short)(Working & 0xFFFC)) / 16F;
            }

            // Internal temperature data
            Working = (short)((Data[2] << 8) | Data[3]);

            // Bits 0-2 are fault flags
            if (IsFaulted)
            {
                Fault = (FaultCode)(Working & 0x7);
            }
            else
            {
                Fault = FaultCode.None;

                // Temperature - 12 bits, signed, 0.0625 degree C resolution
                ColdTemp = ((short)(Working & 0xFFF0)) / 256F;
            }

            return !IsFaulted;
        }
    }
}
