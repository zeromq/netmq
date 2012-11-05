namespace ZeroMQ
{
    using System;

    /// <summary>
    /// Specifies possible socket types defined by ZMQ messaging patterns.
    /// </summary>
    public enum SocketType
    {
        /// <summary>
        /// Can only be connected to a single peer at any one time.
        /// Part of the Exclusive Pair pattern.
        /// </summary>
        PAIR = 0,

        /// <summary>
        /// Used by a publisher to distribute messages in a fan out fashion to all connected peers.
        /// Part of the Publish-Subscribe pattern.
        /// </summary>
        PUB = 1,

        /// <summary>
        /// Used by a subscriber to subscribe to data distributed by a publisher.
        /// Part of the Publish-Subscribe pattern.
        /// </summary>
        SUB = 2,

        /// <summary>
        /// Used by a client to send requests to and receive replies from a service.
        /// Part of the Request-Reply pattern.
        /// </summary>
        REQ = 3,

        /// <summary>
        /// Used by a service to receive requests from and send replies to a client.
        /// Part of the Request-Reply pattern.
        /// </summary>
        REP = 4,

        /// <summary>
        /// Used for extending request/reply sockets. Each message sent is round-robined
        /// among all connected peers, and each message received is fair-queued from all connected peers.
        /// </summary>
        DEALER = 5,

        /// <summary>
        /// Used for extending request/reply sockets. Messages received are fair-queued
        /// from among all connected peers.
        /// </summary>
        /// <remarks>
        /// When receiving messages a <see cref="ROUTER"/> socket shall prepend a message
        /// part containing the identity of the originating peer to the message before
        /// passing it to the application. When sending messages a ZMQ_ROUTER socket shall remove
        /// the first part of the message and use it to determine the identity of the peer the message
        /// shall be routed to. If the peer does not exist anymore the message shall be silently discarded.
        /// </remarks>
        ROUTER = 6,

        /// <summary>
        /// Used by a pipeline node to receive messages from upstream pipeline nodes.
        /// Part of the Pipeline pattern.
        /// </summary>
        PULL = 7,

        /// <summary>
        /// Used by a pipeline node to send messages to downstream pipeline nodes.
        /// Part of the Pipeline pattern.
        /// </summary>
        PUSH = 8,
        
        /// <summary>
        /// Same as <see cref="PUB"/> except subscriptions can be received from peers as incoming messages.
        /// Part of the Publish-Subscribe pattern.
        /// </summary>
        /// <remarks>
        /// Subscription message is a byte '1' (for subscriptions) or byte '0' (for unsubscriptions) followed by the subscription body.
        /// </remarks>
        XPUB = 9,

        /// <summary>
        /// Same as <see cref="SUB"/> except subscription messages can be sent to the publisher.
        /// Part of the Publish-Subscribe pattern.
        /// </summary>
        /// <remarks>
        /// Subscription message is a byte '1' (for subscriptions) or byte '0' (for unsubscriptions) followed by the subscription body.
        /// </remarks>
        XSUB = 10,

        /// <summary>
        /// Alias for <see cref="ROUTER"/>.
        /// </summary>
        [Obsolete("This socket type will be removed in a future release. Use ROUTER instead.")]
        XREP = ROUTER,

        /// <summary>
        /// Alias for <see cref="DEALER"/>.
        /// </summary>
        [Obsolete("This socket type will be removed in a future release. Use DEALER instead.")]
        XREQ = DEALER,
    }
}