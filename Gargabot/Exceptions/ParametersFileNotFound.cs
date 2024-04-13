using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gargabot.Exceptions
{
    public class ParametersFileNotFound : Exception
    {
        public ParametersFileNotFound()
            : base($"Parameters file not found")
        {

        }
    }
}
