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
        private Timer T;
        private OutputPort Output;

        private float Tracking;
        private float _PowerLevel;

        private const float Delta = 1f / 60f;

        public float PowerLevel
        {
            get
            {
                return _PowerLevel;
            }
            set
            {
                _PowerLevel = (float)Math.Max(Math.Min(value, 1.0f), 0.0f);
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
                SSR.Tracking = 0;
                return;
            }

            float Interval = (1000f / (120f * SSR._PowerLevel));
            SSR.Tracking += 8f;
            
            if (SSR.Tracking >= Interval)
            {
                SSR.Tracking -= Interval;
                SSR.Output.Write(true);
            }
            else
            {
                SSR.Output.Write(false);
            }
        }

        public ZeroCrossingSSR(Cpu.Pin SSRPin)
        {
            T = new Timer(Tick, this, 0, 8);
            Output = new OutputPort(SSRPin, false);
        }
    }
}
