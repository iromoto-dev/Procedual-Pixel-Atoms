// Procedural Pixel Creatures - a small, dependency free JSON document model for presets and sprite
// sheet metadata.
//
// The core must compile and behave identically on .NET 8 (Godot, console harness) and on Unity's Mono
// and IL2CPP runtimes, where System.Text.Json is not available. This mirrors the subset of
// System.Text.Json.Nodes the framework uses (objects, arrays, values, typed TryGetValue, indented
// output, comments and trailing commas when parsing) and writes the same text: two space indentation,
// ASCII only output with \uXXXX escapes, shortest round-trip numbers.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace PixelCreatures.Core.Json
{
    public sealed class JsonException : Exception
    {
        public JsonException(string message) : base(message) { }
    }

    public abstract class JsonNode
    {
        public JsonNode? this[string key]
        {
            get => ((JsonObject)this).Get(key);
            set => ((JsonObject)this).Set(key, value);
        }

        public static implicit operator JsonNode(string value) => JsonValue.Create(value);
        public static implicit operator JsonNode(int value) => JsonValue.Create(value);
        public static implicit operator JsonNode(long value) => JsonValue.Create(value);
        public static implicit operator JsonNode(double value) => JsonValue.Create(value);
        public static implicit operator JsonNode(bool value) => JsonValue.Create(value);

        public JsonObject AsObject() => this as JsonObject ?? throw new InvalidOperationException("The node is not a JSON object.");
        public JsonArray AsArray() => this as JsonArray ?? throw new InvalidOperationException("The node is not a JSON array.");
        public JsonValue AsValue() => this as JsonValue ?? throw new InvalidOperationException("The node is not a JSON value.");

        /// <summary>Deep copy (values are immutable and shared).</summary>
        public JsonNode DeepClone()
        {
            switch (this)
            {
                case JsonObject o:
                {
                    var c = new JsonObject();
                    foreach (var kv in o) c.Set(kv.Key, kv.Value?.DeepClone());
                    return c;
                }
                case JsonArray a:
                {
                    var c = new JsonArray();
                    foreach (var item in a) c.Add(item?.DeepClone());
                    return c;
                }
                default:
                    return this;
            }
        }

        /// <summary>Indented (default) or compact JSON text.</summary>
        public string ToJsonString(bool indented = true)
        {
            var sb = new StringBuilder();
            Write(sb, this, indented, 0);
            return sb.ToString();
        }

        public override string ToString() => ToJsonString();

        /// <summary>Parses JSON text. Comments (// and /* */) and trailing commas are accepted. Throws JsonException.</summary>
        public static JsonNode? Parse(string json, int maxDepth = 64) => new JsonReader(json, maxDepth).ReadDocument();

        private static void Write(StringBuilder sb, JsonNode? node, bool indented, int depth)
        {
            switch (node)
            {
                case null:
                    sb.Append("null");
                    break;
                case JsonValue v:
                    v.WriteTo(sb);
                    break;
                case JsonObject o:
                {
                    if (o.Count == 0) { sb.Append("{}"); break; }
                    sb.Append('{');
                    int i = 0;
                    foreach (var kv in o)
                    {
                        if (i++ > 0) sb.Append(',');
                        NewLine(sb, indented, depth + 1);
                        JsonValue.WriteString(sb, kv.Key);
                        sb.Append(indented ? ": " : ":");
                        Write(sb, kv.Value, indented, depth + 1);
                    }
                    NewLine(sb, indented, depth);
                    sb.Append('}');
                    break;
                }
                case JsonArray a:
                {
                    if (a.Count == 0) { sb.Append("[]"); break; }
                    sb.Append('[');
                    for (int i = 0; i < a.Count; i++)
                    {
                        if (i > 0) sb.Append(',');
                        NewLine(sb, indented, depth + 1);
                        Write(sb, a[i], indented, depth + 1);
                    }
                    NewLine(sb, indented, depth);
                    sb.Append(']');
                    break;
                }
            }
        }

        private static void NewLine(StringBuilder sb, bool indented, int depth)
        {
            if (!indented) return;
            sb.Append('\n');
            sb.Append(' ', depth * 2);
        }
    }

    /// <summary>Ordered JSON object (insertion order is kept when writing).</summary>
    public sealed class JsonObject : JsonNode, IEnumerable<KeyValuePair<string, JsonNode?>>
    {
        private readonly List<KeyValuePair<string, JsonNode?>> _items = new List<KeyValuePair<string, JsonNode?>>();
        private readonly Dictionary<string, int> _index = new Dictionary<string, int>(StringComparer.Ordinal);

        public int Count => _items.Count;

        public JsonNode? Get(string key) => _index.TryGetValue(key, out int i) ? _items[i].Value : null;

        public void Set(string key, JsonNode? value)
        {
            if (_index.TryGetValue(key, out int i)) _items[i] = new KeyValuePair<string, JsonNode?>(key, value);
            else
            {
                _index[key] = _items.Count;
                _items.Add(new KeyValuePair<string, JsonNode?>(key, value));
            }
        }

        public bool ContainsKey(string key) => _index.ContainsKey(key);

        public bool Remove(string key)
        {
            if (!_index.TryGetValue(key, out int i)) return false;
            _items.RemoveAt(i);
            _index.Clear();
            for (int k = 0; k < _items.Count; k++) _index[_items[k].Key] = k;
            return true;
        }

        public IEnumerator<KeyValuePair<string, JsonNode?>> GetEnumerator() => _items.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    public sealed class JsonArray : JsonNode, IEnumerable<JsonNode?>
    {
        private readonly List<JsonNode?> _items = new List<JsonNode?>();

        public JsonArray() { }
        public JsonArray(params JsonNode?[] items) { _items.AddRange(items); }

        public int Count => _items.Count;
        public JsonNode? this[int index] { get => _items[index]; set => _items[index] = value; }
        public void Add(JsonNode? item) => _items.Add(item);

        public IEnumerator<JsonNode?> GetEnumerator() => _items.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    public sealed class JsonValue : JsonNode
    {
        private enum Kind : byte { String, Number, True, False }

        private readonly Kind _kind;
        private readonly string _text;

        private JsonValue(Kind kind, string text) { _kind = kind; _text = text; }

        public static JsonValue Create(string value) => new JsonValue(Kind.String, value ?? string.Empty);
        public static JsonValue Create(bool value) => new JsonValue(value ? Kind.True : Kind.False, value ? "true" : "false");
        public static JsonValue Create(int value) => new JsonValue(Kind.Number, value.ToString(CultureInfo.InvariantCulture));
        public static JsonValue Create(long value) => new JsonValue(Kind.Number, value.ToString(CultureInfo.InvariantCulture));
        public static JsonValue Create(ulong value) => new JsonValue(Kind.Number, value.ToString(CultureInfo.InvariantCulture));

        public static JsonValue Create(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) throw new JsonException("NaN and infinity cannot be written as JSON numbers.");
            return new JsonValue(Kind.Number, FormatDouble(value));
        }

        internal static JsonValue FromNumberText(string text) => new JsonValue(Kind.Number, text);

        /// <summary>Shortest text that parses back to exactly the same double (identical on every runtime).</summary>
        public static string FormatDouble(double value)
        {
            if (value == Math.Floor(value) && Math.Abs(value) < 1e15) return ((long)value).ToString(CultureInfo.InvariantCulture);
            for (int digits = 15; digits <= 17; digits++)
            {
                string s = value.ToString("G" + digits.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
                if (double.Parse(s, NumberStyles.Float, CultureInfo.InvariantCulture) == value) return s;
            }
            return value.ToString("R", CultureInfo.InvariantCulture);
        }

        /// <summary>Typed access like System.Text.Json: strings, booleans and numbers (int, long, ulong, double, float).</summary>
        public bool TryGetValue<T>(out T value)
        {
            value = default!;
            object? result = null;
            var t = typeof(T);
            if (t == typeof(string))
            {
                if (_kind == Kind.String) result = _text;
            }
            else if (t == typeof(bool))
            {
                if (_kind == Kind.True) result = true;
                else if (_kind == Kind.False) result = false;
            }
            else if (_kind == Kind.Number)
            {
                if (t == typeof(double))
                {
                    if (double.TryParse(_text, NumberStyles.Float, CultureInfo.InvariantCulture, out double d)) result = d;
                }
                else if (t == typeof(float))
                {
                    if (float.TryParse(_text, NumberStyles.Float, CultureInfo.InvariantCulture, out float f)) result = f;
                }
                else if (t == typeof(int))
                {
                    if (int.TryParse(_text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int i)) result = i;
                }
                else if (t == typeof(long))
                {
                    if (long.TryParse(_text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long l)) result = l;
                }
                else if (t == typeof(ulong))
                {
                    if (ulong.TryParse(_text, NumberStyles.None, CultureInfo.InvariantCulture, out ulong u)) result = u;
                }
            }
            if (result == null) return false;
            value = (T)result;
            return true;
        }

        public T GetValue<T>()
        {
            if (TryGetValue(out T v)) return v;
            throw new InvalidOperationException($"The JSON value cannot be read as {typeof(T).Name}.");
        }

        internal void WriteTo(StringBuilder sb)
        {
            if (_kind == Kind.String) WriteString(sb, _text);
            else sb.Append(_text);
        }

        internal static void WriteString(StringBuilder sb, string s)
        {
            sb.Append('"');
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    default:
                        // like the default System.Text.Json encoder: ASCII output, HTML sensitive characters escaped
                        if (c < 0x20 || c > 0x7E || c == '<' || c == '>' || c == '&' || c == '\'' || c == '+' || c == '`')
                            sb.Append("\\u").Append(((int)c).ToString("X4", CultureInfo.InvariantCulture));
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
        }
    }

    /// <summary>Recursive descent parser (RFC 8259 plus comments and trailing commas).</summary>
    internal sealed class JsonReader
    {
        private readonly string _s;
        private readonly int _maxDepth;
        private int _p;

        public JsonReader(string s, int maxDepth) { _s = s ?? string.Empty; _maxDepth = maxDepth; }

        public JsonNode? ReadDocument()
        {
            SkipWhitespace();
            if (_p >= _s.Length) throw Error("The document is empty.");
            var node = ReadValue(0);
            SkipWhitespace();
            if (_p < _s.Length) throw Error("Unexpected text after the end of the document.");
            return node;
        }

        private JsonException Error(string message)
        {
            int line = 1, col = 1;
            for (int i = 0; i < Math.Min(_p, _s.Length); i++)
            {
                if (_s[i] == '\n') { line++; col = 1; }
                else col++;
            }
            return new JsonException($"{message} (line {line}, column {col})");
        }

        private void SkipWhitespace()
        {
            while (_p < _s.Length)
            {
                char c = _s[_p];
                if (c == ' ' || c == '\t' || c == '\n' || c == '\r' || c == '﻿') { _p++; continue; }
                if (c == '/' && _p + 1 < _s.Length && _s[_p + 1] == '/')
                {
                    while (_p < _s.Length && _s[_p] != '\n') _p++;
                    continue;
                }
                if (c == '/' && _p + 1 < _s.Length && _s[_p + 1] == '*')
                {
                    int end = _s.IndexOf("*/", _p + 2, StringComparison.Ordinal);
                    if (end < 0) throw Error("Unterminated comment.");
                    _p = end + 2;
                    continue;
                }
                break;
            }
        }

        private JsonNode? ReadValue(int depth)
        {
            if (depth > _maxDepth) throw Error("The document is nested too deeply.");
            SkipWhitespace();
            if (_p >= _s.Length) throw Error("Unexpected end of the document.");
            char c = _s[_p];
            switch (c)
            {
                case '{': return ReadObject(depth);
                case '[': return ReadArray(depth);
                case '"': return JsonValue.Create(ReadString());
                case 't': Expect("true"); return JsonValue.Create(true);
                case 'f': Expect("false"); return JsonValue.Create(false);
                case 'n': Expect("null"); return null;
                default:
                    if (c == '-' || (c >= '0' && c <= '9')) return ReadNumber();
                    throw Error($"Unexpected character '{c}'.");
            }
        }

        private void Expect(string word)
        {
            if (string.CompareOrdinal(_s, _p, word, 0, word.Length) != 0) throw Error($"Expected '{word}'.");
            _p += word.Length;
        }

        private JsonObject ReadObject(int depth)
        {
            var o = new JsonObject();
            _p++;
            while (true)
            {
                SkipWhitespace();
                if (_p >= _s.Length) throw Error("Unterminated object.");
                if (_s[_p] == '}') { _p++; return o; }
                if (_s[_p] != '"') throw Error("Expected a property name.");
                string key = ReadString();
                SkipWhitespace();
                if (_p >= _s.Length || _s[_p] != ':') throw Error("Expected ':' after a property name.");
                _p++;
                o.Set(key, ReadValue(depth + 1));
                SkipWhitespace();
                if (_p < _s.Length && _s[_p] == ',') { _p++; continue; }
                if (_p < _s.Length && _s[_p] == '}') { _p++; return o; }
                throw Error("Expected ',' or '}'.");
            }
        }

        private JsonArray ReadArray(int depth)
        {
            var a = new JsonArray();
            _p++;
            while (true)
            {
                SkipWhitespace();
                if (_p >= _s.Length) throw Error("Unterminated array.");
                if (_s[_p] == ']') { _p++; return a; }
                a.Add(ReadValue(depth + 1));
                SkipWhitespace();
                if (_p < _s.Length && _s[_p] == ',') { _p++; continue; }
                if (_p < _s.Length && _s[_p] == ']') { _p++; return a; }
                throw Error("Expected ',' or ']'.");
            }
        }

        private string ReadString()
        {
            _p++;
            var sb = new StringBuilder();
            while (true)
            {
                if (_p >= _s.Length) throw Error("Unterminated string.");
                char c = _s[_p++];
                if (c == '"') return sb.ToString();
                if (c < 0x20) throw Error("Control character in a string.");
                if (c != '\\') { sb.Append(c); continue; }
                if (_p >= _s.Length) throw Error("Unterminated escape sequence.");
                char e = _s[_p++];
                switch (e)
                {
                    case '"': sb.Append('"'); break;
                    case '\\': sb.Append('\\'); break;
                    case '/': sb.Append('/'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'n': sb.Append('\n'); break;
                    case 'r': sb.Append('\r'); break;
                    case 't': sb.Append('\t'); break;
                    case 'u':
                    {
                        if (_p + 4 > _s.Length || !int.TryParse(_s.Substring(_p, 4), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out int code))
                            throw Error("Invalid \\u escape.");
                        sb.Append((char)code);
                        _p += 4;
                        break;
                    }
                    default: throw Error($"Invalid escape '\\{e}'.");
                }
            }
        }

        private static bool IsDigit(char c) => c >= '0' && c <= '9';

        private JsonValue ReadNumber()
        {
            int start = _p;
            if (_s[_p] == '-') _p++;
            if (_p >= _s.Length || !IsDigit(_s[_p])) throw Error("Invalid number.");
            if (_s[_p] == '0') _p++;
            else while (_p < _s.Length && IsDigit(_s[_p])) _p++;
            if (_p < _s.Length && _s[_p] == '.')
            {
                _p++;
                if (_p >= _s.Length || !IsDigit(_s[_p])) throw Error("Invalid number.");
                while (_p < _s.Length && IsDigit(_s[_p])) _p++;
            }
            if (_p < _s.Length && (_s[_p] == 'e' || _s[_p] == 'E'))
            {
                _p++;
                if (_p < _s.Length && (_s[_p] == '+' || _s[_p] == '-')) _p++;
                if (_p >= _s.Length || !IsDigit(_s[_p])) throw Error("Invalid number.");
                while (_p < _s.Length && IsDigit(_s[_p])) _p++;
            }
            return JsonValue.FromNumberText(_s.Substring(start, _p - start));
        }
    }
}
