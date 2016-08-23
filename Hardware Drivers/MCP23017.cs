using Microsoft.SPOT.Hardware;


namespace ReflowOvenController.HardwareDrivers
{
    /// <summary>
    ///     Driver class for MCP23017 16-bit GPIO Port Expander
    /// </summary>
    /// <remarks>
    ///     The MCP23017 is a 16-bit I²C GPIO Port Expander with two interrupt pins.  It has a configurable address and active-low reset input.
    /// </remarks>
    public class MCP23017
    {

        private I2CBus _Bus;
        private I2CDevice.Configuration Config = new I2CDevice.Configuration(0x20, 50);
        private GPIOBank _GPIOA;
        private GPIOBank _GPIOB;

        /// <summary>
        ///     GPIO Port A on device
        /// </summary>
        public GPIOBank GPIOA
        {
            get { return _GPIOA; }
            set
            {
                if (value._Expander != null)
                {
                    _GPIOA.SetValue(value);
                }
                else
                {
                    _GPIOA.SetValue(value._Set);
                }
            }
        }

        /// <summary>
        ///     GPIO Port B on device
        /// </summary>
        public GPIOBank GPIOB
        {
            get { return _GPIOB; }
            set
            {
                if (value._Expander != null)
                {
                    _GPIOB.SetValue(value);
                }
                else
                {
                    _GPIOB.SetValue(value._Set);
                }
            }
        }

        /// <summary>
        ///     Whether to mirror interrupt pins
        /// </summary>
        /// <remarks>
        ///     If TRUE, both pins are internally ORed together.  If FALSE, each pin corresponds only to its specific GPIO Port's Interrupts.
        /// </remarks>
        public bool MirrorInterrupts
        {
            get
            {
                return (ReadRegister(0x05) & 0x40) == 0x40;
            }
            set
            {
                SetRegisterBitsConditional(0x5, 0x40, value);
            }
        }

        /// <summary>
        ///     What signalling paradigm to use for the interrupt pins
        /// </summary>
        /// <remarks>
        ///     The three options are:
        ///     <list type="bullet">
        ///         <listheader>
        ///             <term>Option</term>
        ///             <description>Operation</description>
        ///         </listheader>
        ///         <item>
        ///             <term>Open-Drain</term>
        ///             <description>The pin is left floating for no interrupt, and Gnd when there is an interrupt</description>
        ///         </item>
        ///         <item>
        ///             <term>Active High</term>
        ///             <description>The pin is Low when there is no interrupt, and High when there is</description>
        ///         </item>
        ///         <item>
        ///             <term>Active Low</term>
        ///             <description>The pin is High when there is no interrupt, and Low when there is</description>
        ///         </item>
        ///     </list>
        /// </remarks>
        public IntPolarity InterruptPolarity
        {
            get
            {
                switch (ReadRegister(0x5) & 0x6)
                {
                    case 0x0:
                        return IntPolarity.ActiveLow;
                    case 0x2:
                        return IntPolarity.ActiveHigh;
                    default:
                        return IntPolarity.OpenDrain;
                }
            }
            set
            {
                SetRegisterBitsConditional(0x5, 0x4, value == IntPolarity.OpenDrain);
                if (value != IntPolarity.OpenDrain)
                {
                    SetRegisterBitsConditional(0x5, 0x2, value == IntPolarity.ActiveHigh);
                }
            }
        }

        /// <summary>
        ///     Initialize a port expander on the I²C Bus with the standard address
        /// </summary>
        public MCP23017()
        {
            _Bus = I2CBus.GetInstance();

            SetRegister(0x0A, 0xEA);

            _GPIOA = new GPIOBank(this, 0x00);
            _GPIOB = new GPIOBank(this, 0x10);

            _GPIOA.ClearBits(0xFF);
            _GPIOA.DisablePullups(0xFF);
            _GPIOA.SetInputs(0xFF);

            _GPIOB.ClearBits(0xFF);
            _GPIOB.DisablePullups(0xFF);
            _GPIOB.SetInputs(0xFF);
        }

        /// <summary>
        ///     Initialize a port expander on the I²C Bus with a nonstandard address
        /// </summary>
        /// <param name="Address">
        ///     What address (from 0 to 7) the Port Expander is set to
        /// </param>
        public MCP23017(int Address)
            : this()
        {
            Config = new I2CDevice.Configuration((ushort)(0x20 | (Address & 0x7)), 100);
        }

        /// <summary>
        ///     Set bits of a specific on-chip register according to a conditional
        /// </summary>
        /// <param name="Address">
        ///     What register to modify
        /// </param>
        /// <param name="Bits">
        ///     What bits to modify
        /// </param>
        /// <param name="Set">
        ///     Whether to set or clear the bits
        /// </param>
        /// <remarks>
        ///     This function also reads the register as part of the function call.  This may cause unintended behaviour with some devices.
        /// </remarks>
        public void SetRegisterBitsConditional(byte Address, byte Bits, bool Set)
        {
            SetRegister(Address, (byte)((ReadRegister(Address) & ~Bits) | (Set ? Bits : 0x0)));
        }

