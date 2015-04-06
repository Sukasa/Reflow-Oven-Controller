using System;
using System.Threading;
using System.Reflection;
using System.Runtime;

namespace Reflow_Oven_Controller.Process_Control
{

    public delegate float GetCurrent();
    /// <summary>
    ///     PID Controller based on the Delta Controls variation of PID .
    /// </summary>
    /// <remarks>
    ///     Unlike a "standard" PID loop, the Delta Controls PID controller is a simpler concept designed for ease of use and deployment.  It is not as accurate as true PID, but can be deployed and tuned
    ///     in minutes or less.  It is designed for applications with slower response times, primarily in HVAC controls.  This is not the "true" Delta implementation, but is recreated from descriptions of how
    ///     the Delta PID code works.
    /// </remarks>
    class DeltaPID
    {

        public float Setpoint { get; set; }
        public GetCurrent GetCurrentValue;

        public float ProportionalBand { get; set; }
        public float ProportionalGain { get; set; }

        public float Deadband { get; set; }

        public float Bias { get; set; }
        public float IntegralRate { get; set; }
        public float IntegralResetBand { get; set; }

        private float _DerivativeTime;
        private float[] _DerivativeValues;
        private int _DerivativePtr;
        public float DerivativeGain { get; set; }
        public float DerivativeBand { get; set; }
        public float DerivativeTime
        {
            get
            {
                return _DerivativeTime;
            }
            set
            {
                _DerivativeTime = value;
                float[] NewBuffer = new float[(int)(_DerivativeTime * TargetHz)];
                
                if (_DerivativeValues == null)
                    _DerivativeValues = NewBuffer;

                Array.Copy(_DerivativeValues, NewBuffer, Math.Min(_DerivativeValues.Length, NewBuffer.Length));
                _DerivativePtr = Math.Min(_DerivativePtr, NewBuffer.Length);
                _DerivativeValues = NewBuffer;
            }
        }

        public float Value { get; set; }
        public bool ReverseActing { get; set; }

        private int _TargetHz;
        public int TargetHz
        {
            get
            {
                return _TargetHz;
            }
            set
            {
                _TargetHz = value;
                _DerivativeTime = DerivativeTime; // Trigger the side effect of resizing the derivative values array
            }
        }

        private Thread PIDThread;
        private void Tick()
        {
            if (GetCurrentValue == null)
                return;

            float CurrentValue = GetCurrentValue();
            float Offset = CurrentValue - Setpoint;

            float Proportional = 0f;
            float Derivative = 0f;

            if (Math.Abs(Offset) < (Deadband / 2))
                return;

            if (ProportionalBand > 0f)
            {
                Proportional = (float)Math.Max(Math.Min((Offset / (ProportionalBand / 2)) * 50f, 50.0f), -50.0f);

                if (ReverseActing)
                    Proportional *= -1f;
            }
            else
            {
                Proportional = 0f;
            }

            if (IntegralRate > 0f)
            {
                float I;

                I = (float)Math.Abs(Offset) - (Deadband / 2f);

                if (I >= (IntegralResetBand / 2f)) {
                    I = IntegralRate;
                }
                else
                {
                    I = (I / (IntegralResetBand / 2f)) * IntegralRate;
                }

                I /= (float)(60 * TargetHz);

                if (ReverseActing)
                {
                    Bias -= I * (float)Math.Sign(Offset);
                }
                else
                {
                    Bias += I * (float)Math.Sign(Offset);
                }

                Bias = (float)Math.Min(Math.Max(Bias, 0f), 100f);
            }

            if (DerivativeGain > 0f && DerivativeTime > 0f)
            {
                int _LastDerivativePtr = _DerivativePtr;
                _DerivativeValues[_DerivativePtr++] = CurrentValue;
                _DerivativePtr %= _DerivativeValues.Length;

                float D = (float)Math.Abs(Offset) - (Deadband / 2f);

                if (D >= (DerivativeBand / 2f))
                {
                    D = 0f;
                }
                else
                {
                    D = 1f - (D / (DerivativeBand / 2f));
                }

                Derivative = _DerivativeValues[_LastDerivativePtr] - _DerivativeValues[_DerivativePtr];
                Derivative *= DerivativeGain * D;

                if (ReverseActing)
                    Derivative *= -1f;
            }
            else
            {
                Derivative = 0f;
            }

            Value = (float)Math.Max(Math.Min(Proportional + Bias + Derivative, 100f), 0f);
        }

        private void Run()
        {
            while (true)
            {
                Tick();
                Thread.Sleep((int)(1000 / TargetHz));
            }
        }

        public DeltaPID(GetCurrent ValueGetter)
        {
            GetCurrentValue = ValueGetter;
            Bias = 50f;
            TargetHz = 20;
            ProportionalGain = 50f;

            PIDThread = new Thread(Run);
            PIDThread.Start();
        }

        ~DeltaPID()
        {
            GetCurrentValue = null;
            PIDThread.Abort();
        }
    }
}
