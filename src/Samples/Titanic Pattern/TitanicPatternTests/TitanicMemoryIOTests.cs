using System;
using System.Collections.Generic;
using System.Linq;

using FluentAssertions;
using NUnit.Framework;

using NetMQ;

using TitanicProtocol;

namespace TitanicProtocolTests
{
    [TestFixture]
    public class TitanicMemoryIOTests
    {
        [Test]
        public void ctor_CreateObject_ShouldReturnNotNull ()
        {
            var sut = new TitanicMemoryIO ();
            sut.Should ().NotBeNull ();
        }

        #region Handling of REQUESTS

        [Test]
        public void SaveRequestEntry_InvalidParameter_ShouldThrowArgumentNullException ()
        {
            var sut = new TitanicMemoryIO ();

            sut.Invoking (o => o.SaveRequestEntry (null)).ShouldThrow<ArgumentNullException> ();
        }

        [Test]
        public void SaveRequestEntry_ValidRequestEntry_ShouldUpdateTheQueue ()
        {
            var sut = new TitanicMemoryIO ();
            var request = new NetMQMessage ();
            var id = Guid.NewGuid ();
            var entry = new RequestEntry { RequestId = id, Request = request };

            sut.SaveRequestEntry (entry);

            sut.NumberOfRequests.Should ().Be (1, "because we just added one");

            var result = sut.GetRequestEntry (id);

            result.RequestId.Should ().Be (id);
        }

        [Test]
        public void SaveRequestEntry_MultipleRequestEntry_ShouldUpdateTheQueue ()
        {
            var sut = new TitanicMemoryIO ();

            for (var i = 0; i < 10; i++)
                sut.SaveRequestEntry (new RequestEntry { RequestId = Guid.NewGuid () });

            sut.NumberOfRequests.Should ().Be (10, "because we just added 10 Requests");

        }

        [Test]
        public void SaveNewRequest_GuidAndRequest_ShouldUpdateQueue ()
        {
            var sut = new TitanicMemoryIO ();
            var request = new NetMQMessage ();
            request.Push ("A Request");
            var id = Guid.NewGuid ();
            var entry = new RequestEntry { RequestId = id, Request = request };

            sut.SaveRequestEntry (entry);

            var result = sut.GetRequestEntry (id);

            sut.NumberOfRequests.Should ().Be (1, "because we just added one");
            result.RequestId.Should ().Be (id);
            result.Request.ShouldBeEquivalentTo (request);
            result.Position.Should ().Be (-1);
            result.State.Should ().Be (RequestEntry.Is_Pending);
        }

        [Test]
        public void SaveNewRequest_GuidOnly_ShouldUpdateQueue ()
        {
            var sut = new TitanicMemoryIO ();
            var request = new NetMQMessage ();
            var id = Guid.NewGuid ();
            var entry = new RequestEntry { RequestId = id, Request = request };

            sut.SaveRequestEntry (entry);

            var result = sut.GetRequestEntry (id);

            result.RequestId.Should ().Be (id);
            result.Request.ShouldBeEquivalentTo (request);
            result.Position.Should ().Be (-1);
            result.State.Should ().Be (RequestEntry.Is_Pending);
        }

        [Test]
        public void SaveNewRequest_MultipleRequestGuidOnly_ShouldUpdateQueue ()
        {
            var sut = new TitanicMemoryIO ();

            for (var i = 0; i < 10; i++)
                sut.SaveNewRequestEntry (Guid.NewGuid ());

            sut.NumberOfRequests.Should ().Be (10);
        }

        [Test]
        public void SaveProcessedRequest_ExistingRequestNoRequestData_ShouldUpdateEntryAppropriate ()
        {
            var sut = new TitanicMemoryIO ();
            var request = new NetMQMessage ();
            var id = Guid.NewGuid ();
            var entry = new RequestEntry { RequestId = id, Request = request };

            sut.SaveRequestEntry (entry);
            sut.SaveProcessedRequestEntry (entry);

            var result = sut.GetRequestEntry (id);

            result.RequestId.Should ().Be (id);
            result.Request.ShouldBeEquivalentTo (request);
            result.Position.Should ().Be (-1);
            result.State.Should ().Be (RequestEntry.Is_Processed);
        }

