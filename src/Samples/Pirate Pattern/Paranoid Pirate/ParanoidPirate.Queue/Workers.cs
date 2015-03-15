using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NetMQ;

namespace ParanoidPirate.Queue
{
    public class Workers : IEnumerable<Worker>
    {
        private readonly List<Worker> m_workers = new List<Worker>();

        /// <summary>
        /// true if there are workers available
        /// </summary>
        public bool Available
        {
            get { return m_workers.Count > 0; }
        }

        /// <summary>
        /// stores a worker for a LRU pattern
        /// </summary>
        public void Ready(Worker worker)
        {
            m_workers.Add(worker);
        }

        /// <summary>
        /// a NetMQFrame with the identity of the next available worker 
        /// or null if no worker is available
        /// </summary>
        public NetMQFrame Next()
        {
            if (m_workers.Count == 0)
                return null;

            // get the oldest worker
            var worker = m_workers.Last();
            // remove it from list
            m_workers.Remove(worker);

            return worker.Identity;
        }

        /// <summary>
        /// removes every worker which has exceeded his livetime
        /// </summary>
        public void Purge()
        {
            foreach (var worker in m_workers.Where(worker => worker.Expiry >= DateTime.UtcNow))
                m_workers.Remove(worker);
        }

        public IEnumerator<Worker> GetEnumerator()
        {
            return m_workers.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}