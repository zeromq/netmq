using System;
using System.Threading;
using NetMQ.Sockets;

namespace NetMQ.Core.Utils
{
    internal class StopSignaler : ISocketPollable, IDisposable
    {
        private readonly PairSocket m_writer;
        private readonly PairSocket m_reader;
        private int m_isDisposed;

        public StopSignaler()
        {
            PairSocket.CreateSocketPair(out m_writer, out m_reader);
            m_reader.ReceiveReady += delegate
            {
                m_reader.SkipFrame();
                IsStopRequested = true;
            };

            IsStopRequested = false;
        }

        public bool IsStopRequested { get; private set; }

        NetMQSocket ISocketPollable.Socket => m_reader;

        public bool IsDisposed => m_isDisposed != 0;

        public void Dispose()
        {
            if (Interlocked.CompareExchange(ref m_isDisposed, 1, 0) != 0)
                return;
            m_reader.Dispose();
            m_writer.Dispose();
        }

        public void Reset()
        {
            IsStopRequested = false;
        }

        public void RequestStop()
        {
            lock (m_writer)
            {
                m_writer.SignalOK();
            }
        }
    }
}
