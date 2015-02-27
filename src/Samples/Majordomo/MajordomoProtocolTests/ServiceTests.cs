using System.Linq;
using MajordomoProtocol;
using NetMQ;
using NUnit.Framework;

namespace MajordomoTests
{
    [TestFixture]
    public class ServiceTests
    {
        [Test]
        public void ctor_New_ShouldReturnInstantiatedObject()
        {
            var service = new Service("echo");

            Assert.That(service, Is.Not.Null);
            Assert.That(service.Name, Is.EqualTo("echo"));
            Assert.That(service.WaitingWorkers, Is.Not.Null);
            Assert.That(service.PendingRequests, Is.Not.Null);
        }

        [Test]
        public void DoWorkerExist_NoWorker_ShouldReturnFalse()
        {
            var service = new Service("tcp://*:5555");

            Assert.That(service.DoWorkersExist(), Is.False);
        }

        [Test]
        public void AddWaitingWorker_KnownWorker_ShouldNotAdd()
        {
            var service = new Service("service");
            var worker = new Worker("W001", new NetMQFrame("W001"), service);

            service.AddWaitingWorker(worker);
            service.AddWaitingWorker(worker);

            Assert.That(service.WaitingWorkers.Count(), Is.EqualTo(1));
            Assert.That(service.DoWorkersExist(), Is.True);
        }

        [Test]
        public void AddWaitingWorker_UnknownWorker_ShouldReturnAddToWaitingAndKnownWorkers()
        {
            var service = new Service("service");
            var worker = new Worker("W001", new NetMQFrame("W001"), service);

            service.AddWaitingWorker(worker);

            Assert.That(service.WaitingWorkers.Count(), Is.EqualTo(1));
            Assert.That(service.DoWorkersExist(), Is.True);
        }

        [Test]
        public void DeleteWorker_KnownWorker_ShouldReturnAddToWaitingAndKnownWorkers()
        {
            Worker workerToDelete = null;
            var service = new Service("service");

            for (var i = 0; i < 10; i++)
            {
                var id = string.Format("W0{0:N3}", i);
                var worker = new Worker(id, new NetMQFrame(id), service);

                if (i == 5)
                    workerToDelete = worker;

                service.AddWaitingWorker(worker);
            }

            Assert.That(service.WaitingWorkers.Count(), Is.EqualTo(10));
            Assert.That(service.DoWorkersExist(), Is.True);

            service.DeleteWorker(workerToDelete);

            Assert.That(service.WaitingWorkers.Count(), Is.EqualTo(9));
        }

        [Test]
        public void GetNextWorker_SomeWaitingWorker_ShouldReturnOldestWorkerFirst()
        {
            Worker oldestWorker = null;
            var service = new Service("service");

            for (var i = 0; i < 10; i++)
            {
                var id = string.Format("W0{0:N3}", i);
                var worker = new Worker(id, new NetMQFrame(id), service);

                if (i == 0)
                    oldestWorker = worker;

                service.AddWaitingWorker(worker);
            }

            Assert.That(service.GetNextWorker(), Is.EqualTo(oldestWorker));
            Assert.That(service.WaitingWorkers.Count(), Is.EqualTo(9));
        }

        [Test]
        public void GetNextWorker_GetAllWaitingWorkers_ShouldReturnEmptyWaitingAndLeaveKnownUnchanged()
        {
            var service = new Service("service");

            for (var i = 0; i < 10; i++)
            {
                var id = string.Format("W0{0:N3}", i);
                var worker = new Worker(id, new NetMQFrame(id), service);
                service.AddWaitingWorker(worker);
                service.GetNextWorker();
            }

            Assert.That(service.WaitingWorkers, Is.Empty);
            Assert.That(service.DoWorkersExist(), Is.True);
        }

        [Test]
        public void DeleteWorker_SomeWaitingWorker_ShouldReturnChangeWaitingAndKnown()
        {
            Worker workerToDelete = null;
            var service = new Service("service");

            for (var i = 0; i < 10; i++)
            {
                var id = string.Format("W0{0:N3}", i);
                var worker = new Worker(id, new NetMQFrame(id), service);

                service.AddWaitingWorker(worker);
                if (i == 4)
                    workerToDelete = worker;
            }

            service.DeleteWorker(workerToDelete);

            Assert.That(service.DoWorkersExist(), Is.True);
            Assert.That(service.WaitingWorkers.Count(), Is.EqualTo(9));
        }