        [Test]
        public void SaveProcessedRequest_ExistingRequestWithRequestData_ShouldUpdateEntryAppropriate ()
        {
            var sut = new TitanicMemoryIO ();
            var request = new NetMQMessage ();
            request.Push ("Processed Request Data");
            var id = Guid.NewGuid ();
            var entry = new RequestEntry { RequestId = id, Request = request };

            sut.SaveRequestEntry (entry);
            sut.SaveProcessedRequestEntry (entry);

            var result = sut.GetRequestEntry (id);

            result.RequestId.Should ().Be (id);
            result.Request.ShouldBeEquivalentTo (request);
            result.Position.Should ().Be (-1);
            result.State.Should ().Be (RequestEntry.Is_Processed);
        }

        [Test]
        public void GetRequestEntry_NotExistingEntry_ShouldReturnDefaultObject ()
        {
            var sut = new TitanicMemoryIO ();

            sut.GetRequestEntry (Guid.NewGuid ()).Should ().Be (default (RequestEntry));
        }

        [Test]
        public void GetRequestEntry_ExistingEntry_ShouldReturnDefaultObject ()
        {
            var sut = new TitanicMemoryIO ();
            var id = Guid.NewGuid ();

            sut.SaveRequestEntry (new RequestEntry { RequestId = id });

            var result = sut.GetRequestEntry (id);

            result.RequestId.Should ().Be (id);
            result.State.Should ().Be (RequestEntry.Is_Pending);
            result.Position.Should ().Be (-1);
            result.Request.Should ().BeNull ();
        }

        [Test]
        public void GetRequestEntries_NotExistingEntries_ShouldReturnDefaultObject ()
        {
            var sut = new TitanicMemoryIO ();
            var id = Guid.NewGuid ();

            sut.GetRequestEntries (e => e.RequestId == id).ShouldAllBeEquivalentTo (default (IEnumerable<RequestEntry>));
        }

        [Test]
        public void GetRequestEntries_ExistingEntriesCorrectPredicate_ShouldReturnCorrectSequence ()
        {
            var sut = new TitanicMemoryIO ();

            for (var i = 0; i < 10; i++)
                sut.SaveRequestEntry (i % 2 == 0
                                          ? new RequestEntry { RequestId = Guid.NewGuid (), State = RequestEntry.Is_Processed }
                                          : new RequestEntry { RequestId = Guid.NewGuid () });

            var result = sut.GetRequestEntries (e => e.State == RequestEntry.Is_Processed);

            result.Should ().BeOfType (typeof (RequestEntry[]));
            result.Count ().Should ().Be (5);
            result.All (e => e.State == RequestEntry.Is_Processed).Should ().BeTrue ();
        }

        [Test]
        public void GetRequestEntries_ExistingEntriesNoResultPredicate_ShouldReturnDefaultSequence ()
        {
            var sut = new TitanicMemoryIO ();

            for (var i = 0; i < 10; i++)
                sut.SaveRequestEntry (i % 2 == 0
                                          ? new RequestEntry { RequestId = Guid.NewGuid (), State = RequestEntry.Is_Processed }
                                          : new RequestEntry { RequestId = Guid.NewGuid () });

            sut.NumberOfRequests.Should ().Be (10);

            var result = sut.GetRequestEntries (e => e.State == RequestEntry.Is_Closed);

            result.Should ().BeOfType (typeof (RequestEntry[]));
            result.Count ().Should ().Be (0);
        }

