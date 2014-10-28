using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NetMQ
{
    public static class EnumHelper
    {
        public static bool HasFlag(this Enum value, Enum flag)
        {
            if (value == null)
                return false;

            if (value == null)
                throw new ArgumentNullException("value");
            
            if (!Enum.IsDefined(value.GetType(), value))
            {
                throw new ArgumentException(string.Format(
                    "Enumeration type mismatch.  The flag is of type '{0}', was expecting '{1}'.",
                    value.GetType(), value.GetType()));
            }

            ulong num = Convert.ToUInt64(value);
            return ((Convert.ToUInt64(value) & num) == num);
        }
    }
}
