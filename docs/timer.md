Timer
=====

A `NetMQTimer` allows actions to be performed periodically. Timer instances may be added to a `NetMQPoller`, and their
`Elapsed` event will fire according to the specified `Interval` and `Enabled` property values.

The event is raised on the poller's thread.

``` csharp
var timer = new NetMQTimer(TimeSpan.FromMilliseconds(100));
timer.Elapsed += (sender, args) => { /* handle timer event */ };
using (var poller = new NetMQPoller { timer })
{
    poller.Run();
}
