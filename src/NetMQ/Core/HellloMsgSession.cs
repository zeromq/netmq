namespace NetMQ.Core
{
    class HelloMsgSession : SessionBase
    {
        bool m_newPipe;

        public HelloMsgSession(IOThread ioThread, bool connect, SocketBase socket, Options options, Address addr) : 
            base(ioThread, connect, socket, options, addr)
        {
            m_newPipe = true;
        }

        public override PullMsgResult PullMsg(ref Msg msg)
        {
            if (m_newPipe)
            {
                Assumes.NotNull(m_options.HelloMsg);

                m_newPipe = false;
                msg.InitPool(m_options.HelloMsg.Length);
                msg.Put(m_options.HelloMsg, 0, m_options.HelloMsg.Length);
                return PullMsgResult.Ok;
            }

            return base.PullMsg(ref msg);
        }

        protected override void Reset()
        {
            base.Reset();
            m_newPipe = true;
        }
    }
}