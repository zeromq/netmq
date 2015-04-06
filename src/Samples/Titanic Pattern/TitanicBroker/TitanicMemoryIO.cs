using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using JetBrains.Annotations;

using NetMQ;

using TitanicCommons;

namespace TitanicProtocol
{
    /// <summary>
    ///     this class handles the I/O for TITANIC
    ///     if allows to transparently write, read, find and delete entries
    ///     in memory (CocurrentQueue)
    /// 
    ///     it is fast but all information is lost in case this computer or
    ///     the process dies!
    /// </summary>
    internal class TitanicMemoryIO : ITitanicIO
    {
        private readonly ConcurrentDictionary<Guid, RequestEntry> m_titanicQueue;

        /// <summary>
        ///     the hook for the handling of logging messages published
        /// </summary>
        public event EventHandler<TitanicLogEventArgs> LogInfoReady;

        public string TitanicDirectory { get { return "In Memory processing!"; } }
        public string TitanicQueue { get { return "In Memory processing!"; } }

        /// <summary>
        ///     ctor
        /// </summary>
        public TitanicMemoryIO ()
        {
            m_titanicQueue = new ConcurrentDictionary<Guid, RequestEntry> ();
        }

        #region REQUEST/REPLY QUEUE I/O HANDLING

        /// <summary>
        ///     get the request entry identifyed by the GUID from the infrastructure
        /// </summary>
        /// <param name="id"></param>
        /// <returns>a request entry or default(RequestEntry) if no request entry with the id exists</returns>
        public RequestEntry GetRequestEntry (Guid id)
        {
            RequestEntry entry;

            return m_titanicQueue.TryGetValue (id, out entry) ? entry : default (RequestEntry);
        }

        /// <summary>
        ///     gets all existing request entries from the infrastructure satisfying the specified predicate
        /// </summary>
        /// <param name="predicate">the predicate to satisfy</param>
        /// <returns>a sequence of request entries if any or an empty sequence</returns>
        public IEnumerable<RequestEntry> GetRequestEntries ([NotNull] Func<RequestEntry, bool> predicate)
        {
            return m_titanicQueue.IsEmpty
                       ? default (IEnumerable<RequestEntry>)
                       : m_titanicQueue.Values.Where (predicate).ToArray ();
        }

        /// <summary>
        ///     gets all request entries from the infrastructure which are NOT closed
        ///     only State = (Is_Pending OR Is_Processed) are considered
        /// </summary>
        /// <returns>sequence of request entries</returns>
        public IEnumerable<RequestEntry> GetNotClosedRequestEntries ()
        {
            return GetRequestEntries (e => e.State != RequestEntry.Is_Closed);
        }

        /// <summary>
        ///     saves a request entry to the infrastructure
        /// </summary>
        /// <param name="entry">the request entry to save</param>
        /// <exception cref="OverflowException">max. number of elements exceeded, <see cref="F:System.Int32.MaxValue" />.</exception>
        /// <exception cref="ArgumentNullException"><paramref name="entry.RequestId" /> is a 'null' reference.</exception>
        public void SaveRequestEntry (RequestEntry entry)
        {
            m_titanicQueue.TryAdd (entry.RequestId, entry);
        }

        /// <summary>
        ///     save a new request under a GUID
        /// </summary>
        /// <param name="id">the id of the request</param>
        /// <exception cref="OverflowException">max. number of elements exceeded, <see cref="F:System.Int32.MaxValue" />.</exception>
        public void SaveNewRequestEntry (Guid id)
        {
            var entry = new RequestEntry () { RequestId = id, Position = -1, State = RequestEntry.Is_Pending };

            SaveRequestEntry (entry);
        }

        /// <summary>
        ///     save a processed request entry and mark it as such
        /// </summary>
        /// <param name="entry">the entry to save</param>
        /// <exception cref="OverflowException">max. number of elements exceeded, <see cref="F:System.Int32.MaxValue" />.</exception>
        public void SaveProcessedRequestEntry ([NotNull] RequestEntry entry)
        {
            entry.State = RequestEntry.Is_Processed;

            SaveRequestEntry (entry);
        }

