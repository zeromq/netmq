namespace ParanoidPirate.Queue
{
    public static class Commons
    {
        public const int HeartbeatLiveliness = 3;   // 3-5 is reasonable
        public const int HeartbeatInterval = 1000;  // in ms
        public const int IntervalInit = 1000;       // in ms
        public const int IntervalMax = 32000;       // in ms

        public const string PPPReady = "READY";
        public const string PPPHeartbeat = "HEARTBEAT";
        public const int PPPTick = 500;             // in ms

        public const string QueueFrontend = "tcp://127.0.0.1:5556";
        public const string QueueBackend = "tcp://127.0.0.1:5557";

        public const int RequestClientTimeout = 2500; // in ms - should be > 1000!
        public const int RequestClientRetries = 10;
    }
}
