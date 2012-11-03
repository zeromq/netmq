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
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using NetMQ;
using System.IO;

public class Utils {

    private static Random random = new Random();

    public static int generate_random()
    {
        return random.Next();
    }

   
    public static void tune_tcp_socket(Socket fd) 
    {
        //  Disable Nagle's algorithm. We are doing data batching on 0MQ level,
        //  so using Nagle wouldn't improve throughput in anyway, but it would
        //  hurt latency.
        try {
            fd.NoDelay = (true);
        } catch (SocketException) {
        }
    }   
    
    public static void tune_tcp_keepalives(Socket fd, int tcp_keepalive,
            int tcp_keepalive_cnt, int tcp_keepalive_idle,
            int tcp_keepalive_intvl) {

        if (tcp_keepalive != -1) {
            //fd.setKeepAlive(true);           
        }
    }
    
    public static void unblock_socket(System.Net.Sockets.Socket s) {
        s.Blocking = false;
    }
    
    //@SuppressWarnings("unchecked")
    public static T[] realloc<T>(T[] src, int size, bool ended) {
        T[] dest;
        
        if (size > src.Length) {
            dest = new T[ size];
            if (ended)

                Buffer.BlockCopy(src, 0, dest, 0, src.Length);
            else
                Buffer.BlockCopy(src, 0, dest, size - src.Length, src.Length);
        } else if (size < src.Length) {
            dest = new T[ size];
            if (ended)
                Buffer.BlockCopy(src, 0, dest, 0, size);
            else
                Buffer.BlockCopy(src, src.Length - size, dest, 0, size);

        } else {
            dest = src;
        }
        return dest;
    }

    public static void swap<T>(List<T> items, int index1_, int index2_)
    {
        if (index1_ == index2_)
            return;

        T item1 = items[index1_];
        T item2 = items[index2_];
        if (item1 != null)
            items[index2_] = item1;
        if (item2 != null)
            items[index1_] = item2;
    }

    public static byte[] realloc(byte[] src, int size) {

        byte[] dest = new byte[size];
        if (src != null)
            Buffer.BlockCopy(src, 0, dest, 0, src.Length);
        
        return dest;
    }    

    //public static bool delete(File path) {
    //    if (!path.exists())
    //        return false; 
    //    bool ret = true;
    //    if (path.isDirectory()){
    //        foreach (File f in path.listFiles()){
    //            ret = ret && delete(f);
    //        }
    //    }
    //    return ret && path.delete();
    //}

}
