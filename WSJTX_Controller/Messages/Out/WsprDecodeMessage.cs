using System;

namespace WsjtxUdpLib.Messages.Out
{
    /*
     * WSPRDecode    Out       10                     quint32
     *                         Id (unique key)        utf8
     *                         New                    bool
     *                         Time                   QTime
     *                         snr                    qint32
     *                         Delta time (S)         float (serialized as double)
     *                         Frequency (Hz)         quint64
     *                         Drift (Hz)             qint32
     *                         Callsign               utf8
     *                         Grid                   utf8
     *                         Power (dBm)            qint32
     *                         Off air                bool
     *
     *      The decode message is sent when  a new decode is completed, in
     *      this case the 'New' field is true. It is also used in response
     *      to  a "Replay"  message where  each  old decode  in the  "Band
     *      activity" window, that  has not been erased, is  sent in order
     *      as  a one  of  these  messages with  the  'New'  field set  to
     *      false.  See   the  "Replay"  message  below   for  details  of
     *      usage. The off air field indicates that the decode was decoded
     *      from a played back recording.
     */

    public class WsprDecodeMessage : WsjtxMessage
    {
        public int SchemaVersion { get; private set; }
        public string Id { get; private set; }
        public bool New { get; private set; }
        public TimeSpan StartTime { get; private set; }
        public int Snr { get; private set; }
        public double DeltaTime { get; private set; }
        public ulong Frequency { get; private set; }
        public int Drift { get; private set; }
        public string Callsign { get; private set; }
        public string Grid { get; private set; }
        public int PowerDbm { get; private set; }
        public bool FromRecording { get; private set; }

        public static new WsjtxMessage Parse(byte[] message)
        {
            if (!CheckMagicNumber(message))
            {
                return null;
            }

            var statusMessage = new WsprDecodeMessage();

            int cur = MAGIC_NUMBER_LENGTH;
            statusMessage.SchemaVersion = DecodeQInt32(message, ref cur);

            var messageType = (MessageType)DecodeQInt32(message, ref cur);

            if (messageType != MessageType.WSPR_DECODE_MESSAGE_TYPE)
            {
                return null;
            }

            statusMessage.Id = DecodeString(message, ref cur);
            statusMessage.New = DecodeBool(message, ref cur);
            statusMessage.StartTime = DecodeQTime(message, ref cur);
            statusMessage.Snr = DecodeQInt32(message, ref cur);
            statusMessage.DeltaTime = DecodeDouble(message, ref cur);
            statusMessage.Frequency = DecodeQUInt64(message, ref cur);
            statusMessage.Drift = DecodeQInt32(message, ref cur);
            statusMessage.Callsign = DecodeString(message, ref cur);
            statusMessage.Grid = DecodeString(message, ref cur);
            statusMessage.PowerDbm = DecodeQInt32(message, ref cur);
            statusMessage.FromRecording = DecodeBool(message, ref cur);

            return statusMessage;
        }

        public override string ToString() =>
            $"WSPR       {this.ToCompactLine(nameof(Id))}";
    }
}