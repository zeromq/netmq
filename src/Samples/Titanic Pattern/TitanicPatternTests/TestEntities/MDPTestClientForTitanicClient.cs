using System;
using System.Text;
using JetBrains.Annotations;
using MDPCommons;
using NetMQ;
using TitanicCommons;
using TitanicProtocol;

namespace TitanicProtocolTests.TestEntities
{
    /// <summary>
    ///     injected in lieu of the really MDPClient to avoid the installing of the infrastructure
    ///     just for testing
    /// </summary>
    internal class MDPTestClientForTitanicClient : IMDPClient
    {
        public event EventHandler<MDPLogEventArgs> LogInfoReady;

        public TimeSpan Timeout { get { throw new NotImplementedException (); } set { throw new NotImplementedException (); } }

        public int Retries { get { throw new NotImplementedException (); } set { throw new NotImplementedException (); } }

        public string Address { get { return "Fake Address"; } }

        public byte[] Identity { get { return Encoding.UTF8.GetBytes ("Fake Identity"); } }

        /// <remarks>
        ///  return messages can be:
        ///
        ///  REQUEST:
        ///      returns -> [return code][Guid]
        ///  REPLY
        ///      returns -> [return code][reply]
        ///  CLOSE
        ///      in goes -> [titanic operation][request id]
        /// </remarks>
        public NetMQMessage ReplyMessage { get; set; }

        /// <summary>
        ///     use for defining a specific NetMQFrame to be pushed to reply message
        /// </summary>
        public NetMQFrame ReplyDataFrame { get; set; }

        public Guid RequestId { get; set; }

        public MDPTestClientForTitanicClient ()
        {
            ReplyMessage = null;
            RequestId = Guid.Empty;
        }

        // messages can be:
        //
        //  REQUEST:
        //      in goes -> [service name][request]
        //      returns -> [return code][Guid]
        //  REPLY
        //      in goes -> [request id]
        //      returns -> [return code][reply]
        //  CLOSE
        //      in goes -> [request id]
        //
        public NetMQMessage Send (string serviceName, NetMQMessage message)
        {
            Log (string.Format ("requested service <{0}> with request <{1}>", serviceName, message));

            if (ReplyMessage != null)
                return new NetMQMessage (ReplyMessage);     // to keep it intact for multiple tries return a copy(!)

            var operation = (TitanicOperation) Enum.Parse (typeof (TitanicOperation), serviceName);
            var reply = new NetMQMessage ();

            switch (operation)
            {
                case TitanicOperation.Request:
                    var id = RequestId == Guid.Empty ? Guid.NewGuid () : RequestId;
                    reply.Push (id.ToString ());
                    reply.Push (TitanicReturnCode.Ok.ToString ());
                    break;
                case TitanicOperation.Reply:
                    if (ReplyMessage == null)
                    {
                        reply.Push (ReplyDataFrame);
                        reply.Push (TitanicReturnCode.Ok.ToString ());
                    }
                    else
                        reply = ReplyMessage;
                    break;
                case TitanicOperation.Close:
                    reply.Push (TitanicReturnCode.Ok.ToString ());
                    break;
                default:
                    reply.Push (TitanicReturnCode.Failure.ToString ());
                    break;
            }

            Log (string.Format ("reply <{0}>", reply));

            return reply;
        }

        public void Dispose () { return; }

        private void Log ([NotNull] string info)
        {
            OnLogInfoReady (new MDPLogEventArgs { Info = "[FAKE MDP CLIENT] " + info });
        }

        protected virtual void OnLogInfoReady (MDPLogEventArgs e)
        {
            var handler = LogInfoReady;

            if (handler != null)
                handler (this, e);
        }

    }
}
