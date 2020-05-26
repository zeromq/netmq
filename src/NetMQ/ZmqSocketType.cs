namespace NetMQ
{
    /// <summary>
    /// This enum-type is used to specify the basic type of message-queue socket
    /// based upon the intended pattern, such as Pub,Sub, Req,Rep, Dealer,Router, Pull,Push, Xpub,Xsub.
    /// </summary>
    public enum ZmqSocketType
    {
        /// <summary>
        /// No socket-type is specified
        /// </summary>
        None = -1,

        /// <summary>
        /// This denotes a Pair socket (usually paired with another Pair socket).
        /// </summary>
        Pair = 0,

        /// <summary>
        /// This denotes a Publisher socket (usually paired with a Subscriber socket).
        /// </summary>
        Pub = 1,

        /// <summary>
        /// This denotes a Subscriber socket (usually paired with a Publisher socket).
        /// </summary>
        Sub = 2,

        /// <summary>
        /// This denotes a Request socket (usually paired with a Response socket).
        /// </summary>
        Req = 3,

        /// <summary>
        /// This denotes a Response socket (usually paired with a Request socket).
        /// </summary>
        Rep = 4,

        /// <summary>
        /// This denotes an Dealer socket.
        /// </summary>
        Dealer = 5,

        /// <summary>
        /// This denotes an Router socket.
        /// </summary>
        Router = 6,

        /// <summary>
        /// This denotes a Pull socket (usually paired with a PUsh socket).
        /// </summary>
        Pull = 7,

        /// <summary>
        /// This denotes a Push socket (usually paired with a Pull socket).
        /// </summary>
        Push = 8,

        /// <summary>
        /// This denotes an XPublisher socket.
        /// </summary>
        Xpub = 9,

        /// <summary>
        /// This denotes an XSubscriber socket.
        /// </summary>
        Xsub = 10,

        /// <summary>
        /// This denotes a Stream socket - which is a parent-class to the other socket types.
        /// </summary>
        Stream = 11,
        
        /// <summary>
        /// This denotes an Server socket.
        /// </summary>
        Server = 12,
        
        /// <summary>
        /// This denotes an Client socket.
        /// </summary>
        Client = 13,
        
        /// <summary>
        /// This denotes an Radio socket.
        /// </summary>
        Radio = 14,
        
        /// <summary>
        /// This denotes an Dish socket.
        /// </summary>
        Dish = 15,

        /// <summary>
        /// This denotes an Gather socket.
        /// </summary>
        Gather = 16,
        
        /// <summary>
        /// This denotes an Scatter socket.
        /// </summary>
        Scatter = 17,
        
        /// <summary>
        /// This denotes a Peer socket.
        /// </summary>
        Peer = 19
    }
}