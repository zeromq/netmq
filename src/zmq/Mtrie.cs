/*              
    Copyright (c) 2011 250bpm s.r.o.
    Copyright (c) 2011-2012 Spotify AB
    Copyright (c) 2011 Other contributors as noted in the AUTHORS file
                    
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
using System.Diagnostics;

//Multi-trie. Each node in the trie is a set of pointers to pipes.
public class Mtrie {
    private HashSet<Pipe> pipes;

    private int min;
    private int count;
    private int live_nodes;
    private Mtrie[] next;

    public delegate void IMtrieDelegate(Pipe pipe, byte[] data, Object arg);
    
    public Mtrie() {
        min = 0;
        count = 0;
        live_nodes = 0;
        
        pipes = null;
        next = null;
    }
    
    public bool add (byte[] prefix_, Pipe pipe_)
    {
        return add_helper (prefix_, 0, pipe_);
    }

    //  Add key to the trie. Returns true if it's a new subscription
    //  rather than a duplicate.
    public bool add (byte[] prefix_, int start_, Pipe pipe_)
    {
        return add_helper (prefix_, start_, pipe_);
    }


    private bool add_helper (byte[] prefix_, int start_, Pipe pipe_)
    {
        //  We are at the node corresponding to the prefix. We are done.
        if (prefix_ == null || prefix_.Length == start_) {
            bool result = pipes == null;
            if (pipes == null)
                pipes = new HashSet<Pipe>();
            pipes.Add (pipe_);
            return result;
        }

        byte c = prefix_[start_];
        if (c < min || c >= min + count) {

            //  The character is out of range of currently handled
            //  charcters. We have to extend the table.
            if (count == 0) {
                min = c;
                count = 1;
                next = null;
            }
            else if (count == 1) {
                int oldc = min;
                Mtrie oldp = next[0];
                count = (min < c ? c - min : min - c) + 1;
                next = new Mtrie[count];
                min = Math.Min (min, c);
                next[oldc - min] = oldp;
            }
            else if (min < c) {

                //  The new character is above the current character range.
                count = c - min + 1;
                next = realloc(next, count, true);
            }
            else {

                //  The new character is below the current character range.
                count = (min + count) - c;
                next = realloc(next, count, false);
                min = c;
            }
        }

        //  If next node does not exist, create one.
        if (count == 1) {
            if (next == null) {
                next = new Mtrie[1];
                next[0] = new Mtrie();
                ++live_nodes;
                //alloc_Debug.Assert(next.node);
            }
            return next[0].add_helper (prefix_, start_ + 1, pipe_);
        }
        else {
            if (next[c - min] == null) {
                next[c - min] = new Mtrie();
                ++live_nodes;
                //alloc_Debug.Assert(next.table [c - min]);
            }
            return next[c - min].add_helper (prefix_ , start_ + 1, pipe_);
        }
    }
    
    private Mtrie[] realloc (Mtrie[] table, int size, bool ended) 
    {
        return Utils.realloc(table, size, ended);
    }
    
    //  Remove all subscriptions for a specific peer from the trie.
    //  If there are no subscriptions left on some topics, invoke the
    //  supplied callback function.
    public bool rm (Pipe pipe_, IMtrieDelegate func, Object arg) {
        return rm_helper(pipe_, new byte[0], 0, 0, func, arg );
    }

    private bool rm_helper(Pipe pipe_, byte[] buff_, int buffsize_, int maxbuffsize_,
            IMtrieDelegate func_, Object arg_) {
        
        //  Remove the subscription from this node.
        if (pipes != null && pipes.Remove(pipe_) && pipes.Count == 0) {
            func_(null, buff_, arg_);
            pipes = null;
        }

        //  Adjust the buffer.
        if (buffsize_ >= maxbuffsize_) {
            maxbuffsize_ = buffsize_ + 256;
            buff_ = Utils.realloc(buff_, maxbuffsize_);
        }

        //  If there are no subnodes in the trie, return.
        if (count == 0)
            return true;

        //  If there's one subnode (optimisation).
        if (count == 1) {
            buff_[buffsize_] = (byte) min;
            buffsize_ ++;
            next[0].rm_helper (pipe_, buff_, buffsize_, maxbuffsize_,
                func_, arg_);

            //  Prune the node if it was made redundant by the removal
            if (next[0].is_redundant ()) {
                next = null;
                count = 0;
                --live_nodes;
                Debug.Assert(live_nodes == 0);
            }
            return true;
        }

        //  If there are multiple subnodes.
        //
        //  New min non-null character in the node table after the removal
        int new_min = min + count - 1;
        //  New max non-null character in the node table after the removal
        int new_max = min;
        for (int c = 0; c != count; c++) {
            buff_[buffsize_] = (byte) (min + c);
            if (next[c] != null) {
                next[c].rm_helper (pipe_, buff_, buffsize_ + 1,
                    maxbuffsize_, func_, arg_);

                //  Prune redundant nodes from the mtrie
                if (next[c].is_redundant ()) {
                    next[c] = null;

                    Debug.Assert(live_nodes > 0);
                    --live_nodes;
                }
                else {
                    //  The node is not redundant, so it's a candidate for being
                    //  the new min/max node.
                    //
                    //  We loop through the node array from left to right, so the
                    //  first non-null, non-redundant node encountered is the new
                    //  minimum index. Conversely, the last non-redundant, non-null
                    //  node encountered is the new maximum index.
                    if (c + min < new_min)
                        new_min = c + min;
                    if (c + min > new_max)
                        new_max = c + min;
                }
            }
        }

        Debug.Assert(count > 1);

        //  Free the node table if it's no longer used.
        if (live_nodes == 0) {
            next = null;
            count = 0;
        }
        //  Compact the node table if possible
        else if (live_nodes == 1) {
            //  If there's only one live node in the table we can
            //  switch to using the more compact single-node
            //  representation
            Debug.Assert(new_min == new_max);
            Debug.Assert(new_min >= min && new_min < min + count);
            Mtrie node = next [new_min - min];
            Debug.Assert(node != null);
            next = null;
            next = new Mtrie[]{node};
            count = 1;
            min = new_min;
        }
        else if (live_nodes > 1 && (new_min > min || new_max < min + count - 1)) {
            Debug.Assert(new_max - new_min + 1 > 1);

            Mtrie[] old_table = next;
            Debug.Assert(new_min > min || new_max < min + count - 1);
            Debug.Assert(new_min >= min);
            Debug.Assert(new_max <= min + count - 1);
            Debug.Assert(new_max - new_min + 1 < count);
            count = new_max - new_min + 1;
            next = new Mtrie[count];

            Buffer.BlockCopy(old_table, (new_min - min), next, 0, count);

            min = new_min;
        }
        return true;
    }

    //  Remove specific subscription from the trie. Return true is it was
    //  actually removed rather than de-duplicated.
    public bool rm (byte[] prefix_, int start_, Pipe pipe_)
    {
        return rm_helper (prefix_, start_,  pipe_);
    }


    private bool rm_helper (byte[] prefix_, int start_, Pipe pipe_)
    {
        if (prefix_ == null || prefix_.Length == start_) {
            if (pipes != null) {
                bool erased = pipes.Remove(pipe_);
                Debug.Assert(erased);
                if (pipes.Count == 0) {
                    pipes = null;
                }
            }
            return pipes == null;
        }

        byte c = prefix_[ start_ ];
        if (count == 0 || c < min || c >= min + count)
            return false;

        Mtrie next_node =
            count == 1 ? next[0] : next[c - min];

        if (next_node == null)
            return false;

        bool ret = next_node.rm_helper (prefix_ , start_ + 1, pipe_);
        if (next_node.is_redundant ()) {
            Debug.Assert(count > 0);

            if (count == 1) {
                next = null;
                count = 0;
                --live_nodes;
                Debug.Assert(live_nodes == 0);
            }
            else {
                next[c - min] = null ;
                Debug.Assert(live_nodes > 1);
                --live_nodes;

                //  Compact the table if possible
                if (live_nodes == 1) {
                    //  If there's only one live node in the table we can
                    //  switch to using the more compact single-node
                    //  representation
                    int i;
                    for (i = 0; i < count; ++i) {
                        if (next[i] != null) {
                            break;
                        }
                    }

                    Debug.Assert(i < count);
                    min += i;
                    count = 1;
                    Mtrie old = next [i];
                    next = new Mtrie [] { old };
                }
                else if (c == min) {
                    //  We can compact the table "from the left"
                    int i;
                    for (i = 1; i < count; ++i) {
                        if (next[i] != null) {
                            break;
                        }
                    }

                    Debug.Assert(i < count);
                    min += i;
                    count -= i;
                    next = realloc (next, count, true);
                }
                else if (c == min + count - 1) {
                    //  We can compact the table "from the right"
                    int i;
                    for (i = 1; i < count; ++i) {
                        if (next[count - 1 - i] != null) {
                            break;
                        }
                    }
                    Debug.Assert(i < count);
                    count -= i;
                    next = realloc(next, count, false);
                }
            }
        }

        return ret;
    }

    //  Signal all the matching pipes.
    public void match(byte[] data_, int size_, IMtrieDelegate func_, Object arg_) {
        Mtrie current = this;
        
        int idx = 0;
        
        while (true) {
            
            
            //  Signal the pipes attached to this node.
            if (current.pipes != null) {
                foreach (Pipe it in current.pipes)
                    func_(it, null, arg_);
            }

            //  If we are at the end of the message, there's nothing more to match.
            if (size_ == 0)
                break;

            //  If there are no subnodes in the trie, return.
            if (current.count == 0)
                break;

            byte c = data_[idx];
            //  If there's one subnode (optimisation).
            if (current.count == 1) {
                if (c != current.min)
                    break;
                current = current.next[0];
                idx++;
                size_--;
                continue;
            }

            //  If there are multiple subnodes.
            if (c < current.min || c >=
                  current.min + current.count)
                break;
            if (current.next [c - current.min] == null)
                break;
            current = current.next [c - current.min];
            idx++;
            size_--;
        }
    }

    private bool is_redundant ()
    {
        return pipes == null && live_nodes == 0;
    }

}
