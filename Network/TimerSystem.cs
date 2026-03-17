using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace Network
{
    internal class TimerSystem
    {
        private Stopwatch _timer;
        private long _prevTick;
        private long _nowTick;

        public float deltaTime 
        {
            get;
            private set;
        }

        public void Init()
        {
            _timer = Stopwatch.StartNew();
            _prevTick = _nowTick = _timer.ElapsedTicks;
        }

        public void Update()
        {
            _nowTick = _timer.ElapsedTicks;
            long deltaTick = _nowTick - _prevTick;
            deltaTime = (float)deltaTick / Stopwatch.Frequency;
        }


    }
}
