using System;

namespace WsjtxUdpLib.Messages
{
    /*
     * Replay        In        7                      quint32
     *                         Id (unique key)        utf8
     *
     *      When a server starts it may  be useful for it to determine the
     *      state  of preexisting  clients. Sending  this message  to each
     *      client as it is discovered  will cause that client (WSJT-X) to
     *      send a "Decode" message for each decode currently in its "Band
     *      activity"  window. Each  "Decode" message  sent will  have the
     *      "New" flag set to false so that they can be distinguished from
     *      new decodes. After  all the old decodes have  been broadcast a
     *      "Status" message  is also broadcast.  If the server  wishes to
     *      determine  the  status  of  a newly  discovered  client;  this
     *      message should be used.
     */

    public class ReplayMessage : IWsjtxCommandMessageGenerator
    {
        public byte[] GetBytes() => throw new NotImplementedException();
    }
}
