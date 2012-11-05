namespace ZeroMQ
{
    using System;
    using System.Text;

    /// <summary>
    /// Defines extensions for Send/Receive methods in <see cref="ZmqSocket"/>.
    /// </summary>
    public static class SendReceiveExtensions
    {
        /// <summary>
        /// Queue a single-part (or final multi-part) message buffer to be sent by the socket in blocking mode.
        /// </summary>
        /// <remarks>
        /// This method assumes that the message fills the entire buffer.
        /// </remarks>
        /// <param name="socket">A <see cref="ZmqSocket"/> object.</param>
        /// <param name="buffer">A <see cref="byte"/> array that contains the message to be sent.</param>
        /// <returns>A <see cref="SendStatus"/> describing the outcome of the send operation.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="buffer"/> is null.</exception>
        /// <exception cref="ZmqSocketException">An error occurred sending data to a remote endpoint.</exception>
        /// <exception cref="ObjectDisposedException">The <see cref="ZmqSocket"/> has been closed.</exception>
        /// <exception cref="NotSupportedException">The current socket type does not support Send operations.</exception>
        public static SendStatus Send(this ZmqSocket socket, byte[] buffer)
        {
            VerifySocket(socket);

            socket.Send(buffer, buffer.Length, SocketFlags.None);

            return socket.SendStatus;
        }

        /// <summary>
        /// Queue a single-part (or final multi-part) message buffer to be sent by the socket in
        /// non-blocking mode with a specified timeout.
        /// </summary>
        /// <remarks>
        /// This method assumes that the message fills the entire buffer.
        /// </remarks>
        /// <param name="socket">A <see cref="ZmqSocket"/> object.</param>
        /// <param name="buffer">A <see cref="byte"/> array that contains the message to be sent.</param>
        /// <param name="timeout">A <see cref="TimeSpan"/> specifying the send timeout.</param>
        /// <returns>A <see cref="SendStatus"/> describing the outcome of the send operation.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="buffer"/> is null.</exception>
        /// <exception cref="ZmqSocketException">An error occurred sending data to a remote endpoint.</exception>
        /// <exception cref="ObjectDisposedException">The <see cref="ZmqSocket"/> has been closed.</exception>
        /// <exception cref="NotSupportedException">The current socket type does not support Send operations.</exception>
        public static SendStatus Send(this ZmqSocket socket, byte[] buffer, TimeSpan timeout)
        {
            VerifySocket(socket);

            socket.Send(buffer, buffer.Length, SocketFlags.None, timeout);

            return socket.SendStatus;
        }

        /// <summary>
        /// Queue a single-part (or final multi-part) message string to be sent by the socket in blocking mode.
        /// </summary>
        /// <param name="socket">A <see cref="ZmqSocket"/> object.</param>
        /// <param name="message">A <see cref="string"/> that contains the message to be sent.</param>
        /// <param name="encoding">The <see cref="Encoding"/> to use when converting <paramref name="message"/> to a buffer.</param>
        /// <returns>A <see cref="SendStatus"/> describing the outcome of the send operation.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="message"/> or <paramref name="encoding"/> is null.</exception>
        /// <exception cref="ZmqSocketException">An error occurred sending data to a remote endpoint.</exception>
        /// <exception cref="ObjectDisposedException">The <see cref="ZmqSocket"/> has been closed.</exception>
        /// <exception cref="NotSupportedException">The current socket type does not support Send operations.</exception>
        public static SendStatus Send(this ZmqSocket socket, string message, Encoding encoding)
        {
            return Send(socket, message, encoding, TimeSpan.MaxValue);
        }

        /// <summary>
        /// Queue a single-part (or final multi-part) message string to be sent by the socket in
        /// non-blocking mode with a specified timeout.
        /// </summary>
        /// <param name="socket">A <see cref="ZmqSocket"/> object.</param>
        /// <param name="message">A <see cref="string"/> that contains the message to be sent.</param>
        /// <param name="encoding">The <see cref="Encoding"/> to use when converting <paramref name="message"/> to a buffer.</param>
        /// <param name="timeout">A <see cref="TimeSpan"/> specifying the send timeout.</param>
        /// <returns>A <see cref="SendStatus"/> describing the outcome of the send operation.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="message"/> or <paramref name="encoding"/> is null.</exception>
        /// <exception cref="ZmqSocketException">An error occurred sending data to a remote endpoint.</exception>
        /// <exception cref="ObjectDisposedException">The <see cref="ZmqSocket"/> has been closed.</exception>
        /// <exception cref="NotSupportedException">The current socket type does not support Send operations.</exception>
        public static SendStatus Send(this ZmqSocket socket, string message, Encoding encoding, TimeSpan timeout)
        {
            VerifySocket(socket);
            VerifyStringMessage(message);
            VerifyEncoding(encoding);

            byte[] buffer = encoding.GetBytes(message);

            socket.Send(buffer, buffer.Length, SocketFlags.None, timeout);

            return socket.SendStatus;
        }

        /// <summary>
        /// Queue a non-final message-part buffer to be sent by the socket in blocking mode.
        /// </summary>
        /// <remarks>
        /// This method assumes that the message fills the entire buffer. The final message-part in
        /// this series must be sent with <see cref="Send(ZeroMQ.ZmqSocket,byte[])"/> or another overload
        /// that does not specify <see cref="SocketFlags.SendMore"/>.
        /// </remarks>
        /// <param name="socket">A <see cref="ZmqSocket"/> object.</param>
        /// <param name="buffer">A <see cref="byte"/> array that contains the message to be sent.</param>
        /// <returns>A <see cref="SendStatus"/> describing the outcome of the send operation.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="buffer"/> is null.</exception>
        /// <exception cref="ZmqSocketException">An error occurred sending data to a remote endpoint.</exception>
        /// <exception cref="ObjectDisposedException">The <see cref="ZmqSocket"/> has been closed.</exception>
        /// <exception cref="NotSupportedException">The current socket type does not support Send operations.</exception>
        public static SendStatus SendMore(this ZmqSocket socket, byte[] buffer)
        {
            return SendMore(socket, buffer, TimeSpan.MaxValue);
        }

        /// <summary>
        /// Queue a non-final message-part buffer to be sent by the socket in non-blocking mode with a specified timeout.
        /// </summary>
        /// <remarks>
        /// This method assumes that the message fills the entire buffer. The final message-part in
        /// this series must be sent with <see cref="Send(ZeroMQ.ZmqSocket,byte[],TimeSpan)"/> or another overload
        /// that does not specify <see cref="SocketFlags.SendMore"/>.
        /// </remarks>
        /// <param name="socket">A <see cref="ZmqSocket"/> object.</param>
        /// <param name="buffer">A <see cref="byte"/> array that contains the message to be sent.</param>
        /// <param name="timeout">A <see cref="TimeSpan"/> specifying the send timeout.</param>
        /// <returns>A <see cref="SendStatus"/> describing the outcome of the send operation.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="buffer"/> is null.</exception>
        /// <exception cref="ZmqSocketException">An error occurred sending data to a remote endpoint.</exception>
        /// <exception cref="ObjectDisposedException">The <see cref="ZmqSocket"/> has been closed.</exception>
        /// <exception cref="NotSupportedException">The current socket type does not support Send operations.</exception>
        public static SendStatus SendMore(this ZmqSocket socket, byte[] buffer, TimeSpan timeout)
        {
            VerifySocket(socket);

            socket.Send(buffer, buffer.Length, SocketFlags.SendMore, timeout);

            return socket.SendStatus;
        }

        /// <summary>
        /// Queue a non-final message-part string to be sent by the socket in blocking mode.
        /// </summary>
        /// <param name="socket">A <see cref="ZmqSocket"/> object.</param>
        /// <param name="message">A <see cref="string"/> that contains the message to be sent.</param>
        /// <param name="encoding">The <see cref="Encoding"/> to use when converting <paramref name="message"/> to a buffer.</param>
        /// <returns>A <see cref="SendStatus"/> describing the outcome of the send operation.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="message"/> or <paramref name="encoding"/> is null.</exception>
        /// <exception cref="ZmqSocketException">An error occurred sending data to a remote endpoint.</exception>
        /// <exception cref="ObjectDisposedException">The <see cref="ZmqSocket"/> has been closed.</exception>
        /// <exception cref="NotSupportedException">The current socket type does not support Send operations.</exception>
        public static SendStatus SendMore(this ZmqSocket socket, string message, Encoding encoding)
        {
            return SendMore(socket, message, encoding, TimeSpan.MaxValue);
        }

        /// <summary>
        /// Queue a non-final message-part string to be sent by the socket in non-blocking mode with a specified timeout.
        /// </summary>
        /// <param name="socket">A <see cref="ZmqSocket"/> object.</param>
        /// <param name="message">A <see cref="string"/> that contains the message to be sent.</param>
        /// <param name="encoding">The <see cref="Encoding"/> to use when converting <paramref name="message"/> to a buffer.</param>
        /// <param name="timeout">A <see cref="TimeSpan"/> specifying the send timeout.</param>
        /// <returns>A <see cref="SendStatus"/> describing the outcome of the send operation.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="message"/> or <paramref name="encoding"/> is null.</exception>
        /// <exception cref="ZmqSocketException">An error occurred sending data to a remote endpoint.</exception>
        /// <exception cref="ObjectDisposedException">The <see cref="ZmqSocket"/> has been closed.</exception>
        /// <exception cref="NotSupportedException">The current socket type does not support Send operations.</exception>
        public static SendStatus SendMore(this ZmqSocket socket, string message, Encoding encoding, TimeSpan timeout)
        {
            VerifySocket(socket);
            VerifyStringMessage(message);
            VerifyEncoding(encoding);

            byte[] buffer = encoding.GetBytes(message);

            socket.Send(buffer, buffer.Length, SocketFlags.SendMore, timeout);

            return socket.SendStatus;
        }

        /// <summary>
        /// Receive a string message from a remote socket in blocking mode.
        /// </summary>
        /// <param name="socket">A <see cref="ZmqSocket"/> object.</param>
        /// <param name="encoding">The <see cref="Encoding"/> to use when converting the received buffer to a string.</param>
        /// <returns>A <see cref="string"/> containing the message received from the remote endpoint.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="encoding"/> is null.</exception>
        /// <exception cref="ZmqSocketException">An error occurred receiving data from a remote endpoint.</exception>
        /// <exception cref="ObjectDisposedException">The <see cref="ZmqSocket"/> has been closed.</exception>
        /// <exception cref="NotSupportedException">The current socket type does not support Receive operations.</exception>
        public static string Receive(this ZmqSocket socket, Encoding encoding)
        {
            return Receive(socket, encoding, TimeSpan.MaxValue);
        }

        /// <summary>
        /// Receive a string message from a remote socket in non-blocking mode with a specified timeout.
        /// </summary>
        /// <param name="socket">A <see cref="ZmqSocket"/> object.</param>
        /// <param name="encoding">The <see cref="Encoding"/> to use when converting the received buffer to a string.</param>
        /// <param name="timeout">A <see cref="TimeSpan"/> specifying the receive timeout.</param>
        /// <returns>
        /// A <see cref="string"/> containing the message received from the remote endpoint or <c>null</c>
        /// if the timeout expired before a message was received.
        /// </returns>
        /// <exception cref="ArgumentNullException"><paramref name="encoding"/> is null.</exception>
        /// <exception cref="ZmqSocketException">An error occurred receiving data from a remote endpoint.</exception>
        /// <exception cref="ObjectDisposedException">The <see cref="ZmqSocket"/> has been closed.</exception>
        /// <exception cref="NotSupportedException">The current socket type does not support Receive operations.</exception>
        public static string Receive(this ZmqSocket socket, Encoding encoding, TimeSpan timeout)
        {
            VerifySocket(socket);
            VerifyEncoding(encoding);

            int messageSize;
            byte[] buffer = socket.Receive(null, timeout, out messageSize);

            return socket.ReceiveStatus == ReceiveStatus.Received
                ? encoding.GetString(buffer, 0, messageSize)
                : null;
        }

        /// <summary>
        /// Receive a single frame from a remote socket in blocking mode.
        /// </summary>
        /// <remarks>
        /// This overload will allocate a new <see cref="Frame"/> for receiving all available data in the message-part.
        /// </remarks>
        /// <param name="socket">A <see cref="ZmqSocket"/> object.</param>
        /// <returns>A <see cref="Frame"/> containing the data received from the remote endpoint.</returns>
        /// <exception cref="ZmqSocketException">An error occurred receiving data from a remote endpoint.</exception>
        /// <exception cref="ObjectDisposedException">The <see cref="ZmqSocket"/> has been closed.</exception>
        /// <exception cref="NotSupportedException">The current socket type does not support Receive operations.</exception>
        public static Frame ReceiveFrame(this ZmqSocket socket)
        {
            return ReceiveFrame(socket, null);
        }

        /// <summary>
        /// Receive a single frame from a remote socket in non-blocking mode with a specified timeout.
        /// </summary>
        /// <remarks>
        /// This overload will allocate a new <see cref="Frame"/> for receiving all available data in the message-part.
        /// </remarks>
        /// <param name="socket">A <see cref="ZmqSocket"/> object.</param>
        /// <param name="timeout">A <see cref="TimeSpan"/> specifying the receive timeout.</param>
        /// <returns>A <see cref="Frame"/> containing the data received from the remote endpoint.</returns>
        /// <exception cref="ZmqSocketException">An error occurred receiving data from a remote endpoint.</exception>
        /// <exception cref="ObjectDisposedException">The <see cref="ZmqSocket"/> has been closed.</exception>
        /// <exception cref="NotSupportedException">The current socket type does not support Receive operations.</exception>
        public static Frame ReceiveFrame(this ZmqSocket socket, TimeSpan timeout)
        {
            return ReceiveFrame(socket, null, timeout);
        }

        /// <summary>
        /// Receive a single frame from a remote socket in blocking mode.
        /// </summary>
        /// <remarks>
        /// This overload will receive all available data in the message-part. If the buffer size of <paramref name="frame"/>
        /// is insufficient, a new buffer will be allocated.
        /// </remarks>
        /// <param name="socket">A <see cref="ZmqSocket"/> object.</param>
        /// <param name="frame">A <see cref="Frame"/> that will store the received data.</param>
        /// <returns>A <see cref="Frame"/> containing the data received from the remote endpoint.</returns>
        /// <exception cref="ZmqSocketException">An error occurred receiving data from a remote endpoint.</exception>
        /// <exception cref="ObjectDisposedException">The <see cref="ZmqSocket"/> has been closed.</exception>
        /// <exception cref="NotSupportedException">The current socket type does not support Receive operations.</exception>
        public static Frame ReceiveFrame(this ZmqSocket socket, Frame frame)
        {
            VerifySocket(socket);

            if (frame == null)
            {
                frame = Frame.Empty;
            }

            int size;

            frame.Buffer = socket.Receive(frame.Buffer, out size);
            SetFrameProperties(frame, socket, size);

            return frame;
        }

        /// <summary>
        /// Receive a single frame from a remote socket in non-blocking mode with a specified timeout.
        /// </summary>
        /// <remarks>
        /// This overload will receive all available data in the message-part. If the buffer size of <paramref name="frame"/>
        /// is insufficient, a new buffer will be allocated.
        /// </remarks>
        /// <param name="socket">A <see cref="ZmqSocket"/> object.</param>
        /// <param name="frame">A <see cref="Frame"/> that will store the received data.</param>
        /// <param name="timeout">A <see cref="TimeSpan"/> specifying the receive timeout.</param>
        /// <returns>A <see cref="Frame"/> containing the data received from the remote endpoint.</returns>
        /// <exception cref="ZmqSocketException">An error occurred receiving data from a remote endpoint.</exception>
        /// <exception cref="ObjectDisposedException">The <see cref="ZmqSocket"/> has been closed.</exception>
        /// <exception cref="NotSupportedException">The current socket type does not support Receive operations.</exception>
        public static Frame ReceiveFrame(this ZmqSocket socket, Frame frame, TimeSpan timeout)
        {
            VerifySocket(socket);

            if (frame == null)
            {
                frame = Frame.Empty;
            }

            int size;

            frame.Buffer = socket.Receive(frame.Buffer, timeout, out size);
            SetFrameProperties(frame, socket, size);

            return frame;
        }

        /// <summary>
        /// Queue a message frame to be sent by the socket in blocking mode.
        /// </summary>
        /// <remarks>
        /// The <see cref="Frame.HasMore"/> property on <paramref name="frame"/> will be used to indicate whether
        /// more frames will follow in the current multi-part message sequence.
        /// </remarks>
        /// <param name="socket">A <see cref="ZmqSocket"/> object.</param>
        /// <param name="frame">A <see cref="Frame"/> that contains the message to be sent.</param>
        /// <returns>A <see cref="SendStatus"/> describing the outcome of the send operation.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="frame"/> is null.</exception>
        /// <exception cref="ZmqSocketException">An error occurred sending data to a remote endpoint.</exception>
        /// <exception cref="ObjectDisposedException">The <see cref="ZmqSocket"/> has been closed.</exception>
        /// <exception cref="NotSupportedException">The current socket type does not support Send operations.</exception>
        public static SendStatus SendFrame(this ZmqSocket socket, Frame frame)
        {
            return SendFrame(socket, frame, TimeSpan.MaxValue);
        }

        /// <summary>
        /// Queue a message frame to be sent by the socket in non-blocking mode with a specified timeout.
        /// </summary>
        /// <remarks>
        /// The <see cref="Frame.HasMore"/> property on <paramref name="frame"/> will be used to indicate whether
        /// more frames will follow in the current multi-part message sequence.
        /// </remarks>
        /// <param name="socket">A <see cref="ZmqSocket"/> object.</param>
        /// <param name="frame">A <see cref="Frame"/> that contains the message to be sent.</param>
        /// <param name="timeout">A <see cref="TimeSpan"/> specifying the send timeout.</param>
        /// <returns>A <see cref="SendStatus"/> describing the outcome of the send operation.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="frame"/> is null.</exception>
        /// <exception cref="ZmqSocketException">An error occurred sending data to a remote endpoint.</exception>
        /// <exception cref="ObjectDisposedException">The <see cref="ZmqSocket"/> has been closed.</exception>
        /// <exception cref="NotSupportedException">The current socket type does not support Send operations.</exception>
        public static SendStatus SendFrame(this ZmqSocket socket, Frame frame, TimeSpan timeout)
        {
            VerifySocket(socket);
            VerifyFrame(frame);

            socket.Send(frame.Buffer, frame.MessageSize, frame.HasMore ? SocketFlags.SendMore : SocketFlags.None, timeout);

            return socket.SendStatus;
        }

        /// <summary>
        /// Receive all parts of a multi-part message from a remote socket in blocking mode.
        /// </summary>
        /// <param name="socket">A <see cref="ZmqSocket"/> object.</param>
        /// <returns>A <see cref="ZmqMessage"/> containing a collection of <see cref="Frame"/>s received from the remote endpoint.</returns>
        /// <exception cref="ZmqSocketException">An error occurred receiving data from a remote endpoint.</exception>
        /// <exception cref="ObjectDisposedException">The <see cref="ZmqSocket"/> has been closed.</exception>
        /// <exception cref="NotSupportedException">The current socket type does not support Receive operations.</exception>
        public static ZmqMessage ReceiveMessage(this ZmqSocket socket)
        {
            return ReceiveMessage(socket, new ZmqMessage());
        }

        /// <summary>
        /// Receive all parts of a multi-part message from a remote socket in blocking mode
        /// and append them to a given message.
        /// </summary>
        /// <param name="socket">A <see cref="ZmqSocket"/> object.</param>
        /// <param name="message">The <see cref="ZmqMessage"/> to which message-parts will be appended.</param>
        /// <returns>The supplied <see cref="ZmqMessage"/> with newly received <see cref="Frame"/> objects appended.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="message"/> is null.</exception>
        /// <exception cref="ZmqSocketException">An error occurred receiving data from a remote endpoint.</exception>
        /// <exception cref="ObjectDisposedException">The <see cref="ZmqSocket"/> has been closed.</exception>
        /// <exception cref="NotSupportedException">The current socket type does not support Receive operations.</exception>
        public static ZmqMessage ReceiveMessage(this ZmqSocket socket, ZmqMessage message)
        {
            return ReceiveMessage(socket, message, TimeSpan.MaxValue);
        }

        /// <summary>
        /// Receive all parts of a multi-part message from a remote socket in non-blocking mode.
        /// </summary>
        /// <remarks>
        /// The <paramref name="frameTimeout"/> will be used for each underlying Receive operation. If the timeout
        /// elapses before the last message is received, an incomplete message will be returned. Use the
        /// <see cref="ReceiveMessage(ZeroMQ.ZmqSocket,ZeroMQ.ZmqMessage,System.TimeSpan)"/> overload to continue
        /// appending message-parts if the returned <see cref="ZmqMessage"/> has its <see cref="ZmqMessage.IsComplete"/>
        /// property set to false.
        /// </remarks>
        /// <param name="socket">A <see cref="ZmqSocket"/> object.</param>
        /// <param name="frameTimeout">A <see cref="TimeSpan"/> specifying the receive timeout for each frame.</param>
        /// <returns>A <see cref="ZmqMessage"/> containing newly received <see cref="Frame"/> objects.</returns>
        /// <exception cref="ZmqSocketException">An error occurred receiving data from a remote endpoint.</exception>
        /// <exception cref="ObjectDisposedException">The <see cref="ZmqSocket"/> has been closed.</exception>
        /// <exception cref="NotSupportedException">The current socket type does not support Receive operations.</exception>
        public static ZmqMessage ReceiveMessage(this ZmqSocket socket, TimeSpan frameTimeout)
        {
            return ReceiveMessage(socket, new ZmqMessage(), frameTimeout);
        }

        /// <summary>
        /// Receive all parts of a multi-part message from a remote socket in non-blocking mode.
        /// </summary>
        /// <remarks>
        /// The <paramref name="frameTimeout"/> will be used for each underlying Receive operation. If the timeout
        /// elapses before the last message is received, an incomplete message will be returned.
        /// </remarks>
        /// <param name="socket">A <see cref="ZmqSocket"/> object.</param>
        /// <param name="message">The <see cref="ZmqMessage"/> to which message-parts will be appended.</param>
        /// <param name="frameTimeout">A <see cref="TimeSpan"/> specifying the receive timeout for each frame.</param>
        /// <returns>A <see cref="ZmqMessage"/> containing newly received <see cref="Frame"/> objects.</returns>
        /// <exception cref="ZmqSocketException">An error occurred receiving data from a remote endpoint.</exception>
        /// <exception cref="ObjectDisposedException">The <see cref="ZmqSocket"/> has been closed.</exception>
        /// <exception cref="NotSupportedException">The current socket type does not support Receive operations.</exception>
        public static ZmqMessage ReceiveMessage(this ZmqSocket socket, ZmqMessage message, TimeSpan frameTimeout)
        {
            VerifySocket(socket);
            VerifyMessage(message);

            Frame frame;

            do
            {
                frame = socket.ReceiveFrame(frameTimeout);

                if (frame.ReceiveStatus == ReceiveStatus.Received)
                {
                    message.AppendShallowCopy(frame);
                }
            }
            while (frame.ReceiveStatus == ReceiveStatus.Received && frame.HasMore);

            return message;
        }

        /// <summary>
        /// Queue a multi-part message to be sent by the socket in blocking mode.
        /// </summary>
        /// <param name="socket">A <see cref="ZmqSocket"/> object.</param>
        /// <param name="message">A <see cref="ZmqMessage"/> that contains the message parts to be sent.</param>
        /// <returns>A <see cref="SendStatus"/> describing the outcome of the send operation.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="message"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="message"/> is incomplete.</exception>
        /// <exception cref="ZmqSocketException">An error occurred sending data to a remote endpoint.</exception>
        /// <exception cref="ObjectDisposedException">The <see cref="ZmqSocket"/> has been closed.</exception>
        /// <exception cref="NotSupportedException">The current socket type does not support Send operations.</exception>
        public static SendStatus SendMessage(this ZmqSocket socket, ZmqMessage message)
        {
            VerifySocket(socket);
            VerifyMessage(message);

            if (message.IsEmpty)
            {
                return SendStatus.Sent;
            }

            if (!message.IsComplete)
            {
                throw new ArgumentException("Unable to send an incomplete message. Ensure HasMore on the last Frame is set to 'false'.", "message");
            }

            foreach (Frame frame in message)
            {
                socket.SendFrame(frame);
            }

            return socket.SendStatus;
        }

        private static void SetFrameProperties(Frame frame, ZmqSocket socket, int size)
        {
            if (size >= 0)
            {
                frame.MessageSize = size;
            }

            frame.HasMore = socket.ReceiveMore;
            frame.ReceiveStatus = socket.ReceiveStatus;
        }

        private static void VerifySocket(ZmqSocket socket)
        {
            if (socket == null)
            {
                throw new ArgumentNullException("socket");
            }
        }

        private static void VerifyMessage(ZmqMessage message)
        {
            if (message == null)
            {
                throw new ArgumentNullException("message");
            }
        }

        private static void VerifyFrame(Frame frame)
        {
            if (frame == null)
            {
                throw new ArgumentNullException("frame");
            }
        }

        private static void VerifyStringMessage(string message)
        {
            if (message == null)
            {
                throw new ArgumentNullException("message");
            }
        }

        private static void VerifyEncoding(Encoding encoding)
        {
            if (encoding == null)
            {
                throw new ArgumentNullException("encoding");
            }
        }
    }
}
