Queue
======

`NetMQQueue<T>` is a producer-consumer queue that supports multiple producers and a single consumer.

You should add the queue to a `NetMQPoller`, and attach the consumer to its `ReceiveReady` event.
Producers call `Enqueue(T)`.

This class can eliminate boilerplate code associated with marshalling operations onto a single thread.

``` csharp
using (var queue = new NetMQQueue<ICommand>())
using (var poller = new NetMQPoller { queue })
{
    queue.ReceiveReady += (sender, args) => ProcessCommand(queue.Dequeue());
    poller.RunAsync();
    // Then, from various threads...
    queue.Enqueue(new DoSomethingCommand());
    queue.Enqueue(new DoSomethingElseCommand());
}
