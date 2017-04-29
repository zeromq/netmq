/*
    Copyright (c) 2009-2011 250bpm s.r.o.
    Copyright (c) 2007-2009 iMatix Corporation
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

using JetBrains.Annotations;

namespace NetMQ.Core
{
    /// <summary>
    /// Defines a command sent between threads.
    /// </summary>
    internal struct Command
    {
        /// <summary>
        /// Create a new Command object for the given destination, type, and optional argument.
        /// </summary>
        /// <param name="destination">a ZObject that denotes the destination for this command</param>
        /// <param name="type">the CommandType of the new command</param>
        /// <param name="arg">an Object to comprise the argument for the command (optional)</param>
        public Command([CanBeNull] ZObject destination, CommandType type, [CanBeNull] object arg = null) : this()
        {
            Destination = destination;
            CommandType = type;
            Arg = arg;
        }

        /// <summary>The destination to which the command should be applied.</summary>
        [CanBeNull]
        public ZObject Destination { get; }

        /// <summary>The type of this command.</summary>
        public CommandType CommandType { get; }

        /// <summary>
        /// Get the argument to this command.
        /// </summary>
        [CanBeNull]
        public object Arg { get; }

        /// <summary>
        /// Override of ToString, which returns a string in the form [ command-type, destination ].
        /// </summary>
        /// <returns>a string that denotes the command-type and destination</returns>
        public override string ToString()
        {
            return base.ToString() + "[" + CommandType + ", " + Destination + "]";
        }
    }
}