        [Test]
        public void GetNotClosedEntries_ExistingEntries_ShouldReturnCorrectSequence ()
        {
            var sut = new TitanicMemoryIO ();

            for (var i = 0; i < 10; i++)
                sut.SaveRequestEntry (i % 3 == 0
                                          ? new RequestEntry { RequestId = Guid.NewGuid (), State = RequestEntry.Is_Closed }
                                          : new RequestEntry { RequestId = Guid.NewGuid () });

            sut.NumberOfRequests.Should ().Be (10);

            var result = sut.GetNotClosedRequestEntries ();

            result.Count ().Should ().Be (6);
        }

        [Test]
        public void GetNotClosedEntries_ExistingEntriesNoResult_ShouldReturnCorrectSequence ()
        {
            var sut = new TitanicMemoryIO ();

            for (var i = 0; i < 10; i++)
                sut.SaveRequestEntry (new RequestEntry { RequestId = Guid.NewGuid (), State = RequestEntry.Is_Closed });

            sut.NumberOfRequests.Should ().Be (10);

            var result = sut.GetNotClosedRequestEntries ();

            result.Count ().Should ().Be (0);
        }

        [Test]
        public void CloseRequest_NotExistingRequest_ShouldReturnWithOutChangingQueue ()
        {
            var sut = new TitanicMemoryIO ();

            for (var i = 0; i < 10; i++)
                sut.SaveNewRequestEntry (Guid.NewGuid ());

            var id = Guid.NewGuid ();

            sut.CloseRequest (id);

            sut.NumberOfRequests.Should ().Be (10);
        }

        [Test]
        public void CloseRequest_NoRequestExisting_ShouldReturn ()
        {
            var sut = new TitanicMemoryIO ();

            sut.CloseRequest (Guid.NewGuid ());
        }

        [Test]
        public void CloseRequest_ExistingEntries_ShouldAlterQueueAppropriate ()
        {
            var sut = new TitanicMemoryIO ();

            for (var i = 0; i < 10; i++)
                sut.SaveRequestEntry (i % 3 == 0
                                          ? new RequestEntry { RequestId = Guid.NewGuid (), State = RequestEntry.Is_Closed }
                                          : new RequestEntry { RequestId = Guid.NewGuid () });

            sut.NumberOfRequests.Should ().Be (10);

            var result = sut.GetRequestEntries (e => e.State == RequestEntry.Is_Closed);

            result.Count ().Should ().Be (4);
            foreach (var requestEntry in result)
                sut.CloseRequest (requestEntry.RequestId);

            sut.NumberOfRequests.Should ().Be (6);
        }

        #endregion

        #region Handling of MESSAGES

        [Test]
        public void GetMessage_ExistingRequest_ShouldReturnCorrectMessage ()
        {
            var sut = new TitanicMemoryIO ();
            var ids = new Guid[10];
            var expected = new NetMQMessage ();
            expected.Push ("Request #3");
            expected.Push ("echo");

            for (var i = 0; i < 10; i++)
            {
                ids[i] = Guid.NewGuid ();
                var request = new NetMQMessage ();
                request.Push (string.Format ("Request #{0}", i));
                request.Push ("echo");
                sut.SaveNewRequestEntry (ids[i], request);
            }

            var result = sut.GetMessage (TitanicOperation.Request, ids[3]);

            result.Should ().BeEquivalentTo (expected);
        }

        [Test]
        public void GetMessage_ExistingRequestWrongState_ShouldReturnCorrectMessage ()
        {
            var sut = new TitanicMemoryIO ();
            var ids = new Guid[10];
            var expected = new NetMQMessage ();

            for (var i = 0; i < 10; i++)
            {
                ids[i] = Guid.NewGuid ();
                var request = new NetMQMessage ();
                request.Push (string.Format ("Request #{0}", i));
                request.Push ("echo");
                sut.SaveNewRequestEntry (ids[i], request);
            }

            var result = sut.GetMessage (TitanicOperation.Reply, Guid.NewGuid ());

            result.Should ().BeEquivalentTo (expected);
        }

