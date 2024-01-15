using System;

namespace IxMilia.ThreeMf
{

    public class ThreeMfParseException : Exception
    {
        public ThreeMfParseException(string message)
            : base(message)
        {
        }

        public ThreeMfParseException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }
}