        [Test]
        public void DeleteWorker_AllWorker_ShouldEmptyWaitingAndKnown()
        {
            var service = new Service("service");

            for (var i = 0; i < 10; i++)
            {
                var id = string.Format("W0{0:N3}", i);
                var worker = new Worker(id, new NetMQFrame(id), service);
                service.AddWaitingWorker(worker);
                service.DeleteWorker(worker);
            }

            Assert.That(service.WaitingWorkers, Is.Empty);
            Assert.That(service.DoWorkersExist(), Is.False);
        }

        [Test]
        public void AddRequest_OneMultiFrameRequest_ShouldChangePendingRequests()
        {
            var request = new NetMQMessage();
            request.Push("DATA");
            request.Push("SERVICE");
            request.Push("HEADER");

            var service = new Service("service");

            service.AddRequest(request);

            Assert.That(service.PendingRequests.Count, Is.EqualTo(1));
        }

        [Test]
        public void AddRequest_MultipleRequest_ShouldChangePendingRequests()
        {
            var service = new Service("service");

            for (int i = 0; i < 10; i++)
            {
                var request = new NetMQMessage();
                request.Push("DATA");
                request.Push("SERVICE");
                request.Push("HEADER");

                service.AddRequest(request);
            }

            Assert.That(service.PendingRequests.Count, Is.EqualTo(10));
        }

        [Test]
        public void GetNextRequest_SingleRequestExist_ShouldReturnOldesRequestAndDeleteFromPending()
        {
            var request = new NetMQMessage();
            request.Push("DATA");
            request.Push("SERVICE");
            request.Push("HEADER");

            var service = new Service("service");

            service.AddRequest(request);

            Assert.That(service.GetNextRequest(), Is.EqualTo(request));
            Assert.That(service.PendingRequests.Count, Is.EqualTo(0));
        }

        [Test]
        public void GetNextRequest_MultipleRequestExist_ShouldReturnOldesRequestAndDeleteFromPending()
        {
            var service = new Service("service");

            for (int i = 0; i < 10; i++)
            {
                var request = new NetMQMessage();
                request.Push("DATA");
                request.Push("SERVICE");
                request.Push(string.Format("HEADER_{0}", i));

                service.AddRequest(request);
            }

            for (int i = 0; i < 5; i++)
            {
                var req = service.GetNextRequest();

                Assert.That(req.First.ConvertToString(), Is.EqualTo(string.Format("HEADER_{0}", i)));
            }

            Assert.That(service.PendingRequests.Count, Is.EqualTo(5));
        }

        [Test]
        public void ToString_Simple_ShouldReturnFormattedInfo()
        {
            var service = new Service("service");

            var s = service.ToString();

            Assert.That(s, Is.EqualTo("Name = service / Worker 0 - Waiting 0 - Pending REQ 0"));
        }

        [Test]
        public void Equals_EqualReference_ShouldReturnTrue()
        {
            var service = new Service("service");

            var other = service;

            Assert.That(service.Equals(other), Is.True);
        }

        [Test]
        public void Equals_EqualButDifferentServiceObjects_ShouldReturnTrue()
        {
            var service = new Service("service");
            var other = new Service("service");

            Assert.That(service.Equals(other), Is.True);
        }

        [Test]
        public void Equals_NotEqual_ShouldReturnFalse()
        {
            var service = new Service("service");
            var other = new Service("echo");

            Assert.That(service.Equals(other), Is.False);
        }

        [Test]
        public void Equals_DifferentTypes_ShouldReturnFalse()
        {
            var service = new Service("service");
            var other = new Worker("id", new NetMQFrame("id"), service);

            Assert.That(service.Equals(other), Is.False);
        }

        [Test]
        public void GetHashCode_EqualButDifferentServiceObjects_ShouldReturnSameHashCode()
        {
            var service = new Service("service");
            var other = new Service("service");

            Assert.That(service.GetHashCode(), Is.EqualTo(other.GetHashCode()));
        }

        [Test]
        public void GetHashCode_DifferentServiceObjects_ShouldReturnSameHashCode()
        {
            var service = new Service("service");
            var other = new Service("service1");

            Assert.That(service.GetHashCode(), Is.Not.EqualTo(other.GetHashCode()));
        }
    }
}