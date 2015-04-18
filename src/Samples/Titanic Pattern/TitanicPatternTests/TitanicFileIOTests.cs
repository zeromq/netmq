using System;
using System.IO;
using System.Linq;

using FluentAssertions;
using NUnit.Framework;

using NetMQ;

using TitanicProtocol;

namespace TitanicProtocolTests
{
    [TestFixture]
    public class TitanicFileIOTests
    {
        private const string _request_ending = ".request";
        private const string _reply_ending = ".reply";

        [Test]
        public void SetConfig_ValidPath_ShouldCreateDirectoryAndFile ()
        {
            var sut = new TitanicFileIO (Path.GetTempPath ());

            var expectedDirectory = sut.TitanicDirectory;
            var expectedFile = sut.TitanicQueue;

            Directory.Exists (expectedDirectory)
                     .Should ()
                     .BeTrue (string.Format ("because {0} should have been created!", expectedDirectory));

            File.Exists (expectedFile)
                .Should ()
                .BeTrue (string.Format ("because {0} should have been created!", expectedFile));
        }

        [Test]
        public void SaveMessage_RequestMessage_ShouldCreateNewFileWithGuidAsName ()
        {
            var sut = new TitanicFileIO (Path.GetTempPath ());

            var message = new NetMQMessage ();
            message.Push ("Hello World");
            message.Push ("echo");

            var messageSize = message[0].BufferSize + message[1].BufferSize + 4;    // + 2 lines with \r\n

            var id = Guid.NewGuid ();

            sut.SaveMessage (TitanicOperation.Request, id, message);

            var expectedDir = sut.TitanicDirectory;
            var expectedFile = Path.Combine (expectedDir, id + _request_ending);

            File.Exists (expectedFile).Should ().BeTrue ("because the file exists");

            var info = new FileInfo (expectedFile);

            info.Length.Should ().Be (messageSize);

            File.Delete (expectedFile);
        }

        [Test]
        public void SaveMessage_ReplyMessage_SholdCreateNewFileWithGuidAsName ()
        {
            var sut = new TitanicFileIO (Path.GetTempPath ());

            var message = new NetMQMessage ();
            message.Push ("Hello World");
            message.Push ("echo");

            var messageSize = message[0].BufferSize + message[1].BufferSize + 4;    // 2 lines with \r\n

            var id = Guid.NewGuid ();

            sut.SaveMessage (TitanicOperation.Reply, id, message);

            var expectedDir = sut.TitanicDirectory;
            var expectedFile = Path.Combine (expectedDir, id + _reply_ending);

            File.Exists (expectedFile).Should ().BeTrue ("because the file exists");

            var info = new FileInfo (expectedFile);

            info.Length.Should ().Be (messageSize);

            File.Delete (expectedFile);
        }

        [Test]
        public void RetrieveMessage_RequestMessage_ShouldReturnOriginalMessage ()
        {
            var sut = new TitanicFileIO (Path.GetTempPath ());

            var message = new NetMQMessage ();
            message.Push ("Hello World");
            message.Push ("echo");

            var id = Guid.NewGuid ();

            sut.SaveMessage (TitanicOperation.Request, id, message);

            var result = sut.GetMessage (TitanicOperation.Request, id);

            result.FrameCount.Should ().Be (2, "because there are two frames");

            for (var i = 0; i < result.FrameCount; i++)
                (result[i] == message[i]).Should ().BeTrue ("because they are identical.");

            var expectedDir = sut.TitanicDirectory;
            var expectedFile = Path.Combine (expectedDir, id + _request_ending);

            File.Delete (expectedFile);
        }

        [Test]
        public void RetrieveMessage_ReplyMessage_ShouldReturnOriginalMessage ()
        {
            var sut = new TitanicFileIO (Path.GetTempPath ());

            var message = new NetMQMessage ();
            message.Push ("Hello World");
            message.Push ("echo");

            var id = Guid.NewGuid ();

            sut.SaveMessage (TitanicOperation.Reply, id, message);

            var result = sut.GetMessage (TitanicOperation.Reply, id);

            result.FrameCount.Should ().Be (2, "because there are two frames");

            for (var i = 0; i < result.FrameCount; i++)
                (result[i] == message[i]).Should ().BeTrue ("because they are identical.");

            var expectedDir = sut.TitanicDirectory;
            var expectedFile = Path.Combine (expectedDir, id + _reply_ending);

            File.Delete (expectedFile);
        }

        [Test]
        public void Exists_ExistingRequest_ShouldReturnTrue ()
        {
            var sut = new TitanicFileIO (Path.GetTempPath ());

            const TitanicOperation op = TitanicOperation.Request;

            var message = new NetMQMessage ();
            message.Push ("Hello World");
            message.Push ("echo");

            var id = Guid.NewGuid ();

            sut.SaveMessage (op, id, message);

            sut.ExistsMessage (op, id).Should ().BeTrue ("because it has been created.");

            var expectedDir = sut.TitanicDirectory;
            var expectedFile = Path.Combine (expectedDir, id + _request_ending);

            File.Delete (expectedFile);
        }

