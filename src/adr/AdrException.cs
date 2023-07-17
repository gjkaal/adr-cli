using System;
using System.Runtime.Serialization;

namespace adr
{
    public class AdrException : ApplicationException
    {
        public AdrException()
        {
        }

        public AdrException(string message) : base(message)
        {
        }

        public AdrException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected AdrException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}