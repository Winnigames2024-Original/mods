using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NareLauncher.Core
{
    /// <summary>
    /// Перевод текста сообщений через бесплатный публичный эндпоинт Google.
    /// Если текст в основном на кириллице — переводим на английский, иначе на русский.
    /// Ключ не нужен; при недоступности сервиса бросает исключение (ловит вызывающий).
    /// </summary>
    public static class Translate
    {
        public static async Task<string> TextAsync(string text, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(text)) return text;
            string tl = MostlyCyrillic(text) ? "en" : "ru";
            string url = "https://translate.googleapis.com/translate_a/single?client=gtx&sl=auto&tl="
                         + tl + "&dt=t&q=" + Net.UrlEncode(text);
            string raw = await Net.GetStringAsync(url, ct).ConfigureAwait(false);
            // Ответ: [[["перевод сегмента","оригинал",...], ...], ...]
            var root = Json.Parse(raw);
            var segs = root[0];
            var sb = new StringBuilder();
            foreach (var seg in segs.Items)
                sb.Append(seg[0].AsString(""));
            string outp = sb.ToString();
            return string.IsNullOrEmpty(outp) ? text : outp;
        }

        private static bool MostlyCyrillic(string s)
        {
            int cyr = 0, lat = 0;
            foreach (char c in s)
            {
                if (c >= 'а' && c <= 'я' || c >= 'А' && c <= 'Я' || c == 'ё' || c == 'Ё') cyr++;
                else if (c >= 'a' && c <= 'z' || c >= 'A' && c <= 'Z') lat++;
            }
            return cyr > lat;
        }
    }
}
