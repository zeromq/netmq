using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;

namespace MajordomoTests
{
    [TestFixture]
    public class MDPWorkerTests
    {
        [Test]
        public void ctor_ValidParameter_ShouldReturnWorker ()
        {

        }

        [Test]
        public void ctor_InvalidBrokerAddress_ShouldThrowApplicationException ()
        {

        }

        [Test]
        public void ctor_invalidServerName_ShouldThrowApplicationException ()
        {

        }

        //Connect == 2 LogMessages
        [Test]
        public void ReceiveImplicitConnect_ValidScenario_ShouldReturnRequest ()
        {

        }

        [Test]
        public void Receive_BrokerDisconnectedWithLogging_ShouldReturnRequest ()
        {
            // 3 x 2 Log messages from connect/send
            // 2 x 1 log message .... reconnecting
            // 1 x 1 log message .... abandoning
        }

        [Test]
        public void Receive_RequestWithMDPVersionMismatch_ShouldThrowApplicationException ()
        {

        }

        [Test]
        public void Receive_RequestWithWrongFirstFrame_ShouldThrowApplicationException ()
        {

        }

        [Test]
        public void Receive_RequestWithWrongMDPComand_ShouldThrowApplicationException ()
        {

        }

        [Test]
        public void Receive_RequestWithTooManyFrames_ShouldThrowApplicationException ()
        {

        }

        [Test]
        public void Receive_RequestWithKillCommand_ShouldLogAbandoningMessage ()
        {

        }

    }
}
