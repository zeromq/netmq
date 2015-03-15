using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NetMQ;

namespace TitanicProtocol
{
    /// <summary>
    ///     this static class handles the I/O for TITANIC
    ///     if allows to transparently write, read, find and delete entries
    /// </summary>
    internal static class TitanicIO
    {
        #region REQUEST I/O HANDLING

        private const int _SIZE_OF_ENTRY = 17;

        /// <summary>
        ///     grants access to the next non-processed request
        /// </summary>
        /// <param name="filename">the name of the file to work on</param>
        /// <returns>an iterable sequence of request entries</returns>
        public static IEnumerable<RequestEntry> ReadNextNonProcessedRequest (string filename)
        {
            RequestEntry result;

            long pos = 0;

            while ((result = ReadRequestEntry (filename, pos)) != null)
            {
                if (!result.IsProcessed)
                {
                    pos = result.Position + _SIZE_OF_ENTRY;
                    yield return result;
                }
            }
        }

        /// <summary>
        ///     returns an iterable sequence of request entries in a specified file
        /// </summary>
        /// <param name="filename">the name of the file to work on</param>
        public static IEnumerable<RequestEntry> ReadRequests (string filename)
        {
            long pos = 0;
            RequestEntry result;

            while ((result = ReadRequestEntry (filename, pos)) != null)
            {
                pos = result.Position + _SIZE_OF_ENTRY;

                yield return result;
            }
        }

        /// <summary>
        ///     finds a specific request in a specified file
        /// </summary>
        /// <param name="filename">the name of the file to work on</param>
        /// <param name="id">the id of the request to search for</param>
        /// <returns>the request entry or <c>default (RequestEntry)</c> if none was found</returns>
        public static RequestEntry FindRequest (string filename, Guid id)
        {
            return ReadRequests (filename).SingleOrDefault (r => r.RequestId == id);
        }

        /// <summary>
        ///     appends a new request to the specified file
        /// </summary>
        /// <param name="filename">the name of the file</param>
        /// <param name="id">the id of the request</param>
        public static void WriteNewRequest (string filename, Guid id)
        {
            using (var file = File.OpenWrite (filename))
            {
                var source = CreateFileEntry (id, false);

                WriteRequest (file, source, file.Seek (0, SeekOrigin.End));
            }
        }

        /// <summary>
        ///     writes a processed request entry to a file at a specified 
        ///     position within that file
        /// </summary>
        /// <param name="filename">the name of the file</param>
        /// <param name="entry">the entry to write</param>
        public static void WriteProcessedRequest (string filename, RequestEntry entry)
        {
            using (var file = File.OpenWrite (filename))
            {
                var source = CreateFileEntry (entry.RequestId, entry.IsProcessed);

                var pos = entry.Position;

                WriteRequest (file, source, pos);
            }
        }

        /// <summary>
        ///     read an entry of the specified file from a specified position
        ///     within the file
        /// </summary>
        /// <param name="filename">the name of the file</param>
        /// <param name="pos">the position from the beginning in bytes - 0 based</param>
        /// <returns>the read request entry or null if EOF</returns>
        private static RequestEntry ReadRequestEntry (string filename, long pos)
        {
            using (var file = File.OpenRead (filename))
            {
                var target = new byte[_SIZE_OF_ENTRY];

                file.Seek (pos, SeekOrigin.Begin);

                var readBytes = file.Read (target, 0, _SIZE_OF_ENTRY);

                return readBytes == 0
                           ? null
                           : new RequestEntry
                             {
                                 IsProcessed = target[0] == (byte) '+',
                                 Position = pos,
                                 RequestId = GetGuidFromStoredRequest (target)
                             };
            }
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
        /// <param name="isProcessed">true id already processed, false otherwise</param>
        /// <returns></returns>
        private static byte[] CreateFileEntry (Guid id, bool isProcessed)
        {
            var b = id.ToByteArray ();
            var flag = (byte) (isProcessed ? '+' : '-');

            return AddByteAtStart (b, flag);
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

        #endregion REQUEST

        #region NetMQMessage File I/O HANDLING
        
        /// <summary>
        ///     read a NetMQ message from a file
        /// </summary>
        public static NetMQMessage ReadMessageFromFile (string filename)
        {
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
        ///     write a NetMQ message to a file
        /// </summary>
        public static void WriteMessageToFile (string filename, NetMQMessage message)
        {
            using (var file = File.Open (filename, FileMode.OpenOrCreate))
            using (var w = new StreamWriter (file))
            {
                // save each frame as string -> upon reading it back use Read (string)
                for (var i = 0; i < message.FrameCount; i++)
                    w.WriteLine (message[i].ConvertToString ());
            }
        }

        #endregion REPLY
    }
}
