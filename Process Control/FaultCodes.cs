using System;

namespace Reflow_Oven_Controller.Process_Control
{
    [Flags()] public enum FaultCodes : uint
    {
        // Thermocouple 1 no reading - check detailed fault code
        Therm1Fail = 1,

        // Thermocouple 2 no reading - check detailed fault code
        Therm2Fail = 2,

        // MAX31855 #1 no data
        TSense1Fail = 4,

        // MAX31855 #2 no data
        TSense2Fail = 8,

        // Oven fan not responding to control
        OvenFanFail = 16,

        // Unable to communicate with port expander
        I2CFail = 32,

        // Unable to communicate with Lcd
        LcdFail = 64,

        // No internet connection
        NoNetConnection = 128
    }
}
