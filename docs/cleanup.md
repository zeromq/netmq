Cleanup
=======

New with NetMQ version 4 we said goobye to NetMQContext. We can now create sockets with the new operator without the NetMQContext.
Though this makes using the library simpler, it also adds complication to cleaning it up.

# Why does NetMQ need Cleanup?

NetMQ creates a few background threads. Also, when you call Dispose on a Socket the process is asynchronous and actually happens in a background thread.
Because NetMQ threads are background threads you can actually exit the application without a proper cleanup of the NetMQ library, though it is not recommeneded.

When exiting AppDomain it might be more complicated, and you have to cleanup the NetMQ library.

# What is Linger?

Linger is the allowed time for the Socket to send all messages before getting disposed.
So when we call Dispose on a Socket with Linger set to 1 second it might take up to 1 second to the socket to get diposed.
In that time the library will try to send all the pending messages, if it finish before the linger the socket will get disposed before the time is up.

As said all this is happening in the background, so if the linger is set but we are not properly cleaning-up the library the linger will get ingored.
So if the linger is important for you, make sure you properly cleanup the library.

With version 4 the default Linger of a socket is zero, it means that the library will not wait before disposing the socket.
You can change the Linger for a Socket, but also change the default Linger for all Socket with NetMQConfig.Linger.

# How to Cleanup?

The most important thing to know about cleanup is that you must call Dispose on all sockets before calling Cleanup. 
Also make sure to cleanup any other resource from NetMQ library, like NetMQPoller, NetMQQueue and etc...
If socket is not get disposed the NetMQConfig.Cleanup will block forever.

Lastly you need to call the NetMQConfig.Cleanup. You can do that in the last line of the Main method:

    :::csharp
	static void Main(string[] args)
	{
	    try
	    {
	        // Do you logic here
	    }
	    finally
	    {
	        NetMQConfig.Cleanup();
	    }
	}

If you are lazy and don't care about cleaning the library you can also call NetMQConfig.Cleanup with the block parameter set to false.
When set to false the cleanup will not wait for Sockets to Send all messages and will just kill the background threads.

# Tests

When using NetMQ within your tests you also need to make sure you Cleanup the library.

I suggest to just add global tear down to your tests and then call NetMQConfig.Cleanup.

This how to do it with NUnit:

    :::csharp
    [SetUpFixture]
    public class Setup
    {
        [OneTimeTearDown]
        public void TearDown()
        {
            NetMQConfig.Cleanup(false);
        }
    }

In tests it is important to call Cleanup with block set to false so in case of failing test the entire process is not hanging.
