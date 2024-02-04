using WsjtxUdpLib.Messages.Out;
using System;
using System.Text;
using System.IO;

namespace WsjtxUdpLib.Messages
{
    /*
     * Reply         In        4                      quint32
     *                         Id (target unique key) utf8
     *                         Time                   QTime
     *                         snr                    qint32
     *                         Delta time (S)         float (serialized as double)
     *                         Delta frequency (Hz)   quint32
     *                         Mode                   utf8
     *                         Message                utf8
     *                         Low confidence         bool
     *                         Modifiers              quint8
     *
     *      In order for a server  to provide a useful cooperative service
     *      to WSJT-X it  is possible for it to initiate  a QSO by sending
     *      this message to a client. WSJT-X filters this message and only
     *      acts upon it  if the message exactly describes  a prior decode
     *      and that decode  is a CQ or QRZ message.   The action taken is
     *      exactly equivalent to the user  double clicking the message in
     *      the "Band activity" window. The  intent of this message is for
     *      servers to be able to provide an advanced look up of potential
     *      QSO partners, for example determining if they have been worked
     *      before  or if  working them  may advance  some objective  like
     *      award progress.  The  intention is not to  provide a secondary
     *      user  interface for  WSJT-X,  it is  expected  that after  QSO
     *      initiation the rest  of the QSO is carried  out manually using
     *      the normal WSJT-X user interface.
     *
     *      The  Modifiers   field  allows  the  equivalent   of  keyboard
     *      modifiers to be sent "as if" those modifier keys where pressed
     *      while  double-clicking  the  specified  decoded  message.  The
     *      modifier values (hexadecimal) are as follows:
     *
     *          no modifier     0x00
     *          SHIFT           0x02
     *          CTRL            0x04  CMD on Mac
     *          ALT             0x08
     *          META            0x10  Windows key on MS Windows
     *          KEYPAD          0x20  Keypad or arrows
     *          Group switch    0x40  X11 only
     */

    public class ReplyMessage : WsjtxMessage, IWsjtxCommandMessageGenerator
    {
        public int SchemaVersion { get; set; }
        public string Id { get; set; }
         public TimeSpan SinceMidnight { get; set; }
        public int Snr { get; set; }
        public double DeltaTime { get; set; }
        public int DeltaFrequency { get; set; }
        public string Mode { get; set; }
        public string Message { get; set; }
        public bool UseStdReply { get; set; }
        public byte Modifiers { get; set; }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append("Reply     ");
            sb.Append($"{Col(SinceMidnight, 8, Align.Left)} ");
            sb.Append($"{Col(Snr, 3, Align.Right)} ");
            sb.Append($"{Col(DeltaFrequency, 4, Align.Right)} ");
            sb.Append($"{Col(DeltaTime, 4, Align.Right)} ");
            sb.Append($"{Col(Mode, 1, Align.Left)} ");
            sb.Append($"{(UseStdReply ? "USR" : "  ")} ");
            sb.Append($"{Col(Message, 20, Align.Left)} ");

            return sb.ToString();
        }
        public byte[] GetBytes()
        {
            using (MemoryStream m = new MemoryStream())
            {
                using (BinaryWriter writer = new BinaryWriter(m))
                {
                    writer.Write(WsjtxMessage.MagicNumber);
                    writer.Write(EncodeQUInt32((UInt32)SchemaVersion));
                    writer.Write(EncodeQUInt32(4));    //msg type
                    writer.Write(EncodeString(Id));
                    writer.Write(EncodeQTime(SinceMidnight));
                    writer.Write(EncodeQInt32(Snr));
                    writer.Write(EncodeDouble(DeltaTime));
                    writer.Write(EncodeQUInt32((UInt32)DeltaFrequency));
                    writer.Write(EncodeString(Mode));
                    writer.Write(EncodeString(Message));
                    writer.Write(EncodeBoolean(UseStdReply));
                    writer.Write(Modifiers);
               }
                return m.ToArray();
            }
        }
        //public override string ToString() => $"Reply      {this.ToCompactLine(nameof(Id))}";
    }
}
