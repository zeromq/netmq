/*
    Copyright (c) 2007-2012 iMatix Corporation
    Copyright (c) 2009-2011 250bpm s.r.o.
    Copyright (c) 2007-2015 Other contributors as noted in the AUTHORS file

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

using System;
using System.Text;
using JetBrains.Annotations;

namespace NetMQ.Core.Patterns
{
    internal sealed class Sub : XSub
    {
        public class SubSession : XSubSession
        {
            public SubSession([NotNull] IOThread ioThread, bool connect, [NotNull] SocketBase socket, [NotNull] Options options, [NotNull] Address addr)
                : base(ioThread, connect, socket, options, addr)
            {}
        }

        public Sub([NotNull] Ctx parent, int threadId, int socketId)
            : base(parent, threadId, socketId)
        {
            m_options.SocketType = ZmqSocketType.Sub;

            // Switch filtering messages on (as opposed to XSUB which where the filtering is off).
            m_options.Filter = true;
        }

        /// <summary>
        /// Set the specified option on this socket - which must be either a SubScribe or an Unsubscribe.
        /// </summary>
        /// <param name="option">which option to set</param>
        /// <param name="optionValue">the value to set the option to</param>
        /// <returns><c>true</c> if successful</returns>
        /// <exception cref="InvalidException">optionValue must be a String or a byte-array.</exception>
        protected override bool XSetSocketOption(ZmqSocketOption option, object optionValue)
        {
            // Only subscribe/unsubscribe options are supported
            if (option != ZmqSocketOption.Subscribe && option != ZmqSocketOption.Unsubscribe)
                return false;

            byte[] topic;
            if (optionValue is string)
                topic = Encoding.ASCII.GetBytes((string)optionValue);
            else if (optionValue is byte[])
                topic = (byte[])optionValue;
            else
                throw new InvalidException($"In Sub.XSetSocketOption({option},{optionValue?.ToString() ?? "null"}), optionValue must be either a string or a byte-array.");

            // Create the subscription message.
            var msg = new Msg();
            msg.InitPool(topic.Length + 1);
            msg.Put(option == ZmqSocketOption.Subscribe ? (byte)1 : (byte)0);
            msg.Put(topic, 1, topic.Length);

            try
            {
                // Pass it further on in the stack.
                var isMessageSent = base.XSend(ref msg);

                if (!isMessageSent)
                    throw new Exception($"in Sub.XSetSocketOption({option}, {optionValue}), XSend returned false.");
            }
            finally
            {
                msg.Close();
            }

            return true;
        }

        /// <summary>
        /// XSend transmits a given message. The <c>Send</c> method calls this to do the actual sending.
        /// This override of that abstract method simply throws NotSupportedException because XSend is not supported on a Sub socket.
        /// </summary>
        /// <param name="msg">the message to transmit</param>
        /// <exception cref="NotSupportedException">XSend not supported on Sub socket</exception>
        protected override bool XSend(ref Msg msg)
        {
            // Overload the XSUB's send.
            throw new NotSupportedException("XSend not supported on Sub socket");
        }

        /// <summary>
        /// Return false to indicate that XHasOut is not applicable on a Sub socket.
        /// </summary>
        /// <returns></returns>
        protected override bool XHasOut()
        {
            // Overload the XSUB's send.
            return false;
        }
    }
}