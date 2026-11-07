using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NareLauncher.Core
{
    /// <summary>Один сервер из списка игры.</summary>
    public class ServerEntry
    {
        public string Name = "";
        public string Address = "";
        public string IconBase64 = "";

        // то, что приходит с самого сервера
        public string Motd = "";
        public string VersionName = "";
        public int Online = -1;
        public int Max = -1;
        public int Ping = -1;
        public string Error = "";
        public bool Checked;

        public string Host
        {
            get
            {
                string a = (Address ?? "").Trim();
                int colon = a.LastIndexOf(':');
                return colon > 0 ? a.Substring(0, colon) : a;
            }
        }

        public int Port
        {
            get
            {
                string a = (Address ?? "").Trim();
                int colon = a.LastIndexOf(':');
                int port;
                if (colon > 0 && int.TryParse(a.Substring(colon + 1), out port) && port > 0 && port < 65536)
                    return port;
                return 25565;
            }
        }
    }

    /// <summary>
    /// Список серверов игры: читаем и пишем servers.dat той же сборки,
    /// которую запускает лаунчер. Игра подхватит изменения при старте.
    /// </summary>
    public static class ServerList
    {
        public static string FileFor(string versionId)
        {
            string dir = string.IsNullOrEmpty(versionId) ? Paths.GameRoot : Paths.InstanceDir(versionId);
            return Path.Combine(dir, "servers.dat");
        }

        public static List<ServerEntry> Load(string versionId)
        {
            var result = new List<ServerEntry>();
            string file = FileFor(versionId);
            if (!File.Exists(file)) return result;

            try
            {
                var root = NbtTag.ReadFile(file);
                var list = root.Child("servers");
                if (list == null || list.Children == null) return result;

                foreach (var item in list.Children)
                {
                    result.Add(new ServerEntry
                    {
                        Name = item.ChildText("name", "Сервер"),
                        Address = item.ChildText("ip", ""),
                        IconBase64 = item.ChildText("icon", "")
                    });
                }
            }
            catch (Exception ex)
            {
                Log.Warn("servers.dat не прочитан: " + ex.Message);
            }

            return result;
        }

        public static void Save(string versionId, List<ServerEntry> servers)
        {
            string file = FileFor(versionId);
            Paths.EnsureForFile(file);

            var list = NbtTag.NewList("servers", NbtTag.Compound);
            foreach (var s in servers)
            {
                var item = NbtTag.NewCompound("");
                item.Put(NbtTag.OfString("name", s.Name));
                item.Put(NbtTag.OfString("ip", s.Address));
                if (!string.IsNullOrEmpty(s.IconBase64)) item.Put(NbtTag.OfString("icon", s.IconBase64));
                item.Put(NbtTag.OfByte("acceptTextures", 1));
                list.Children.Add(item);
            }

            var root = NbtTag.NewCompound("");
            root.Put(list);

            // старый файл держим рядом — мало ли
            try { if (File.Exists(file)) File.Copy(file, file + ".bak", true); } catch { }

            root.WriteFile(file);
            Log.Info("Список серверов сохранён: " + file);
        }

        // ------------------------------------------------------------- опрос

        /// <summary>
        /// Спрашивает сервер о себе так же, как это делает игра в меню
        /// «Сетевая игра»: рукопожатие, запрос статуса, разбор ответа.
        /// </summary>
        public static async Task QueryAsync(ServerEntry server, int timeoutMs = 6000)
        {
            server.Checked = true;
            server.Error = "";
            server.Online = server.Max = server.Ping = -1;
            server.Motd = server.VersionName = "";

            if (string.IsNullOrWhiteSpace(server.Address)) { server.Error = "нет адреса"; return; }

            var clock = Stopwatch.StartNew();

            try
            {
                using (var client = new TcpClient())
                {
                    var connect = client.ConnectAsync(server.Host, server.Port);
                    if (await Task.WhenAny(connect, Task.Delay(timeoutMs)).ConfigureAwait(false) != connect)
                    {
                        server.Error = "не отвечает";
                        return;
                    }
                    await connect.ConfigureAwait(false);

                    var stream = client.GetStream();
                    stream.ReadTimeout = timeoutMs;
                    stream.WriteTimeout = timeoutMs;

                    // рукопожатие: версия протокола 1.21.4, состояние 1 = статус.
                    // «любую» (-1) часть прокси-серверов просто обрывает.
                    var handshake = new MemoryStream();
                    WriteVarInt(handshake, 0x00);
                    WriteVarInt(handshake, 769);
                    WriteStr(handshake, server.Host);
                    handshake.WriteByte((byte)(server.Port >> 8));
                    handshake.WriteByte((byte)(server.Port & 0xFF));
                    WriteVarInt(handshake, 1);
                    await SendAsync(stream, handshake.ToArray()).ConfigureAwait(false);

                    var request = new MemoryStream();
                    WriteVarInt(request, 0x00);
                    await SendAsync(stream, request.ToArray()).ConfigureAwait(false);

                    int length = await ReadVarIntAsync(stream, timeoutMs).ConfigureAwait(false);
                    if (length < 0) { server.Error = "молчит или закрыл связь"; return; }
                    if (length == 0 || length > 4 * 1024 * 1024) { server.Error = "непонятный ответ"; return; }

                    var payload = await ReadExactAsync(stream, length, timeoutMs).ConfigureAwait(false);
                    server.Ping = (int)clock.ElapsedMilliseconds;

                    var reader = new MemoryStream(payload);
                    ReadVarInt(reader);                       // id пакета
                    int jsonLength = ReadVarInt(reader);
                    if (jsonLength <= 0) { server.Error = "пустой ответ"; return; }

                    var text = new byte[Math.Min(jsonLength, payload.Length - (int)reader.Position)];
                    reader.Read(text, 0, text.Length);

                    Fill(server, Json.Parse(Encoding.UTF8.GetString(text)));
                }
            }
            catch (Exception ex)
            {
                server.Error = Short(ex.Message);
            }
        }

        private static void Fill(ServerEntry server, Json json)
        {
            server.VersionName = json["version"]["name"].AsString("");
            server.Online = json["players"]["online"].AsInt(-1);
            server.Max = json["players"]["max"].AsInt(-1);
            server.Motd = Flatten(json["description"]).Replace("\n", " · ").Trim();

            string favicon = json["favicon"].AsString("");
            if (favicon.StartsWith("data:image", StringComparison.OrdinalIgnoreCase))
            {
                int comma = favicon.IndexOf(',');
                if (comma > 0) server.IconBase64 = favicon.Substring(comma + 1);
            }
        }

        /// <summary>Описание сервера бывает строкой, а бывает деревом из кусочков.</summary>
        private static string Flatten(Json node)
        {
            if (node.Type == JsonType.String) return StripCodes(node.Str);
            if (node.IsArray)
            {
                var sb = new StringBuilder();
                foreach (var item in node.Items) sb.Append(Flatten(item));
                return sb.ToString();
            }
            if (!node.IsObject) return "";

            var text = new StringBuilder(StripCodes(node["text"].AsString("")));
            foreach (var extra in node["extra"].Items) text.Append(Flatten(extra));
            return text.ToString();
        }

        /// <summary>Убирает §-раскраску — в списке она была бы мусором.</summary>
        private static string StripCodes(string s)
        {
            if (string.IsNullOrEmpty(s) || s.IndexOf('§') < 0) return s ?? "";

            var sb = new StringBuilder(s.Length);
            for (int i = 0; i < s.Length; i++)
            {
                if (s[i] == '§') { i++; continue; }
                sb.Append(s[i]);
            }
            return sb.ToString();
        }

        private static string Short(string message)
        {
            if (string.IsNullOrEmpty(message)) return "не отвечает";
            if (message.Length > 60) message = message.Substring(0, 60) + "…";
            return message;
        }

        // --------------------------------------------------------- протокол

        private static async Task SendAsync(NetworkStream stream, byte[] body)
        {
            var packet = new MemoryStream();
            WriteVarInt(packet, body.Length);
            packet.Write(body, 0, body.Length);

            var data = packet.ToArray();
            await stream.WriteAsync(data, 0, data.Length).ConfigureAwait(false);
            await stream.FlushAsync().ConfigureAwait(false);
        }

        private static void WriteVarInt(Stream s, int value)
        {
            uint v = (uint)value;
            while (true)
            {
                if ((v & ~0x7Fu) == 0) { s.WriteByte((byte)v); return; }
                s.WriteByte((byte)((v & 0x7F) | 0x80));
                v >>= 7;
            }
        }

        private static void WriteStr(Stream s, string text)
        {
            var data = Encoding.UTF8.GetBytes(text ?? "");
            WriteVarInt(s, data.Length);
            s.Write(data, 0, data.Length);
        }

        private static int ReadVarInt(Stream s)
        {
            int result = 0, shift = 0;
            while (true)
            {
                int b = s.ReadByte();
                if (b < 0) return result;

                result |= (b & 0x7F) << shift;
                if ((b & 0x80) == 0) return result;

                shift += 7;
                if (shift >= 35) return result;
            }
        }

        private static async Task<int> ReadVarIntAsync(NetworkStream stream, int timeoutMs)
        {
            int result = 0, shift = 0;
            var one = new byte[1];

            while (true)
            {
                var read = stream.ReadAsync(one, 0, 1);
                if (await Task.WhenAny(read, Task.Delay(timeoutMs)).ConfigureAwait(false) != read) return -1;
                if (await read.ConfigureAwait(false) <= 0) return -1;

                result |= (one[0] & 0x7F) << shift;
                if ((one[0] & 0x80) == 0) return result;

                shift += 7;
                if (shift >= 35) return -1;
            }
        }

        private static async Task<byte[]> ReadExactAsync(NetworkStream stream, int count, int timeoutMs)
        {
            var buffer = new byte[count];
            int done = 0;

            while (done < count)
            {
                var read = stream.ReadAsync(buffer, done, count - done);
                if (await Task.WhenAny(read, Task.Delay(timeoutMs)).ConfigureAwait(false) != read) break;

                int got = await read.ConfigureAwait(false);
                if (got <= 0) break;
                done += got;
            }

            if (done == count) return buffer;

            var cut = new byte[done];
            Array.Copy(buffer, cut, done);
            return cut;
        }
    }
}
