using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gargabot.Exceptions
{
    public class InvalidVoiceNextSession : Exception
    {

        public InvalidVoiceNextSession(string message)
            : base("[InvalidVoiceNextSession Exception] "+message)
        {

        }
    }
}
