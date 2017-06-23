Pollers
===

## Motivation 1: Efficiency

There are many use cases for the `NetMQPoller`. First let's look at a simple server:

    :::csharp
    using (var rep = new ResponseSocket("@tcp://*:5002"))
    {
        // process requests, forever...
        while (true)
        {
            // receive a request message
            var msg = rep.ReceiveFrameString();

            // send a canned response
            rep.Send("Response");
        }
    }

This server will happily process responses indefinitely.

What now if we wanted to have one thread handling two different response sockets?

    :::csharp
    using (var rep1 = new ResponseSocket("@tcp://*:5001"))
    using (var rep2 = new ResponseSocket("@tcp://*:5002"))
    {
        while (true)
        {
            // Hmmm....
        }
    }

How would we fairly service both of these response sockets? Can't we just  process them each in turn?

    :::csharp
    // blocks until a message is received
    var msg1 = rep1.ReceiveString();

    // might never reach this code!
    var msg2 = rep2.ReceiveString();

A receive call blocks until a message arrives. If we make a blocking receive call on `rep1`, then we will ignore any messages for `rep2` until `rep1` actually receives something&mdash;which may never happen. This is clearly not a suitable solution.

Instead we could use non-blocking receive calls on `rep1` and `rep2`. However this would keep the CPU maxed out even when no messages are available. Again, this is not a suitable approach.

We could introduce a timeout on the non-blocking receive calls. However, what would a sensible value be? If we chose 10ms, then if `rep1` wasn't receiving messages, we could only dequeue up to 100 messages/sec on `rep2` (or vice versa). This severely limits throughput and doesn't utilise resources efficiently.

Clearly a better approach is needed.

## Motivation 2: Correctness

Following from the example above, you might consider using one thread per socket and going back to blocking receive calls. Indeed in some cases this is a fine solution. However it comes with some restrictions.

For ZeroMQ/NetMQ to give great performance, some restrictions exist on how we can use its sockets. In particular, `NetMQSocket` is not threadsafe. It is invalid to use a socket from multiple threads simultaneously.

For example, consider socket A with a service loop in thread A, and socket B with a service loop in thread B. It would be invalid to receive a message from socket A (on thread A) and then attempt to send it on socket B. The socket is not threadsafe, and so attempts to use is simultaneously from threads A and B would cause errors.

In fact the pattern described here is known as a [proxy](proxy.md), and one is built into NetMQ. At this point you may not be surprised to learn that it is powered by a `NetMQPoller`.

## Example: ReceiveReady

Let's use a `Poller` to easily service two sockets from a single thread:

    :::csharp
    using (var rep1 = new ResponseSocket("@tcp://*:5001"))
    using (var rep2 = new ResponseSocket("@tcp://*:5002"))
    using (var poller = new NetMQPoller { rep1, rep2 })
    {
        // these event will be raised by the Poller
        rep1.ReceiveReady += (s, a) =>
        {
            // receive won't block as a message is ready
            string msg = a.Socket.ReceiveString();
            // send a response
            a.Socket.Send("Response");
        };
        rep2.ReceiveReady += (s, a) =>
        {
            // receive won't block as a message is ready
            string msg = a.Socket.ReceiveString();
            // send a response
            a.Socket.Send("Response");
        };

        // start polling (on this thread)
        poller.Run();
    }

This code sets up two sockets and bind them to different addresses. It then adds those sockets to a `NetMQPoller` using the collection initialiser (you could also call `Add(NetMQSocket)`). Event handlers are attached to each socket's `ReceiveReady` event. Finally the poller is started via `Run()`, which blocks until `Stop` is called on the poller.

Internally, the `NetMQPoller` solves the problem described above in an optimal fashion.

## Example: SendReady

**TODO** add a realistic example showing use of the `SendReady` event.

## Timers

Pollers have an additional feature: timers.

If you wish to perform some operation periodically, and need that operation to be performed on a thread which is allowed to use one or more sockets, you can add a `NetMQTimer` to the `NetMQPoller` along with the sockets you wish to use.

