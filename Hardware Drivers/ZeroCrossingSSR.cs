using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Microsoft.SPOT.Hardware;
using SecretLabs.NETMF.Hardware;
using SecretLabs.NETMF.Hardware.Netduino;

namespace Reflow_Oven_Controller
{
    class ZeroCrossingSSR
    {
        private Timer _PulseTimer;
        private OutputPort _Output;

        private float _Tracking;
        private float _PowerLevel;
        private float _Increment;

        public float PowerLevel
        {
            get
            {
                return _PowerLevel;
            }
            set
            {
                float _OldPowerLevel = _PowerLevel;
                _PowerLevel = (float)Math.Max(Math.Min(value, 1.0f), 0.0f);

                if (Math.Abs(_OldPowerLevel - _PowerLevel) > 0.001)
                {
                    float OnPulses = (float)Math.Floor(125f * _PowerLevel);
                    float OffPulses = 125f - OnPulses;
                    if (OnPulses != 0)
                        _Increment = OffPulses / OnPulses;
                }
            }
        }

        /// <summary>
        ///     Roughly pulse the output pin to the SSR to maintain a steady dimmed output.  This function pulses a little faster than 120Hz, however.
        /// </summary>
        /// <param name="State"></param>
        private static void Tick(object State)
        {
            ZeroCrossingSSR SSR = (ZeroCrossingSSR)State;

            if (SSR._PowerLevel < 0.01)
            {
                SSR._Tracking = 0;
                SSR._Output.Write(false);
                return;
            }

            if (SSR._Tracking <= 0f)
            {
                SSR._Tracking = (float)Math.Max(SSR._Tracking + SSR._Increment, -1.0);
                SSR._Output.Write(true);
            }
            else
            {
                SSR._Tracking -= 1f;
                SSR._Output.Write(false);
            }
        }

        public ZeroCrossingSSR(Cpu.Pin SSRPin)
        {
            _PulseTimer = new Timer(Tick, this, 0, 8);
            _Output = new OutputPort(SSRPin, false);
        }
    }
}
