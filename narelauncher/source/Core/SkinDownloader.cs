using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NareLauncher.Core
{
    /// <summary>
    /// Скачивает скин игрока с сайтов по нику или по прямой ссылке на PNG.
    /// Официальный источник — Mojang: там же лежит и тип модели (обычные или тонкие руки).
    /// Остальные нужны, когда Mojang недоступен.
    /// </summary>
    public static class SkinDownloader
    {
        public enum Source { Auto, Mojang, Minotar, McHeads }

        /// <summary>Сколько ждём один источник, прежде чем перейти к следующему.</summary>
        public const int AttemptSeconds = 30;

        public static string Title(Source source)
        {
            switch (source)
            {
                case Source.Mojang: return "Mojang";
                case Source.Minotar: return "Minotar";
                case Source.McHeads: return "MC-Heads";
                default: return "Авто";
            }
        }

        public static readonly Source[] All =
        {
            Source.Auto, Source.Mojang, Source.Minotar, Source.McHeads
        };

        /// <summary>Что получилось: сам скин и откуда он взялся.</summary>
        public class Result
        {
            public SkinFile Skin;
            public Source From;
            public string Uuid;
            public string Note;
        }

        public static async Task<Result> FetchAsync(string query, Source source, CancellationToken ct,
                                                   IProgress<string> progress = null)
        {
            query = (query ?? "").Trim();
            if (query.Length == 0) throw new Exception("Введите ник игрока или ссылку на PNG.");

            if (query.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                query.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                if (progress != null) progress.Report("Качаем файл по ссылке…");

                using (var attempt = Limited(ct))
                {
                    var direct = await FromUrlAsync(query, null, attempt.Token).ConfigureAwait(false);
                    direct.Name = NameFromUrl(query);
                    return new Result { Skin = direct, From = source, Note = "по прямой ссылке" };
                }
            }

            if (!ValidName(query))
                throw new Exception("Ник — латиница, цифры и _, от 3 до 16 символов.");

            var problems = new List<string>();

            foreach (var one in Order(source))
            {
                ct.ThrowIfCancellationRequested();
                if (progress != null) progress.Report("Спрашиваем " + Title(one) + "…");

                using (var attempt = Limited(ct))
                {
                    try
                    {
                        var result = await OneAsync(query, one, attempt.Token).ConfigureAwait(false);
                        if (result != null)
                        {
                            result.Skin.Name = query;
                            return result;
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        if (ct.IsCancellationRequested) throw;   // окно закрыли — выходим
                        problems.Add(Title(one) + " — не ответил за " + AttemptSeconds + " с");
                        Log.Warn("Скин «" + query + "»: " + Title(one) + " не ответил за " +
                                 AttemptSeconds + " с");
                    }
                    catch (Exception ex)
                    {
                        problems.Add(Title(one) + " — " + Short(ex));
                        Log.Warn("Скин «" + query + "» с " + Title(one) + ": " + ex.Message);
                    }
                }
            }

            throw new Exception("Скин не найден. " + string.Join("; ", problems.ToArray()));
        }

        /// <summary>Отдельный срок на попытку, чтобы молчащий сайт не держал всё окно.</summary>
        private static CancellationTokenSource Limited(CancellationToken ct)
        {
            var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(AttemptSeconds));
            return cts;
        }

        private static IEnumerable<Source> Order(Source source)
        {
            if (source != Source.Auto) { yield return source; yield break; }

            yield return Source.Mojang;
            yield return Source.Minotar;
            yield return Source.McHeads;
        }

        private static async Task<Result> OneAsync(string name, Source source, CancellationToken ct)
        {
            switch (source)
            {
                case Source.Mojang:
                    return await FromMojangAsync(name, ct).ConfigureAwait(false);

                case Source.Minotar:
                    return new Result
                    {
                        Skin = await FromUrlAsync("https://minotar.net/skin/" + Net.UrlEncode(name), null, ct)
                                     .ConfigureAwait(false),
                        From = Source.Minotar,
                        Note = "зеркало Minotar — если игрока нет, вернётся стандартный скин"
                    };

                case Source.McHeads:
                    return new Result
                    {
                        Skin = await FromUrlAsync("https://mc-heads.net/skin/" + Net.UrlEncode(name), null, ct)
                                     .ConfigureAwait(false),
                        From = Source.McHeads,
                        Note = "зеркало MC-Heads — если игрока нет, вернётся стандартный скин"
                    };
            }

            return null;
        }

        /// <summary>Официальный путь: ник -> UUID -> профиль -> ссылка на текстуру.</summary>
        private static async Task<Result> FromMojangAsync(string name, CancellationToken ct)
        {
            var profile = await Net.GetJsonAsync(
                "https://api.mojang.com/users/profiles/minecraft/" + Net.UrlEncode(name), ct)
                .ConfigureAwait(false);

            string uuid = profile.Has("id") ? profile["id"].AsString(null) : null;
            if (string.IsNullOrEmpty(uuid)) throw new Exception("такого игрока нет");

            var full = await Net.GetJsonAsync(
                "https://sessionserver.mojang.com/session/minecraft/profile/" + uuid, ct)
                .ConfigureAwait(false);

            if (!full.Has("properties")) throw new Exception("профиль без текстур");

            foreach (var property in full["properties"].Items)
            {
                if (!property.Has("name") || property["name"].AsString("") != "textures") continue;

                string encoded = property["value"].AsString("");
                if (string.IsNullOrEmpty(encoded)) continue;

                string json = Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
                var decoded = Json.Parse(json);

                if (!decoded.Has("textures")) continue;
                var textures = decoded["textures"];
                if (!textures.Has("SKIN")) throw new Exception("у игрока стандартный скин");

                var node = textures["SKIN"];
                string url = node.Has("url") ? node["url"].AsString(null) : null;
                if (string.IsNullOrEmpty(url)) throw new Exception("в профиле нет ссылки на скин");

                bool slim = node.Has("metadata") && node["metadata"].Has("model") &&
                            node["metadata"]["model"].AsString("") == "slim";

                var skin = await FromUrlAsync(url, slim, ct).ConfigureAwait(false);

                return new Result
                {
                    Skin = skin,
                    From = Source.Mojang,
                    Uuid = uuid,
                    Note = "официальный профиль Mojang"
                };
            }

            throw new Exception("в профиле нет текстур");
        }

        public static async Task<SkinFile> FromUrlAsync(string url, bool? slim, CancellationToken ct)
        {
            byte[] data = await Net.GetBytesAsync(url, ct).ConfigureAwait(false);
            if (data == null || data.Length < 16) throw new Exception("пустой ответ");

            using (var ms = new MemoryStream(data))
            using (var bmp = new Bitmap(ms))
            {
                var skin = SkinFile.FromBitmap(bmp);
                if (slim.HasValue) skin.Arms = slim.Value ? SkinArms.Slim : SkinArms.Wide;
                return skin;
            }
        }

        public static bool ValidName(string name)
        {
            if (string.IsNullOrEmpty(name) || name.Length < 3 || name.Length > 16) return false;

            foreach (char c in name)
            {
                bool ok = (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') ||
                          (c >= '0' && c <= '9') || c == '_';
                if (!ok) return false;
            }
            return true;
        }

        private static string NameFromUrl(string url)
        {
            try
            {
                string last = new Uri(url).Segments[new Uri(url).Segments.Length - 1].Trim('/');
                last = Path.GetFileNameWithoutExtension(last);
                return string.IsNullOrEmpty(last) ? "skin" : Paths.SafeName(last);
            }
            catch { return "skin"; }
        }

        private static string Short(Exception ex)
        {
            string text = ex.Message ?? "ошибка";
            if (text.IndexOf("404", StringComparison.Ordinal) >= 0) return "игрок не найден";
            if (text.IndexOf("204", StringComparison.Ordinal) >= 0) return "игрок не найден";
            return text.Length > 90 ? text.Substring(0, 90) + "…" : text;
        }
    }
}
