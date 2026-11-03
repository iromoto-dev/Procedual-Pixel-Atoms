// Procedural Pixel Creatures - minimal, dependency free PNG encoder (RGBA8, zlib via .NET).
// Used by exporters and tools so that output does not depend on the engine. Only .NET Standard 2.0 APIs
// are used (Godot, Unity Mono and IL2CPP): the zlib stream is assembled from DeflateStream plus the zlib
// header and Adler-32 trailer, with uncompressed deflate blocks where a platform has no deflate.

#nullable enable

using System;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace PixelCreatures.Core.Export
{
    public static class PngEncoder
    {
        private static readonly uint[] CrcTable = BuildCrcTable();

        private static uint[] BuildCrcTable()
        {
            var t = new uint[256];
            for (uint n = 0; n < 256; n++)
            {
                uint c = n;
                for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
                t[n] = c;
            }
            return t;
        }

        private static uint Crc(byte[] data, int offset, int count, uint crc = 0xFFFFFFFFu)
        {
            for (int i = 0; i < count; i++) crc = CrcTable[(crc ^ data[offset + i]) & 0xFF] ^ (crc >> 8);
            return crc;
        }

        /// <summary>Encodes RGBA8 pixels (row major, width*height*4 bytes) as PNG. Optional tEXt chunks.</summary>
        public static byte[] Encode(byte[] rgba, int width, int height, params (string key, string value)[] text)
        {
            if (rgba.Length < width * height * 4) throw new ArgumentException("Pixel buffer too small.");
            using var ms = new MemoryStream();
            ms.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, 0, 8);
            var ihdr = new byte[13];
            WriteBE(ihdr, 0, (uint)width);
            WriteBE(ihdr, 4, (uint)height);
            ihdr[8] = 8;  // bit depth
            ihdr[9] = 6;  // RGBA
            ihdr[10] = 0; ihdr[11] = 0; ihdr[12] = 0;
            WriteChunk(ms, "IHDR", ihdr);
            foreach (var (key, value) in text)
            {
                byte[] k = Latin1(key);
                byte[] v = Encoding.UTF8.GetBytes(value);
                var payload = new byte[k.Length + 1 + v.Length];
                Array.Copy(k, payload, k.Length);
                Array.Copy(v, 0, payload, k.Length + 1, v.Length);
                WriteChunk(ms, "tEXt", payload);
            }
            // filtered scanlines (filter type 0 keeps the encoder simple and deterministic)
            var raw = new byte[(width * 4 + 1) * height];
            for (int y = 0; y < height; y++)
            {
                raw[y * (width * 4 + 1)] = 0;
                Array.Copy(rgba, y * width * 4, raw, y * (width * 4 + 1) + 1, width * 4);
            }
            WriteChunk(ms, "IDAT", ZLib(raw));
            WriteChunk(ms, "IEND", Array.Empty<byte>());
            return ms.ToArray();
        }

        /// <summary>PNG keywords are Latin-1; characters outside it become '?'.</summary>
        private static byte[] Latin1(string s)
        {
            var b = new byte[s.Length];
            for (int i = 0; i < s.Length; i++) b[i] = s[i] <= 0xFF ? (byte)s[i] : (byte)'?';
            return b;
        }

        /// <summary>zlib stream (RFC 1950): header, deflate data, Adler-32 of the uncompressed data.</summary>
        public static byte[] ZLib(byte[] data)
        {
            byte[] deflated;
            byte level;
            try
            {
                using var cms = new MemoryStream();
                using (var z = new DeflateStream(cms, CompressionLevel.Optimal, true)) z.Write(data, 0, data.Length);
                deflated = cms.ToArray();
                level = 0xDA;
            }
            catch (Exception e) when (e is PlatformNotSupportedException || e is NotSupportedException || e is DllNotFoundException || e is EntryPointNotFoundException || e is TypeInitializationException)
            {
                deflated = StoredBlocks(data);
                level = 0x01;
            }
            var o = new byte[2 + deflated.Length + 4];
            o[0] = 0x78;
            o[1] = level;
            Array.Copy(deflated, 0, o, 2, deflated.Length);
            WriteBE(o, o.Length - 4, Adler32(data));
            return o;
        }

        /// <summary>Uncompressed deflate blocks (RFC 1951, BTYPE 00): valid everywhere, no compression.</summary>
        private static byte[] StoredBlocks(byte[] data)
        {
            using var ms = new MemoryStream();
            int offset = 0;
            do
            {
                int n = Math.Min(65535, data.Length - offset);
                bool last = offset + n >= data.Length;
                ms.WriteByte(last ? (byte)1 : (byte)0);
                ms.WriteByte((byte)(n & 0xFF)); ms.WriteByte((byte)(n >> 8));
                ms.WriteByte((byte)(~n & 0xFF)); ms.WriteByte((byte)((~n >> 8) & 0xFF));
                ms.Write(data, offset, n);
                offset += n;
            }
            while (offset < data.Length);
            return ms.ToArray();
        }

        private static uint Adler32(byte[] data)
        {
            const uint Mod = 65521;
            uint a = 1, b = 0;
            int i = 0;
            while (i < data.Length)
            {
                int n = Math.Min(5552, data.Length - i);
                for (int k = 0; k < n; k++) { a += data[i++]; b += a; }
                a %= Mod; b %= Mod;
            }
            return (b << 16) | a;
        }

        private static void WriteChunk(Stream s, string type, byte[] data)
        {
            var len = new byte[4];
            WriteBE(len, 0, (uint)data.Length);
            s.Write(len, 0, 4);
            byte[] t = Encoding.ASCII.GetBytes(type);
            s.Write(t, 0, 4);
            s.Write(data, 0, data.Length);
            uint crc = Crc(t, 0, 4);
            crc = Crc(data, 0, data.Length, crc) ^ 0xFFFFFFFFu;
            var c = new byte[4];
            WriteBE(c, 0, crc);
            s.Write(c, 0, 4);
        }

        private static void WriteBE(byte[] b, int o, uint v)
        {
            b[o] = (byte)(v >> 24); b[o + 1] = (byte)(v >> 16); b[o + 2] = (byte)(v >> 8); b[o + 3] = (byte)v;
        }

        /// <summary>Nearest neighbour integer upscale of an RGBA buffer.</summary>
        public static byte[] Upscale(byte[] rgba, int width, int height, int factor)
        {
            if (factor <= 1) return (byte[])rgba.Clone();
            int w2 = width * factor, h2 = height * factor;
            var o = new byte[w2 * h2 * 4];
            for (int y = 0; y < h2; y++)
            {
                int sy = y / factor;
                for (int x = 0; x < w2; x++)
                {
                    int sx = x / factor;
                    int si = (sy * width + sx) * 4, di = (y * w2 + x) * 4;
                    o[di] = rgba[si]; o[di + 1] = rgba[si + 1]; o[di + 2] = rgba[si + 2]; o[di + 3] = rgba[si + 3];
                }
            }
            return o;
        }
    }
}
