using System;
using System.Threading;

namespace NetMQ.Core.Utils
{
    internal class Switch : IDisposable
    {
        private ManualResetEvent m_offEvent;
        private ManualResetEvent m_onEvent;
        private volatile bool m_status;

        public Switch(bool status)
        {
            m_offEvent = new ManualResetEvent(!status);
            m_onEvent = new ManualResetEvent(status);
            m_status = status;
        }

        public bool Status
        {
            get { return m_status; }
        }

        public void WaitForOff()
        {
            m_offEvent.WaitOne();
        }

        public void WaitForOn()
        {
            m_onEvent.WaitOne();
        }

        public void SwitchOn()
        {
            if (!m_status)
            {                
                m_offEvent.Reset();
                m_status = true;
                m_onEvent.Set();
            }
        }

        public void SwitchOff()
        {
            if (m_status)
            {
                m_onEvent.Reset();
                m_status = false;
                m_offEvent.Set();
            }
        }

        public void Dispose()
        {
            m_offEvent.Close();
            m_onEvent.Close();
        }
    }
}
