using System;

namespace NetMQ.zmq
{
	public enum ContextOption
	{
		IOThreads = 1,
		MaxSockets = 2,
	}


	public enum ZmqSocketType
	{
		None = -1,
		Pair = 0,
		Pub = 1,
		Sub = 2,
		Req = 3,
		Rep = 4,
		Dealer = 5,
		Router = 6,
		Pull = 7,
		Push = 8,
		Xpub = 9,
		Xsub = 10,	
	}

	public enum ZmqSocketOptions
	{
		Affinity = 4,
		Identity = 5,
		Subscribe = 6,
		Unsubscribe = 7,
		Rate = 8,
		RecoveryIvl = 9,
		SendBuffer = 11,
		ReceivevBuffer = 12,
		ReceiveMore = 13,
		FD = 14,
		Events = 15,
		Type = 16,
		Linger = 17,
		ReconnectIvl = 18,
		Backlog = 19,
		ReconnectIvlMax = 21,
		Maxmsgsize = 22,
		SendHighWatermark = 23,
		ReceivevHighWatermark = 24,
		MulticastHops = 25,
		ReceiveTimeout = 27,
		SendTimeout = 28,
		IPv4Only = 31,
		LastEndpoint = 32,
		RouterMandatory = 33,
		TcpKeepalive = 34,
		TcpKeepaliveCnt = 35,
		TcpKeepaliveIdle = 36,
		TcpKeepaliveIntvl = 37,
		TcpAcceptFilter = 38,
		DelayAttachOnConnect = 39,
		XpubVerbose = 40,

		Endian = 1000,

		[Obsolete]
		FailUnroutable = RouterMandatory,

		[Obsolete]
		RouterBehavior = RouterMandatory,
}

	public enum Endianness
	{
		Big, Little
	}

	[Flags]
	public enum SendReceiveOptions
	{
		None = 0,
		DontWait = 1,
		SendMore = 2,

		/*  Deprecated aliases                                                        */
		[Obsolete]
		NoBlock = DontWait,
	}

/*  Socket transport events (tcp and ipc only)                                */	

	[Flags]
	public enum SocketEvent
	{
		Connected = 1,
		ConnectDelayed = 2,
		ConnectRetried = 4,		

		Listening = 8,
		BindFailed = 16,

		Accepted = 32,
		AcceptFailed = 64,

		Closed = 128,
		CloseFailed = 256,
		Disconnected = 512,

		All = Connected | ConnectDelayed |
		                ConnectRetried | Listening |
		                BindFailed | Accepted |
		                AcceptFailed | Closed |
		                CloseFailed | Disconnected,
	}

	[Flags]
	public enum PollEvents
	{		
		None = 0x0,
		PollIn = 0x1,
		PollOut = 0x2,
		PollError = 0x4
	}
}