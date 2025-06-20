using System;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;

namespace ConPty.Sample.ConsoleApi
{
    [Serializable]
    public class InteropException : Exception
    {
        public InteropException() { }

        public InteropException(string message)
            : base(message) { }

        public InteropException(string message, Exception? innerException)
            : base(message, innerException) { }

        public static InteropException CreateWithInnerHResultException(string message)
        {
            return new InteropException(
                message,
                Marshal.GetExceptionForHR(Marshal.GetHRForLastWin32Error()));
        }
    }
}
