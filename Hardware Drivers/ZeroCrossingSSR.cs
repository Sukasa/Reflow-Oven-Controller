using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Microsoft.SPOT.Hardware;
using SecretLabs.NETMF.Hardware;
using SecretLabs.NETMF.Hardware.Netduino;

namespace ReflowOvenController
{
    class ZeroCrossingSSR
    {
        private Timer _PulseTimer;
        private OutputPort _Output;

        private float _Tracking;
        private float _PowerLevel;
        //private float _Increment;

        public float PowerLevel
        {
            get
            {
                return _PowerLevel;
            }
            set
            {
                _PowerLevel = value;
            }
        }

        /// <summary>
        ///     Roughly pulse the output pin to the SSR to maintain a steady dimmed output.  This function pulses a little faster than 120Hz, however.
        /// </summary>
        /// <param name="State"></param>
        private static void Tick(object State)
        {
            try
            {
                ZeroCrossingSSR SSR = (ZeroCrossingSSR)State;

                if (SSR._PowerLevel < 0.01)
                {
                    SSR._Output.Write(false);
                    return;
                }

                if (SSR._Tracking <= SSR._PowerLevel)
                {
                    SSR._Output.Write(true);
                }
                else
                {
                    SSR._Output.Write(false);
                }
                SSR._Tracking = (SSR._Tracking + 0.05f) % 1f;
            }
            catch
            {
                // I don't care the error - shut down SSRs immediately on fault.
                ((ZeroCrossingSSR)State)._Output.Write(false);
            }
        }

        public ZeroCrossingSSR(Cpu.Pin SSRPin)
        {
            _PulseTimer = new Timer(Tick, this, 0, 200);
            _Output = new OutputPort(SSRPin, false);
        }
    }
}
