using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NetMQ.zmq;

namespace NetMQ
{
    public class Context
    {
    	readonly Ctx m_ctx;

        private Context(Ctx ctx)
        {
            m_ctx = ctx;
        }

        public static Context Create()
        {
            return new Context(ZMQ.CtxNew());
        }

        public int IOThreads
        {
            get { return ZMQ.CtxGet(m_ctx, ContextOption.IOThreads); }
            set { ZMQ.CtxSet(m_ctx, ContextOption.IOThreads, value); }
        }

        public int MaxSockets
        {
            get { return ZMQ.CtxGet(m_ctx, zmq.ContextOption.MaxSockets); }
            set { ZMQ.CtxSet(m_ctx, ContextOption.MaxSockets, value); }
        }

        public RequestSocket CreateRequestSocket()
        {
            var socketHandle = ZMQ.Socket(m_ctx, ZmqSocketType.Req);

            return new RequestSocket(socketHandle);
        }

        public ResponseSocket CreateResponseSocket()
        {
            var socketHandle = ZMQ.Socket(m_ctx, ZmqSocketType.Rep);

            return new ResponseSocket(socketHandle);
        }

        public DealerSocket CreateDealerSocket()
        {
            var socketHandle = ZMQ.Socket(m_ctx, ZmqSocketType.Dealer);

            return new DealerSocket(socketHandle);
        }

        public RouterSocket CreateRouterSocket()
        {
            var socketHandle = ZMQ.Socket(m_ctx, ZmqSocketType.Router);

            return new RouterSocket(socketHandle);
        }

        public XPublisherSocket CreateXPublisherSocket()
        {
            var socketHandle = ZMQ.Socket(m_ctx, ZmqSocketType.Xpub);

            return new XPublisherSocket(socketHandle);
        }

        public PairSocket CreatePairSocket()
        {
            var socketHandle = ZMQ.Socket(m_ctx, ZmqSocketType.Pair);

            return new PairSocket(socketHandle);
        }

        public PushSocket CreatePushSocket()
        {
            var socketHandle = ZMQ.Socket(m_ctx, ZmqSocketType.Push);

            return new PushSocket(socketHandle);
        }

        public PublisherSocket CreatePublisherSocket()
        {
            var socketHandle = ZMQ.Socket(m_ctx, ZmqSocketType.Pub);

            return new PublisherSocket(socketHandle);
        }

        public PullSocket CreatePullSocket()
        {
            var socketHandle = ZMQ.Socket(m_ctx, ZmqSocketType.Pull);

            return new PullSocket(socketHandle);
        }

        public SubscriberSocket CreateSubscriberSocket()
        {
            var socketHandle = ZMQ.Socket(m_ctx, ZmqSocketType.Sub);

            return new SubscriberSocket(socketHandle);
        }

        public XSubscriberSocket CreateXSubscriberSocket()
        {
            var socketHandle = ZMQ.Socket(m_ctx, ZmqSocketType.Xsub);

            return new XSubscriberSocket(socketHandle);
        }            

        public void Terminate()
        {
            ZMQ.Term(m_ctx);
        }
    }
}
