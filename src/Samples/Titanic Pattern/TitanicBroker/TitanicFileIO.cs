using System;
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
    ///     to a file in a standard or specified directory
    /// 
    ///     it creates it own infrastructure only if it does not exist
    ///     if it already exists it is assumed that this is a restart after
    ///     a crash and all information are deemed to be valid
    /// </summary>
    public class TitanicFileIO : ITitanicIO
    {
        private const string _titanic_dir = ".titanic";
        private const string _titanic_queue = "titanic.queue";
        private const int _size_of_entry = 17;

        /// <summary>
        ///     acts a synchronization root
        /// </summary>
        private readonly object m_syncRoot = new object ();

        /// <summary>
        ///     the hook for the handling of logging messages published
        /// </summary>
        public event EventHandler<TitanicLogEventArgs> LogInfoReady;

        /// <summary>
        ///     if a certain amount of "titanic.close" requests have been performed
        ///     the "titanic.queue" file needs a re-organization to remove the requests
        ///     marked as closed
        /// </summary>
        private int m_deleteCycles;

        // the queue will contain 1000 deleted marked requests before it is purged
        private readonly int m_thresholdForQueueDeletes = 10000;

        /// <summary>
        ///     represents the application directory
        /// </summary>
        private readonly string m_appDir;

        public string TitanicDirectory { get { return m_appDir; } }

        /// <summary>
        ///     represents the filename of the queue of requests
        /// </summary>
        private readonly string m_titanicQueue;

        public string TitanicQueue { get { return m_titanicQueue; } }

        /// <summary>
        ///     ctor - creation with all standard values
        /// 
        ///     app dir = application directory \ .titanic
        ///     titanic queue = 'app dir'\titanic.queue
        ///     purge threshold = 10,000 -> 170,000 bytes
        /// </summary>
        public TitanicFileIO ()
        {
            m_appDir = Path.Combine (AppDomain.CurrentDomain.BaseDirectory, _titanic_dir);
            m_titanicQueue = Path.Combine (m_appDir, _titanic_queue);

            CheckConfig ();
        }

        /// <summary>
        ///     ctor - creation with all standard values
        /// </summary>
        /// <param name="path">root path to titanic files, if empty or whitespace standards are used</param>
        public TitanicFileIO ([NotNull] string path)
            : this ()
        {
            if (string.IsNullOrWhiteSpace (path))
                return;

            m_appDir = path;
            m_titanicQueue = Path.Combine (m_appDir, _titanic_queue);

            CheckConfig ();
        }

        /// <summary>
        ///     ctor - creation with all standard values
        /// </summary>
        /// <param name="path">root path to titanic files</param>
        /// <param name="threshold">max delete cycles before compressing files</param>
        public TitanicFileIO ([NotNull] string path, int threshold)
            : this ()
        {
            if (!string.IsNullOrWhiteSpace (path))
            {
                m_appDir = path;
                m_titanicQueue = Path.Combine (m_appDir, _titanic_queue);

                CheckConfig ();
            }

            m_thresholdForQueueDeletes = threshold;
        }

        /// <summary>
        ///     make sure all the directories and files are existing
        ///     if they don't create them
        /// </summary>
        private void CheckConfig ()
        {
            if (!Directory.Exists (m_appDir))
                Directory.CreateDirectory (m_appDir);

            if (!File.Exists (m_titanicQueue))
            {
                // create file but release all resources immediately
                var f = File.Create (m_titanicQueue);
                f.Dispose ();
            }
        }

        #region REQUEST/REPLY QUEUE I/O HANDLING

        /// <summary>
        ///     get the request entry identified by the GUID from the infrastructure
        /// </summary>
        /// <param name="id"></param>
        /// <returns>a request entry or default(RequestEntry) if no request entry with the id exists</returns>
        public RequestEntry GetRequestEntry (Guid id) { return GetRequestEntries (e => e.RequestId == id).FirstOrDefault (); }

        /// <summary>
        ///     gets all existing request entries from the infrastructure satisfying the specified predicate
        /// </summary>
        /// <param name="predicate">the predicate to satisfy</param>
        /// <returns>a sequence of request entries</returns>
        public IEnumerable<RequestEntry> GetRequestEntries (Func<RequestEntry, bool> predicate)
        {
            lock (m_syncRoot)
            {
                using (var file = File.Open (m_titanicQueue, FileMode.Open, FileAccess.ReadWrite))
                {
                    return ReadRequestEntries (file).Where (predicate).ToArray ();
                }
            }
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
        public void SaveRequestEntry (RequestEntry entry)
        {
            lock (m_syncRoot)
            {
                using (var file = File.OpenWrite (m_titanicQueue))
                {
                    var source = CreateFileEntry (entry.RequestId, entry.State);

                    var position = entry.Position == -1 ? file.Seek (0, SeekOrigin.End) : entry.Position;

                    WriteRequest (file, source, file.Seek (position, SeekOrigin.Begin));
                }
            }
        }

        /// <summary>
        ///     save a new request under a GUID
        /// </summary>
        /// <param name="id">the id of the request</param>
        public void SaveNewRequestEntry (Guid id)
        {
            var entry = new RequestEntry () { RequestId = id, Position = -1, State = RequestEntry.Is_Pending };
            SaveRequestEntry (entry);
        }

        /// <summary>
        ///     save a new request under a GUID and saves the request data as well
        /// </summary>
        /// <param name="id">the id of the request</param>
        /// <param name="request">the request data to save</param>
        public void SaveNewRequestEntry (Guid id, NetMQMessage request)
        {
            // save the request data to file first and then
            SaveMessage (TitanicOperation.Request, id, request);
            // create the appropriate entry and
            var entry = new RequestEntry () { RequestId = id, Position = -1, State = RequestEntry.Is_Pending };
            // save the request to queue
            SaveRequestEntry (entry);
        }

        /// <summary>
        ///     save a processed request entry and mark it as such
        /// </summary>
        /// <param name="entry">the entry to save</param>
        public void SaveProcessedRequestEntry ([NotNull] RequestEntry entry)
        {
            entry.State = RequestEntry.Is_Processed;

            SaveRequestEntry (entry);
        }

        /// <summary>
        ///     mark a request entry identified by the specified GUID as closed
        ///     and purge queue if necessary
        /// </summary>
        /// <param name="id">the GUID of the request to close</param>
        public void CloseRequest (Guid id)
        {
            if (id == Guid.Empty)
                return;

            CloseMessage (id);

            m_deleteCycles++;

            MarkRequestClosed (id);
        }

        #endregion

        #region Message FIle I/O HANDLING

        /// <summary>
        ///     get a NetMQ message identified by a GUID from the infrastructure
        /// </summary>
        /// <param name="id">the guid of the message to read</param>
        /// <param name="op">defines whether it is a REQUEST or REPLY message</param>
        public NetMQMessage GetMessage (TitanicOperation op, Guid id)
        {
            var filename = op == TitanicOperation.Request ? GetRequestFileName (id) : GetReplyFileName (id);
            var message = new NetMQMessage ();

            using (var file = File.Open (filename, FileMode.Open))
            using (var r = new StreamReader (file))
            {
                string s;
                while ((s = r.ReadLine ()) != null)
                {
                    var b = Encoding.UTF8.GetBytes (s);
                    // first read -> first frame => append each read frame(!)
                    message.Append (b);
                }
            }

            return message;
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
        public bool SaveMessage (TitanicOperation op, Guid id, [NotNull] NetMQMessage message)
        {
            var filename = op == TitanicOperation.Request ? GetRequestFileName (id) : GetReplyFileName (id);

            using (var file = File.Open (filename, FileMode.OpenOrCreate))
            using (var w = new StreamWriter (file))
            {
                // save each frame as string -> upon reading it back use Read (string)
                for (var i = 0; i < message.FrameCount; i++)
                    w.WriteLine (message[i].ConvertToString ());
            }

            return true; // just needed for the async method(!)
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
        public bool ExistsMessage (TitanicOperation op, Guid id)
        {
            if (op == TitanicOperation.Close)
                return false;

            var filename = op == TitanicOperation.Request ? GetRequestFileName (id) : GetReplyFileName (id);

            return File.Exists (filename);
        }

        #endregion

        #region Helper Methods

        /// <summary>
        ///     delete message with a specific GUID
        /// </summary>
        /// <param name="id">the GUID of the request/reply to delete</param>
        private void CloseMessage (Guid id)
        {
            if (id == Guid.Empty)
                return;

            var reqFile = GetRequestFileName (id);
            var replyFile = GetReplyFileName (id);

            File.Delete (reqFile);
            File.Delete (replyFile);
        }

        /// <summary>
        ///     reads all request entries available
        /// </summary>
        private IEnumerable<RequestEntry> ReadRequestEntries ([NotNull] FileStream f)
        {
            var entries = new List<RequestEntry> ();
            var position = 0;

            foreach (var entry in ReadRawRequestEntries (f))
            {
                entries.Add (new RequestEntry
                {
                    State = entry[0],
                    Position = position,
                    RequestId = GetGuidFromStoredRequest (entry)
                });
                position += _size_of_entry;
            }

            return entries;
        }

        /// <summary>
        ///     reads the bytes a request entry consists of
        /// </summary>
        /// <returns>a sequence of those raw entries</returns>
        private static List<byte[]> ReadRawRequestEntries ([NotNull] FileStream f)
        {
            var entries = new List<byte[]> ();
            var target = new byte[_size_of_entry];

            f.Seek (0, SeekOrigin.Begin);

            while (f.Read (target, 0, _size_of_entry) != 0)
            {
                entries.Add (target);
                target = new byte[_size_of_entry];
            }

            return entries;
        }

        /// <summary>
        ///     searches for the entry in titanic.queue and marks it as closed
        /// </summary>
        private void MarkRequestClosed (Guid id)
        {
            var request = GetRequestEntry (id);

            // entry does not exist
            if (request == default (RequestEntry))
                return;

            request.State = RequestEntry.Is_Closed;

            SaveRequestEntry (request);

            if (m_deleteCycles >= m_thresholdForQueueDeletes)
                PurgeQueue ();
        }

        /// <summary>
        ///     re-create the file from the existing requests and replies
        /// </summary>
        /// <remarks>
        ///     this method locks the file for exclusive access in order to prevent 
        ///     any change during the recreation of the file
        ///     it reads all entries and collects any entry but the closed once
        ///     then it resets the file size to 0 and writes the collected entries 
        ///     to the file and releases the exclusive access
        /// </remarks>
        private void PurgeQueue ()
        {
            lock (m_syncRoot)
            {
                using (var file = File.Open (m_titanicQueue, FileMode.Open, FileAccess.ReadWrite))
                {
                    // get all NOT closed requests
                    var entries = ReadRawRequestEntries (file).Where (e => e[0] != RequestEntry.Is_Closed);
                    // reset the file, in order to recreate
                    file.SetLength (0);

                    // write all collected entries to queue -> will only be the not closed one
                    foreach (var entry in entries)
                    {
                        // write the entry at the current position in the file
                        file.Write (entry, 0, entry.Length);
                    }
                }
            }
        }

        /// <summary>
        ///     writes an request to file at a specified position
        /// </summary>
        /// <param name="f">FileStream</param>
        /// <param name="b">byte sequence to write</param>
        /// <param name="pos">the 0 based position from the beginning of the file</param>
        private static void WriteRequest ([NotNull] FileStream f, [NotNull] byte[] b, long pos)
        {
            f.Seek (pos, SeekOrigin.Begin);
            f.Write (b, 0, b.Length);
        }

        /// <summary>
        ///     accesses the enbedded Guid of a raw request entry
        /// </summary>
        /// <param name="bytes">the raw request entry as byte sequence</param>
        /// <returns>the enbedded Guid</returns>
        private static Guid GetGuidFromStoredRequest ([NotNull] byte[] bytes)
        {
            var guid = new byte[16];

            Array.Copy (bytes, 1, guid, 0, 16);

            return new Guid (guid);
        }

        /// <summary>
        ///     creates a raw request file entry
        /// </summary>
        /// <param name="id">Guid of the request</param>
        /// <param name="state">is either IsProcessed, IsPending or IsClosed</param>
        /// <returns></returns>
        private byte[] CreateFileEntry (Guid id, byte state)
        {
            var b = id.ToByteArray ();

            return AddByteAtStart (b, state);
        }

        /// <summary>
        ///     adds a byte to the specified sequence of bytes at the beginning
        /// </summary>
        /// <param name="bytes">sequence of bytes</param>
        /// <param name="b">byte to add</param>
        private static byte[] AddByteAtStart ([NotNull] byte[] bytes, byte b)
        {
            var target = new byte[bytes.Length + 1];

            target[0] = b;

            Array.Copy (bytes, 0, target, 1, 16);

            return target;
        }

        /// <summary>
        ///     return the full path + filename for a request
        /// </summary>
        private string GetRequestFileName (Guid guid)
        {
            return Path.Combine (m_appDir, guid + ".request");
        }

        /// <summary>
        ///     return the full path + filename for a reply
        /// </summary>
        private string GetReplyFileName (Guid guid)
        {
            return Path.Combine (m_appDir, guid + ".reply");
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
