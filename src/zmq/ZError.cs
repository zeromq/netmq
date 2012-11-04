/*
    Copyright other contributors as noted in the AUTHORS file.
    
    This file is part of 0MQ.

    0MQ is free software; you can redistribute it and/or modify it under
    the terms of the GNU Lesser General Public License as published by
    the Free Software Foundation; either version 3 of the License, or
    (at your option) any later version.

    0MQ is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU Lesser General Public License for more details.

    You should have received a copy of the GNU Lesser General Public License
    along with this program.  If not, see <http://www.gnu.org/licenses/>.
*/

using System.Threading;

public struct ErrorNumber
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

namespace zmq
{
	public class ZError
	{
		private static ThreadLocal<ErrorNumber> s_errno = new ThreadLocal<ErrorNumber>(() => 0);
	
		public static ErrorNumber ErrorNumber
		{
			get
			{
				return s_errno.Value;
			}
			set
			{
				s_errno.Value = value;
			}
		}

		public static bool IsError(int code)
		{
			if (code == ErrorNumber.EINTR.Value)
			{
				return false;
			}
			else
			{
				return ErrorNumber.Value == code;
			}
		}

		public static bool IsError(ErrorNumber code)
		{
			return IsError(code.Value);
		}

		public static void Clear()
		{
			ErrorNumber = 0;
		}


	}
}
