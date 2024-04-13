using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gargabot.Exceptions
{
    public class MessageNotFound : Exception
    {
        public MessageNotFound(string message)
            : base("[Message Not Found] "+ message)
        {

        }
    }
}
