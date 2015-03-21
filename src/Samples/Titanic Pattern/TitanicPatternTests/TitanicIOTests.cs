using System;
using System.Collections;
using System.IO;
using System.Threading;
using FluentAssertions;
using NetMQ;
using NUnit.Framework;
using TitanicCommons;
using TitanicProtocol;

namespace TitanicProtocolTests
{
    [TestFixture]
    public class TitanicIOTests
    {
        /*
         *  IEnumerable<RequestEntry> RetrieveRequests ()
         *  RequestEntry FindRequest (Guid id)
         *  void SaveNewRequest (Guid id)
         *  void SaveProcessedRequest (RequestEntry entry)
         *  NetMQMessage RetrieveMessage (TitanicOperation op, Guid id)
         *  void SaveMessage (TitanicOperation op, Guid id, NetMQMessage message)
         *  bool Exists (TitanicOperation op, Guid id)
         *  void CloseRequest (Guid id)
         *  
         * Purge ()!
         *
         */

        private const string _titanic_dir = ".titanic";
        private const string _titanic_queue = "titanic.queue";
        private const string _request_ending = ".request";
        private const string _reply_ending = ".reply";

        [Test]
        public void SetConfig_ValidPath_ShouldCreateDirectoryAndFile ()
        {
            var path = Path.GetTempPath ();
            var expectedDirectory = Path.Combine (path, _titanic_dir);
            var expectedFile = Path.Combine (expectedDirectory, _titanic_queue);

            TitanicIO.SetConfig (path);


            Assert.IsTrue (Directory.Exists (expectedDirectory));
            Assert.IsTrue (File.Exists (expectedFile));

            // immediate deleting fails, so wait 100ms for freeing the handles
            Thread.Sleep (100);

            File.Delete (expectedFile);
            Directory.Delete (expectedDirectory);
        }

        [Test]
        public void SaveMessage_RequestMessage_SholdCreateNewFileWithGuidAsName ()
        {
            var path = Path.GetTempPath ();
            TitanicIO.SetConfig (path);

            var message = new NetMQMessage ();
            message.Push ("Hello World");
            message.Push ("echo");

            var messageSize = message[0].BufferSize + message[1].BufferSize + 4;    // 2 lines with \r\n

            var id = Guid.NewGuid ();

            TitanicIO.SaveMessage (TitanicOperation.Request, id, message);

            var expectedDir = Path.Combine (path, _titanic_dir);
            var expectedFile = Path.Combine (expectedDir, id + _request_ending);

            File.Exists (expectedFile).Should ().BeTrue ("because the file exists");

            var info = new FileInfo (expectedFile);

            info.Length.Should ().Be (messageSize);

            File.Delete (expectedFile);
        }

        [Test]
        public void SaveMessage_ReplyMessage_SholdCreateNewFileWithGuidAsName ()
        {
            var path = Path.GetTempPath ();
            TitanicIO.SetConfig (path);

            var message = new NetMQMessage ();
            message.Push ("Hello World");
            message.Push ("echo");

            var messageSize = message[0].BufferSize + message[1].BufferSize + 4;    // 2 lines with \r\n

            var id = Guid.NewGuid ();

            TitanicIO.SaveMessage (TitanicOperation.Reply, id, message);

            var expectedDir = Path.Combine (path, _titanic_dir);
            var expectedFile = Path.Combine (expectedDir, id + _reply_ending);

            File.Exists (expectedFile).Should ().BeTrue ("because the file exists");

            var info = new FileInfo (expectedFile);

            info.Length.Should ().Be (messageSize);

            File.Delete (expectedFile);
        }

        [Test]
        public void RetrieveMessage_RequestMessage_ShouldReturnOriginalMessage ()
        {
            var path = Path.GetTempPath ();
            TitanicIO.SetConfig (path);

            var message = new NetMQMessage ();
            message.Push ("Hello World");
            message.Push ("echo");

            var id = Guid.NewGuid ();

            TitanicIO.SaveMessage (TitanicOperation.Request, id, message);

            var expectedDir = Path.Combine (path, _titanic_dir);
            var expectedFile = Path.Combine (expectedDir, id + _reply_ending);

            var result = TitanicIO.RetrieveMessage (TitanicOperation.Request, id);

            result.FrameCount.Should ().Be (2, "because there are two frames");

            for (var i = 0; i < result.FrameCount; i++)
                (result[i] == message[i]).Should ().BeTrue ("because they are identical.");

            File.Delete (expectedFile);
        }

        [Test]
        public void RetrieveMessage_ReplyMessage_ShouldReturnOriginalMessage ()
        {
            var path = Path.GetTempPath ();
            TitanicIO.SetConfig (path);

            var message = new NetMQMessage ();
            message.Push ("Hello World");
            message.Push ("echo");

            var id = Guid.NewGuid ();

            TitanicIO.SaveMessage (TitanicOperation.Reply, id, message);

            var expectedDir = Path.Combine (path, _titanic_dir);
            var expectedFile = Path.Combine (expectedDir, id + _reply_ending);

            var result = TitanicIO.RetrieveMessage (TitanicOperation.Reply, id);

            result.FrameCount.Should ().Be (2, "because there are two frames");

            for (var i = 0; i < result.FrameCount; i++)
                (result[i] == message[i]).Should ().BeTrue ("because they are identical.");

            File.Delete (expectedFile);
        }
    }
}
