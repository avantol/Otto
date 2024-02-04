using WsjtxUdpLib.Messages.Out;
using System;

namespace WsjtxUdpLib.Messages
{
    /*
     * Close         Out/In    6                      quint32
     *                         Id (unique key)        utf8
     *
     *      Close is  sent by  a client immediately  prior to  it shutting
     *      down gracefully. When sent by  a server it requests the target
     *      client to close down gracefully.
     */

    public class CloseMessage : WsjtxMessage, IWsjtxCommandMessageGenerator
    {
        public UInt32 SchemaVersion { get; set; }
        public string Id { get; set; }

        public static new WsjtxMessage Parse(byte[] message) => new CloseMessage();

        public byte[] GetBytes() => throw new NotImplementedException();

        public override string ToString() => $"Close      {this.ToCompactLine(nameof(Id))}";
    }
}
