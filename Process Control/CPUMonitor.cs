using System;
using System.Threading;
using Microsoft.SPOT.Hardware;

namespace Reflow_Oven_Controller.Process_Control
{
    public class CPUMonitor
    {
        private long _idle;
        private long _last;

        public int LoadPercentage;

        /// <summary>
        /// Create this object to monitor the CPU usage.
        /// </summary>
        public CPUMonitor()
        {
            Thread t = new Thread(Calculator);
            t.Priority = ThreadPriority.Lowest;
            t.Start();
            new Thread(Idler).Start(); 
        }

        private void Calculator()
        {
            lock (this)
            {
                _idle = 0;
                _last = DateTime.Now.Ticks;
            }
            while (true)
            {
                Thread.Sleep(1000);
                lock (this)
                {
                    long now = DateTime.Now.Ticks;
                    LoadPercentage = (int)(100 - _idle * 100 / (now - _last));
                    _last = now;
                    _idle = 0;
                }
            }
        }

        private void Idler()
        {
            long last = DateTime.Now.Ticks;
            while (true)
            {
                PowerState.WaitForIdleCPU(1, int.MaxValue);
                long now = DateTime.Now.Ticks;
                long delta = now - last;
                if (delta < 10000)     // 1 ms
                    lock (this)
                        _idle += delta;
                last = now;
            }
        }
    }
}