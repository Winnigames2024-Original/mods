using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace NareLauncher.Core
{
    /// <summary>
    /// Маленький читатель и писатель NBT — ровно столько, сколько нужно,
    /// чтобы разобрать servers.dat и положить его обратно без потерь.
    /// Формат big-endian, строки в UTF-8.
    /// </summary>
    public class NbtTag
    {
        public const byte End = 0, Byte = 1, Short = 2, Int = 3, Long = 4,
                          Float = 5, Double = 6, ByteArray = 7, String = 8,
                          List = 9, Compound = 10, IntArray = 11, LongArray = 12;

        public byte Type = End;
        public string Name = "";

        public long Number;              // byte / short / int / long
        public double Real;              // float / double
        public string Text = "";         // string
        public byte[] Bytes;             // byte[]
        public int[] Ints;               // int[]
        public long[] Longs;             // long[]
        public byte ListType = End;      // тип элементов списка
        public List<NbtTag> Children;    // list / compound

        public static NbtTag NewCompound(string name)
        {
            return new NbtTag { Type = Compound, Name = name, Children = new List<NbtTag>() };
        }

        public static NbtTag NewList(string name, byte itemType)
        {
            return new NbtTag { Type = List, Name = name, ListType = itemType, Children = new List<NbtTag>() };
        }

        public static NbtTag OfString(string name, string value)
        {
            return new NbtTag { Type = String, Name = name, Text = value ?? "" };
        }

        public static NbtTag OfByte(string name, int value)
        {
            return new NbtTag { Type = Byte, Name = name, Number = value };
        }

        /// <summary>Дочерний тег по имени — или null.</summary>
        public NbtTag Child(string name)
        {
            if (Children == null) return null;
            foreach (var c in Children)
                if (string.Equals(c.Name, name, StringComparison.Ordinal)) return c;
            return null;
        }

        public string ChildText(string name, string fallback = "")
        {
            var c = Child(name);
            return c == null ? fallback : (c.Text ?? fallback);
        }

        public void Put(NbtTag tag)
        {
            if (Children == null) Children = new List<NbtTag>();
            for (int i = 0; i < Children.Count; i++)
                if (Children[i].Name == tag.Name) { Children[i] = tag; return; }
            Children.Add(tag);
        }

        // ------------------------------------------------------------- чтение

        /// <summary>Читает файл. Понимает и сжатый gzip, и обычный NBT.</summary>
        public static NbtTag ReadFile(string path)
        {
            byte[] raw = File.ReadAllBytes(path);
            if (raw.Length >= 2 && raw[0] == 0x1F && raw[1] == 0x8B)
            {
                using (var source = new MemoryStream(raw))
                using (var gz = new GZipStream(source, CompressionMode.Decompress))
                using (var plain = new MemoryStream())
                {
                    gz.CopyTo(plain);
                    raw = plain.ToArray();
                }
            }

            using (var stream = new MemoryStream(raw))
                return ReadNamed(stream);
        }

        private static NbtTag ReadNamed(Stream s)
        {
            byte type = (byte)s.ReadByte();
            if (type == End) return new NbtTag();

            var tag = new NbtTag { Type = type, Name = ReadString(s) };
            ReadPayload(s, tag);
            return tag;
        }

        private static void ReadPayload(Stream s, NbtTag tag)
        {
            switch (tag.Type)
            {
                case Byte: tag.Number = (sbyte)s.ReadByte(); break;
                case Short: tag.Number = ReadInt(s, 2); break;
                case Int: tag.Number = (int)ReadInt(s, 4); break;
                case Long: tag.Number = ReadInt(s, 8); break;
                case Float: tag.Real = BitConverter.ToSingle(Flip(Take(s, 4)), 0); break;
                case Double: tag.Real = BitConverter.ToDouble(Flip(Take(s, 8)), 0); break;
                case String: tag.Text = ReadString(s); break;

                case ByteArray:
                    tag.Bytes = Take(s, (int)ReadInt(s, 4));
                    break;

                case IntArray:
                    {
                        int n = (int)ReadInt(s, 4);
                        tag.Ints = new int[n];
                        for (int i = 0; i < n; i++) tag.Ints[i] = (int)ReadInt(s, 4);
                        break;
                    }

                case LongArray:
                    {
                        int n = (int)ReadInt(s, 4);
                        tag.Longs = new long[n];
                        for (int i = 0; i < n; i++) tag.Longs[i] = ReadInt(s, 8);
                        break;
                    }

                case List:
                    {
                        tag.ListType = (byte)s.ReadByte();
                        int n = (int)ReadInt(s, 4);
                        tag.Children = new List<NbtTag>();
                        for (int i = 0; i < n; i++)
                        {
                            var item = new NbtTag { Type = tag.ListType, Name = "" };
                            if (tag.ListType != End) ReadPayload(s, item);
                            tag.Children.Add(item);
                        }
                        break;
                    }

                case Compound:
                    {
                        tag.Children = new List<NbtTag>();
                        while (true)
                        {
                            int peek = s.ReadByte();
                            if (peek <= 0) break;            // End или конец потока

                            var child = new NbtTag { Type = (byte)peek, Name = ReadString(s) };
                            ReadPayload(s, child);
                            tag.Children.Add(child);
                        }
                        break;
                    }
            }
        }

        private static byte[] Take(Stream s, int count)
        {
            var buffer = new byte[Math.Max(0, count)];
            int read = 0;
            while (read < buffer.Length)
            {
                int got = s.Read(buffer, read, buffer.Length - read);
                if (got <= 0) break;
                read += got;
            }
            return buffer;
        }

        private static byte[] Flip(byte[] data)
        {
            if (BitConverter.IsLittleEndian) Array.Reverse(data);
            return data;
        }

        private static long ReadInt(Stream s, int width)
        {
            long value = 0;
            for (int i = 0; i < width; i++) value = (value << 8) | (uint)(s.ReadByte() & 0xFF);

            if (width == 2) return (short)value;
            if (width == 4) return (int)value;
            return value;
        }

        private static string ReadString(Stream s)
        {
            int length = (int)ReadIntUnsigned(s, 2);
            return Encoding.UTF8.GetString(Take(s, length));
        }

        private static long ReadIntUnsigned(Stream s, int width)
        {
            long value = 0;
            for (int i = 0; i < width; i++) value = (value << 8) | (uint)(s.ReadByte() & 0xFF);
            return value;
        }

        // ------------------------------------------------------------- запись

        public void WriteFile(string path)
        {
            using (var stream = new MemoryStream())
            {
                WriteNamed(stream, this);
                File.WriteAllBytes(path, stream.ToArray());
            }
        }

        private static void WriteNamed(Stream s, NbtTag tag)
        {
            s.WriteByte(tag.Type);
            if (tag.Type == End) return;

            WriteString(s, tag.Name);
            WritePayload(s, tag);
        }

        private static void WritePayload(Stream s, NbtTag tag)
        {
            switch (tag.Type)
            {
                case Byte: s.WriteByte((byte)(sbyte)tag.Number); break;
                case Short: WriteInt(s, tag.Number, 2); break;
                case Int: WriteInt(s, tag.Number, 4); break;
                case Long: WriteInt(s, tag.Number, 8); break;
                case Float: WriteRaw(s, Flip(BitConverter.GetBytes((float)tag.Real))); break;
                case Double: WriteRaw(s, Flip(BitConverter.GetBytes(tag.Real))); break;
                case String: WriteString(s, tag.Text); break;

                case ByteArray:
                    {
                        var data = tag.Bytes ?? new byte[0];
                        WriteInt(s, data.Length, 4);
                        WriteRaw(s, data);
                        break;
                    }

                case IntArray:
                    {
                        var data = tag.Ints ?? new int[0];
                        WriteInt(s, data.Length, 4);
                        foreach (int v in data) WriteInt(s, v, 4);
                        break;
                    }

                case LongArray:
                    {
                        var data = tag.Longs ?? new long[0];
                        WriteInt(s, data.Length, 4);
                        foreach (long v in data) WriteInt(s, v, 8);
                        break;
                    }

                case List:
                    {
                        var items = tag.Children ?? new List<NbtTag>();
                        byte type = tag.ListType;
                        if (type == End && items.Count > 0) type = items[0].Type;

                        s.WriteByte(type);
                        WriteInt(s, items.Count, 4);
                        foreach (var item in items) WritePayload(s, item);
                        break;
                    }

                case Compound:
                    {
                        var items = tag.Children ?? new List<NbtTag>();
                        foreach (var item in items) WriteNamed(s, item);
                        s.WriteByte(End);
                        break;
                    }
            }
        }

        private static void WriteRaw(Stream s, byte[] data) { s.Write(data, 0, data.Length); }

        private static void WriteInt(Stream s, long value, int width)
        {
            for (int i = width - 1; i >= 0; i--) s.WriteByte((byte)((value >> (i * 8)) & 0xFF));
        }

        private static void WriteString(Stream s, string text)
        {
            var data = Encoding.UTF8.GetBytes(text ?? "");
            WriteInt(s, data.Length, 2);
            WriteRaw(s, data);
        }
    }
}
