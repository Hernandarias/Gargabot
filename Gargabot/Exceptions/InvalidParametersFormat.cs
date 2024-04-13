using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gargabot.Exceptions
{
    public class InvalidParametersFormat : Exception
    {
        public InvalidParametersFormat()
            : base("Parameters file has wrong format")
        {

        }
    }
}
