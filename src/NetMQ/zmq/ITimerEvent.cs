using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NetMQ.zmq
{
    public interface ITimerEvent
    {
        // Called when timer expires.
        void TimerEvent(int id);     
    }
}
