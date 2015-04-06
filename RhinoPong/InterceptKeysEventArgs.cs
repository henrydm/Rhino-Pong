using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RhinoPong
{
    internal class InterceptKeysEventArgs: EventArgs
    {
        public int vkCode {get;private set;}
        public InterceptKeysEventArgs(int code)
        {
            vkCode = code;
        }
    }
}
