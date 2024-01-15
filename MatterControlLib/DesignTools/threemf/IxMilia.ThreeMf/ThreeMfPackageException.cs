using System;

namespace IxMilia.ThreeMf
{

    public class ThreeMfPackageException : Exception
    {
        public ThreeMfPackageException(string message)
            : base(message)
        {
        }

        public ThreeMfPackageException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }
}
