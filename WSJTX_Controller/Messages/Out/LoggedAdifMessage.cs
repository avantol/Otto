using System;

namespace WsjtxUdpLib.Messages.Out
{
    /*
     * Logged ADIF    Out      12                     quint32
     *                         Id (unique key)        utf8
     *                         ADIF text              utf8
     *
     *      The  logged ADIF  message is  sent to  the server(s)  when the
     *      WSJT-X user accepts the "Log  QSO" dialog by clicking the "OK"
     *      button. The  "ADIF text" field  consists of a valid  ADIF file
     *      such that  the WSJT-X  UDP header information  is encapsulated
     *      into a valid ADIF header. E.g.:
     *
     *          <magic-number><schema-number><type><id><32-bit-count>  # binary encoded fields
     *          # the remainder is the contents of the ADIF text field
     *          <adif_ver:5>3.0.7
     *          <programid:6>WSJT-X
     *          <EOH>
     *          ADIF log data fields ...<EOR>
     *
     *      Note that  receiving applications can treat  the whole message
     *      as a valid ADIF file with one record without special parsing.
     */

    public class LoggedAdifMessage : WsjtxMessage
    {
        public string Id { get; private set; }
        public int SchemaVersion { get; private set; }
        /// <summary>
        /// A complete ADIF file with one record after the header.
        /// </summary>
        public string AdifText { get; set; }

        public static new WsjtxMessage Parse(byte[] message)
        {
            if (!CheckMagicNumber(message))
            {
                return null;
            }

            var loggedAdifMessage = new LoggedAdifMessage();

            int cur = MAGIC_NUMBER_LENGTH;
            loggedAdifMessage.SchemaVersion = DecodeQInt32(message, ref cur);

            var messageType = (MessageType)DecodeQInt32(message, ref cur);

            if (messageType != MessageType.LOGGED_ADIF_MESSAGE_TYPE)
            {
                return null;
            }

            loggedAdifMessage.Id = DecodeString(message, ref cur);
            loggedAdifMessage.AdifText = DecodeString(message, ref cur)?.Trim();

            return loggedAdifMessage;
        }

        public override string ToString() =>
            $"ADIF      {AdifText}";
    }
}
