using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace NetMQ.Zyre
{
    public class ZreGroup
    {
        private readonly string m_name;
        private Dictionary<Guid, ZrePeer> m_peers;

        public ZreGroup(string name)
        {
            m_name = name;
            m_peers = new Dictionary<Guid, ZrePeer>();
        }

        /// <summary>
        /// Construct new group object
        /// </summary>
        /// <param name="name">name of the new group</param>
        /// <param name="container">container of groups</param>
        /// <returns></returns>
        public static ZreGroup NewGroup(string name, Dictionary<string, ZreGroup> container)
        {
            var group = new ZreGroup(name);
            container[name] = group;
            return group;
        }

        /// <summary>
        /// Add peer to group
        /// Ignore duplicate joins
        /// </summary>
        /// <param name="peer"></param>
        public void Join(ZrePeer peer)
        {
            m_peers[peer.Uuid] = peer;
            peer.IncrementStatus();
        }

        /// <summary>
        /// Remove peer from group
        /// </summary>
        /// <param name="peer"></param>
        public void Leave(ZrePeer peer)
        {
            m_peers.Remove(peer.Uuid);
            peer.IncrementStatus();
        }

        /// <summary>
        /// Send message to all peers in group
        /// </summary>
        /// <param name="msg"></param>
        public void Send(ZreMsg msg)
        {
            foreach (var peer in m_peers.Values)
            {
                peer.Send(msg);
            }
        }
    }
}
