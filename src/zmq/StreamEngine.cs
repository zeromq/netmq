/*
    Copyright (c) 2009-2011 250bpm s.r.o.
    Copyright (c) 2007-2009 iMatix Corporation
    Copyright (c) 2007-2011 Other contributors as noted in the AUTHORS file

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
using System.Net.Sockets;
using System.Diagnostics;

public class StreamEngine : IEngine, IPollEvents, IMsgSink
{

    //  Size of the greeting message:
    //  Preamble (10 bytes) + version (1 byte) + socket type (1 byte).
    private static int GREETING_SIZE = 12;

    //private IOObject io_object;
    private Socket handle;

    private ArraySegment<byte> inpos;
    private int insize;
    private DecoderBase decoder;
    private bool input_error;

    private ArraySegment<byte> outpos;
    private int outsize;
    private EncoderBase encoder;

    //  When true, we are still trying to determine whether
    //  the peer is using versioned protocol, and if so, which
    //  version.  When false, normal message flow has started.
    private bool handshaking;

    const int greeting_size = 12;

    //  The receive buffer holding the greeting message
    //  that we are receiving from the peer.
    private byte[] greeting = new byte[12];

    //  The number of bytes of the greeting message that
    //  we have already received.

    int greeting_bytes_read;

    //  The send buffer holding the greeting message
    //  that we are sending to the peer.
    private byte[] greeting_output_buffer = new byte[12];

    //  The session this engine is attached to.
    private SessionBase session;

    //  Detached transient session.
    //private SessionBase leftover_session;

    private Options options;

    // String representation of endpoint
    private String endpoint;

    private bool plugged;

    // Socket
    private SocketBase socket;

    private IOObject io_object;


    public StreamEngine(Socket fd_, Options options_, String endpoint_)
    {
        handle = fd_;
        //      inbuf = null;
        insize = 0;
        input_error = false;
        //        outbuf = null;
        outsize = 0;
        handshaking = true;
        session = null;
        options = options_;
        plugged = false;
        endpoint = endpoint_;
        socket = null;
        encoder = null;
        decoder = null;

        //  Put the socket into non-blocking mode.
        Utils.unblock_socket(handle);

        //  Set the socket buffer limits for the underlying socket.
        if (options.sndbuf != 0)
        {
            handle.SendBufferSize = ((int)options.sndbuf);
        }
        if (options.rcvbuf != 0)
        {
            handle.ReceiveBufferSize = ((int)options.rcvbuf);
        }



    }

    private DecoderBase new_decoder(int size, long max)
    {

        //if (options.decoder == null)
        return new Decoder(size, max);

        //try {
        //    Constructor<? : DecoderBase> dcon = 
        //                options.decoder.getConstructor(int.class, long.class);
        //    decoder = dcon.newInstance(size, max);
        //    return decoder;
        //} catch (SecurityException e) {
        //    throw new ZError.InstantiationException(e);
        //} catch (NoSuchMethodException e) {
        //    throw new ZError.InstantiationException(e);
        //} catch (InvocationTargetException e) {
        //    throw new ZError.InstantiationException(e);
        //} catch (IllegalAccessException e) {
        //    throw new ZError.InstantiationException(e);
        //} catch (InstantiationException e) {
        //    throw new ZError.InstantiationException(e);
        //}
    }

    private EncoderBase new_encoder(int size)
    {

        //if (options.encoder == null)
        return new Encoder(size);

        //try {
        //    Constructor<? : EncoderBase> econ = 
        //            options.encoder.getConstructor(int.class);
        //    encoder = econ.newInstance(size);
        //    return encoder;
        //} catch (SecurityException e) {
        //    throw new ZError.InstantiationException(e);
        //} catch (NoSuchMethodException e) {
        //    throw new ZError.InstantiationException(e);
        //} catch (InvocationTargetException e) {
        //    throw new ZError.InstantiationException(e);
        //} catch (IllegalAccessException e) {
        //    throw new ZError.InstantiationException(e);
        //} catch (InstantiationException e) {
        //    throw new ZError.InstantiationException(e);
        //}
    }



    public void destroy()
    {
        Debug.Assert(!plugged);

        if (handle != null)
        {
            try
            {
                handle.Close();
            }
            catch (SocketException)
            {
            }
            handle = null;
        }
    }

    public void plug(IOThread io_thread_,
            SessionBase session_)
    {
        Debug.Assert(!plugged);
        plugged = true;

        //  Connect to session object.
        Debug.Assert(session == null);
        Debug.Assert(session_ != null);
        session = session_;
        socket = session.get_soket();

        io_object = new IOObject(null);
        io_object.set_handler(this);
        //  Connect to I/O threads poller object.
        io_object.plug(io_thread_);
        io_object.add_fd(handle);

        //  Send the 'length' and 'flags' fields of the identity message.
        //  The 'length' field is encoded in the long format.

        greeting_output_buffer[outsize++] = ((byte)0xff);

        Buffer.BlockCopy(BitConverter.GetBytes((long)options.identity_size + 1), 0, greeting_output_buffer, 1, 8);
        outsize += 8;
        greeting_output_buffer[outsize++] = ((byte)0x7f);

        outpos = new ArraySegment<byte>(greeting_output_buffer,0, outsize);


        io_object.set_pollin(handle);
        io_object.set_pollout(handle);

        //  Flush all the data that may have been already received downstream.
        in_event();
    }

    private void unplug()
    {
        Debug.Assert(plugged);
        plugged = false;

        //  Cancel all fd subscriptions.
        if (!input_error)
            io_object.rm_fd(handle);

        //  Disconnect from I/O threads poller object.
        io_object.unplug();

        //  Disconnect from session object.
        if (encoder != null)
            encoder.set_msg_source(null);
        if (decoder != null)
            decoder.set_msg_sink(null);
        session = null;
    }

    public void terminate()
    {
        unplug();
        destroy();
    }

    public void in_event()
    {

        //  If still handshaking, receive and process the greeting message.
        if (handshaking)
            if (!handshake())
                return;

        Debug.Assert(decoder != null);
        bool disconnection = false;

        //  If there's no data to process in the buffer...
        if (insize == 0)
        {

            //  Retrieve the buffer and read as much data as possible.
            //  Note that buffer can be arbitrarily large. However, we assume
            //  the underlying TCP layer has fixed buffer size and thus the
            //  number of bytes read will be always limited.
            decoder.get_buffer(ref inpos);
            insize = inpos.Count;
            insize = read(inpos);

            //  Check whether the peer has closed the connection.
            if (insize == -1)
            {
                insize = 0;
                disconnection = true;
            }
        }

        //  Push the data to the decoder.
        int processed = decoder.process_buffer(
            new ArraySegment<byte>(inpos.Array, inpos.Offset, insize));

        if (processed == -1)
        {
            disconnection = true;
        }
        else
        {

            //  Stop polling for input if we got stuck.
            if (processed < insize)
                io_object.reset_pollin(handle);

            //  Adjust the buffer.
            inpos = new ArraySegment<byte>(inpos.Array, inpos.Offset + processed, inpos.Count - processed);
            insize -= processed;
        }

        //  Flush all messages the decoder may have produced.
        session.flush();

        //  An input error has occurred. If the last decoded message
        //  has already been accepted, we terminate the engine immediately.
        //  Otherwise, we stop waiting for socket events and postpone
        //  the termination until after the message is accepted.
        if (disconnection)
        {
            if (decoder.stalled())
            {
                io_object.rm_fd(handle);
                input_error = true;
            }
            else
                error();
        }

    }

    public void out_event()
    {
        //  If write buffer is empty, try to read new data from the encoder.
        if (outsize == 0)
        {
            outpos = new ArraySegment<byte>(outpos.Array, 0 ,0);
            encoder.get_data(ref outpos);
            outsize = outpos.Count;
            
            //  If there is no data to send, stop polling for output.
            if (outsize == 0)
            {
                io_object.reset_pollout(handle);
                
                return;
            }
        }

        //  If there are any data to write in write buffer, write as much as
        //  possible to the socket. Note that amount of data to write can be
        //  arbitratily large. However, we assume that underlying TCP layer has
        //  limited transmission buffer and thus the actual number of bytes
        //  written should be reasonably modest.
        int nbytes = write(outpos);

        //  IO error has occurred. We stop waiting for output events.
        //  The engine is not terminated until we detect input error;
        //  this is necessary to prevent losing incomming messages.
        if (nbytes == -1)
        {
            io_object.reset_pollout(handle);
            return;
        }

        outpos = new ArraySegment<byte>(outpos.Array, outpos.Offset + nbytes, outpos.Count - nbytes); 
        outsize -= nbytes;

        //  If we are still handshaking and there are no data
        //  to send, stop polling for output.
        if (handshaking)
            if (outsize == 0)
                io_object.reset_pollout(handle);
    }    

    public void timer_event(int id_)
    {
        throw new NotSupportedException();

    }

    public void activate_out()
    {
        io_object.set_pollout(handle);

        //  Speculative write: The assumption is that at the moment new message
        //  was sent by the user the socket is probably available for writing.
        //  Thus we try to write the data to socket avoiding polling for POLLOUT.
        //  Consequently, the latency should be better in request/reply scenarios.
        out_event();
    }

    public void activate_in()
    {
        if (input_error)
        {
            //  There was an input error but the engine could not
            //  be terminated (due to the stalled decoder).
            //  Flush the pending message and terminate the engine now.
            decoder.process_buffer(new ArraySegment<byte>(inpos.Array, inpos.Offset, 0));
            Debug.Assert(!decoder.stalled());
            session.flush();
            error();
            return;
        }

        io_object.set_pollin(handle);

        //  Speculative read.
        io_object.in_event();
    }

    private bool handshake()
    {
        Debug.Assert(handshaking);

        //  Receive the greeting.
        while (greeting_bytes_read < GREETING_SIZE)
        {
            ArraySegment<byte> greetingSegment = 
                new ArraySegment<byte>(greeting, greeting_bytes_read, greeting_size - greeting_bytes_read);

            int n = read(greetingSegment);
            if (n == -1)
            {
                error();
                return false;
            }

            if (n == 0)
                return false;

            greeting_bytes_read += n;

            //  We have received at least one byte from the peer.
            //  If the first byte is not 0xff, we know that the
            //  peer is using unversioned protocol.
            if (greeting[0] != 0xff)
                break;

            if (greeting_bytes_read < 10)
                continue;
            
            //  Inspect the right-most bit of the 10th byte (which coincides
            //  with the 'flags' field if a regular message was sent).
            //  Zero indicates this is a header of identity message
            //  (i.e. the peer is using the unversioned protocol).
            if ((greeting[9] & 0x01) == 0)
                break;

            //  The peer is using versioned protocol.
            //  Send the rest of the greeting, if necessary.
            if (!(outpos.Array == greeting_output_buffer && 
                outpos.Offset + outsize == greeting_size))
            {
                if (outsize == 0)
                    io_object.set_pollout(handle);

                outpos.Array[outpos.Offset + outsize++] = 1; // Protocol version
                outpos.Array[outpos.Offset + outsize++] = (byte)options.type;

                outpos = new ArraySegment<byte>(outpos.Array, outpos.Offset, outsize);
            }
        }

        //  Position of the version field in the greeting.
        int version_pos = 10;

        //  Is the peer using the unversioned protocol?
        //  If so, we send and receive rests of identity
        //  messages.
        if (greeting[0] != 0xff || (greeting[9] & 0x01) == 0)
        {
            encoder = new_encoder(Config.out_batch_size);
            encoder.set_msg_source(session);

            decoder = new_decoder(Config.in_batch_size, options.maxmsgsize);
            decoder.set_msg_sink(session);

            //  We have already sent the message header.
            //  Since there is no way to tell the encoder to
            //  skip the message header, we simply throw that
            //  header data away.
            int header_size = options.identity_size + 1 >= 255 ? 10 : 2;
            byte[] tmp = new byte[10];
            ArraySegment<byte> bufferp = new ArraySegment<byte>(tmp, 0, header_size);
            encoder.get_data(ref bufferp);
            
            Debug.Assert(bufferp.Count == header_size);

            //  Make sure the decoder sees the data we have already received.
            inpos = new ArraySegment<byte>( greeting, 0, greeting_bytes_read);            
            insize = greeting_bytes_read;

            //  To allow for interoperability with peers that do not forward
            //  their subscriptions, we inject a phony subsription
            //  message into the incomming message stream. To put this
            //  message right after the identity message, we temporarily
            //  divert the message stream from session to ourselves.
            if (options.type == ZMQ.ZMQ_PUB || options.type == ZMQ.ZMQ_XPUB)
                decoder.set_msg_sink(this);
        }
        else
            if (greeting[version_pos] == 0)
            {
                //  ZMTP/1.0 framing.
                encoder = new_encoder(Config.out_batch_size);
                encoder.set_msg_source(session);

                decoder = new_decoder(Config.in_batch_size, options.maxmsgsize);
                decoder.set_msg_sink(session);
            }
            else
            {
                //  v1 framing protocol.
                encoder = new V1Encoder(Config.out_batch_size, session);

                decoder = new V1Decoder(Config.in_batch_size, options.maxmsgsize, session);
            }
        // Start polling for output if necessary.
        if (outsize == 0)
            io_object.set_pollout(handle);

        //  Handshaking was successful.
        //  Switch into the normal message flow.
        handshaking = false;

        return true;
    }

    public bool push_msg(Msg msg_)
    {
        Debug.Assert(options.type == ZMQ.ZMQ_PUB || options.type == ZMQ.ZMQ_XPUB);

        //  The first message is identity.
        //  Let the session process it.
        bool rc = session.push_msg(msg_);
        Debug.Assert(rc);

        //  Inject the subscription message so that the ZMQ 2.x peer
        //  receives our messages.
        msg_ = new Msg(1);
        msg_.put((byte)1);
        rc = session.push_msg(msg_);
        session.flush();

        //  Once we have injected the subscription message, we can
        //  Divert the message flow back to the session.
        Debug.Assert(decoder != null);
        decoder.set_msg_sink(session);

        return rc;
    }

    private void error()
    {
        Debug.Assert(session != null);
        socket.event_disconnected(endpoint, handle);
        session.detach();
        unplug();
        destroy();
    }

    private int write(ArraySegment<byte> data)
    {
        int nbytes = 0;
        try
        {
            nbytes = handle.Send(data.Array, data.Offset, data.Count, SocketFlags.None);
        }
        catch (SocketException ex)
        {
            //  If not a single byte can be written to the socket in non-blocking mode
            //  we'll get an error (this may happen during the speculative write).
            if (ex.SocketErrorCode == SocketError.WouldBlock)
            {
                return 0;
            }
            else if ((
              ex.SocketErrorCode == SocketError.NetworkDown ||
              ex.SocketErrorCode == SocketError.NetworkReset ||
              ex.SocketErrorCode == SocketError.HostUnreachable ||
              ex.SocketErrorCode == SocketError.ConnectionAborted ||
              ex.SocketErrorCode == SocketError.TimedOut ||
              ex.SocketErrorCode == SocketError.ConnectionReset))
            {
                return -1;
            }
            else
            {
                Debug.Assert(false);
            }
        }

        return nbytes;
    }

    private int read(ArraySegment<byte> data)
    {
        int nbytes = 0;
        try
        {
            nbytes = handle.Receive(data.Array, data.Offset, data.Count, SocketFlags.None);
        }
        catch (SocketException ex)
        {
            //  If not a single byte can be written to the socket in non-blocking mode
            //  we'll get an error (this may happen during the speculative write).
            if (ex.SocketErrorCode == SocketError.WouldBlock)
            {
                return 0;
            }
            else if ((
              ex.SocketErrorCode == SocketError.NetworkDown ||
              ex.SocketErrorCode == SocketError.NetworkReset ||
              ex.SocketErrorCode == SocketError.HostUnreachable ||
              ex.SocketErrorCode == SocketError.ConnectionAborted ||
              ex.SocketErrorCode == SocketError.TimedOut ||
              ex.SocketErrorCode == SocketError.ConnectionReset))
            {
                return -1;
            }
            else
            {
                Debug.Assert(false);
            }
        }

        if (nbytes == 0)
        {
            return -1;
        }

        return nbytes;
    }
}
