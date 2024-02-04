using WsjtxUdpLib.Messages.Out;
using System;

namespace WsjtxUdpLib.Messages
{
    public sealed class ParseFailureException : Exception
    {
        public MessageType MessageType { get; private set; }
        public byte[] Datagram { get; private set; }

        public ParseFailureException(MessageType messageType, byte[] datagram, Exception ex) : base("Failed to parse a WSJT-X datagram", ex)
        {
            MessageType = messageType;
            Datagram = datagram;
        }
    }
}