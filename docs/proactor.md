Proactor
========

`NetMQProactor` quickly processes messages received on a socket using a dedicated thread.

``` csharp
using (var receiveSocket = new DealerSocket(">tcp://localhost:5555"))
using (var proactor = new NetMQProactor(receiveSocket, 
        (socket, message) => ProcessMessage(message)))
{
    // ...
}
```

Internally the proactor creates a `NetMQPoller` for the socket, and a `NetMQActor` to coordinate
the poller thread and disposal.