        /// <summary>
        ///     Set bits in a register
        /// </summary>
        /// <param name="Address">
        ///     What register to modify
        /// </param>
        /// <param name="Bits">
        ///     What bits to set
        /// </param>
        /// <remarks>
        ///     This function also reads the register as part of the function call.  This may cause unintended behaviour with some devices.
        /// </remarks>
        public void SetRegisterBits(byte Address, byte Bits)
        {
            SetRegister(Address, (byte)(ReadRegister(Address) | Bits));
        }

        /// <summary>
        ///     Clear bits in a register
        /// </summary>
        /// <param name="Address">
        ///     What register to modify
        /// </param>
        /// <param name="Bits">
        ///     What bits to Clear
        /// </param>
        /// <remarks>
        ///     This function also reads the register as part of the function call.  This may cause unintended behaviour with some devices.
        /// </remarks>
        public void ClearRegisterBits(byte Address, byte Bits)
        {
            SetRegister(Address, (byte)(ReadRegister(Address) & ~Bits));
        }

        /// <summary>
        ///     Write a value to the given device register
        /// </summary>
        /// <param name="Address">
        ///     What register address to write to
        /// </param>
        /// <param name="Value">
        ///     The value to write
        /// </param>
        public void SetRegister(byte Address, byte Value)
        {
            byte[] Data = new byte[] { Address, Value };
            _Bus.Write(Config, Data, 100);
        }

        /// <summary>
        ///     Read a value from the given device register
        /// </summary>
        /// <param name="Address">
        ///     What register address to read from
        /// </param>
        /// <returns>
        ///     A byte representing the data at that register
        /// </returns>
        public byte ReadRegister(byte Address)
        {
            byte[] Buff = { 0 };
            _Bus.ReadRegister(Config, Address, Buff, 100);
            return Buff[0];
        }

        /// <summary>
        ///     GPIO Bank driver class for MCP23017
        /// </summary>
        public class GPIOBank
        {
            internal MCP23017 _Expander;
            internal byte _Register;
            internal byte _Set;

            internal GPIOBank(MCP23017 Owner, byte Register)
            {
                _Expander = Owner;
                _Register = Register;
            }
            internal GPIOBank(byte Value)
            {
                _Set = Value;
            }

            /// <summary>
            ///     Read the value of the input pins
            /// </summary>
            /// <returns>
            ///     The MCP23017 Documentation does not specify if the state of the output latches is also read
            /// </returns>
            public byte GetValue()
            {
                return _Expander.ReadRegister((byte)(_Register | 0x09));
            }

            /// <summary>
            ///     Set the value of the output latches <br />
            ///     This function does not change the output/input status of the pins
            /// </summary>
            /// <param name="Value">
            ///     What value to write to the latches
            /// </param>
            /// <remarks>
            ///     Output latches corresponding to pins currently configured as inputs will be set, but the latches will remain disconnected from the pins.
            /// </remarks>
            public void SetValue(byte Value)
            {
                _Expander.SetRegister((byte)(_Register | 0x09), Value);
            }

            /// <summary>
            ///     Set specific output bits
            /// </summary>
            /// <param name="Bits">
            ///     What output bits to drive high
            /// </param>
            /// <remarks>
            ///     Output latches corresponding to pins currently configured as inputs will be set, but the latches will remain disconnected from the pins.
            /// </remarks>
            public void SetBits(byte Bits)
            {
                _Expander.SetRegister((byte)(_Register | 0x09), (byte)(_Expander.ReadRegister((byte)(_Register | 0x09)) | Bits));
            }

            /// <summary>
            /// 
            /// </summary>
            ///     Set specific output bits according to a conditional
            /// </summary>
            /// <param name="Bits">
            ///     What output bits to set or clear
            /// </param>
            /// <param name="Value">
            ///     Whether to set or clear the bits
            /// </param>
            /// <remarks>
            ///     Output latches corresponding to pins currently configured as inputs will be set, but the latches will remain disconnected from the pins.
            /// </remarks>
            public void SetBits(byte Bits, bool Value)
            {
                _Expander.SetRegister((byte)(_Register | 0x09), (byte)(_Expander.ReadRegister((byte)((_Register | 0x09))) & ~Bits | (Value ? Bits : 0)));
            }

            /// <summary>
            ///     Clear specific output bits
            /// </summary>
            /// <param name="Bits">
            ///     What output bits to drive low
            /// </param>
            /// <remarks>
            ///     Output latches corresponding to pins currently configured as inputs will be set, but the latches will remain disconnected from the pins.
            /// </remarks>
            public void ClearBits(byte Bits)
            {
                _Expander.SetRegister((byte)(_Register | 0x09), (byte)(_Expander.ReadRegister((byte)(_Register | 0x09)) & ~Bits));
            }

