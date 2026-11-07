using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace NareLauncher.Core
{
    public enum JsonType { Null, Object, Array, String, Number, Bool }

    /// <summary>
    /// Минимальный JSON-парсер/сериализатор без внешних зависимостей.
    /// Обращение к несуществующему ключу возвращает Json.Null, поэтому
    /// можно безопасно писать json["a"]["b"][0].AsString("").
    /// </summary>
    public class Json
    {
        public JsonType Type = JsonType.Null;
        public Dictionary<string, Json> Obj;
        public List<Json> Arr;
        public string Str;
        public double Num;
        public bool Bool;

        public static readonly Json Null = new Json();

        public static Json NewObject()
        {
            return new Json { Type = JsonType.Object, Obj = new Dictionary<string, Json>(StringComparer.Ordinal) };
        }

        public static Json NewArray()
        {
            return new Json { Type = JsonType.Array, Arr = new List<Json>() };
        }

        public static Json Of(string s)
        {
            if (s == null) return Null;
            return new Json { Type = JsonType.String, Str = s };
        }

        public static Json Of(double d) { return new Json { Type = JsonType.Number, Num = d }; }
        public static Json Of(bool b) { return new Json { Type = JsonType.Bool, Bool = b }; }

        public bool Exists { get { return Type != JsonType.Null; } }
        public bool IsObject { get { return Type == JsonType.Object; } }
        public bool IsArray { get { return Type == JsonType.Array; } }

        public Json this[string key]
        {
            get
            {
                Json v;
                if (Type == JsonType.Object && Obj.TryGetValue(key, out v)) return v;
                return Null;
            }
            set
            {
                if (Type != JsonType.Object)
                {
                    Type = JsonType.Object;
                    Obj = new Dictionary<string, Json>(StringComparer.Ordinal);
                }
                Obj[key] = value ?? Null;
            }
        }

        public Json this[int index]
        {
            get
            {
                if (Type == JsonType.Array && index >= 0 && index < Arr.Count) return Arr[index];
                return Null;
            }
        }

        public void Add(Json value)
        {
            if (Type != JsonType.Array)
            {
                Type = JsonType.Array;
                Arr = new List<Json>();
            }
            Arr.Add(value ?? Null);
        }

        public void Set(string key, string value) { this[key] = Of(value); }
        public void Set(string key, double value) { this[key] = Of(value); }
        public void Set(string key, bool value) { this[key] = Of(value); }
        public void Set(string key, Json value) { this[key] = value; }

        public bool Has(string key) { return Type == JsonType.Object && Obj.ContainsKey(key); }
        public void Remove(string key) { if (Type == JsonType.Object) Obj.Remove(key); }

        public int Count
        {
            get
            {
                if (Type == JsonType.Array) return Arr.Count;
                if (Type == JsonType.Object) return Obj.Count;
                return 0;
            }
        }

        private static readonly Json[] EmptyArr = new Json[0];
        private static readonly string[] EmptyKeys = new string[0];

        public IEnumerable<Json> Items
        {
            get { return Type == JsonType.Array ? (IEnumerable<Json>)Arr : EmptyArr; }
        }

        public IEnumerable<string> Keys
        {
            get { return Type == JsonType.Object ? (IEnumerable<string>)Obj.Keys : EmptyKeys; }
        }

        public IEnumerable<KeyValuePair<string, Json>> Pairs
        {
            get
            {
                if (Type != JsonType.Object) yield break;
                foreach (var kv in Obj) yield return kv;
            }
        }

        public string AsString(string def = null)
        {
            switch (Type)
            {
                case JsonType.String: return Str;
                case JsonType.Number: return Num.ToString(CultureInfo.InvariantCulture);
                case JsonType.Bool: return Bool ? "true" : "false";
                default: return def;
            }
        }

        public int AsInt(int def = 0)
        {
            if (Type == JsonType.Number) return (int)Num;
            if (Type == JsonType.String)
            {
                int r;
                if (int.TryParse(Str, NumberStyles.Any, CultureInfo.InvariantCulture, out r)) return r;
            }
            return def;
        }

        public long AsLong(long def = 0)
        {
            if (Type == JsonType.Number) return (long)Num;
            if (Type == JsonType.String)
            {
                long r;
                if (long.TryParse(Str, NumberStyles.Any, CultureInfo.InvariantCulture, out r)) return r;
            }
            return def;
        }

        public double AsDouble(double def = 0)
        {
            if (Type == JsonType.Number) return Num;
            if (Type == JsonType.String)
            {
                double r;
                if (double.TryParse(Str, NumberStyles.Any, CultureInfo.InvariantCulture, out r)) return r;
            }
            return def;
        }

        public bool AsBool(bool def = false)
        {
            if (Type == JsonType.Bool) return Bool;
            if (Type == JsonType.Number) return Math.Abs(Num) > double.Epsilon;
            if (Type == JsonType.String) return string.Equals(Str, "true", StringComparison.OrdinalIgnoreCase);
            return def;
        }

        public Json Clone()
        {
            switch (Type)
            {
                case JsonType.Object:
                    {
                        var o = NewObject();
                        foreach (var kv in Obj) o.Obj[kv.Key] = kv.Value.Clone();
                        return o;
                    }
                case JsonType.Array:
                    {
                        var a = NewArray();
                        foreach (var it in Arr) a.Arr.Add(it.Clone());
                        return a;
                    }
                case JsonType.String: return Of(Str);
                case JsonType.Number: return Of(Num);
                case JsonType.Bool: return Of(Bool);
                default: return Null;
            }
        }

        // ---------- Парсинг ----------

        public static Json Parse(string text)
        {
            if (string.IsNullOrEmpty(text)) return Null;
            int i = 0;
            // Пропускаем BOM
            if (text.Length > 0 && text[0] == '﻿') i = 1;
            try { return ParseValue(text, ref i); }
            catch { return Null; }
        }

        public static Json ParseFile(string path)
        {
            if (!File.Exists(path)) return Null;
            try { return Parse(File.ReadAllText(path, Encoding.UTF8)); }
            catch { return Null; }
        }

        private static void SkipWs(string s, ref int i)
        {
            while (i < s.Length)
            {
                char c = s[i];
                if (c == ' ' || c == '\t' || c == '\r' || c == '\n') i++;
                else break;
            }
        }

        private static Json ParseValue(string s, ref int i)
        {
            SkipWs(s, ref i);
            if (i >= s.Length) return Null;
            char c = s[i];
            if (c == '{') return ParseObject(s, ref i);
            if (c == '[') return ParseArray(s, ref i);
            if (c == '"') return Of(ParseString(s, ref i));
            if (c == 't') { i += 4; return Of(true); }
            if (c == 'f') { i += 5; return Of(false); }
            if (c == 'n') { i += 4; return Null; }
            return ParseNumber(s, ref i);
        }

        private static Json ParseObject(string s, ref int i)
        {
            var o = NewObject();
            i++; // {
            while (true)
            {
                SkipWs(s, ref i);
                if (i >= s.Length) break;
                if (s[i] == '}') { i++; break; }
                if (s[i] == ',') { i++; continue; }
                if (s[i] != '"') { i++; continue; }
                string key = ParseString(s, ref i);
                SkipWs(s, ref i);
                if (i < s.Length && s[i] == ':') i++;
                o.Obj[key] = ParseValue(s, ref i);
            }
            return o;
        }

        private static Json ParseArray(string s, ref int i)
        {
            var a = NewArray();
            i++; // [
            while (true)
            {
                SkipWs(s, ref i);
                if (i >= s.Length) break;
                if (s[i] == ']') { i++; break; }
                if (s[i] == ',') { i++; continue; }
                a.Arr.Add(ParseValue(s, ref i));
            }
            return a;
        }

        private static string ParseString(string s, ref int i)
        {
            var sb = new StringBuilder();
            i++; // "
            while (i < s.Length)
            {
                char c = s[i++];
                if (c == '"') break;
                if (c == '\\' && i < s.Length)
                {
                    char e = s[i++];
                    switch (e)
                    {
                        case 'n': sb.Append('\n'); break;
                        case 't': sb.Append('\t'); break;
                        case 'r': sb.Append('\r'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case '/': sb.Append('/'); break;
                        case '\\': sb.Append('\\'); break;
                        case '"': sb.Append('"'); break;
                        case 'u':
                            if (i + 4 <= s.Length)
                            {
                                int code;
                                if (int.TryParse(s.Substring(i, 4), NumberStyles.HexNumber,
                                                 CultureInfo.InvariantCulture, out code))
                                    sb.Append((char)code);
                                i += 4;
                            }
                            break;
                        default: sb.Append(e); break;
                    }
                }
                else sb.Append(c);
            }
            return sb.ToString();
        }

        private static Json ParseNumber(string s, ref int i)
        {
            int start = i;
            while (i < s.Length)
            {
                char c = s[i];
                if (char.IsDigit(c) || c == '-' || c == '+' || c == '.' || c == 'e' || c == 'E') i++;
                else break;
            }
            double d;
            double.TryParse(s.Substring(start, i - start), NumberStyles.Any, CultureInfo.InvariantCulture, out d);
            return Of(d);
        }

        // ---------- Сериализация ----------

        public override string ToString() { return ToJson(false); }

        public string ToJson(bool indented)
        {
            var sb = new StringBuilder();
            Write(sb, this, indented, 0);
            return sb.ToString();
        }

        public void SaveTo(string path, bool indented = true)
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(path, ToJson(indented), new UTF8Encoding(false));
        }

        private static void Pad(StringBuilder sb, int level)
        {
            sb.Append('\n');
            sb.Append(' ', level * 2);
        }

        private static void Write(StringBuilder sb, Json v, bool ind, int level)
        {
            switch (v.Type)
            {
                case JsonType.Null: sb.Append("null"); break;
                case JsonType.Bool: sb.Append(v.Bool ? "true" : "false"); break;
                case JsonType.Number:
                    if (Math.Abs(v.Num - Math.Round(v.Num)) < 1e-9 && Math.Abs(v.Num) < 1e15)
                        sb.Append(((long)Math.Round(v.Num)).ToString(CultureInfo.InvariantCulture));
                    else
                        sb.Append(v.Num.ToString("R", CultureInfo.InvariantCulture));
                    break;
                case JsonType.String: WriteString(sb, v.Str); break;
                case JsonType.Array:
                    if (v.Arr.Count == 0) { sb.Append("[]"); break; }
                    sb.Append('[');
                    for (int i = 0; i < v.Arr.Count; i++)
                    {
                        if (i > 0) sb.Append(',');
                        if (ind) Pad(sb, level + 1);
                        Write(sb, v.Arr[i], ind, level + 1);
                    }
                    if (ind) Pad(sb, level);
                    sb.Append(']');
                    break;
                case JsonType.Object:
                    if (v.Obj.Count == 0) { sb.Append("{}"); break; }
                    sb.Append('{');
                    bool first = true;
                    foreach (var kv in v.Obj)
                    {
                        if (!first) sb.Append(',');
                        first = false;
                        if (ind) Pad(sb, level + 1);
                        WriteString(sb, kv.Key);
                        sb.Append(':');
                        if (ind) sb.Append(' ');
                        Write(sb, kv.Value, ind, level + 1);
                    }
                    if (ind) Pad(sb, level);
                    sb.Append('}');
                    break;
            }
        }

        private static void WriteString(StringBuilder sb, string s)
        {
            sb.Append('"');
            if (s != null)
            {
                foreach (char c in s)
                {
                    switch (c)
                    {
                        case '"': sb.Append("\\\""); break;
                        case '\\': sb.Append("\\\\"); break;
                        case '\n': sb.Append("\\n"); break;
                        case '\r': sb.Append("\\r"); break;
                        case '\t': sb.Append("\\t"); break;
                        default:
                            if (c < ' ') sb.Append("\\u").Append(((int)c).ToString("x4"));
                            else sb.Append(c);
                            break;
                    }
                }
            }
            sb.Append('"');
        }
    }
}
