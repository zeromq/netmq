using System;
using System.Threading;

namespace NetMQ.Core.Utils
{
    internal class Switch
    {
        private readonly object m_sync;
        private volatile bool m_status;

        public Switch(bool status)
        {
            m_sync = new object();
            m_status = status;
        }

        public bool Status
        {
            get
            {
                lock (m_sync)
                {
                    return m_status;
                }
            }
        }

        public void WaitForOff()
        {
            lock (m_sync)
            {
                // while the status is on
                while (m_status)
                {
                    Monitor.Wait(m_sync);
                }
            }
        }

        public void WaitForOn()
        {
            lock (m_sync)
            {
                // while the status is off
                while (!m_status)
                {
                    Monitor.Wait(m_sync);
                }
            }
        }

        public void SwitchOn()
        {
            lock (m_sync)
            {
                if (!m_status)
                {
                    m_status = true;
                    Monitor.PulseAll(m_sync);
                }
            }

        }

        public void SwitchOff()
        {
            lock (m_sync)
            {
                if (m_status)
                {
                    m_status = false;
                    Monitor.PulseAll(m_sync);
                }
            }
        }
    }
}
