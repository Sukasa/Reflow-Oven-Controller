using System;
using System.Threading;

namespace ReflowOvenController.ProcessControl
{

    public delegate float GetCurrent();

    public class DeltaPID
    {
        private static Thread _PIDsThread;
        private static DeltaPID[] _AllPIDs;

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

        private static int _TargetHz;
        public static int TargetHz
        {
            get
            {
                return _TargetHz;
            }
            set
            {
                _TargetHz = value;
                for (int i = 0; i < _AllPIDs.Length; i++ )
                    _AllPIDs[i].DerivativeTime = _AllPIDs[i]._DerivativeTime; // Trigger the side effect of resizing the derivative values array
            }
        }
 
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

            }
            Bias = (float)Math.Min(Math.Max(Bias, 0f), 100f);

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

        private static void Run()
        {
            while (true)
            {
                for(int i = 0; i < _AllPIDs.Length; i++)
                    _AllPIDs[i].Tick();
                Thread.Sleep((int)(1000 / TargetHz));
            }
        }

        public static void StartAll()
        {
            _PIDsThread = new Thread(Run);
            _PIDsThread.Start();
        }

        public static void AllocatePIDs(int NumPIDs)
        {
            _AllPIDs = new DeltaPID[NumPIDs];
            for (int i = 0; i < NumPIDs; i++)
                _AllPIDs[i] = new DeltaPID();
        }

        public static DeltaPID GetPID(int Index) {
            return _AllPIDs[Index];
        }

        public void SetGetter(GetCurrent ValueGetter)
        {
            GetCurrentValue = ValueGetter;
        }

        public DeltaPID()
        {
            Bias = 50f;
            ProportionalGain = 50f;
        }

        ~DeltaPID()
        {
            GetCurrentValue = null;
        }
    }
}
