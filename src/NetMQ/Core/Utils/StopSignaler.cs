using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NetMQ.Sockets;

namespace NetMQ.Core.Utils
{
    class StopSignaler : ISocketPollable, IDisposable
    {
        private PairSocket m_writer;
        private PairSocket m_reader;

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

        NetMQSocket ISocketPollable.Socket
        {
            get
            {
                return m_reader;                
            }
        }

        public void Dispose()
        {
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
