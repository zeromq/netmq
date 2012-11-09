using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using zmq;

namespace NetMQ
{
    public interface IMonitoringEventsHandler
    {
        void OnConnected(string address, IntPtr fd);

        void OnConnectDelayed(string address, ErrorNumber errorCode);

        void OnConnectRetried(string address, int interval);

        void OnConnectFailed(string address, ErrorNumber errorCode);
        void OnListening(string address, IntPtr fd);


        void OnBindFailed(string address, ErrorNumber errorCode);

        void OnAccepted(string address, IntPtr fd);
        void OnAcceptFailed(string address, ErrorNumber errorCode);

        void OnClosed(string address, IntPtr fd);
        void OnCloseFailed(string address, ErrorNumber errorCode);
        void OnDisconnected(string address, IntPtr fd);
    }
}
