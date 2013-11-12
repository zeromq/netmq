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
// Note: To target a version of .NET earlier than 4.0, build this with the pragma PRE_4 defined.  jh
#if PRE_4
using System.Net;
#endif
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Sockets;
using System.Security.Cryptography;

namespace NetMQ.zmq
{
    public static class Utils
    {

        private static readonly Random s_random = new Random();

        public static int GenerateRandom()
        {
            return s_random.Next();
        }


        public static void TuneTcpSocket(Socket fd)
        {
            //  Disable Nagle's algorithm. We are doing data batching on 0MQ level,
            //  so using Nagle wouldn't improve throughput in anyway, but it would
            //  hurt latency.
            try
            {
                fd.NoDelay = (true);
            }
            catch (SocketException)
            {
            }
        }


        public static void TuneTcpKeepalives(Socket fd, int tcpKeepalive,
                                                                                     int tcpKeepaliveCnt, int tcpKeepaliveIdle,
                                                                                     int tcpKeepaliveIntvl)
        {

            if (tcpKeepalive != -1)
            {
                fd.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, tcpKeepalive);


                if (tcpKeepaliveIdle != -1 && tcpKeepaliveIntvl != -1)
                {
                    ByteArraySegment bytes = new ByteArraySegment(new byte[12]);

                    Endianness endian = BitConverter.IsLittleEndian ? Endianness.Little : Endianness.Big;

                    bytes.PutInteger(endian, tcpKeepalive, 0);
                    bytes.PutInteger(endian, tcpKeepaliveIdle, 4);
                    bytes.PutInteger(endian, tcpKeepaliveIntvl, 8);

                    fd.IOControl(IOControlCode.KeepAliveValues, (byte[])bytes, null);
                }
            }
        }

        public static void UnblockSocket(Socket s)
        {
            s.Blocking = false;
        }

        //@SuppressWarnings("unchecked")
        public static T[] Realloc<T>(T[] src, int size, bool ended)
        {
            T[] dest;

            if (size > src.Length)
            {
                dest = new T[size];
                if (ended)
                    Array.Copy(src, 0, dest, 0, src.Length);
                else
                    Array.Copy(src, 0, dest, size - src.Length, src.Length);
            }
            else if (size < src.Length)
            {
                dest = new T[size];
                if (ended)
                    Array.Copy(src, 0, dest, 0, size);
                else
                    Array.Copy(src, src.Length - size, dest, 0, size);

            }
            else
            {
                dest = src;
            }
            return dest;
        }

        public static void Swap<T>(List<T> items, int index1, int index2) where T : class
        {
            if (index1 == index2)
                return;

            T item1 = items[index1];
            T item2 = items[index2];
            if (item1 != null)
                items[index2] = item1;
            if (item2 != null)
                items[index1] = item2;
        }

        public static byte[] Realloc(byte[] src, int size)
        {

            byte[] dest = new byte[size];
            if (src != null)
                Buffer.BlockCopy(src, 0, dest, 0, src.Length);

            return dest;
        }

        [Conditional("PRE_4")]
        public static void Dispose(this HashAlgorithm hashAlgorithm)
        {
            // Dispose is provided as an extension-method here
            // simply so that it may be called in code without breaking compatibility
            // with pre- .NET 4.0 code.
            hashAlgorithm.Clear();
        }

        [Conditional("PRE_4")]
        public static void Dispose(this System.Threading.Mutex mutex)
        {
            // Dispose is provided as an extension-method here
            // simply so that it may be called in code without breaking compatibility
            // with pre- .NET 4.0 code.
            mutex.Close();
        }

        [Conditional("PRE_4")]
        public static void Dispose(this RandomNumberGenerator randomNumberGenerator)
        {
            // Do nothing here. Dispose is provided as an extension-method here
            // simply so that it may be called in code without breaking compatibility
            // with pre- .NET 4.0 code.
        }

        [Conditional("PRE_4")]
        public static void Dispose(this SymmetricAlgorithm symmetricAlgorithm)
        {
            // Dispose is provided as an extension-method here
            // simply so that it may be called in code without breaking compatibility
            // with pre- .NET 4.0 code.
            symmetricAlgorithm.Clear();
        }

        #region HasFlag
#if PRE_4
        /// <summary>
        /// Return true if the given flags are set within a flag-enum variable.
        /// </summary>
        /// <param name="enumInstance">the flag-enumeration variable</param>
        /// <param name="flagValue">the flag (or flags ORed together) to test for</param>
        /// <returns>true if the flagValue is set</returns>
        public static bool HasFlag(this Enum enumInstance, Enum flagValue)
        {
            if (enumInstance != null)
            {
                // Validate the argument values..
                if (flagValue == null)
                {
                    throw new ArgumentNullException("flagValue");
                }
                if (!Enum.IsDefined(enumInstance.GetType(), flagValue))
                {
                    throw new ArgumentException(
                        string.Format("Enumeration-type mismatch: the flag is of type {0} where {1} is expected.",
                                      flagValue.GetType(), enumInstance.GetType()));
                }
                // Normalize the possible enum base-types by converting whatever it is into a 64-bit unsigned integer to test against.
                ulong numberWithBitsSet = Convert.ToUInt64(flagValue);
                // Now do the actual bit-field test and return true if the value (representing a bit or a set of bits ORd together) are set.
                return ((Convert.ToUInt64(enumInstance) & numberWithBitsSet) == numberWithBitsSet);
            }
            else
            {
                return false;
            }
        }
#endif
        #endregion

        #region Is64BitProcess
        /// <summary>
        /// Get whether this assembly is running as a 64-bit process.
        /// </summary>
        public static bool Is64BitProcess
        {
            get
            {
#if (PRE_4)
                return IntPtr.Size == 8;
#else
                return Environment.Is64BitProcess;
#endif
            }
        }
        #endregion
    }


    #region class DnsEndPoint
#if PRE_4
    /// <summary>
    /// This class substitutes for the DnsEndPoint class that is provided by .NET 4.0 and higher.
    /// </summary>
    public class DnsEndPoint : EndPoint
    {
        /// <summary>
        /// Create a new instance of DnsEndPoint with the given host and port-number.
        /// </summary>
        /// <param name="host">either a hostname or IP-address</param>
        /// <param name="portNumber">the port-number</param>
        public DnsEndPoint(string host, int portNumber)
        {
            Host = host;
            Port = portNumber;
        }

        /// <summary>
        /// Get the host name or string representation of the IP address.
        /// </summary>
        public string Host { get; set; }

        /// <summary>
        /// Get or set the port number.
        /// </summary>
        public int Port { get; set; }
    }
#endif
    #endregion
}
