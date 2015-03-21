using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using NetMQ;

using TitanicCommons;

namespace TitanicProtocol
{
    /// <summary>
    ///     this static class handles the I/O for TITANIC
    ///     if allows to transparently write, read, find and delete entries
    /// </summary>
    internal static class TitanicIO
    {
        private const string _titanic_dir = ".titanic";
        private const string _titanic_queue = "titanic.queue";
        private const int _size_of_entry = 17;
        // the queue will contain 1000 deleted marked requests
        private const int _threshold_for_queue_deletes = 1000;

        /// <summary>
        ///     if a certain amount of "titanic.close" requests have been performed
        ///     the "titanic.queue" file needs a re-organization to remove the requests
        ///     marked as closed
        /// </summary>
        private static int s_deleteCycles;

        /// <summary>
        ///     represents the application directory
        /// </summary>
        private static string s_appDir = Path.Combine (AppDomain.CurrentDomain.BaseDirectory, _titanic_dir);

        /// <summary>
        ///     represents the filename of the queue of requests
        /// </summary>
        private static string s_titanicQueue = Path.Combine (s_appDir, _titanic_queue);

        /// <summary>
        ///     make sure all the directories and files are existing
        ///     if they don't create them
        /// </summary>
        public static void CheckConfig ()
        {
            if (!Directory.Exists (s_appDir))
                Directory.CreateDirectory (s_appDir);

            if (!File.Exists (s_titanicQueue))
                File.Create (s_titanicQueue);
        }

        /// <summary>
        ///     set a specific path for the files to be created under
        /// </summary>
        public static void SetConfig (string path)
        {
            s_appDir = Path.Combine (path, _titanic_dir);
            s_titanicQueue = Path.Combine (s_appDir, _titanic_queue);

            CheckConfig ();
        }

        #region REQUEST I/O HANDLING

        /// <summary>
        ///     returns an iterable sequence of request entries
        /// </summary>
        public static IEnumerable<RequestEntry> RetrieveRequests ()
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
        ///     finds a specific request in a specified file
        /// </summary>
        /// <param name="id">the id of the request to search for</param>
        /// <returns>the request entry or <c>default (RequestEntry)</c> if none was found</returns>
        public static RequestEntry FindRequest (Guid id)
        {
            return RetrieveRequests ().SingleOrDefault (r => r.RequestId == id);
        }

        /// <summary>
        ///     appends a new request to titanic.queue
        /// </summary>
        /// <param name="id">the id of the request</param>
        public static void SaveNewRequest (Guid id)
        {
            using (var file = File.OpenWrite (s_titanicQueue))
            {
                var source = CreateFileEntry (id, RequestEntry.Is_Pending);

                WriteRequest (file, source, file.Seek (0, SeekOrigin.End));
            }
        }

        /// <summary>
        ///     writes a processed request entry to a file at a specified 
        ///     position within that file
        /// </summary>
        /// <param name="entry">the entry to write</param>
        public static void SaveProcessedRequest (RequestEntry entry)
        {
            using (var file = File.OpenWrite (s_titanicQueue))
            {
                var source = CreateFileEntry (entry.RequestId, entry.State);

                var pos = entry.Position;

                WriteRequest (file, source, pos);
            }
        }

        #endregion REQUEST

        #region NetMQMessage File I/O HANDLING

        /// <summary>
        ///     read a NetMQ message from a file
        /// </summary>
        /// <param name="id">the guid of the message to read</param>
        /// <param name="op">defines whether it is a REQUEST or REPLY message</param>
        public static NetMQMessage RetrieveMessage (TitanicOperation op, Guid id)
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
        ///     read a NetMQ message from a file asynchronous
        /// </summary>
        /// <param name="id">the guid of the message to read</param>
        /// <param name="op">defines whether it is a REQUEST or REPLY message</param>
        public static Task<NetMQMessage> RetrieveMessageAsync (TitanicOperation op, Guid id)
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
        ///     write a NetMQ message to a file
        /// </summary>
        /// <param name="op">defines whether it is a REQUEST or REPLY message</param>
        /// <param name="id">the guid of the message to save</param>
        /// <param name="message">the message to save</param>
        public static bool SaveMessage (TitanicOperation op, Guid id, NetMQMessage message)
        {
            var filename = op == TitanicOperation.Request ? GetRequestFileName (id) : GetReplyFileName (id);

            using (var file = File.Open (filename, FileMode.OpenOrCreate))
            using (var w = new StreamWriter (file))
            {
                // save each frame as string -> upon reading it back use Read (string)
                for (var i = 0; i < message.FrameCount; i++)
                    w.WriteLine (message[i].ConvertToString ());
            }

            return true;    // just needed for the async method(!)
        }

