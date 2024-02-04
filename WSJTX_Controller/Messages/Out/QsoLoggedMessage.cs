using System;

namespace WsjtxUdpLib.Messages.Out
{
    /*
     * QSO Logged    Out       5                      quint32
     *                         Id (unique key)        utf8
     *                         Date & Time Off        QDateTime
     *                         DX call                utf8
     *                         DX grid                utf8
     *                         Tx frequency (Hz)      quint64
     *                         Mode                   utf8
     *                         Report sent            utf8
     *                         Report received        utf8
     *                         Tx power               utf8
     *                         Comments               utf8
     *                         Name                   utf8
     *                         Date & Time On         QDateTime
     *                         Operator call          utf8
     *                         My call                utf8
     *                         My grid                utf8
     *                         Exchange sent          utf8
     *                         Exchange received      utf8
     *
     *      The  QSO logged  message is  sent  to the  server(s) when  the
     *      WSJT-X user accepts the "Log  QSO" dialog by clicking the "OK"
     *      button.
     */

    public class QsoLoggedMessage : WsjtxMessage
    {
        public int SchemaVersion { get; private set; }
        public string Id { get; private set; }
        public DateTime DateTimeOff { get; private set; }
        public string DxCall { get; private set; }
        public string DxGrid { get; private set; }
        public ulong TxFrequency { get; private set; }
        public string Mode { get; private set; }
        public string ReportSent { get; private set; }
        public string ReportReceived { get; private set; }
        public string TxPower { get; private set; }
        public string Comments { get; private set; }
        public string Name { get; private set; }
        public DateTime DateTimeOn { get; private set; }
        public string OperatorCall { get; private set; }
        public string MyCall { get; private set; }
        public string MyGrid { get; private set; }
        public string ExchangeSent { get; private set; }
        public string ExchangeReceived { get; private set; }

        public static new WsjtxMessage Parse(byte[] message)
        {
            if (!CheckMagicNumber(message))
            {
                return null;
            }

            var qsoLoggedMessage = new QsoLoggedMessage();

            int cur = MAGIC_NUMBER_LENGTH;
            qsoLoggedMessage.SchemaVersion = DecodeQInt32(message, ref cur);

            var messageType = (MessageType)DecodeQInt32(message, ref cur);

            if (messageType != MessageType.QSO_LOGGED_MESSAGE_TYPE)
            {
                return null;
            }

            qsoLoggedMessage.Id = DecodeString(message, ref cur);
            qsoLoggedMessage.DateTimeOff = DecodeQDateTimeWithoutTimezone(message, ref cur);
            qsoLoggedMessage.DxCall = DecodeString(message, ref cur);
            qsoLoggedMessage.DxGrid = DecodeString(message, ref cur);
            qsoLoggedMessage.TxFrequency = DecodeQUInt64(message, ref cur);
            qsoLoggedMessage.Mode = DecodeString(message, ref cur);
            qsoLoggedMessage.ReportSent = DecodeString(message, ref cur);
            qsoLoggedMessage.ReportReceived = DecodeString(message, ref cur);
            qsoLoggedMessage.TxPower = DecodeString(message, ref cur);
            qsoLoggedMessage.Comments = DecodeString(message, ref cur);
            qsoLoggedMessage.Name = DecodeString(message, ref cur);
            qsoLoggedMessage.DateTimeOn = DecodeQDateTimeWithoutTimezone(message, ref cur);
            qsoLoggedMessage.OperatorCall = DecodeString(message, ref cur);
            qsoLoggedMessage.MyCall = DecodeString(message, ref cur);
            qsoLoggedMessage.MyGrid = DecodeString(message, ref cur);
            qsoLoggedMessage.ExchangeSent = DecodeString(message, ref cur);
            qsoLoggedMessage.ExchangeReceived = DecodeString(message, ref cur);

            return qsoLoggedMessage;
        }

        public override string ToString() => $"QSOLogged  {this.ToCompactLine(nameof(Id))}";
    }
}
