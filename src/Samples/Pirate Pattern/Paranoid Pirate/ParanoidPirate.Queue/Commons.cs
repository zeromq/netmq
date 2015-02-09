using System;

namespace ParanoidPirate.Queue
{
    public class Commons
    {
        public const int HEARTBEAT_LIVELINESS = 3;   // 3-5 is reasonable
        public const int HEARTBEAT_INTERVAL = 1000;  // in ms
        public const int INTERVAL_INIT = 1000;       // in ms
        public const int INTERVAL_MAX = 32000;       // in ms

        public const string PPP_READY = "READY";
        public const string PPP_HEARTBEAT = "HEARTBEAT";
        public const int PPP_TICK = 500;            // in ms

        public const string QUEUE_FRONTEND = "tcp://127.0.0.1:5556";
        public const string QUEUE_BACKEND = "tcp://127.0.0.1:5557";

        public const int REQUEST_CLIENT_TIMEOUT = 2500; // in ms - should be > 1000!
        public const int REQUEST_CLIENT_RETRIES = 10;
    }
}
