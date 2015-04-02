using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using MajordomoProtocol;
using MajordomoProtocol.Contracts;
using NetMQ;
using NUnit.Framework;

using TitanicCommons;
using TitanicProtocol;

namespace TitanicProtocolTests
{
    [TestFixture]
    public class TitanicBrokerTests
    {
        private const string _titanic_broker_address = "tcp://localhost:5555";
        private const string _titanic_internal_communication = "inproc://titanic.inproc";
        private const string _titanic_directory = ".titanic";
        private const string _titanic_queue = "titanic.queue";
        private const string _request_ending = ".request";
        private const string _reply_ending = ".reply";

        [Test]
        public void ctor_WithStandardPath_SholdNOTCreateRootFileAndQueue ()
        {
            using (ITitanicBroker sut = new TitanicBroker ())
            {
                sut.Should ().NotBeNull ();

                var path = Path.Combine (AppDomain.CurrentDomain.BaseDirectory, _titanic_directory);
                var queue = Path.Combine (path, _titanic_queue);

                File.Exists (queue).Should ().BeFalse (string.Format ("because {0} should NOT have been created!", queue));

                File.Delete (queue);
            }
        }

        [Test]
        public void Run_CheckStandardConfiguration_ShouldHaveCreatedInfrastructure ()
        {
            using (ITitanicBroker sut = new TitanicBroker ())
            using (var cts = new CancellationTokenSource ())
            {
                var t = Task.Factory.StartNew (() => sut.Run (), cts.Token);
                t.Status.Should ().Be (TaskStatus.WaitingToRun, "because it should wait for a request!");

                var path = Path.Combine (AppDomain.CurrentDomain.BaseDirectory, _titanic_directory);
                var queue = Path.Combine (path, _titanic_queue);

                Directory.Exists (path).Should ().BeTrue (string.Format ("because {0} should have been created!", path));
                File.Exists (queue).Should ().BeTrue (string.Format ("because {0} should have been created!", queue));

                cts.Cancel ();

                File.Delete (queue);
            }
        }
     }
}
