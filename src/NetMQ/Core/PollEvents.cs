using System;

namespace NetMQ.Core
{
    [Flags]
    enum PollEvents
    {
        None = 0x0,
        PollIn = 0x1,
        PollOut = 0x2,
        PollError = 0x4
    }
}