using System;
using System.Collections.Generic;
using System.Text;

namespace WsjtxUdpLib.Messages
{
    /*
     * Configure      In       15                     quint32
     *                         Id (unique key)        utf8
     *                         Mode                   utf8
     *                         Frequency Tolerance    quint32
     *                         Submode                utf8
     *                         Fast Mode              bool
     *                         T/R Period             quint32
     *                         Rx DF                  quint32
     *                         DX Call                utf8
     *                         DX Grid                utf8
     *                         Generate Messages      bool
     *
     *      The server  may send  this message at  any time.   The message
     *      specifies  various  configuration  options.  For  utf8  string
     *      fields an empty value implies no change, for the quint32 Rx DF
     *      and  Frequency  Tolerance  fields the  maximum  quint32  value
     *      implies  no change.   Invalid or  unrecognized values  will be
     *      silently ignored.
     */

    public class ConfigureMessage : IWsjtxCommandMessageGenerator
    {
        public byte[] GetBytes() => throw new NotImplementedException();
    }
}