        /// <summary>
        ///     write asynchronously a NetMQ message to a file
        /// </summary>
        /// <param name="op">defines whether it is a REQUEST or REPLY message</param>
        /// <param name="id">the guid of the message to save</param>
        /// <param name="message">the message to save</param>
        public static Task<bool> SaveMessageAsync (TitanicOperation op, Guid id, NetMQMessage message)
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
        ///     test if a file for either a request or reply exists
        /// </summary>
        /// <param name="op">commands whether it is for a REQUEST or a REPLY</param>
        /// <param name="id">the GUID für the Request/Reply</param>
        /// <returns>
        ///         true if it exists and false otherwise and if 'op' is not one 
        ///         of the aforementioned
        /// </returns>
        public static bool Exists (TitanicOperation op, Guid id)
        {
            if (op == TitanicOperation.Close)
                return false;

            var filename = op == TitanicOperation.Request ? GetRequestFileName (id) : GetReplyFileName (id);

            return File.Exists (filename);
        }

        /// <summary>
        ///     mark a request as closed
        /// </summary>
        /// <param name="id"></param>
        public static void CloseRequest (Guid id)
        {
            var reqFile = GetRequestFileName (id);
            var replyFile = GetReplyFileName (id);

            File.Delete (reqFile);
            File.Delete (replyFile);

            s_deleteCycles++;

            MarkRequestClosed (id);
        }

        #endregion

        /// <summary>
        ///     searches for the entry in titanic.queue and marks it as closed
        /// </summary>
        private static void MarkRequestClosed (Guid id)
        {
            var request = FindRequest (id);

            request.State = RequestEntry.Is_Closed;

            SaveProcessedRequest (request);

            if (s_deleteCycles > _threshold_for_queue_deletes)
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
        private static void PurgeQueue ()
        {
            var entries = new List<RequestEntry> ();

            using (var file = File.Open (_titanic_queue, FileMode.Open, FileAccess.ReadWrite))
            {
                var target = new byte[_size_of_entry];

                // get exclusive file access - no other thread can write to that file
                file.Lock (0, file.Length);

                file.Seek (0, SeekOrigin.Begin);

                // 0 == End Of File and Positin is automatically advanced
                while (file.Read (target, (int) file.Position, _size_of_entry) != 0)
                {
                    // collect all entries BUT the closed once
                    if (target[0] != RequestEntry.Is_Closed)
                        entries.Add (new RequestEntry
                                     {
                                         State = target[0],
                                         // Position is int this case of no importance!
                                         // Position is recreated in new file anyway
                                         RequestId = GetGuidFromStoredRequest (target)
                                     });
                }
                // reset the file in order to re-create the content
                file.SetLength (0);
                // write all collected entries to queue -> will only be the not closed one
                foreach (var source in entries.Select (entry => CreateFileEntry (entry.RequestId, entry.State)))
                {
                    // write the entry at the current position in the file
                    file.Write (source, 0, source.Length);
                }

                // release the file for others to access
                file.Unlock (0, file.Length);
            }
        }

        /// <summary>
        ///     read an entry of the specified file from a specified position
        ///     within the file
        /// </summary>
        /// <param name="pos">the position from the beginning in bytes - 0 based</param>
        /// <returns>the read request entry or null if EOF</returns>
        private static RequestEntry ReadRequestEntry (long pos)
        {
            using (var file = File.OpenRead (s_titanicQueue))
            {
                return ReadRequestedEntry (pos, file);
            }
        }

        private static RequestEntry ReadRequestedEntry (long pos, FileStream f)
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
        private static void WriteRequest (FileStream f, byte[] b, long pos)
        {
            f.Seek (pos, SeekOrigin.Begin);
            f.Write (b, 0, b.Length);
        }

        /// <summary>
        ///     accesses the enbedded Guid of a raw request entry
        /// </summary>
        /// <param name="bytes">the raw request entry as byte sequence</param>
        /// <returns>the enbedded Guid</returns>
        private static Guid GetGuidFromStoredRequest (byte[] bytes)
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
        private static byte[] CreateFileEntry (Guid id, byte state)
        {
            var b = id.ToByteArray ();

            return AddByteAtStart (b, state);
        }

        /// <summary>
        ///     adds a byte to the specified sequence of bytes at the beginning
        /// </summary>
        /// <param name="bytes">sequence of bytes</param>
        /// <param name="b">byte to add</param>
        private static byte[] AddByteAtStart (byte[] bytes, byte b)
        {
            var target = new byte[bytes.Length + 1];

            target[0] = b;

            Array.Copy (bytes, 0, target, 1, 16);

            return target;
        }

        /// <summary>
        ///     return the full path + filename for a request
        /// </summary>
        private static string GetRequestFileName (Guid guid)
        {
            return Path.Combine (s_appDir, guid + ".request");
        }

        /// <summary>
        ///     return the full path + filename for a reply
        /// </summary>
        private static string GetReplyFileName (Guid guid)
        {
            return Path.Combine (s_appDir, guid + ".reply");
        }

    }
}
