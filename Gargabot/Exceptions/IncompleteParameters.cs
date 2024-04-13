using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gargabot.Exceptions
{
    public class IncompleteParameters : Exception
    {
        public IncompleteParameters(string message) : base(message)
        {
        }
    }
}
