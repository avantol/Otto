using WsjtxUdpLib.Messages.Out;
using System;
using System.IO;
using System.Net.Mail;
using System.Threading;

namespace WsjtxUdpLib.Messages
{
    /*
     * Free Text     In        9
     *                         Id (unique key)        utf8
     *                         Text                   utf8
     *                         Send                   bool
     *
     *      This message  allows the server  to set the current  free text
     *      message content. Sending this  message with a non-empty "Text"
     *      field is equivalent to typing  a new message (old contents are
     *      discarded) in to  the WSJT-X free text message  field or "Tx5"
     *      field (both  are updated) and if  the "Send" flag is  set then
     *      clicking the "Now" radio button for the "Tx5" field if tab one
     *      is current or clicking the "Free  msg" radio button if tab two
     *      is current.
     *
     *      It is the responsibility of the  sender to limit the length of
     *      the  message   text  and   to  limit   it  to   legal  message
     *      characters. Despite this,  it may be difficult  for the sender
     *      to determine the maximum message length without reimplementing
     *      the complete message encoding protocol. Because of this is may
     *      be better  to allow any  reasonable message length and  to let
     *      the WSJT-X application encode and possibly truncate the actual
     *      on-air message.
     *
     *      If the  message text is  empty the  meaning of the  message is
     *      refined  to send  the  current free  text  unchanged when  the
     *      "Send" flag is set or to  clear the current free text when the
     *      "Send" flag is  unset.  Note that this API does  not include a
     *      command to  determine the  contents of  the current  free text
     *      message.
     */

    public class FreeTextMessage : WsjtxMessage, IWsjtxCommandMessageGenerator
    {
        public int SchemaVersion { get; set; }
        public string Id { get; set; }
        public string Text { get; set; }
        public bool Send { get; set; }

        public byte[] GetBytes()
        {
            using (MemoryStream m = new MemoryStream())
            {
                using (BinaryWriter writer = new BinaryWriter(m))
                {
                    writer.Write(WsjtxMessage.MagicNumber);
                    writer.Write(EncodeQUInt32((UInt32)SchemaVersion));
                    writer.Write(EncodeQUInt32(9));    //msg type
                    writer.Write(EncodeString(Id));
                    writer.Write(EncodeString(Text));
                    writer.Write(EncodeBoolean(Send));
                }
                return m.ToArray();
            }
        }
        public override string ToString() => $"FreeText   {this.ToCompactLine(nameof(Id))}";
    }
}