        /// <summary>
        ///     remove a request from the queue
        /// </summary>
        /// <param name="id">the GUID of the request to close</param>
        public void CloseRequest (Guid id)
        {
            if (id == Guid.Empty)
                return;

            RequestEntry entry;

            m_titanicQueue.TryRemove (id, out entry);
        }

        #endregion

        #region Message HANDLING

        /// <summary>
        ///     get a NetMQ message identified by a GUID from the infrastructure
        /// </summary>
        /// <param name="id">the guid of the message to read, must not be empty</param>
        /// <param name="op">defines whether it is a REQUEST or REPLY message</param>
        public NetMQMessage GetMessage (TitanicOperation op, Guid id)
        {
            RequestEntry entry;

            return m_titanicQueue.TryGetValue (id, out entry) ? entry.Request : new NetMQMessage ();
        }

        /// <summary>
        ///     read a NetMQ message identified by a GUID asynchronously
        /// </summary>
        /// <param name="op">defines whether it is a REQUEST or REPLY message</param>
        /// <param name="id">the guid of the message to read</param>
        public Task<NetMQMessage> GetMessageAsync (TitanicOperation op, Guid id)
        {
            var tcs = new TaskCompletionSource<NetMQMessage> ();

            try
            {
                tcs.SetResult (GetMessage (op, id));
            }
            catch (Exception ex)
            {
                tcs.SetException (ex);
            }

            return tcs.Task;
        }

        /// <summary>
        ///     save a NetMQ message under a GUID
        /// </summary>
        /// <param name="op">defines whether it is a REQUEST or REPLY message</param>
        /// <param name="id">the guid of the message to save</param>
        /// <param name="message">the message to save</param>
        /// <exception cref="OverflowException">max. number of elements exceeded, <see cref="F:System.Int32.MaxValue" />.</exception>
        public bool SaveMessage (TitanicOperation op, Guid id, [NotNull] NetMQMessage message)
        {
            var entry = new RequestEntry
                        {
                            RequestId = id,
                            Request = message,
                            State = (byte) (op == TitanicOperation.Request ? '-' : '+')
                        };


            return m_titanicQueue.TryAdd (id, entry);
        }

        /// <summary>
        ///     save  a NetMQ message with a GUID asynchronously
        /// </summary>
        /// <param name="op">defines whether it is a REQUEST or REPLY message</param>
        /// <param name="id">the guid of the message to save</param>
        /// <param name="message">the message to save</param>
        public Task<bool> SaveMessageAsync (TitanicOperation op, Guid id, [NotNull] NetMQMessage message)
        {
            var tcs = new TaskCompletionSource<bool> ();

            try
            {
                tcs.SetResult (SaveMessage (op, id, message));
            }
            catch (Exception ex)
            {
                tcs.SetException (ex);
            }

            return tcs.Task;
        }

        /// <summary>
        ///     test if a request or reply with the GUID exists
        /// </summary>
        /// <param name="op">commands whether it is for a REQUEST or a REPLY</param>
        /// <param name="id">the GUID für the Request/Reply</param>
        /// <returns>
        ///         true if it exists and false otherwise and if 'op' is not one 
        ///         of the aforementioned
        /// </returns>
        public bool Exists (TitanicOperation op, Guid id)
        {
            byte state = (byte) (op == TitanicOperation.Request ? '-' : '+');

            return m_titanicQueue.Any (e => e.Value.RequestId == id && e.Value.State == state);
        }

        #endregion

        private void Log ([NotNull] string info)
        {
            OnLogInfoReady (new TitanicLogEventArgs { Info = info });
        }

        protected virtual void OnLogInfoReady (TitanicLogEventArgs e)
        {
            var handler = LogInfoReady;

            if (handler != null)
                handler (this, e);
        }
    }
}