This code sample will publish a message every second to all connected peers.

    :::csharp
    var timer = new NetMQTimer(TimeSpan.FromSeconds(1));

    using (var pub = new PublisherSocket("@tcp://*:5001"))
    using (var poller = new NetMQPoller { pub, timer })
    {
        pub.ReceiveReady += (s, a) => { /* ... */ };

        timer.Elapsed += (s, a) =>
        {
            pub.Send("Beep!");
        };

        poller.Run();
    }

## Adding/removing sockets/timers

Sockets and timers may be safely added and removed from the poller while it is running.

Note that implementations of `ISocketPollable` include `NetMQSocket`, `NetMQActor` and `NetMQBeacon`. Therefore a `NetMQPoller` can observe any of these types.

* `Add(ISocketPollable)`
* `Remove(ISocketPollable)`
* `Add(NetMQTimer)`
* `Remove(NetMQTimer)`
* `Add(System.Net.Sockets.Socket, Action<Socket>)`
* `Remove(System.Net.Sockets.Socket)`

## Controlling polling

So far we've seen `Run`. This devotes the calling thread to polling activity until the poller is cancelled, either from a socket/timer event handler, or from another thread.

If you wish to continue using the calling thread for other actions, you may call `RunAsync` instead, which calls `Run` in a new thread.

To stop a poller, use either `Stop` or `StopAsync`. The latter waits until the poller's loop has completely exited before returning, and may be necessary during graceful teardown of an application.

## A more complex example

Let's see a more involved example that uses much of what we've seen so far. We'll remove a `ResponseSocket` from the `NetMQPoller` once it receives its first message after which `ReceiveReady` will not fire for that socket, even if messages are available.

    :::csharp
    using (var rep = new ResponseSocket("@tcp://127.0.0.1:5002"))
    using (var req = new RequestSocket(">tcp://127.0.0.1:5002"))
    using (var poller = new NetMQPoller { rep })
    {
        // this event will be raised by the Poller
        rep.ReceiveReady += (s, a) =>
        {
            bool more;
            string messageIn = a.Socket.ReceiveFrameString(out more);
            Console.WriteLine("messageIn = {0}", messageIn);
            a.Socket.SendFrame("World");

            // REMOVE THE SOCKET!
            poller.Remove(a.Socket);
        };

        // start the poller
        poller.RunAsync();

        // send a request
        req.SendFrame("Hello");

        bool more2;
        string messageBack = req.ReceiveFrameString(out more2);
        Console.WriteLine("messageBack = {0}", messageBack);

        // SEND ANOTHER MESSAGE
        req.SendFrame("Hello Again");

        // give the message a chance to be processed (though it won't be)
        Thread.Sleep(1000);
    }

Which when run gives this output now.

    :::text
    messageIn = Hello
    messageBack = World

See how the `Hello Again` message was not received? This is due to the `ResponseSocket` being removed from the `NetMQPoller` during processing of the first message in the `ReceiveReady` event handler.

## Performance

Receiving messages with poller is slower than directly calling Receive method on the socket. 
When handling thousands of messages a second, or more, poller can be a bottleneck.
However the solution is pretty simple, we just need to fetch all messages currently available with the socket using the Try* methods. Following is an example:

    :::csharp
    rep1.ReceiveReady += (s, a) =>
    {
        string msg;
        // receiving all messages currently available in the socket before returning to the poller
        while (a.Socket.TryReceiveFrameString(out msg))
        {
            // send a response
            a.Socket.Send("Response");
        }
    };

The above solution can cause starvation of other sockets if socket is loaded with non-stop flow of messages.
To solve this you can limit the number of messages that can be fetch in one batch.

    :::csharp
    rep1.ReceiveReady += (s, a) =>
    {
        string msg;
        //  receiving 1000 messages or less if not available
        for (int count = 0; count < 1000; i++)
        {
            // exit the for loop if failed to receive a message
            if (!a.Socket.TryReceiveFrameString(out msg))
                break;
                
            // send a response
            a.Socket.Send("Response");
        }
    };

## Further Reading

A good place to look for more information and code samples is the [`Poller` unit test source](https://github.com/zeromq/netmq/blob/master/src/NetMQ.Tests/NetMQPollerTest.cs).