        [Test]
        public void SaveMessage_NewMessage_ShouldUpdateQueue ()
        {
            var sut = new TitanicMemoryIO ();
            var id = Guid.NewGuid ();
            var request = new NetMQMessage ();
            request.Push ("Request #1");
            request.Push ("echo");


            sut.SaveMessage (TitanicOperation.Request, id, request).Should ().BeTrue ();
            sut.NumberOfRequests.Should ().Be (1);

            var result = sut.GetRequestEntry (id);

            result.Request.Should ().Equal (request);
            result.State.Should ().Be (RequestEntry.Is_Pending);
        }

        [Test]
        public void SaveMessage_UpdateExistingMessage_ShouldUpdateCorrectRequestEntry ()
        {
            const int id_to_retrieve = 7;

            var sut = new TitanicMemoryIO ();
            var ids = new Guid[10];

            for (var i = 0; i < 10; i++)
            {
                ids[i] = Guid.NewGuid ();

                var request = new NetMQMessage ();
                request.Push (string.Format ("Request #{0}", i));
                request.Push ("echo");
                sut.SaveMessage (TitanicOperation.Request, ids[i], request);
            }

            sut.NumberOfRequests.Should ().Be (10);

            var reply = new NetMQMessage ();
            reply.Push (string.Format ("This is a REPLY to Request #{0}", id_to_retrieve));
            reply.Push ("echo");

            sut.SaveMessage (TitanicOperation.Reply, ids[id_to_retrieve], reply).Should ().BeTrue ();

            sut.NumberOfRequests.Should ().Be (10);

            var result = sut.GetRequestEntry (ids[id_to_retrieve]);

            result.State.Should ().Be (RequestEntry.Is_Processed);
            result.RequestId.Should ().Be (ids[id_to_retrieve]);
            result.Request.Should ().Equal (reply);
        }

        [Test]
        public void ExistsMessage_ExistingMessage_ShouldUpdateCorrectRequestEntry ()
        {
            const int id_to_retrieve = 7;

            var sut = new TitanicMemoryIO ();
            var ids = new Guid[10];

            for (var i = 0; i < 10; i++)
            {
                ids[i] = Guid.NewGuid ();

                var request = new NetMQMessage ();
                request.Push (string.Format ("Request #{0}", i));
                request.Push ("echo");
                sut.SaveMessage (TitanicOperation.Request, ids[i], request);
            }

            sut.ExistsMessage (TitanicOperation.Request, ids[id_to_retrieve]).Should ().BeTrue ();
        }

        [Test]
        public void ExistsMessage_NotExistingMessage_ShouldUpdateCorrectRequestEntry ()
        {

            var sut = new TitanicMemoryIO ();
            var ids = new Guid[10];

            for (var i = 0; i < 10; i++)
            {
                ids[i] = Guid.NewGuid ();

                var request = new NetMQMessage ();
                request.Push (string.Format ("Request #{0}", i));
                request.Push ("echo");
                sut.SaveMessage (TitanicOperation.Request, ids[i], request);
            }

            sut.ExistsMessage (TitanicOperation.Request, Guid.NewGuid ()).Should ().BeFalse ();
        }

        [Test]
        public void ExistsMessage_ExistingMessageWrongState_ShouldUpdateCorrectRequestEntry ()
        {
            const int id_to_retrieve = 7;

            var sut = new TitanicMemoryIO ();
            var ids = new Guid[10];

            for (var i = 0; i < 10; i++)
            {
                ids[i] = Guid.NewGuid ();

                var request = new NetMQMessage ();
                request.Push (string.Format ("Request #{0}", i));
                request.Push ("echo");
                sut.SaveMessage (TitanicOperation.Request, ids[i], request);
            }

            sut.ExistsMessage (TitanicOperation.Reply, ids[id_to_retrieve]).Should ().BeFalse ();
        }

        #endregion
    }
}
