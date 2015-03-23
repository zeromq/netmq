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
    ///     this static class handles the I/O for TITANIC
    ///     if allows to transparently write, read, find and delete entries
    /// 
    ///     it creates it own infrastructure only if it does not exist
    ///     if it already exists it is assumed that this is a restart after
    ///     a crash and all information are deemed to be valid
    /// </summary>
    internal class TitanicIO
    {
        private const string _titanic_dir = ".titanic";
        private const string _titanic_queue = "titanic.queue";
        private const int _size_of_entry = 17;

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
        public TitanicIO ()
        {
            m_appDir = Path.Combine (AppDomain.CurrentDomain.BaseDirectory, _titanic_dir);
            m_titanicQueue = Path.Combine (m_appDir, _titanic_queue);

            CheckConfig ();
        }

        /// <summary>
        ///     ctor - creation with all standard values
        /// </summary>
        /// <param name="path">root path to titanic files</param>
        public TitanicIO ([NotNull] string path)
        {
            m_appDir = path;
            m_titanicQueue = Path.Combine (m_appDir, _titanic_queue);

            CheckConfig ();
        }

        /// <summary>
        ///     ctor - creation with all standard values
        /// </summary>
        /// <param name="path">root path to titanic files</param>
        /// <param name="threshold">max delete cycles before compressing files</param>
        public TitanicIO ([NotNull] string path, int threshold)
        {
            m_appDir = path;
            m_titanicQueue = Path.Combine (m_appDir, _titanic_queue);
            m_thresholdForQueueDeletes = threshold;

            CheckConfig ();
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
                File.Create (m_titanicQueue).Dispose ();    // create file but release all ressources immediately
        }

        #region REQUEST/REPLY I/O HANDLING

        /// <summary>
        ///     returns an iterable sequence of all request entries or an empty sequence
        /// </summary>
        public IEnumerable<RequestEntry> RetrieveRequests ()
        {
            long pos = 0;
            RequestEntry result;

            while ((result = ReadRequestEntry (pos)) != null)
            {
                pos = result.Position + _size_of_entry;

                yield return result;
            }
        }

        /// <summary>
        ///     finds a specific request identified with a GUID
        /// </summary>
        /// <param name="id">the id of the request to search for</param>
        /// <returns>the request entry or <c>default (RequestEntry)</c> if none was found</returns>
        public RequestEntry FindRequest (Guid id)
        {
            return id == Guid.Empty
                       ? default (RequestEntry)
                       : RetrieveRequests ().SingleOrDefault (r => r.RequestId == id);
        }

        /// <summary>
        ///     save a new request under a GUID
        /// </summary>
        /// <param name="id">the id of the request</param>
        public void SaveNewRequest (Guid id)
        {
            using (var file = File.OpenWrite (m_titanicQueue))
            {
                var source = CreateFileEntry (id, RequestEntry.Is_Pending);

                WriteRequest (file, source, file.Seek (0, SeekOrigin.End));
            }
        }

        /// <summary>
        ///     save a processed request entry and mark it as such
        /// </summary>
        /// <param name="entry">the entry to save</param>
        public void SaveProcessedRequest ([NotNull] RequestEntry entry)
        {
            entry.State = RequestEntry.Is_Processed;

            SaveRequest (entry);
        }

        #endregion REQUEST/REPLY I/O HANDLING

        #region NetMQMessage I/O HANDLING

        /// <summary>
        ///     read a NetMQ message identified by a GUID
        /// </summary>
        /// <param name="id">the guid of the message to read</param>
        /// <param name="op">defines whether it is a REQUEST or REPLY message</param>
        public NetMQMessage RetrieveMessage (TitanicOperation op, Guid id)
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
        /// <param name="id">the guid of the message to read</param>
        /// <param name="op">defines whether it is a REQUEST or REPLY message</param>
        public Task<NetMQMessage> RetrieveMessageAsync (TitanicOperation op, Guid id)
        {
            var tcs = new TaskCompletionSource<NetMQMessage> ();

            try
            {
                tcs.SetResult (RetrieveMessage (op, id));
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
        public bool Exists (TitanicOperation op, Guid id)
        {
            if (op == TitanicOperation.Close)
                return false;

            var filename = op == TitanicOperation.Request ? GetRequestFileName (id) : GetReplyFileName (id);

            return File.Exists (filename);
        }

        /// <summary>
        ///     mark a request with a GUID as closed
        /// </summary>
        /// <param name="id">the GUID of the request to close</param>
        public void CloseRequest (Guid id)
        {
            if (id == Guid.Empty)
                return;

            var reqFile = GetRequestFileName (id);
            var replyFile = GetReplyFileName (id);

            File.Delete (reqFile);
            File.Delete (replyFile);

            m_deleteCycles++;

            MarkRequestClosed (id);
        }

        #endregion

        /// <summary>
        ///     searches for the entry in titanic.queue and marks it as closed
        /// </summary>
        private void MarkRequestClosed (Guid id)
        {
            var request = FindRequest (id);

            // entry does not exist
            if (request == default (RequestEntry))
                return;

            request.State = RequestEntry.Is_Closed;

            SaveRequest (request);

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
            var entries = new List<byte[]> ();

            using (var file = File.Open (m_titanicQueue, FileMode.Open, FileAccess.ReadWrite))
            {
                var target = new byte[_size_of_entry];

                // get exclusive file access - no other thread can write to that file
                file.Lock (0, file.Length);

                file.Seek (0, SeekOrigin.Begin);

                // 0 == End Of File and Positin is automatically advanced
                while (file.Read (target, 0, _size_of_entry) != 0)
                {
                    // collect all entries BUT the closed once
                    if (target[0] != RequestEntry.Is_Closed)
                        entries.Add (target);
                }
                // reset the file in order to re-create the content
                file.SetLength (0);
                // write all collected entries to queue -> will only be the not closed one
                for (var i = 0; i < entries.Count; i++)
                {
                    // write the entry at the current position in the file
                    file.Write (entries[i], 0, entries[i].Length);
                }
            }
        }

        /// <summary>
        ///     saves a request entry to file
        /// </summary>
        /// <param name="entry"></param>
        private void SaveRequest (RequestEntry entry)
        {
            using (var file = File.OpenWrite (m_titanicQueue))
            {
                var source = CreateFileEntry (entry.RequestId, entry.State);

                var pos = entry.Position;

                WriteRequest (file, source, pos);
            }
        }

        /// <summary>
        ///     read an entry from a specified position within Titanic.Queue
        /// </summary>
        /// <param name="pos">the position from the beginning in bytes - 0 based</param>
        /// <returns>the read request entry or null if EOF</returns>
        private RequestEntry ReadRequestEntry (long pos)
        {
            using (var file = File.OpenRead (m_titanicQueue))
            {
                return ReadRequestedEntry (pos, file);
            }
        }

        /// <summary>
        ///     read an entry from a specified position within a specified filestream
        /// </summary>
        /// <param name="pos">the position from the beginning in bytes - 0 based</param>
        /// <param name="f">the filestream to read from</param>
        /// <returns></returns>
        private RequestEntry ReadRequestedEntry (long pos, [NotNull] FileStream f)
        {
            var target = new byte[_size_of_entry];

            f.Seek (pos, SeekOrigin.Begin);

            var readBytes = f.Read (target, 0, _size_of_entry);

            return readBytes == 0
                       ? null
                       : new RequestEntry
                         {
                             State = target[0],
                             Position = pos,
                             RequestId = GetGuidFromStoredRequest (target)
                         };
        }

        /// <summary>
        ///     writes an request to file at a specified position
        /// </summary>
        /// <param name="f">FileStream</param>
        /// <param name="b">byte sequence to write</param>
        /// <param name="pos">the 0 based position from the beginning of the file</param>
        private void WriteRequest ([NotNull] FileStream f, [NotNull] byte[] b, long pos)
        {
            f.Seek (pos, SeekOrigin.Begin);
            f.Write (b, 0, b.Length);
        }

        /// <summary>
        ///     accesses the enbedded Guid of a raw request entry
        /// </summary>
        /// <param name="bytes">the raw request entry as byte sequence</param>
        /// <returns>the enbedded Guid</returns>
        private Guid GetGuidFromStoredRequest ([NotNull] byte[] bytes)
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
        private byte[] AddByteAtStart ([NotNull] byte[] bytes, byte b)
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

    }
}
