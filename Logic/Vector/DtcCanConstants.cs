namespace VectorCanTest.Logic.Vector
{
    public static class DtcCanConstants
    {
        public const string AppName = "DTC_CAN";
        public const int AppChannel = 0;
        public const int Bitrate = 500000;

        public const byte UdsReadDtcInformation = 0x19;
        public const byte UdsReadDataByIdentifier = 0x22;
        public const byte PositiveReadDtcInformation = 0x59;
        public const byte NegativeResponse = 0x7F;
        public const byte NrcResponsePending = 0x78;

        public static readonly byte[] ReadDtcByStatusMask09 = { 0x19, 0x02, 0x09 };
        public static readonly byte[] ReadVin = { 0x22, 0xF1, 0x90 };
        public static readonly byte[] FlowControlContinueToSend = { 0x30, 0x00, 0x00, 0, 0, 0, 0, 0 };
    }
}
