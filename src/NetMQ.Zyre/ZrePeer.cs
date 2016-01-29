using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using NetMQ.Sockets;

namespace NetMQ.Zyre
{
    public class ZrePeer
    {
        private const int PeerEvasive = 10000; // 10 seconds' silence is evasive
        private const int PeerExpired = 30000; // 30 seconds' silence is expired
        private const int ReapInterval = 1000; // Once per second

        private DealerSocket m_mailbox;                   // Socket through to peer
        private readonly string m_uuid;                 // Identity string, 16 bytes
        private string m_endpoint;                      // Endpoint connected to
        private string m_name;                          // Peer's public name
        private string m_origin;                        // Origin node's public name
        private long m_evasiveAt;                       // Peer is being evasive
        private long m_expiredAt;                       // Peer has expired by now
        private bool m_connected;                       // Peer will send messages
        private bool m_ready;                           // Peer has said Hello to us
        private int m_status;                           // Our status counter
        private int m_sentSequence;                     // Outgoing message sequence
        private int m_wantSequence;                     // Incoming message sequence
        private Dictionary<string, string> m_headers;   // Peer headers 
        
        private ZrePeer(string uuid)
        {
            m_uuid = uuid;
            m_ready = false;
            m_connected = false;
            m_sentSequence = 0;
            m_wantSequence = 0;
        }

        /// <summary>
        /// Construct new ZrePeer object
        /// </summary>
        /// <param name="uuid">The identity for this peer</param>
        /// <param name="container">The dictionary of peers</param>
        /// <returns></returns>
        public static ZrePeer NewPeer(string uuid, Dictionary<string, ZrePeer> container)
        {
            var peer = new ZrePeer(uuid);
            container[uuid] = peer; // overwrite any existing entry for same uuid
            return peer;
        }

        /// <summary>
        /// Disconnect this peer. zeromq/zyre also frees peer memory, but our GC will take care of that
        /// </summary>
        public void Destroy()
        {
            Debug.Assert(m_mailbox != null, "Mailbox must not be null");
            Disconnect();
        }

        /// <summary>
        /// Connect peer mailbox (a DealerSocket)
        /// Configures mailbox and connects to peer's router endpoint
        /// </summary>
        /// <param name="replyTo"></param>
        /// <param name="endpoint"></param>
        public void Connect(string endpoint)
        {
            //  Create new outgoing socket (drop any messages in transit)
            m_mailbox = new DealerSocket(m_endpoint) // default action is to connect to the peer node
            {
                Options =
                {
                    //  Set our own identity on the socket so that receiving node knows who each message came from.
                    Identity = Encoding.ASCII.GetBytes(m_uuid),

                    //  Set a high-water mark that allows for reasonable activity
                    SendHighWatermark = PeerExpired * 100,
                }
            };
            m_endpoint = endpoint;
            m_connected = true;
            m_ready = false;
        }

        /// <summary>
        /// Disconnect peer mailbox 
        /// No more messages will be sent to peer until connected again
        /// </summary>
        public void Disconnect()
        {
            m_mailbox = null;
            m_endpoint = null;
            m_connected = false;
            m_ready = false;
        }

        /// <summary>
        /// Send message to peer
        /// </summary>
        /// <param name="msg">the message</param>
        /// <returns></returns>
        public bool Send(ZreMsg msg)
        {
            if (m_connected)
            {
                m_sentSequence++;
                m_sentSequence %= ushort.MaxValue;
                msg.Hello
                msg.Send(m_mailbox);
            }
            return true; // always true
        }
    }
}
