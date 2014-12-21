using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NetMQ.Sockets;
using NetMQ.zmq;

namespace NetMQ
{
    /// <summary>
    /// Forward messages between two sockets, you can also specify control socket which both sockets will send messages to
    /// </summary>
    public class Proxy
    {
	    INetMQFactory m_factory;
        INetMQSocket m_frontend;
        INetMQSocket m_backend;
        INetMQSocket m_control;
        private IPoller m_poller;

        public Proxy(INetMQSocket frontend, INetMQSocket backend, INetMQSocket control, INetMQFactory factory)
        {
            m_frontend = frontend;
            m_backend = backend;
            m_control = control;
	        m_factory = factory;
        }

        /// <summary>
        /// Start the proxy work, this will block until one of the sockets is closed
        /// </summary>
        public void Start()
        {
            m_frontend.ReceiveReady += OnFrontendReady;
            m_backend.ReceiveReady += OnBackendReady;

			m_poller = m_factory.CreatePoller(m_frontend, m_backend);
            m_poller.PollTillCancelled();
        }

        public void Stop()
        {
            m_poller.CancelAndJoin();
        }

        private void OnFrontendReady(object sender, NetMQSocketEventArgs e)
        {
            Msg msg = new Msg();
            msg.InitEmpty();

            Msg copy = new Msg();
            copy.InitEmpty();

            while (true)
            {
                m_frontend.Receive(ref msg, SendReceiveOptions.None);
                bool more = m_frontend.Options.ReceiveMore;

                if (m_control != null)
                {
                    copy.Copy(ref msg);

                    m_control.Send(ref copy, more ? SendReceiveOptions.SendMore : SendReceiveOptions.None);
                }

                m_backend.Send(ref msg, more ? SendReceiveOptions.SendMore : SendReceiveOptions.None);

                if (!more)
                {
                    break;
                }
            }

            copy.Close();
            msg.Close();
        }

        private void OnBackendReady(object sender, NetMQSocketEventArgs e)
        {
            Msg msg = new Msg();
            msg.InitEmpty();

            Msg copy = new Msg();
            copy.InitEmpty();

            while (true)
            {
                m_backend.Receive(ref msg, SendReceiveOptions.None);
                bool more = m_backend.Options.ReceiveMore;

                if (m_control != null)
                {
                    copy.Copy(ref msg);

                    m_control.Send(ref copy, more ? SendReceiveOptions.SendMore : SendReceiveOptions.None);
                }

                m_frontend.Send(ref msg, more ? SendReceiveOptions.SendMore : SendReceiveOptions.None);



                if (!more)
                {
                    break;
                }
            }

            copy.Close();
            msg.Close();
        }
    }
}
