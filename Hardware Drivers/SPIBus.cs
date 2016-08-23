using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Microsoft.SPOT;
using Microsoft.SPOT.Hardware;
using SecretLabs.NETMF.Hardware;
using SecretLabs.NETMF.Hardware.Netduino;

namespace ReflowOvenController.HardwareDrivers
{
    class SPIBus
    {
        SPI DeviceBus;
        static private SPIBus _Instance;

        // Singleton pattern
        private SPIBus()
        {

        }

        /// <summary>
        ///     Get the global SPI bus instance
        /// </summary>
        /// <returns></returns>
        static public SPIBus Instance()
        {
            return _Instance != null ? _Instance : _Instance = new SPIBus();
        }

        /// <summary>
        ///     Selects a device to be interacted with over SPI.  Create devices using <seealso cref="CreateBusDevice"/>
        /// </summary>
        /// <param name="Device"></param>
        public void SelectDevice(SPI.Configuration Device)
        {
            DeviceBus.Config = Device;
        }

        /// <summary>
        ///     Creates a bus configuration entry for a specific SPI device
        /// </summary>
        /// <param name="CS">
        ///     The pin to use as the chip select output
        /// </param>
        /// <param name="ActiveState">
        ///     Active state for the chip select pin.  Defaults to logic low.
        /// </param>
        /// <param name="Speed">
        ///     Maximum communications speed of the device.  Defaults to 1Mhz 
        /// </param>
        /// <returns></returns>
        public SPI.Configuration CreateBusDevice(Cpu.Pin CS, bool ActiveState = false, uint Speed = 1000)
        {
            SPI.Configuration NewDevice = new SPI.Configuration(CS, ActiveState, 100, 100, false, true, Speed, SPI_Devices.SPI1);

            if (DeviceBus == null)
                DeviceBus = new SPI(NewDevice);

            return NewDevice;
        }

        public byte[] Read(int NumBytes)
        {
            return ReadWrite(new byte[NumBytes]);
        }

        public void Write(byte[] Data)
        {
            DeviceBus.Write(Data);
        }

        public void Write(byte[] Data, int StartOffset, int Count)
        {
            DeviceBus.WriteRead(Data, StartOffset, Count, null, 0, 0, Count + 1);
        }

        public byte[] ReadWrite(byte[] Data)
        {
            byte[] ReadBuf = new byte[Data.Length];
            DeviceBus.WriteRead(Data, ReadBuf);
            return ReadBuf;
        }

        public byte[] ReadWrite(byte[] WriteData, int WriteOffset, int WriteCount, int ReadCount, int ReadDelay)
        {
            byte[] ReadBuf = new byte[ReadCount];
            ReadWrite(WriteData, ReadBuf, WriteOffset, WriteCount, 0, ReadCount, ReadDelay);
            return ReadBuf;
        }

        public void ReadWrite(byte[] WriteData, byte[] ReadData, int WriteOffset, int WriteCount, int ReadOffset, int ReadCount, int ReadDelay)
        {
            DeviceBus.WriteRead(WriteData, WriteOffset, WriteCount, ReadData, ReadOffset, ReadCount, ReadDelay);
        }

    }
}
