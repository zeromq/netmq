namespace NetMQ
{
    public enum ZmqSocketType
    {
        None = -1,
        Pair = 0,
        Pub = 1,
        Sub = 2,
        Req = 3,
        Rep = 4,
        Dealer = 5,
        Router = 6,
        Pull = 7,
        Push = 8,
        Xpub = 9,
        Xsub = 10,
        Stream = 11,
    }
}