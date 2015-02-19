using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace NetMQ
{
    public class InterfaceItem
    {
        public InterfaceItem(IPAddress address, IPAddress broadcastAddress)
        {
            Address = address;
            BroadcastAddress = broadcastAddress;
        }

        public IPAddress Address { get; private set; }
        public IPAddress BroadcastAddress { get; private set; }
    }

    public class InterfaceCollection : IEnumerable<InterfaceItem>
    {
        private readonly List<InterfaceItem> m_interfaceItems;

        public InterfaceCollection()
        {

            var interfaces = NetworkInterface.GetAllNetworkInterfaces().Where(i => i.OperationalStatus == OperationalStatus.Up &&
                                         i.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                                         i.NetworkInterfaceType != NetworkInterfaceType.Ppp);

            var addresses = interfaces.SelectMany(i => i.GetIPProperties().UnicastAddresses.Where(a =>
                a.Address.AddressFamily == AddressFamily.InterNetwork));

            m_interfaceItems = new List<InterfaceItem>();

            foreach (var address in addresses)
            {
                byte[] broadcastBytes = address.Address.GetAddressBytes();
                byte[] mask = address.IPv4Mask.GetAddressBytes();

                broadcastBytes[0] |= (byte)~mask[0];
                broadcastBytes[1] |= (byte)~mask[1];
                broadcastBytes[2] |= (byte)~mask[2];
                broadcastBytes[3] |= (byte)~mask[3];

                IPAddress broadcastAddress = new IPAddress(broadcastBytes);


                m_interfaceItems.Add(new InterfaceItem(address.Address, broadcastAddress));
            }
        }

        public IEnumerator<InterfaceItem> GetEnumerator()
        {
            return m_interfaceItems.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return m_interfaceItems.GetEnumerator();
        }
    }
}
