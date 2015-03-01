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
using JetBrains.Annotations;

namespace NetMQ.zmq
{
    /// <summary>
    /// This class defines the commands that can be sent between threads.
    /// </summary>
    internal sealed class Command
    {
        /// <summary>
        /// Default constructor - create the Command object for processing commands.
        /// </summary>
        public Command()
        {
        }

        public Command([CanBeNull] ZObject destination, CommandType type, [CanBeNull] Object arg = null)
        {
            Destination = destination;
            CommandType = type;
            Arg = arg;
        }

        /// <summary>The destination to which the command should be applied.</summary>
        [CanBeNull]
        public ZObject Destination { get; private set; }

        /// <summary>The type of this command.</summary>
        public CommandType CommandType { get; private set; }

        /// <summary>
        /// Get the argument to this command.
        /// </summary>
        [CanBeNull]
        public Object Arg { get; private set; }

        public override String ToString()
        {
            return base.ToString() + "[" + CommandType + ", " + Destination + "]";
        }
    }
}
