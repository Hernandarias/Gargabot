using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gargabot.Exceptions
{
    public class MessagesFileNotFound : Exception
    {
        public MessagesFileNotFound()
            : base($"Messages file not found")
        {

        }
    }
}
