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

namespace NetMQ.zmq
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