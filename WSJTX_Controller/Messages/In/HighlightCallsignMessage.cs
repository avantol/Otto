using System;

namespace WsjtxUdpLib.Messages
{
    /*
     * Highlight Callsign In   13                     quint32
     *                         Id (unique key)        utf8
     *                         Callsign               utf8
     *                         Background Color       QColor
     *                         Foreground Color       QColor
     *                         Highlight last         bool
     *
     *      The server  may send  this message at  any time.   The message
     *      specifies  the background  and foreground  color that  will be
     *      used  to  highlight  the  specified callsign  in  the  decoded
     *      messages  printed  in the  Band  Activity  panel.  The  WSJT-X
     *      clients maintain a list of such instructions and apply them to
     *      all decoded  messages in the  band activity window.   To clear
     *      and  cancel  highlighting send  an  invalid  QColor value  for
     *      either or both  of the background and  foreground fields. When
     *      using  this mode  the  total number  of callsign  highlighting
     *      requests should be limited otherwise the performance of WSJT-X
     *      decoding may be  impacted. A rough rule of thumb  might be too
     *      limit the  number of active  highlighting requests to  no more
     *      than 100.
     *
     *      The "Highlight last"  field allows the sender  to request that
     *      all instances of  "Callsign" in the last  period only, instead
     *      of all instances in all periods, be highlighted.
     */

    public class HighlightCallsignMessage : IWsjtxCommandMessageGenerator
    {
        public byte[] GetBytes() => throw new NotImplementedException();
    }
}
