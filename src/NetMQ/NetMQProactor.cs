using System;
using NetMQ.Sockets;

namespace NetMQ
{
    /// <summary>
    /// Class to quickly handle incoming messages of socket.
    /// New thread is created to handle the messages. Call dispose to stop the thread.
    /// Provided socket will not be disposed by the class.
    /// </summary>
    public class NetMQProactor : IDisposable
    {
        private readonly NetMQActor m_actor;
        private readonly NetMQSocket m_receiveSocket;
        private readonly Action<NetMQSocket, NetMQMessage> m_handler;
        private NetMQPoller m_poller;

        /// <summary>
        /// Create NetMQProactor and start dedicate thread to handle incoming messages.
        /// </summary>
        /// <param name="receiveSocket">Socket to handle messages from</param>
        /// <param name="handler">Handler to handle incoming messages</param>
        public NetMQProactor(NetMQSocket receiveSocket, Action<NetMQSocket, NetMQMessage> handler)
        {
            m_receiveSocket = receiveSocket;
            m_handler = handler;
            m_actor = NetMQActor.Create(Run);
        }

        /// <summary>
        /// Stop the proactor. Provided socket will not be disposed.
        /// </summary>
        public void Dispose()
        {
            m_actor.Dispose();
            m_poller?.Dispose();
        }

        private void Run(PairSocket shim)
        {
            shim.ReceiveReady += OnShimReady;
            m_receiveSocket.ReceiveReady += OnSocketReady;
            m_poller = new NetMQPoller { m_receiveSocket, shim };

            shim.SignalOK();
            m_poller.Run();

            m_receiveSocket.ReceiveReady -= OnSocketReady;
        }

        private void OnShimReady(object sender, NetMQSocketEventArgs e)
        {
            string command = e.Socket.ReceiveFrameString();
            if (command == NetMQActor.EndShimMessage)
            {
                m_poller.Stop();
            }
        }

        private void OnSocketReady(object sender, NetMQSocketEventArgs e)
        {
            NetMQMessage message = m_receiveSocket.ReceiveMultipartMessage();

            m_handler(m_receiveSocket, message);
        }
    }
}
