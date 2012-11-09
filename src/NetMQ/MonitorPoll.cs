using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using zmq;
using System.Threading;

namespace NetMQ
{
    public class MonitorPoll
    {
        bool m_creatingMonitoringSocket;
        IMonitoringEventsHandler m_monitoringEventsHandler;

        MonitorEvent m_monitorEvent;
        CancellationTokenSource m_cancellationTokenSource = new CancellationTokenSource();

        public MonitorPoll(Context context, BaseSocket socket, string address, 
            SocketEvent socketEvent, IMonitoringEventsHandler monitoringEventsHandler,
            bool createMonitoringSocket)
        {
            Socket = socket;
            MonitorAddress = address;
            Events = socketEvent;
            m_creatingMonitoringSocket = createMonitoringSocket;
            m_monitoringEventsHandler = monitoringEventsHandler;
            Context = context;
            Timeout = TimeSpan.FromSeconds(1);
        }

        public SocketEvent Events { get; private set; }

        public string MonitorAddress { get; private set; }

        public BaseSocket Socket { get; private set; }

        public Context Context { get; private set; }

        public TimeSpan Timeout { get; set; }

        internal BaseSocket MonitoringSocket { get; private set; } 

        internal void CreateMonitoringSocket()
        {
            if (m_creatingMonitoringSocket)
            {
                ZMQ.SocketMonitor(Socket.SocketHandle, MonitorAddress, Events);
            }

            MonitoringSocket = Context.CreatePairSocket();

            MonitoringSocket.Connect(MonitorAddress);
        }

        internal void Handle()
        {
            MonitorEvent monitorEvent = MonitorEvent.Read(MonitoringSocket.SocketHandle);

            switch (monitorEvent.Event)
            {
                case SocketEvent.Connected:
                    m_monitoringEventsHandler.OnConnected(monitorEvent.Addr, (IntPtr) monitorEvent.Arg);
                    break;
                case SocketEvent.ConnectDelayed:
                    m_monitoringEventsHandler.OnConnectFailed(monitorEvent.Addr, new ErrorNumber((int)monitorEvent.Arg));
                    break;
                case SocketEvent.ConnectRetried:
                    m_monitoringEventsHandler.OnConnectRetried(monitorEvent.Addr, (int)monitorEvent.Arg);
                    break;
                case SocketEvent.ConnectFailed:
                    m_monitoringEventsHandler.OnConnectFailed(monitorEvent.Addr, new ErrorNumber((int)monitorEvent.Arg));
                    break;
                case SocketEvent.Listening:
                    m_monitoringEventsHandler.OnListening(monitorEvent.Addr, (IntPtr)monitorEvent.Arg);
                    break;
                case SocketEvent.BindFailed:
                    m_monitoringEventsHandler.OnBindFailed(monitorEvent.Addr, new ErrorNumber((int)monitorEvent.Arg));                    
                    break;
                case SocketEvent.Accepted:
                    m_monitoringEventsHandler.OnAccepted(monitorEvent.Addr, (IntPtr)monitorEvent.Arg);                    
                    break;
                case SocketEvent.AcceptFailed:
                    m_monitoringEventsHandler.OnAcceptFailed(monitorEvent.Addr, new ErrorNumber((int)monitorEvent.Arg));                    
                    break;
                case SocketEvent.Closed:
                    m_monitoringEventsHandler.OnClosed(monitorEvent.Addr, (IntPtr)monitorEvent.Arg);                    
                    break;
                case SocketEvent.CloseFailed:
                    m_monitoringEventsHandler.OnCloseFailed(monitorEvent.Addr, new ErrorNumber((int)monitorEvent.Arg));                    
                    break;
                case SocketEvent.Disconnected:
                    m_monitoringEventsHandler.OnDisconnected(monitorEvent.Addr, (IntPtr)monitorEvent.Arg);                    
                    break;
                
                default:
                    throw new Exception("unknown event " + monitorEvent.Event.ToString());                 
                    break;
            }
        }

        public void Start()
        {
            CreateMonitoringSocket();

            PollItem[] items = new PollItem[1];
            items[0] = new PollItem(MonitoringSocket.SocketHandle, PollEvents.PollIn);

            while (!m_cancellationTokenSource.IsCancellationRequested)
            {               
                if (ZMQ.Poll(items, (int)Timeout.TotalMilliseconds) > 0)
                {
                    Handle();
                }
            }
        }

        public void Stop()
        {
            m_cancellationTokenSource.Cancel();
        }
    }
}
