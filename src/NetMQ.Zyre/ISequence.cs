using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NetMQ.Zyre
{
    public interface ISequence
    {
        UInt16 Sequence { get; set; }
    }
}
