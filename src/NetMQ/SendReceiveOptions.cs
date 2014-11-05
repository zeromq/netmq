using System;

namespace NetMQ
{
    [Flags]
    public enum SendReceiveOptions
    {
        None = 0,
        DontWait = 1,
        SendMore = 2,

        /*  Deprecated aliases                                                        */
        [Obsolete]
        NoBlock = DontWait,
    }
}