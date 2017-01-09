Timer
=====
一個`NetMQTimer`讓你可以執行週期性的動作。Timer實體可以加至`NetMQPoller`中，且它的`Elapsed`事件會依指定的`Interval`及`Enabled`屬性值被觸發。

下列事件在poller執行緒中被喚起。
    :::csharp
    var timer = new NetMQTimer(TimeSpan.FromMilliseconds(100));

    timer.Elapsed += (sender, args) => { /* handle timer event */ };

    using (var poller = new NetMQPoller { timer })
    {
        poller.Run();
    }
