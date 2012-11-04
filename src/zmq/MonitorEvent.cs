using System;
using System.Text;

namespace zmq
{
	public class MonitorEvent
	{
		private const int ValueInteger = 1;
		private const int ValueChannel = 2;

		private readonly SocketEvent m_monitorEvent;
		private readonly String m_addr;
		private readonly Object m_arg;
		private readonly int m_flag;

		public MonitorEvent(SocketEvent monitorEvent, String addr, Object arg)
		{
			this.m_monitorEvent = monitorEvent;
			this.m_addr = addr;
			this.m_arg = arg;
			if (arg is int)
				m_flag = ValueInteger;
			else if (arg is System.Net.Sockets.Socket)
				m_flag = ValueChannel;
			else
				m_flag = 0;
		}

		public bool write(SocketBase s)
		{
			int size = 4 + 1 + m_addr.Length + 1; // event + len(addr) + addr + flag
			if (m_flag == ValueInteger)
				size += 4;

			int pos = 0;

			ByteArraySegment buffer = new byte[size];
			buffer.PutInteger((int)m_monitorEvent, pos);
			pos += 4;
			buffer[pos++] = (byte)m_addr.Length;

			// was not here originally

			buffer.PutString(m_addr, pos);
			pos += m_addr.Length;

			buffer[pos++] = ((byte)m_flag);
			if (m_flag == ValueInteger)
				buffer.PutInteger((int)m_arg, pos);
			pos += 4;

			Msg msg = new Msg((byte[])buffer);
			return s.Send(msg, 0);
		}

		public static MonitorEvent Read(SocketBase s)
		{
			Msg msg = s.Recv(0);
			if (msg == null)
				return null;

			int pos = 0;
			byte[] data = msg.Data;

			SocketEvent @event = (SocketEvent)BitConverter.ToInt32(data, pos);
			pos += 4;
			int len = (int)data[pos++];
			string addr = Encoding.ASCII.GetString(data, pos, len);
			pos += len;
			int flag = (int)data[pos++];
			Object arg = null;

			if (flag == ValueInteger)
				arg = BitConverter.ToInt32(data, pos);

			return new MonitorEvent(@event, addr, arg);
		}
	}
}