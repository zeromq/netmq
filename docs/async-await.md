Async Await
===========

Since version (4.0.0.239-pre) NetMQ support async/await.

To use async/await feature you need to create a `NetMQRuntime`.

## Example:

```csharp
async Task ServerAsync()
{
    using (var server = new RouterSocket("inproc://async"))
    {
        for (int i = 0; i < 1000; i++)
        {
            var (routingKey, more) = await server.ReceiveRoutingKeyAsync();
            var (message, _) = await server.ReceiveFrameStringAsync();

            // TODO: process message

            await Task.Delay(100);
            server.SendMoreFrame(routingKey);
            server.SendFrame("Welcome");
        }
    }
}

async Task ClientAsync()
{
    using (var client = new DealerSocket("inproc://async"))
    {
        for (int i = 0; i < 1000; i++)
        {
            client.SendFrame("Hello");
            var (message, more) = await client.ReceiveFrameStringAsync();

            // TODO: process reply

            await Task.Delay(100);
        }
    }
}

static void Main(string[] args)
{
    using (var runtime = new NetMQRuntime())
    {
        runtime.Run(ServerAsync(), ClientAsync());
    }
}
```

NetMQRuntime is a wrapper over NetMQPoller, when calling an async function the socket is automatically added to the internal poller.
NetMQRuntime is also a NetMQScheduler and SyncrhonizationContext, so any awaited function is continuing on the runtime's thread.

NetMQSocket should still be used only within one thread.

`NetMQRuntime.Run` can accept multiple tasks and also a cancellation token.
`NetMQRuntime.Run` runs until all tasks are completed or cancellation token has been cancelled.
