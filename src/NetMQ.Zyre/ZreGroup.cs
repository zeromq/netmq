using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NetMQ.Zyre
{
    public class ZreGroup
    {
        private readonly string m_name;
        private Dictionary<string, ZrePeer> m_peers;

        public ZreGroup(string name)
        {
            m_name = name;
            m_peers = new Dictionary<string, ZrePeer>();
        }
    }
}