        [Test]
        public void Exists_ExistingReply_ShouldReturnTrue ()
        {
            var sut = new TitanicFileIO (Path.GetTempPath ());

            const TitanicOperation op = TitanicOperation.Reply;

            var message = new NetMQMessage ();
            message.Push ("Hello World");
            message.Push ("echo");

            var id = Guid.NewGuid ();

            sut.SaveMessage (op, id, message);

            sut.ExistsMessage (op, id).Should ().BeTrue ("because it has been created.");

            var expectedDir = sut.TitanicDirectory;
            var expectedFile = Path.Combine (expectedDir, id + _reply_ending);

            File.Delete (expectedFile);
        }

        [Test]
        public void Exists_NonExistingRequest_ShouldReturnFalse ()
        {
            var sut = new TitanicFileIO (Path.GetTempPath ());

            var id = Guid.NewGuid ();

            sut.ExistsMessage (TitanicOperation.Request, id).Should ().BeFalse ("because it has never been created.");
        }

        [Test]
        public void Exists_NonExistingReply_ShouldReturnFalse ()
        {
            var sut = new TitanicFileIO (Path.GetTempPath ());

            var id = Guid.NewGuid ();

            sut.ExistsMessage (TitanicOperation.Reply, id).Should ().BeFalse ("because it has never been created.");
        }

        [Test]
        public void SaveNewRequest_ValidId_ShouldAddRequestMarkedAsNew ()
        {
            var sut = new TitanicFileIO (Path.GetTempPath ());

            var id = Guid.NewGuid ();

            sut.SaveNewRequestEntry (id);      // -> to titanic.queue

            sut.GetRequestEntries (null)
                     .First ()
                     .State.Should ()
                     .Be (RequestEntry.Is_Pending, "because it is a new request.");

            var file = sut.TitanicQueue;

            File.Exists (file).Should ().BeTrue (string.Format ("because {0} was created.", file));
        }

        [Test]
        public void SaveNewRequest_NonExistingId_ShouldReturnEmpty ()
        {
            var sut = new TitanicFileIO (Path.GetTempPath ());

            var titanicQueue = sut.TitanicQueue;

            // create empty queue but release filestream immediately again
            File.Create (titanicQueue).Dispose ();

            sut.GetRequestEntries (null).Should ().BeEmpty ("because there are none.");
        }

        [Test]
        public void SaveNewRequest_MultipleValidIds_ShouldAddAllRequestMarkedAsNew ()
        {
            var sut = new TitanicFileIO (Path.GetTempPath ());

            for (var i = 0; i < 10; i++)
                sut.SaveNewRequestEntry (Guid.NewGuid ()); // -> to titanic.queue

            sut.GetRequestEntries (null)
                     .All (re => re.State == RequestEntry.Is_Pending)
                     .Should ()
                     .BeTrue ("because all entries are new.");

            var titanicQueue = sut.TitanicQueue;
            File.Delete (titanicQueue);
        }

        [Test]
        public void SaveProcessedRequest_NonProcessedRequest_ShouldMarkRequestAppropriately ()
        {
            var sut = new TitanicFileIO (Path.GetTempPath ());

            for (var i = 0; i < 10; i++)
                sut.SaveNewRequestEntry (Guid.NewGuid ()); // -> fill titanic.queue

            var req = sut.GetRequestEntries (null).Skip (3).First ();

            req.State.Should ().Be (RequestEntry.Is_Pending);

            sut.SaveProcessedRequestEntry (req);

            sut.GetRequestEntries (null)
                     .Count (re => re.State == RequestEntry.Is_Processed)
                     .Should ()
                     .Be (1, "because only one request has been processed.");

            var titanicQueue = sut.TitanicQueue;
            File.Delete (titanicQueue);
        }

        [Test]
        public void FindRequest_ExistingRequest_ShouldReturnCorrectRequestEntry ()
        {
            var sut = new TitanicFileIO (Path.GetTempPath ());

            for (var i = 0; i < 10; i++)
                sut.SaveNewRequestEntry (Guid.NewGuid ()); // -> fill titanic.queue

            var requests = sut.GetRequestEntries (null).ToArray ();

            var req = requests.Skip (3).First ();

            sut.GetRequestEntry (req.RequestId).Should ().Be (req, "because it was searched.");

            var titanicQueue = sut.TitanicQueue;
            File.Delete (titanicQueue);
        }

