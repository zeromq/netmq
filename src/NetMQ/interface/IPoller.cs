using System;
using System.Net.Sockets;

namespace NetMQ
{
	public interface IPoller : IDisposable
	{
		bool IsStarted { get; }

		int PollTimeout { get; set; }

		void AddPollInSocket(Socket socket, Action<Socket> callback);

		void AddSocket(ISocketPollable socket);

		void AddTimer(NetMQTimer timer);

		void Cancel();

		void CancelAndJoin();

		void PollOnce();

		void PollTillCancelled();

		void PollTillCancelledNonBlocking();

		void RemovePollInSocket(Socket socket);

		void RemoveSocket(ISocketPollable socket);

		void RemoveTimer(NetMQTimer timer);

		[Obsolete("Use PollTillCancelled instead")]
		void Start();

		[Obsolete("Use Cancel or CancelAndJoin")]
		void Stop();

		[Obsolete("Use Cancel or CancelAndJoin")]
		void Stop(bool waitForCloseToComplete);
	}
}
