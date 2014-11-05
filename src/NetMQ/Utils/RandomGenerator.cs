using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NetMQ.Utils
{
    static class RandomGenerator
    {
        static Random s_random = new Random();

        public static int Generate()
        {
            return s_random.Next();
        }
    }
}
