using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gargabot.Exceptions
{
    public class InvalidMessagesFormat : Exception
    {
        public InvalidMessagesFormat()
            : base("Messages file has wrong format")
        {

        }
    }
}
