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

        public static float deltaTime 
        {
            get;
            private set;
        }

        private static TimerSystem _instance;

        public static TimerSystem Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new TimerSystem();
                }
                return _instance;
            }
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
