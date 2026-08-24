using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ParcelSort
{
    public enum JsonKind
    {
        Null = 0,
        Bool = 1,
        Number = 2,
        String = 3,
        Array = 4,
        Object = 5
    }

    /// <summary>
    /// Minimal read-only JSON value tree. Unity's JsonUtility cannot express nested
    /// arrays or dictionaries, which the level schema needs for belt path points.
    /// </summary>
    public class JsonValue
    {
        static readonly JsonValue MissingValue = new JsonValue { Kind = JsonKind.Null };

        public JsonKind Kind { get; private set; }
        public bool BoolValue { get; private set; }
        public double NumberValue { get; private set; }
        public string StringValue { get; private set; }
        public List<JsonValue> Items { get; private set; }
        public Dictionary<string, JsonValue> Members { get; private set; }

        public static JsonValue Missing => MissingValue;

        public bool Exists => Kind != JsonKind.Null;

        public int Count
        {
            get
            {
                if (Kind == JsonKind.Array)
                {
                    return Items.Count;
                }

                if (Kind == JsonKind.Object)
                {
                    return Members.Count;
                }

                return 0;
            }
        }

        public JsonValue this[int index]
        {
            get
            {
                if (Kind != JsonKind.Array || index < 0 || index >= Items.Count)
                {
                    return MissingValue;
                }

                return Items[index];
            }
        }

        public JsonValue this[string key]
        {
            get
            {
                if (Kind != JsonKind.Object || key == null)
                {
                    return MissingValue;
                }

                return Members.TryGetValue(key, out JsonValue value) ? value : MissingValue;
            }
        }

        public bool Has(string key)
        {
            return Kind == JsonKind.Object && key != null && Members.ContainsKey(key);
        }

        public string AsString(string fallback = "")
        {
            switch (Kind)
            {
                case JsonKind.String:
                    return StringValue;
                case JsonKind.Number:
                    return NumberValue.ToString(CultureInfo.InvariantCulture);
                case JsonKind.Bool:
                    return BoolValue ? "true" : "false";
                default:
                    return fallback;
            }
        }

        public float AsFloat(float fallback = 0f)
        {
            if (Kind == JsonKind.Number)
            {
                return (float)NumberValue;
            }

            if (Kind == JsonKind.String &&
                float.TryParse(StringValue, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed))
            {
                return parsed;
            }

            return fallback;
        }

        public int AsInt(int fallback = 0)
        {
            if (Kind == JsonKind.Number)
            {
                return (int)System.Math.Round(NumberValue);
            }

            if (Kind == JsonKind.String &&
                int.TryParse(StringValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
            {
                return parsed;
            }

            return fallback;
        }

        public bool AsBool(bool fallback = false)
        {
            if (Kind == JsonKind.Bool)
            {
                return BoolValue;
            }

            if (Kind == JsonKind.Number)
            {
                return NumberValue != 0d;
            }

            return fallback;
        }

        internal static JsonValue MakeNull()
        {
            return new JsonValue { Kind = JsonKind.Null };
        }

        internal static JsonValue MakeBool(bool value)
        {
            return new JsonValue { Kind = JsonKind.Bool, BoolValue = value };
        }

        internal static JsonValue MakeNumber(double value)
        {
            return new JsonValue { Kind = JsonKind.Number, NumberValue = value };
        }

        internal static JsonValue MakeString(string value)
        {
            return new JsonValue { Kind = JsonKind.String, StringValue = value };
        }

        internal static JsonValue MakeArray(List<JsonValue> items)
        {
            return new JsonValue { Kind = JsonKind.Array, Items = items };
        }

        internal static JsonValue MakeObject(Dictionary<string, JsonValue> members)
        {
            return new JsonValue { Kind = JsonKind.Object, Members = members };
        }
    }

    public class JsonParseException : System.Exception
    {
        public JsonParseException(string message, int index)
            : base(message + " (at char " + index + ")")
        {
        }
    }

    /// <summary>Recursive descent JSON parser with no third party dependency.</summary>
    public static class MiniJson
    {
        public static JsonValue Parse(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                throw new JsonParseException("Empty JSON text.", 0);
            }

            int index = 0;
            SkipWhitespace(text, ref index);
            JsonValue root = ParseValue(text, ref index);
            SkipWhitespace(text, ref index);
            if (index < text.Length)
            {
                throw new JsonParseException("Unexpected trailing content.", index);
            }

            return root;
        }

        static JsonValue ParseValue(string text, ref int index)
        {
            if (index >= text.Length)
            {
                throw new JsonParseException("Unexpected end of JSON.", index);
            }

            char c = text[index];
            switch (c)
            {
                case '{':
                    return ParseObject(text, ref index);
                case '[':
                    return ParseArray(text, ref index);
                case '"':
                    return JsonValue.MakeString(ParseString(text, ref index));
                case 't':
                    Expect(text, ref index, "true");
                    return JsonValue.MakeBool(true);
                case 'f':
                    Expect(text, ref index, "false");
                    return JsonValue.MakeBool(false);
                case 'n':
                    Expect(text, ref index, "null");
                    return JsonValue.MakeNull();
                default:
                    return JsonValue.MakeNumber(ParseNumber(text, ref index));
            }
        }

        static JsonValue ParseObject(string text, ref int index)
        {
            var members = new Dictionary<string, JsonValue>();
            index++;
            SkipWhitespace(text, ref index);
            if (index < text.Length && text[index] == '}')
            {
                index++;
                return JsonValue.MakeObject(members);
            }

            while (true)
            {
                SkipWhitespace(text, ref index);
                if (index >= text.Length || text[index] != '"')
                {
                    throw new JsonParseException("Expected object key.", index);
                }

                string key = ParseString(text, ref index);
                SkipWhitespace(text, ref index);
                if (index >= text.Length || text[index] != ':')
                {
                    throw new JsonParseException("Expected ':' after key '" + key + "'.", index);
                }

                index++;
                SkipWhitespace(text, ref index);
                members[key] = ParseValue(text, ref index);
                SkipWhitespace(text, ref index);
                if (index >= text.Length)
                {
                    throw new JsonParseException("Unterminated object.", index);
                }

                if (text[index] == ',')
                {
                    index++;
                    continue;
                }

                if (text[index] == '}')
                {
                    index++;
                    return JsonValue.MakeObject(members);
                }

                throw new JsonParseException("Expected ',' or '}' in object.", index);
            }
        }

        static JsonValue ParseArray(string text, ref int index)
        {
            var items = new List<JsonValue>();
            index++;
            SkipWhitespace(text, ref index);
            if (index < text.Length && text[index] == ']')
            {
                index++;
                return JsonValue.MakeArray(items);
            }

            while (true)
            {
                SkipWhitespace(text, ref index);
                items.Add(ParseValue(text, ref index));
                SkipWhitespace(text, ref index);
                if (index >= text.Length)
                {
                    throw new JsonParseException("Unterminated array.", index);
                }

                if (text[index] == ',')
                {
                    index++;
                    continue;
                }

                if (text[index] == ']')
                {
                    index++;
                    return JsonValue.MakeArray(items);
                }

                throw new JsonParseException("Expected ',' or ']' in array.", index);
            }
        }

        static string ParseString(string text, ref int index)
        {
            index++;
            var builder = new StringBuilder();
            while (index < text.Length)
            {
                char c = text[index++];
                if (c == '"')
                {
                    return builder.ToString();
                }

                if (c != '\\')
                {
                    builder.Append(c);
                    continue;
                }

                if (index >= text.Length)
                {
                    break;
                }

                char escape = text[index++];
                switch (escape)
                {
                    case '"':
                        builder.Append('"');
                        break;
                    case '\\':
                        builder.Append('\\');
                        break;
                    case '/':
                        builder.Append('/');
                        break;
                    case 'b':
                        builder.Append('\b');
                        break;
                    case 'f':
                        builder.Append('\f');
                        break;
                    case 'n':
                        builder.Append('\n');
                        break;
                    case 'r':
                        builder.Append('\r');
                        break;
                    case 't':
                        builder.Append('\t');
                        break;
                    case 'u':
                        if (index + 4 > text.Length)
                        {
                            throw new JsonParseException("Bad unicode escape.", index);
                        }

                        string hex = text.Substring(index, 4);
                        index += 4;
                        builder.Append((char)int.Parse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                        break;
                    default:
                        throw new JsonParseException("Unknown escape '\\" + escape + "'.", index);
                }
            }

            throw new JsonParseException("Unterminated string.", index);
        }

        static double ParseNumber(string text, ref int index)
        {
            int start = index;
            while (index < text.Length)
            {
                char c = text[index];
                bool numeric = (c >= '0' && c <= '9') || c == '-' || c == '+' ||
                               c == '.' || c == 'e' || c == 'E';
                if (!numeric)
                {
                    break;
                }

                index++;
            }

            string slice = text.Substring(start, index - start);
            if (!double.TryParse(slice, NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
            {
                throw new JsonParseException("Bad number '" + slice + "'.", start);
            }

            return value;
        }

        static void Expect(string text, ref int index, string literal)
        {
            if (index + literal.Length > text.Length ||
                string.CompareOrdinal(text, index, literal, 0, literal.Length) != 0)
            {
                throw new JsonParseException("Expected literal '" + literal + "'.", index);
            }

            index += literal.Length;
        }

        static void SkipWhitespace(string text, ref int index)
        {
            while (index < text.Length)
            {
                char c = text[index];
                if (c == ' ' || c == '\t' || c == '\n' || c == '\r')
                {
                    index++;
                    continue;
                }

                // Tolerate // line comments so level files can be annotated.
                if (c == '/' && index + 1 < text.Length && text[index + 1] == '/')
                {
                    while (index < text.Length && text[index] != '\n')
                    {
                        index++;
                    }

                    continue;
                }

                return;
            }
        }
    }
}
