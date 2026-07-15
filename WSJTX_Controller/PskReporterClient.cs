using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace WSJTX_Controller
{
    /// <summary>
    /// Minimal MQTT 3.1.1 client for subscribing to the PSK Reporter spot feed.
    /// Supports only CONNECT, SUBSCRIBE (QoS 0), PINGREQ, and incoming PUBLISH.
    /// No external dependencies.
    /// </summary>
    public class PskReporterClient : IDisposable
    {
        public event Action<Spot, string> SpotReceived;         // (spot, band)
        public event Action<string> ConnectionStateChanged;     // "Connected", "Disconnected", "Reconnecting"

        private const string BrokerHost = "mqtt.pskreporter.info";
        private const int BrokerPort = 1883;
        private const int KeepAliveSecs = 60;
        private const int ReconnectBaseMs = 2000;
        private const int ReconnectMaxMs = 60000;

        private string myCall;
        private string currentMode;
        private TcpClient tcpClient;
        private NetworkStream stream;
        private Thread readThread;
        private volatile bool running;
        private volatile bool disposed;
        private string currentTopic;
        private int reconnectDelayMs = ReconnectBaseMs;
        private readonly object connectLock = new object();
        private Timer pingTimer;
        private ushort packetId = 1;

        public string ConnectionState { get; private set; } = "Disconnected";

        public void UpdateCallsign(string call)
        {
            myCall = call;
            Resubscribe();
        }

        public void UpdateMode(string mode)
        {
            if (mode == currentMode) return;
            currentMode = mode;
            Resubscribe();
        }

        public void Connect()
        {
            if (running) return;
            running = true;
            readThread = new Thread(ConnectionLoop) { IsBackground = true, Name = "PskReporterMqtt" };
            readThread.Start();
        }

        private void ConnectionLoop()
        {
            while (running)
            {
                try
                {
                    SetState("Connecting");
                    tcpClient = new TcpClient();
                    tcpClient.Connect(BrokerHost, BrokerPort);
                    stream = tcpClient.GetStream();
                    stream.ReadTimeout = (KeepAliveSecs + 10) * 1000;

                    SendConnect();
                    byte[] connack = ReadPacket();
                    if (connack == null || connack.Length < 4 || connack[3] != 0x00)
                    {
                        throw new Exception("CONNACK rejected");
                    }

                    reconnectDelayMs = ReconnectBaseMs;
                    SetState($"Connected to {BrokerHost}");

                    // Start keep-alive pings
                    pingTimer = new Timer(_ => SendPingreq(), null, KeepAliveSecs * 1000 / 2, KeepAliveSecs * 1000 / 2);

                    // Subscribe if we have call and mode
                    if (!string.IsNullOrEmpty(myCall) && !string.IsNullOrEmpty(currentMode))
                    {
                        string topic = BuildTopic(currentMode, myCall);
                        SendSubscribe(topic);
                        currentTopic = topic;
                    }

                    // Read loop
                    while (running)
                    {
                        byte[] packet = ReadPacket();
                        if (packet == null) break;
                        HandlePacket(packet);
                    }
                }
                catch (Exception)
                {
                    // Connection lost or failed
                }
                finally
                {
                    pingTimer?.Dispose();
                    pingTimer = null;
                    CloseConnection();
                }

                if (!running) break;

                SetState($"Reconnecting to {BrokerHost}");
                Thread.Sleep(reconnectDelayMs);
                reconnectDelayMs = Math.Min(reconnectDelayMs * 2, ReconnectMaxMs);
            }

            SetState("Disconnected");
        }

        private void Resubscribe()
        {
            if (string.IsNullOrEmpty(myCall) || string.IsNullOrEmpty(currentMode)) return;

            string newTopic = BuildTopic(currentMode, myCall);
            if (newTopic == currentTopic) return;

            // Unsubscribe old, subscribe new
            if (stream != null && tcpClient != null && tcpClient.Connected)
            {
                try
                {
                    if (currentTopic != null) SendUnsubscribe(currentTopic);
                    SendSubscribe(newTopic);
                    currentTopic = newTopic;
                }
                catch (Exception)
                {
                    // Will reconnect
                }
            }
        }

        private static string BuildTopic(string mode, string call)
        {
            // Replace '/' with '.' in callsign for MQTT topic (/ is topic separator)
            string safeCall = call.Replace('/', '.');
            return $"pskr/filter/v2/+/{mode}/{safeCall}/#";
        }

        // --- MQTT packet construction ---

        private void SendConnect()
        {
            using (var ms = new MemoryStream())
            using (var w = new BinaryWriter(ms))
            {
                // Variable header
                byte[] protocolName = { 0x00, 0x04, (byte)'M', (byte)'Q', (byte)'T', (byte)'T' };
                byte protocolLevel = 0x04; // MQTT 3.1.1
                byte connectFlags = 0x02; // Clean session
                byte[] keepAlive = { (byte)(KeepAliveSecs >> 8), (byte)(KeepAliveSecs & 0xFF) };

                // Client ID
                string clientId = "otto_" + Guid.NewGuid().ToString("N").Substring(0, 8);
                byte[] clientIdBytes = Encoding.UTF8.GetBytes(clientId);

                w.Write(protocolName);
                w.Write(protocolLevel);
                w.Write(connectFlags);
                w.Write(keepAlive);
                w.Write((byte)(clientIdBytes.Length >> 8));
                w.Write((byte)(clientIdBytes.Length & 0xFF));
                w.Write(clientIdBytes);

                byte[] payload = ms.ToArray();
                SendPacket(0x10, payload); // CONNECT = 0x10
            }
        }

        private void SendSubscribe(string topic)
        {
            using (var ms = new MemoryStream())
            using (var w = new BinaryWriter(ms))
            {
                ushort id = packetId++;
                w.Write((byte)(id >> 8));
                w.Write((byte)(id & 0xFF));

                byte[] topicBytes = Encoding.UTF8.GetBytes(topic);
                w.Write((byte)(topicBytes.Length >> 8));
                w.Write((byte)(topicBytes.Length & 0xFF));
                w.Write(topicBytes);
                w.Write((byte)0x00); // QoS 0

                byte[] payload = ms.ToArray();
                SendPacket(0x82, payload); // SUBSCRIBE = 0x82
            }
        }

        private void SendUnsubscribe(string topic)
        {
            using (var ms = new MemoryStream())
            using (var w = new BinaryWriter(ms))
            {
                ushort id = packetId++;
                w.Write((byte)(id >> 8));
                w.Write((byte)(id & 0xFF));

                byte[] topicBytes = Encoding.UTF8.GetBytes(topic);
                w.Write((byte)(topicBytes.Length >> 8));
                w.Write((byte)(topicBytes.Length & 0xFF));
                w.Write(topicBytes);

                byte[] payload = ms.ToArray();
                SendPacket(0xA2, payload); // UNSUBSCRIBE = 0xA2
            }
        }

        private void SendPingreq()
        {
            try
            {
                lock (connectLock)
                {
                    if (stream != null && tcpClient != null && tcpClient.Connected)
                    {
                        stream.Write(new byte[] { 0xC0, 0x00 }, 0, 2); // PINGREQ
                        stream.Flush();
                    }
                }
            }
            catch (Exception)
            {
                // Will trigger reconnect
            }
        }

        private void SendPacket(byte fixedHeaderByte, byte[] remainingBytes)
        {
            lock (connectLock)
            {
                using (var ms = new MemoryStream())
                {
                    ms.WriteByte(fixedHeaderByte);
                    WriteRemainingLength(ms, remainingBytes.Length);
                    ms.Write(remainingBytes, 0, remainingBytes.Length);
                    byte[] packet = ms.ToArray();
                    stream.Write(packet, 0, packet.Length);
                    stream.Flush();
                }
            }
        }

        private static void WriteRemainingLength(Stream s, int length)
        {
            do
            {
                byte encodedByte = (byte)(length % 128);
                length /= 128;
                if (length > 0) encodedByte |= 0x80;
                s.WriteByte(encodedByte);
            } while (length > 0);
        }

        // --- MQTT packet reading ---

        private byte[] ReadPacket()
        {
            try
            {
                int firstByte = stream.ReadByte();
                if (firstByte < 0) return null;

                int remainingLength = ReadRemainingLength();
                if (remainingLength < 0) return null;

                byte[] packet = new byte[remainingLength + 2]; // generous header size
                // Rebuild full packet for handler
                using (var ms = new MemoryStream())
                {
                    ms.WriteByte((byte)firstByte);
                    WriteRemainingLength(ms, remainingLength);

                    byte[] body = new byte[remainingLength];
                    int read = 0;
                    while (read < remainingLength)
                    {
                        int n = stream.Read(body, read, remainingLength - read);
                        if (n <= 0) return null;
                        read += n;
                    }
                    ms.Write(body, 0, body.Length);
                    return ms.ToArray();
                }
            }
            catch (Exception)
            {
                return null;
            }
        }

        private int ReadRemainingLength()
        {
            int multiplier = 1;
            int value = 0;
            for (int i = 0; i < 4; i++)
            {
                int b = stream.ReadByte();
                if (b < 0) return -1;
                value += (b & 0x7F) * multiplier;
                if ((b & 0x80) == 0) return value;
                multiplier *= 128;
            }
            return -1; // malformed
        }

        private void HandlePacket(byte[] packet)
        {
            if (packet.Length < 1) return;
            int type = (packet[0] >> 4) & 0x0F;

            switch (type)
            {
                case 3: // PUBLISH
                    HandlePublish(packet);
                    break;
                case 13: // PINGRESP
                    break; // keep-alive ack, nothing to do
                case 9: // SUBACK
                    break; // subscription confirmed
                case 11: // UNSUBACK
                    break;
                default:
                    break;
            }
        }

        private void HandlePublish(byte[] packet)
        {
            try
            {
                // Skip fixed header
                int pos = 1;
                // Read remaining length to find where variable header starts
                int multiplier = 1;
                int remainingLength = 0;
                while (pos < packet.Length)
                {
                    byte b = packet[pos++];
                    remainingLength += (b & 0x7F) * multiplier;
                    if ((b & 0x80) == 0) break;
                    multiplier *= 128;
                }

                int variableHeaderStart = pos;

                // Topic length (2 bytes MSB)
                if (pos + 2 > packet.Length) return;
                int topicLength = (packet[pos] << 8) | packet[pos + 1];
                pos += 2;

                if (pos + topicLength > packet.Length) return;
                string topic = Encoding.UTF8.GetString(packet, pos, topicLength);
                pos += topicLength;

                // QoS 0, no packet ID

                // Payload is the rest
                int payloadLength = variableHeaderStart + remainingLength - pos;
                if (payloadLength <= 0 || pos + payloadLength > packet.Length) return;
                string json = Encoding.UTF8.GetString(packet, pos, payloadLength);

                // Extract band from topic: pskr/filter/v2/{BAND}/{MODE}/{CALL}/...
                string band = ExtractBandFromTopic(topic);

                ParseAndEmitSpot(json, band);
            }
            catch (Exception)
            {
                // Skip malformed packets
            }
        }

        private string ExtractBandFromTopic(string topic)
        {
            // Topic format: pskr/filter/v2/{BAND_OR_WILDCARD}/{MODE}/{CALL}/...
            string[] parts = topic.Split('/');
            if (parts.Length >= 4)
            {
                string bandPart = parts[3]; // e.g. "20m" or could be a frequency
                // PSK Reporter uses band names like "20m", "40m", etc. or frequency in MHz
                if (bandPart.EndsWith("m") || bandPart.EndsWith("cm"))
                    return bandPart;
            }
            return null; // will try to derive from frequency
        }

        private void ParseAndEmitSpot(string json, string topicBand)
        {
            // Minimal JSON parsing for fixed fields: sc, rc, rl, rp, f, t
            string sc = ExtractJsonString(json, "sc"); // sender call
            string rc = ExtractJsonString(json, "rc"); // receiver call
            string rl = ExtractJsonString(json, "rl"); // receiver grid
            int? rp = ExtractJsonInt(json, "rp");       // SNR dB
            long? f = ExtractJsonLong(json, "f");        // frequency Hz
            long? t = ExtractJsonLong(json, "t");        // Unix timestamp

            // Validate
            if (rc == null || rl == null || rl.Length < 4 || rp == null) return;

            double lat, lon;
            if (!GeoUtils.GridToLatLon(rl, out lat, out lon)) return;

            // Determine band
            string band = topicBand;
            if (string.IsNullOrEmpty(band) && f.HasValue)
            {
                band = GeoUtils.FreqToBand(f.Value);
            }
            if (string.IsNullOrEmpty(band)) return;

            var spot = new Spot
            {
                ReceiverCall = rc,
                ReceiverGrid = rl,
                SnrDb = rp.Value,
                FrequencyHz = f ?? 0,
                TimestampUtc = t.HasValue ? DateTimeOffset.FromUnixTimeSeconds(t.Value).UtcDateTime : DateTime.UtcNow,
                LatDeg = lat,
                LonDeg = lon
            };

            SpotReceived?.Invoke(spot, band);
        }

        // --- Minimal JSON field extraction ---

        private static string ExtractJsonString(string json, string key)
        {
            string pattern = "\"" + key + "\":\"";
            int idx = json.IndexOf(pattern);
            if (idx < 0) return null;
            int start = idx + pattern.Length;
            int end = json.IndexOf('"', start);
            if (end < 0) return null;
            return json.Substring(start, end - start);
        }

        private static int? ExtractJsonInt(string json, string key)
        {
            long? val = ExtractJsonLong(json, key);
            if (val.HasValue) return (int)val.Value;
            return null;
        }

        private static long? ExtractJsonLong(string json, string key)
        {
            // Try "key":value (number, no quotes)
            string pattern = "\"" + key + "\":";
            int idx = json.IndexOf(pattern);
            if (idx < 0) return null;
            int start = idx + pattern.Length;

            // Skip whitespace
            while (start < json.Length && json[start] == ' ') start++;
            if (start >= json.Length) return null;

            // Handle quoted numbers
            if (json[start] == '"') start++;

            int end = start;
            while (end < json.Length && (char.IsDigit(json[end]) || json[end] == '-'))
                end++;

            if (end == start) return null;
            string numStr = json.Substring(start, end - start);
            long result;
            if (long.TryParse(numStr, out result)) return result;
            return null;
        }

        // --- Cleanup ---

        private void CloseConnection()
        {
            try { stream?.Close(); } catch { }
            try { tcpClient?.Close(); } catch { }
            stream = null;
            tcpClient = null;
        }

        private void SetState(string state)
        {
            ConnectionState = state;
            ConnectionStateChanged?.Invoke(state);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            running = false;
            pingTimer?.Dispose();
            CloseConnection();
        }
    }
}
