using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gargabot.Exceptions
{
    public class InvalidLavalinkSession : Exception
    {
        public InvalidLavalinkSession(string message)
                : base("[InvalidLavalinkSession Exception] "+message)
        {
    
            }
    }
}
