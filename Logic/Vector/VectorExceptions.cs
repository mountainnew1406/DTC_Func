using System;

namespace VectorCanTest.Logic.Vector
{
    public class VectorError : Exception
    {
        public int? ErrorCode { get; }
        public string FunctionName { get; }

        public VectorError(string message, int? errorCode = null, string functionName = null)
            : base(message)
        {
            ErrorCode = errorCode;
            FunctionName = functionName;
        }
    }

    public sealed class VectorInitializationError : VectorError
    {
        public VectorInitializationError(string message, int? errorCode = null, string functionName = null)
            : base(message, errorCode, functionName) { }
    }

    public sealed class VectorOperationError : VectorError
    {
        public VectorOperationError(string message, int? errorCode = null, string functionName = null)
            : base(message, errorCode, functionName) { }
    }

    public sealed class UdsNegativeResponseException : Exception
    {
        public byte Nrc { get; }

        public UdsNegativeResponseException(byte nrc)
            : base($"UDS negative response NRC=0x{nrc:X2}")
        {
            Nrc = nrc;
        }
    }
}
