XPub / XSub
=====

[Pub/Sub](pub-sub.md)適用於多個訂閱者和單一發佈者，但是如果您需要多個發佈者，那麼XPub / XSub模式會比較有趣。

XPub / XSub還可以協助所謂的 _dynamic discovery problem_. From the [ZeroMQ guide](http://zguide.zeromq.org/page:all#The-Dynamic-Discovery-Problem):

> One of the problems you will hit as you design larger distributed architectures is discovery. That is, how do pieces know about each other? It's especially difficult if pieces come and go, so we call this the "dynamic discovery problem".
>
> There are several solutions to dynamic discovery. The simplest is to entirely avoid it by hard-coding (or configuring) the network architecture so discovery is done by hand. That is, when you add a new piece, you reconfigure the network to know about it.
>
> ![](https://github.com/imatix/zguide/raw/master/images/fig12.png)
>
> In practice, this leads to increasingly fragile and unwieldy architectures. Let's say you have one publisher and a hundred subscribers. You connect each subscriber to the publisher by configuring a publisher endpoint in each subscriber. That's easy. Subscribers are dynamic; the publisher is static. Now say you add more publishers. Suddenly, it's not so easy any more. If you continue to connect each subscriber to each publisher, the cost of avoiding dynamic discovery gets higher and higher.
>
> ![](https://github.com/imatix/zguide/raw/master/images/fig13.png)
>
> There are quite a few answers to this, but the very simplest answer is to add an intermediary; that is, a static point in the network to which all other nodes connect. In classic messaging, this is the job of the message broker. ZeroMQ doesn't come with a message broker as such, but it lets us build intermediaries quite easily.
>
> You might wonder, if all networks eventually get large enough to need intermediaries, why don't we simply have a message broker in place for all applications? For beginners, it's a fair compromise. Just always use a star topology, forget about performance, and things will usually work. However, message brokers are greedy things; in their role as central intermediaries, they become too complex, too stateful, and eventually a problem.
>
> It's better to think of intermediaries as simple stateless message switches. A good analogy is an HTTP proxy; it's there, but doesn't have any special role. Adding a pub-sub proxy solves the dynamic discovery problem in our example. We set the proxy in the "middle" of the network. The proxy opens an XSUB socket, an XPUB socket, and binds each to well-known IP addresses and ports. Then, all other processes connect to the proxy, instead of to each other. It becomes trivial to add more subscribers or publishers.
>
> We need XPUB and XSUB sockets because ZeroMQ does subscription forwarding from subscribers to publishers. XSUB and XPUB are exactly like SUB and PUB except they expose subscriptions as special messages. The proxy has to forward these subscription messages from subscriber side to publisher side, by reading them from the XSUB socket and writing them to the XPUB socket. This is the main use case for XSUB and XPUB.


## An Example

所以現在我們已經了解了為什麼要使用XPub / XSub，現在讓我們看一個依上述描述的範例。分為三個部分：

+ Publisher
+ Intermediary
+ Subscriber

### Publisher

可以看到`PublisherSocket`連線到`XSubscriberSocket`的位址。

    :::csharp
    using (var pubSocket = new PublisherSocket(">tcp://127.0.0.1:5678"))
    {
        Console.WriteLine("Publisher socket connecting...");
        pubSocket.Options.SendHighWatermark = 1000;

        var rand = new Random(50);
        
        while (true)
        {
            var randomizedTopic = rand.NextDouble();
            if (randomizedTopic > 0.5)
            {
                var msg = "TopicA msg-" + randomizedTopic;
                Console.WriteLine("Sending message : {0}", msg);
                pubSocket.SendMore("TopicA").Send(msg);
            }
            else
            {
                var msg = "TopicB msg-" + randomizedTopic;
                Console.WriteLine("Sending message : {0}", msg);
                pubSocket.SendMore("TopicB").Send(msg);
            }
        }
    }


### Intermediary

`Intermediary`負責在`XPublisherSocket`和`XSubscriberSocket`之間雙向地中繼訊息。`NetMQ`提供了一個使用簡單的代理類別。

    :::csharp
    using (var xpubSocket = new XPublisherSocket("@tcp://127.0.0.1:1234"))
    using (var xsubSocket = new XSubscriberSocket("@tcp://127.0.0.1:5678"))
    {
        Console.WriteLine("Intermediary started, and waiting for messages");

        // proxy messages between frontend / backend
        var proxy = new Proxy(xsubSocket, xpubSocket);

        // blocks indefinitely
        proxy.Start();
    }


### Subscriber

可以看到`SubscriberSocket`連線到`XPublisherSocket`的位址。

    :::csharp
    string topic = /* ... */; // one of "TopicA" or "TopicB"

    using (var subSocket = new SubscriberSocket(">tcp://127.0.0.1:1234"))
    {
        subSocket.Options.ReceiveHighWatermark = 1000;
        subSocket.Subscribe(topic);
        Console.WriteLine("Subscriber socket connecting...");

        while (true)
        {
            string messageTopicReceived = subSocket.ReceiveString();
            string messageReceived = subSocket.ReceiveString();
            Console.WriteLine(messageReceived);
        }
    }


執行時，可以看到如下列輸出：

![](Images/XPubXSubDemo.png)

不像[Pub/Sub](pub-sub.md)模式，我們可以有不定數量的發佈者及訂閱者。