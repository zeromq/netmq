using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

// Note: This particular module does not support versions of the .NET Framework earlier than 4.0 (those did not provide a TaskScheduler class).
//       In the Visual Studio solution that does target pre-.NET 4, (NetMQ_Vs2008.sln) this module is not included.  James Hurst (jh)

namespace NetMQ
{
	public class NetMQScheduler : TaskScheduler, IDisposable
	{
		private readonly bool m_ownPoller;
		private readonly Poller m_poller;

		private static int s_schedulerCounter;

		private readonly int m_schedulerId;
		private readonly string m_address;

		private readonly NetMQContext m_context;
		private readonly NetMQSocket m_serverSocket;

		private readonly ThreadLocal<NetMQSocket> m_clientSocket;
		private readonly ThreadLocal<bool> m_schedulerThread;

		private readonly ConcurrentBag<NetMQSocket> m_clientSockets;

		private EventHandler<NetMQSocketEventArgs> m_currentMessageHandler; 

		public NetMQScheduler(NetMQContext context, Poller poller = null)
		{
			m_context = context;
			if (poller == null)
			{
				m_ownPoller = true;

				m_poller = new Poller();
			}
			else
			{
				m_ownPoller = false;

				m_poller = poller;
			}

			m_clientSockets = new ConcurrentBag<NetMQSocket>();

			m_schedulerId = Interlocked.Increment(ref s_schedulerCounter);

			m_address = string.Format("inproc://scheduler-{0}", m_schedulerId);

			m_serverSocket = context.CreatePullSocket();
			m_serverSocket.Options.Linger = TimeSpan.Zero;
			m_serverSocket.Bind(m_address);

			m_currentMessageHandler = OnMessageFirstTime;

			m_serverSocket.ReceiveReady += m_currentMessageHandler;

			m_poller.AddSocket(m_serverSocket);

			m_clientSocket = new ThreadLocal<NetMQSocket>(() =>
																											{
																												var socket = m_context.CreatePushSocket();
																												socket.Connect(m_address);

																												m_clientSockets.Add(socket);

																												return socket;
																											});

			m_schedulerThread = new ThreadLocal<bool>(() => false);

			if (m_ownPoller)
			{
				Task.Factory.StartNew(m_poller.Start, TaskCreationOptions.LongRunning);
			}
		}

		private void OnMessageFirstTime(object sender, NetMQSocketEventArgs e)
		{
			// set the current thread as the scheduler thread, this only happen the first time message arrived and important for the TryExecuteTaskInline
			m_schedulerThread.Value = true;

			// stop calling the OnMessageFirstTime and start calling OnMessage
			m_serverSocket.ReceiveReady -= m_currentMessageHandler;
			m_currentMessageHandler = OnMessage;
			m_serverSocket.ReceiveReady += m_currentMessageHandler;
			
			OnMessage(sender, e);
		}

		private void OnMessage(object sender, NetMQSocketEventArgs e)
		{			
			byte[] data = m_serverSocket.Receive();

			IntPtr address;

			// checking if 64bit or 32 bit
			if (data.Length == 8)
			{
				address = new IntPtr(BitConverter.ToInt64(data, 0));
			}
			else
			{
				address = new IntPtr(BitConverter.ToInt32(data, 0));
			}

			GCHandle handle = GCHandle.FromIntPtr(address);

			Task task = (Task)handle.Target;

			TryExecuteTask(task);

			handle.Free();
		}

		protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued)
		{
			return m_schedulerThread.Value && TryExecuteTask(task);
		}

		public override int MaximumConcurrencyLevel
		{
			get { return 1; }
		}

		public void Dispose()
		{
			// disposing on the scheduler thread
			Task task = new Task(DisposeSynced);
			task.Start(this);
			task.Wait();

			// poller cannot be stopped from poller thread
			if (m_ownPoller)
			{
				m_poller.Stop();
			}
		}

		private void DisposeSynced()
		{
			Thread.MemoryBarrier();

			m_poller.RemoveSocket(m_serverSocket);

			m_serverSocket.ReceiveReady -= m_currentMessageHandler;

			foreach (var clientSocket in m_clientSockets)
			{
				clientSocket.Dispose();
			}

			m_serverSocket.Dispose();
			m_clientSocket.Dispose();		
		}

		protected override IEnumerable<Task> GetScheduledTasks()
		{
			// this is not supported, also it's only important for debug propose and doesn't get called in real time
			throw new NotSupportedException();
		}

		protected override void QueueTask(Task task)
		{
			GCHandle handle = GCHandle.Alloc(task, GCHandleType.Normal);

			IntPtr address = GCHandle.ToIntPtr(handle);

			byte[] data;

			// checking if 64bit or 32 bit
			if (IntPtr.Size == 8)
			{
				data = BitConverter.GetBytes(address.ToInt64());
			}
			else
			{
				data = BitConverter.GetBytes(address.ToInt32());
			}

			m_clientSocket.Value.Send(data);
		}
	}
}
