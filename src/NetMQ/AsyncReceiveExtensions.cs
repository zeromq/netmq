#if NETSTANDARD2_0 || NET47

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using JetBrains.Annotations;


namespace NetMQ
{
    /// <summary>
    /// Provides extension methods for the <see cref="NetMQSocket"/>,
    /// via which messages may be received asynchronously.
    /// </summary>
    [SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
    [SuppressMessage("ReSharper", "UnusedMember.Global")]
    [SuppressMessage("ReSharper", "UnusedMethodReturnValue.Global")]
    public static class AsyncReceiveExtensions
    {
        #region Receiving a frame as a byte array

        /// <summary>
        /// Receive a single frame from <paramref name="socket"/>, asynchronously.
        /// </summary>
        /// <param name="socket">The socket to receive from.</param>
        /// <returns>The content of the received message frame and boolean indicate if another frame of the same message follows.</returns>
        [NotNull]
        public static Task<(byte[], bool)> ReceiveFrameBytesAsync([NotNull] this NetMQSocket socket)
        {
            if (NetMQRuntime.Current == null)
                throw new InvalidOperationException("NetMQRuntime must be created before calling async functions");

            socket.AttachToRuntime();

            var msg = new Msg();
            msg.InitEmpty();

            if (socket.TryReceive(ref msg, TimeSpan.Zero))
            {
                var data = msg.CloneData();
                bool more = msg.HasMore;
                msg.Close();

                return Task.FromResult((data, more));
            }

            TaskCompletionSource<(byte[], bool)> source = new TaskCompletionSource<(byte[], bool)>();

            void Listener(object sender, NetMQSocketEventArgs args)
            {
                if (socket.TryReceive(ref msg, TimeSpan.Zero))
                {
                    var data = msg.CloneData();
                    bool more = msg.HasMore;
                    msg.Close();

                    socket.ReceiveReady -=  Listener;
                    source.SetResult((data, more));
                }
            }

            socket.ReceiveReady += Listener;

            return source.Task;
        }

        #endregion

        #region Receiving a frame as a string

        /// <summary>
        /// Receive a single frame from <paramref name="socket"/>, asynchronously, and decode as a string using <see cref="SendReceiveConstants.DefaultEncoding"/>.
        /// </summary>
        /// <param name="socket">The socket to receive from.</param>
        /// <returns>The content of the received message frame as a string and a boolean indicate if another frame of the same message follows.</returns>
        [NotNull]
        public static Task<(string, bool)> ReceiveFrameStringAsync([NotNull] this NetMQSocket socket)
        {
            return socket.ReceiveFrameStringAsync(SendReceiveConstants.DefaultEncoding);
        }


        /// <summary>
        /// Receive a single frame from <paramref name="socket"/>, asynchronously, and decode as a string using <paramref name="encoding"/>.
        /// </summary>
        /// <param name="socket">The socket to receive from.</param>
        /// <param name="encoding">The encoding used to convert the frame's data to a string.</param>
        /// <returns>The content of the received message frame as a string and boolean indicate if another frame of the same message follows..</returns>
        [NotNull]
        public static Task<(string, bool)> ReceiveFrameStringAsync([NotNull] this NetMQSocket socket, [NotNull] Encoding encoding)
        {
            if (NetMQRuntime.Current == null)
                throw new InvalidOperationException("NetMQRuntime must be created before calling async functions");

            socket.AttachToRuntime();

            var msg = new Msg();
            msg.InitEmpty();

            if (socket.TryReceive(ref msg, TimeSpan.Zero))
            {
                var str = msg.Size > 0
                    ? encoding.GetString(msg.Data, msg.Offset, msg.Size)
                    : string.Empty;

                msg.Close();
                return Task.FromResult((str, msg.HasMore));
            }

            TaskCompletionSource<(string, bool)> source = new TaskCompletionSource<(string,bool)>();

            void Listener(object sender, NetMQSocketEventArgs args)
            {
                if (socket.TryReceive(ref msg, TimeSpan.Zero))
                {
                    var str = msg.Size > 0
                        ? encoding.GetString(msg.Data, msg.Offset, msg.Size)
                        : string.Empty;
                    bool more = msg.HasMore;

                    msg.Close();
                    socket.ReceiveReady -=  Listener;
                    source.SetResult((str, more));
                }
            }

            socket.ReceiveReady += Listener;

            return source.Task;
        }

        #endregion

        #region Skipping a message

        /// <summary>
        /// Receive a single frame from <paramref name="socket"/>, asynchronously, then ignore its content.
        /// </summary>
        /// <param name="socket">The socket to receive from.</param>
        /// <returns>Boolean indicate if another frame of the same message follows</returns>
        public static Task<bool> SkipFrameAsync([NotNull] this NetMQSocket socket)
        {
            if (NetMQRuntime.Current == null)
                throw new InvalidOperationException("NetMQRuntime must be created before calling async functions");

            socket.AttachToRuntime();

            var msg = new Msg();
            msg.InitEmpty();

            if (socket.TryReceive(ref msg, TimeSpan.Zero))
            {
                bool more = msg.HasMore;
                msg.Close();
                return Task.FromResult(more);
            }

            TaskCompletionSource<bool> source = new TaskCompletionSource<bool>();

            void Listener(object sender, NetMQSocketEventArgs args)
            {
                if (socket.TryReceive(ref msg, TimeSpan.Zero))
                {
                    bool more = msg.HasMore;
                    msg.Close();
                    socket.ReceiveReady -=  Listener;
                    source.SetResult(more);
                }
            }

            socket.ReceiveReady += Listener;

            return source.Task;
        }


        #endregion

        #region Skipping all frames of a multipart message

        /// <summary>
        /// Receive all frames of the next message from <paramref name="socket"/>, asynchronously, then ignore their contents.
        /// </summary>
        /// <param name="socket">The socket to receive from.</param>
        public static async Task SkipMultipartMessageAsync([NotNull] this NetMQSocket socket)
        {
            while (true)
            {
                bool more = await socket.SkipFrameAsync();
                if (!more)
                    break;
            }
        }


        #endregion

        #region Receiving a routing key

        /// <summary>
        /// Receive a routing-key from <paramref name="socket"/>, blocking until one arrives.
        /// </summary>
        /// <param name="socket">The socket to receive from.</param>
        /// <returns>The routing key and a boolean indicate if another frame of the same message follows.</returns>

        public static async Task<(RoutingKey, bool)> ReceiveRoutingKeyAsync([NotNull] this NetMQSocket socket)
        {
            var (bytes, more) = await socket.ReceiveFrameBytesAsync();

            return (new RoutingKey(bytes), more);
        }

        #endregion
    }
}

#endif