using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ComDebugger
{
    class Customer
    {
        public string BaudRate { get; internal set; }
        public string com { get; set; }
        public string Dbits { get; internal set; }
        public string Parity { get; internal set; }
        public string ParityValue { get; internal set; }
        public string Sbits { get; internal set; }
    }
}