            /// <summary>
            ///     Set which bits to invert when reading
            /// </summary>
            /// <param name="Bits">
            ///     What input pins to invert
            /// </param>
            /// <remarks>
            ///     This does not affect output latches.  The effects of this invert mask on reading back outputs is undetermined.
            /// </remarks>
            public void SetInvertMask(byte Bits)
            {
                _Expander.SetRegister((byte)(_Register | 0x01), Bits);
            }

            /// <summary>
            ///     Enable 100kΩ pullups on the specified pins
            /// </summary>
            /// <param name="Bits">
            ///     What pins to enable pullups for
            /// </param>
            /// <remarks>
            ///     Pullups are disconnected when pins are configured as outputs
            /// </remarks>
            public void EnablePullups(byte Bits)
            {
                _Expander.SetRegister((byte)(_Register | 0x06), (byte)(_Expander.ReadRegister((byte)(_Register | 0x06)) | Bits));
            }

            /// <summary>
            ///     Disable pullups on the specified pins
            /// </summary>
            /// <param name="Bits">
            ///     What pins to disable pullups for
            /// </param>
            /// <remarks>
            ///     Pullups are disconnected when pins are configured as outputs
            /// </remarks>
            public void DisablePullups(byte Bits)
            {
                _Expander.SetRegister((byte)(_Register | 0x06), (byte)(_Expander.ReadRegister((byte)(_Register | 0x06)) & ~Bits));
            }

            /// <summary>
            ///     Set the specified pins as output pins
            /// </summary>
            /// <param name="Pins">
            ///     Which pins to set as outputs
            /// </param>
            /// <remarks>
            ///     Depending on application, it may be desirable to configure the output latches before configuring the pins as outputs.
            /// </remarks>
            public void SetOutputs(byte Pins)
            {
                _Expander.SetRegister((byte)(_Register), (byte)(_Expander.ReadRegister((byte)(_Register)) & ~Pins));
            }

            /// <summary>
            ///     Set the specified pins as input pins
            /// </summary>
            /// <param name="Pins">
            ///     Which pins to set as inputs
            /// </param>
            /// <remarks>
            ///     Depending on application, it may be desirable to configure the pullups before configuring the pins as inputs.
            /// </remarks>
            public void SetInputs(byte Pins)
            {
                _Expander.SetRegister((byte)(_Register), (byte)(_Expander.ReadRegister((byte)(_Register)) | Pins));
            }

            /// <summary>
            ///     Enable interrupt functionality on the specified pins
            /// </summary>
            /// <param name="Pins">
            ///     Which pins to enable interrupts on
            /// </param>
            public void EnableInterrupts(byte Pins)
            {
                _Expander.SetRegisterBits((byte)(_Register | 0x02), Pins);
            }

            /// <summary>
            ///     Disable interrupt functionality on the specified pins
            /// </summary>
            /// <param name="Pins">
            ///     Which pins to disable interrupts on
            /// </param>
            public void DisableInterrupts(byte Pins)
            {
                _Expander.ClearRegisterBits((byte)(_Register | 0x02), Pins);
            }

            /// <summary>
            ///     Configures interrupts for a given pin
            /// </summary>
            /// <param name="Pin">
            ///     Selects which pins
            /// </param>
            /// <param name="OnChange">
            ///     TRUE if the interrupt should be raised when the value changes, FALSE to raise the interrupt when the input value does not match CompareValue
            /// </param>
            /// <param name="CompareValue">
            ///     Stored value to compare pin reading against in DEFVAL (OnChange = False) mode.
            /// </param>
            /// <remarks>
            ///     The interrupt must be enabled through EnableInterrupts for this function call to have any effect.
            /// </remarks>
            public void SetInterruptType(byte Pins, bool OnChange, byte CompareValue)
            {
                if (OnChange)
                {
                    _Expander.ClearRegisterBits((byte)(_Register | 0x04), Pins);
                }
                else
                {
                    _Expander.SetRegisterBits((byte)(_Register | 0x03), CompareValue);
                    _Expander.SetRegisterBits((byte)(_Register | 0x04), Pins);
                }
            }

            public static implicit operator GPIOBank(byte Value)
            {
                return new GPIOBank(null, Value);
            }
            public static implicit operator byte(GPIOBank Bank)
            {
                return Bank._Expander.ReadRegister((byte)(Bank._Register | 0x09));
            }
            public static explicit operator int(GPIOBank Bank)
            {
                return (int)Bank._Expander.ReadRegister((byte)(Bank._Register | 0x09));
            }
        }

        /// <summary>
        ///     Interrupt pin operation specifiers
        /// </summary>
        public enum IntPolarity
        {
            OpenDrain,
            ActiveHigh,
            ActiveLow
        }
    }
}