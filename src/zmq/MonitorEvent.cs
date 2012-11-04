using System;
using System.Text;
using zmq;

public class MonitorEvent
{
	private const int VALUE_INTEGER = 1;
	private const int VALUE_CHANNEL = 2;

	private ZmqSocketEvent monitorEvent;
	private String addr;
	private Object arg;
	private int flag;

	public MonitorEvent(ZmqSocketEvent monitorEvent, String addr, Object arg)
	{
		this.monitorEvent = monitorEvent;
		this.addr = addr;
		this.arg = arg;
		if (arg is int)
			flag = VALUE_INTEGER;
		else if (arg is System.Net.Sockets.Socket)
			flag = VALUE_CHANNEL;
		else
			flag = 0;
	}

	public bool write(SocketBase s)
	{
		int size = 4 + 1 + addr.Length + 1; // event + len(addr) + addr + flag
		if (flag == VALUE_INTEGER)
			size += 4;

		int pos = 0;

		ByteArraySegment buffer = new byte[size];
		buffer.PutInteger((int)monitorEvent, pos);
		pos += 4;
		buffer[pos++] = (byte)addr.Length;

		// was not here originally

		buffer.PutString(addr, pos);
		pos += addr.Length;

		buffer[pos++] = ((byte)flag);
		if (flag == VALUE_INTEGER)
			buffer.PutInteger((int)arg, pos);
		pos += 4;

		Msg msg = new Msg((byte[])buffer);
		return s.send(msg, 0);
	}

	public static MonitorEvent read(SocketBase s)
	{
		Msg msg = s.recv(0);
		if (msg == null)
			return null;

		int pos = 0;
		byte[] data = msg.get_data();

		ZmqSocketEvent @event = (ZmqSocketEvent)BitConverter.ToInt32(data, pos);
		pos += 4;
		int len = (int)data[pos++];
		string addr = Encoding.ASCII.GetString(data, pos, len);
		pos += len;
		int flag = (int)data[pos++];
		Object arg = null;

		if (flag == VALUE_INTEGER)
			arg = BitConverter.ToInt32(data, pos);

		return new MonitorEvent(@event, addr, arg);
	}
}