        [Test]
        public void FindRequest_NonExistingRequest_ShouldReturnCorrectRequestEntry ()
        {
            var sut = new TitanicFileIO (Path.GetTempPath ());

            for (var i = 0; i < 10; i++)
                sut.SaveNewRequestEntry (Guid.NewGuid ()); // -> fill titanic.queue

            sut.GetRequestEntry (Guid.NewGuid ()).Should ().Be (default (RequestEntry));

            var titanicQueue = sut.TitanicQueue;
            File.Delete (titanicQueue);
        }

        [Test]
        public void CloseRequest_ProcessedRequest_ShouldMarkRequestAppropriate ()
        {
            var path = Path.Combine (Path.GetTempPath (), ".titanic", "Close_1");
            var sut = new TitanicFileIO (path);

            for (var i = 0; i < 10; i++)
                sut.SaveNewRequestEntry (Guid.NewGuid ()); // -> fill titanic.queue

            var req = sut.GetRequestEntries (null).Skip (3).First ();

            sut.SaveProcessedRequestEntry (req);

            sut.CloseRequest (req.RequestId);

            sut.GetRequestEntries (null)
                     .Count (re => re.State == RequestEntry.Is_Closed)
                     .Should ()
                     .Be (1, "because only one request has been processed.");

            sut.GetRequestEntry (req.RequestId).State.Should ().Be (RequestEntry.Is_Closed);

            Directory.Delete (sut.TitanicDirectory, true);
        }

        [Test]
        public void CloseRequest_MaxEntryClosedRequests_ShouldPurgeQueue ()
        {
            const int max_entries = 20;
            var path = Path.Combine (Path.GetTempPath (), ".titanic", "Close_2");

            var sut = new TitanicFileIO (path, max_entries);

            for (var i = 0; i < max_entries; i++)
                sut.SaveNewRequestEntry (Guid.NewGuid ()); // -> fill titanic.queue

            var requests = sut.GetRequestEntries (null).ToArray ();
            requests.Length.Should ().Be (max_entries, "because 20 entries were written.");

            foreach (var entry in requests)
                sut.CloseRequest (entry.RequestId);

            sut.GetRequestEntries (null)
                     .Should ().BeEmpty ("because all requests have been closed!");

            Directory.Delete (sut.TitanicDirectory, true);
        }

        [Test]
        public void CloseRequest_MaxEntryClosedRequestsLeaveAdditionalRequests_ShouldReorganizeQueue ()
        {
            const int additional_requests = 5;
            const int max_entries = 20;
            var path = Path.Combine (Path.GetTempPath (), ".titanic", "Close_3");

            var sut = new TitanicFileIO (path, max_entries);

            for (var i = 0; i < max_entries + additional_requests; i++)
                sut.SaveNewRequestEntry (Guid.NewGuid ()); // -> fill titanic.queue

            var requests = sut.GetRequestEntries (null).ToArray ();
            requests.Length.Should ().Be (max_entries + additional_requests);

            for (var i = 0; i < max_entries; i++)
                sut.CloseRequest (requests[i].RequestId);

            sut.GetNotClosedRequestEntries ()
                     .Count ()
                     .Should ()
                     .Be (additional_requests, "because 5 requests should have been left over!");

            Directory.Delete (sut.TitanicDirectory, true);
        }

        [Test]
        public void CloseRequest_MaxEntryClosedAndAdditionalRequestsAndReplies_ShouldReorganizeQueue ()
        {
            const int max_entries = 20;
            const int additional_requests = 5;
            var path = Path.Combine (Path.GetTempPath (), ".titanic", "Close_4");

            var sut = new TitanicFileIO (path, max_entries);

            var titanicQueue = sut.TitanicQueue;

            for (var i = 0; i < max_entries + additional_requests; i++)
            {
                var id = Guid.NewGuid ();
                sut.SaveNewRequestEntry (id); // -> fill titanic.queue

                var message = new NetMQMessage ();
                message.Push (string.Format ("Message #{0}", i));
                message.Push ("echo");

                sut.SaveMessage (TitanicOperation.Request, id, message);
            }

            foreach (var entry in sut.GetRequestEntries (null).Skip (3).Take (5))
            {
                sut.SaveProcessedRequestEntry (entry);

                var message = sut.GetMessage (TitanicOperation.Request, entry.RequestId);

                sut.SaveMessage (TitanicOperation.Reply, entry.RequestId, message);
            }

            var requests = sut.GetRequestEntries (null).ToArray ();
            requests.Length.Should ().Be (max_entries + additional_requests);
            requests.Count (re => re.State == RequestEntry.Is_Processed).Should ().Be (5);

            for (var i = 0; i < max_entries; i++)
                sut.CloseRequest (requests[i].RequestId);     // mark closed not worrying about state

            sut.GetNotClosedRequestEntries ()
                     .Count ()
                     .Should ()
                     .Be (additional_requests, "because 5 requests should have been left over!");

            Directory.Delete (sut.TitanicDirectory, true);
        }
    }
}
