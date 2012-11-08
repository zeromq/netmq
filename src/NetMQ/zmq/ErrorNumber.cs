using System;

namespace NetMQ.zmq
{
	public struct ErrorNumber : IEquatable<ErrorNumber>
	{
		public ErrorNumber(int value)
			: this()
		{
			Value = value;
		}

		public int Value { get; private set; }

		public static implicit operator ErrorNumber(int code)
		{
			return new ErrorNumber(code);
		}

		public bool Equals(ErrorNumber other)
		{
			return other.Value == Value;
		}

		public override bool Equals(object obj)
		{
			if (ReferenceEquals(null, obj)) return false;
			if (obj.GetType() != typeof (ErrorNumber)) return false;
			return Equals((ErrorNumber) obj);
		}

		public override int GetHashCode()
		{
			return Value;
		}

		public static bool operator ==(ErrorNumber left, ErrorNumber right)
		{
			return left.Equals(right);
		}

		public static bool operator !=(ErrorNumber left, ErrorNumber right)
		{
			return !left.Equals(right);
		}

		public static readonly ErrorNumber EINTR = 4;
		public static readonly ErrorNumber EACCESS = 13;
		public static readonly ErrorNumber EFAULT = 14;
		public static readonly ErrorNumber EINVAL = 22;
		public static readonly ErrorNumber EAGAIN = 35;
		public static readonly ErrorNumber EINPROGRESS = 36;
		public static readonly ErrorNumber EPROTONOSUPPORT = 43;
		public static readonly ErrorNumber ENOTSUP = 45;
		public static readonly ErrorNumber EADDRINUSE = 48;
		public static readonly ErrorNumber EADDRNOTAVAIL = 49;
		public static readonly ErrorNumber ENETDOWN = 50;
		public static readonly ErrorNumber ENOBUFS = 55;
		public static readonly ErrorNumber EISCONN = 56;
		public static readonly ErrorNumber ENOTCONN = 57;
		public static readonly ErrorNumber ECONNREFUSED = 61;
		public static readonly ErrorNumber EHOSTUNREACH = 65;

		private static readonly ErrorNumber ZMQ_HAUSNUMERO = 156384712;

		public static readonly ErrorNumber EFSM = new ErrorNumber(ZMQ_HAUSNUMERO.Value + 51);
		public static readonly ErrorNumber ENOCOMPATPROTO = new ErrorNumber(ZMQ_HAUSNUMERO.Value + 52);
		public static readonly ErrorNumber ETERM = new ErrorNumber(ZMQ_HAUSNUMERO.Value + 53);
		public static readonly ErrorNumber EMTHREAD = new ErrorNumber(ZMQ_HAUSNUMERO.Value + 54);

		public static readonly ErrorNumber EIOEXC = new ErrorNumber(ZMQ_HAUSNUMERO.Value + 105);
		public static readonly ErrorNumber ESOCKET = new ErrorNumber(ZMQ_HAUSNUMERO.Value + 106);
		public static readonly ErrorNumber EMFILE = new ErrorNumber(ZMQ_HAUSNUMERO.Value + 107);
	}
}