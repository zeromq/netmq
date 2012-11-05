namespace ZeroMQ
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// A single or multi-part message sent or received via a <see cref="ZmqSocket"/>.
    /// </summary>
    public class ZmqMessage : IEnumerable<Frame>
    {
        private readonly List<Frame> _frames;

        /// <summary>
        /// Initializes a new instance of the <see cref="ZmqMessage"/> class.
        /// Creates an empty message.
        /// </summary>
        public ZmqMessage()
        {
            _frames = new List<Frame>();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ZmqMessage"/> class.
        /// Creates a message that contains the given <see cref="Frame"/> objects.
        /// </summary>
        /// <param name="frames">A collection of <see cref="Frame"/> objects to be stored by this <see cref="ZmqMessage"/>.</param>
        /// <exception cref="ArgumentNullException"><paramref name="frames"/> is null.</exception>
        public ZmqMessage(IEnumerable<Frame> frames)
        {
            if (frames == null)
            {
                throw new ArgumentNullException("frames");
            }

            _frames = new List<Frame>(frames);

            NormalizeFrames();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ZmqMessage"/> class.
        /// Creates a message that contains the given <see cref="byte"/> arrays converted to <see cref="Frame"/>s.
        /// </summary>
        /// <param name="buffers">A collection of <see cref="byte"/> arrays to be stored by this <see cref="ZmqMessage"/>.</param>
        /// <exception cref="ArgumentNullException"><paramref name="buffers"/> is null.</exception>
        public ZmqMessage(IEnumerable<byte[]> buffers)
        {
            if (buffers == null)
            {
                throw new ArgumentNullException("buffers");
            }

            _frames = buffers.Select(buf => new Frame(buf)).ToList();

            NormalizeFrames();
        }

        /// <summary>
        /// Gets the first frame in the current message.
        /// </summary>
        public Frame First
        {
            get { return _frames[0]; }
        }

        /// <summary>
        /// Gets the last frame in the current message.
        /// </summary>
        public Frame Last
        {
            get { return _frames[_frames.Count - 1]; }
        }

        /// <summary>
        /// Gets a value indicating whether the current message is complete
        /// (i.e. no more message parts follow the last part of this message).
        /// </summary>
        public bool IsComplete
        {
            get { return _frames.Count > 0 && !_frames.Last().HasMore; }
        }

        /// <summary>
        /// Gets a value indicating whether the current message is empty.
        /// </summary>
        public bool IsEmpty
        {
            get { return _frames.Count == 0; }
        }

        /// <summary>
        /// Gets the number of <see cref="Frame"/> objects contained by this message.
        /// </summary>
        public int FrameCount
        {
            get { return _frames.Count; }
        }

        /// <summary>
        /// Gets the total number of bytes in this message.
        /// </summary>
        public int TotalSize
        {
            get { return _frames.Sum(f => f.MessageSize); }
        }

        /// <summary>
        /// Gets the <see cref="Frame"/> at the specified index.
        /// </summary>
        /// <param name="index">The zero-based index of the <see cref="Frame"/> to get.</param>
        /// <returns>The <see cref="Frame"/> at the specified index.</returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="index"/>is less than 0 -or- <paramref name="index"/> is equal to or greater than <see cref="FrameCount"/>.
        /// </exception>
        public Frame this[int index]
        {
            get { return _frames[index]; }
        }

        /// <summary>
        /// Adds the given <see cref="Frame"/> to the end of the current <see cref="ZmqMessage"/>.
        /// </summary>
        /// <remarks>
        /// Updates the <see cref="Frame.HasMore"/> property of the preceding frames accordingly.
        /// </remarks>
        /// <param name="frame">A <see cref="Frame"/> object to append to this <see cref="ZmqMessage"/>.</param>
        /// <exception cref="ArgumentNullException"><paramref name="frame"/> is null.</exception>
        public void Append(Frame frame)
        {
            AppendShallowCopy(frame);

            NormalizeFrames();
        }

        /// <summary>
        /// Adds the given <see cref="byte"/> array to the end of the current <see cref="ZmqMessage"/>
        /// as a <see cref="Frame"/>.
        /// </summary>
        /// <remarks>
        /// Updates the <see cref="Frame.HasMore"/> property of the preceding frames accordingly.
        /// </remarks>
        /// <param name="buffer">A <see cref="byte"/> array to append to this <see cref="ZmqMessage"/>.</param>
        /// <exception cref="ArgumentNullException"><paramref name="buffer"/> is null.</exception>
        public void Append(byte[] buffer)
        {
            _frames.Add(new Frame(buffer));

            NormalizeFrames();
        }

        /// <summary>
        /// Adds an empty <see cref="Frame"/> to the end of the current <see cref="ZmqMessage"/>.
        /// </summary>
        /// <remarks>
        /// Updates the <see cref="Frame.HasMore"/> property of the preceding frames accordingly.
        /// </remarks>
        public void AppendEmptyFrame()
        {
            _frames.Add(Frame.Empty);

            NormalizeFrames();
        }

        /// <summary>
        /// Inserts <paramref name="frame"/> at the front of the message.
        /// </summary>
        /// <param name="frame">A <see cref="Frame"/> to insert.</param>
        public void Push(Frame frame)
        {
            _frames.Insert(0, frame);

            NormalizeFrames();
        }

        /// <summary>
        /// Inserts a new <see cref="Frame"/> containing <paramref name="buffer"/>
        /// at the front of the message.
        /// </summary>
        /// <param name="buffer">A <see cref="byte"/> array containing the frame data to push.</param>
        public void Push(byte[] buffer)
        {
            _frames.Insert(0, new Frame(buffer));

            NormalizeFrames();
        }

        /// <summary>
        /// Inserts an empty <see cref="Frame"/> at the front of the message.
        /// </summary>
        public void PushEmptyFrame()
        {
            _frames.Insert(0, Frame.Empty);

            NormalizeFrames();
        }

        /// <summary>
        /// Pushes <paramref name="frame"/> plus an empty frame to the front
        /// of the message.
        /// </summary>
        /// <param name="frame">A <see cref="Frame"/> to push to the front of the message.</param>
        public void Wrap(Frame frame)
        {
            _frames.Insert(0, Frame.Empty);
            _frames.Insert(0, frame);

            NormalizeFrames();
        }

        /// <summary>
        /// Pops a <see cref="Frame"/> off the front of the message.
        /// If the next frame is empty, that empty frame is removed.
        /// </summary>
        /// <returns>The first <see cref="Frame"/> in the message or <c>null</c> if the message is empty.</returns>
        public Frame Unwrap()
        {
            Frame result = null;

            if (_frames.Count > 0)
            {
                result = _frames[0];
                _frames.RemoveAt(0);
            }

            if (_frames.Count > 0 && _frames[0].MessageSize == 0)
            {
                _frames.RemoveAt(0);
            }

            NormalizeFrames();

            return result;
        }

        /// <summary>
        /// Returns an enumerator that iterates through the <see cref="Frame"/> objects
        /// contained by this <see cref="ZmqMessage"/>.
        /// </summary>
        /// <returns>An <see cref="IEnumerator{Frame}"/> for the current <see cref="ZmqMessage"/>.</returns>
        public IEnumerator<Frame> GetEnumerator()
        {
            return _frames.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        internal void AppendShallowCopy(Frame frame)
        {
            if (frame == null)
            {
                throw new ArgumentNullException("frame");
            }

            _frames.Add(new Frame(frame));
        }

        private void NormalizeFrames()
        {
            if (_frames.Count == 0)
            {
                return;
            }

            for (int i = 0; i < _frames.Count; i++)
            {
                _frames[i].HasMore = i < _frames.Count - 1;
            }
        }
    }
}
