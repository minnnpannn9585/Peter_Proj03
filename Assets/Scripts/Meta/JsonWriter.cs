using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ParcelSort
{
    /// <summary>
    /// Serialised JSON node. <see cref="MiniJson"/> only reads, so persistence needs its own
    /// writer; this is the minimum tree needed to emit a profile.
    /// </summary>
    public abstract class JsonNode
    {
        internal abstract void Emit(StringBuilder sb, bool pretty, int indent);

        public string ToJson(bool pretty = true)
        {
            var sb = new StringBuilder(256);
            Emit(sb, pretty, 0);
            return sb.ToString();
        }

        internal static void Indent(StringBuilder sb, bool pretty, int depth)
        {
            if (!pretty)
            {
                return;
            }

            sb.Append('\n');
            for (int i = 0; i < depth; i++)
            {
                sb.Append("  ");
            }
        }
    }

    sealed class JsonLiteral : JsonNode
    {
        readonly string text;

        internal JsonLiteral(string rawText)
        {
            text = rawText;
        }

        internal override void Emit(StringBuilder sb, bool pretty, int indent)
        {
            sb.Append(text);
        }
    }

    /// <summary>Ordered JSON object. Insertion order is preserved so output is byte stable.</summary>
    public sealed class JsonObjectBuilder : JsonNode
    {
        readonly List<KeyValuePair<string, JsonNode>> members =
            new List<KeyValuePair<string, JsonNode>>();

        public int Count => members.Count;

        public JsonObjectBuilder Add(string key, JsonNode value)
        {
            members.Add(new KeyValuePair<string, JsonNode>(key, value ?? JsonWriter.Null()));
            return this;
        }

        public JsonObjectBuilder Add(string key, string value) => Add(key, JsonWriter.Value(value));

        public JsonObjectBuilder Add(string key, int value) => Add(key, JsonWriter.Value(value));

        public JsonObjectBuilder Add(string key, float value) => Add(key, JsonWriter.Value(value));

        public JsonObjectBuilder Add(string key, double value) => Add(key, JsonWriter.Value(value));

        public JsonObjectBuilder Add(string key, bool value) => Add(key, JsonWriter.Value(value));

        internal override void Emit(StringBuilder sb, bool pretty, int indent)
        {
            if (members.Count == 0)
            {
                sb.Append("{}");
                return;
            }

            sb.Append('{');
            for (int i = 0; i < members.Count; i++)
            {
                if (i > 0)
                {
                    sb.Append(',');
                }

                Indent(sb, pretty, indent + 1);
                JsonWriter.AppendEscaped(sb, members[i].Key);
                sb.Append(pretty ? ": " : ":");
                members[i].Value.Emit(sb, pretty, indent + 1);
            }

            Indent(sb, pretty, indent);
            sb.Append('}');
        }
    }

    /// <summary>Ordered JSON array.</summary>
    public sealed class JsonArrayBuilder : JsonNode
    {
        readonly List<JsonNode> items = new List<JsonNode>();

        public int Count => items.Count;

        public JsonArrayBuilder Add(JsonNode value)
        {
            items.Add(value ?? JsonWriter.Null());
            return this;
        }

        public JsonArrayBuilder Add(string value) => Add(JsonWriter.Value(value));

        public JsonArrayBuilder Add(int value) => Add(JsonWriter.Value(value));

        public JsonArrayBuilder Add(float value) => Add(JsonWriter.Value(value));

        public JsonArrayBuilder Add(bool value) => Add(JsonWriter.Value(value));

        internal override void Emit(StringBuilder sb, bool pretty, int indent)
        {
            if (items.Count == 0)
            {
                sb.Append("[]");
                return;
            }

            sb.Append('[');
            for (int i = 0; i < items.Count; i++)
            {
                if (i > 0)
                {
                    sb.Append(',');
                }

                Indent(sb, pretty, indent + 1);
                items[i].Emit(sb, pretty, indent + 1);
            }

            Indent(sb, pretty, indent);
            sb.Append(']');
        }
    }

    /// <summary>
    /// Tiny JSON writer. Numbers use the invariant round-trip format so a value written here
    /// and read back through <see cref="MiniJson"/> compares equal; strings escape everything
    /// outside printable ASCII so the file is safe regardless of the reader's encoding.
    /// </summary>
    public static class JsonWriter
    {
        public static JsonObjectBuilder Object() => new JsonObjectBuilder();

        public static JsonArrayBuilder Array() => new JsonArrayBuilder();

        public static JsonNode Null() => new JsonLiteral("null");

        public static JsonNode Value(bool value) => new JsonLiteral(value ? "true" : "false");

        public static JsonNode Value(int value) =>
            new JsonLiteral(value.ToString(CultureInfo.InvariantCulture));

        public static JsonNode Value(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                return new JsonLiteral("0");
            }

            return new JsonLiteral(value.ToString("R", CultureInfo.InvariantCulture));
        }

        public static JsonNode Value(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                return new JsonLiteral("0");
            }

            return new JsonLiteral(value.ToString("R", CultureInfo.InvariantCulture));
        }

        public static JsonNode Value(string value)
        {
            if (value == null)
            {
                return Null();
            }

            var sb = new StringBuilder(value.Length + 2);
            AppendEscaped(sb, value);
            return new JsonLiteral(sb.ToString());
        }

        /// <summary>Serialises a node tree.</summary>
        public static string Write(JsonNode value, bool pretty = true)
        {
            return value == null ? "null" : value.ToJson(pretty);
        }

        /// <summary>
        /// Serialises a tree that came out of <see cref="MiniJson"/>. Object keys are sorted so
        /// the result is stable even though the parser stores members in a dictionary.
        /// </summary>
        public static string Write(JsonValue value, bool pretty = true)
        {
            var sb = new StringBuilder(256);
            EmitJsonValue(value, sb, pretty, 0);
            return sb.ToString();
        }

        static void EmitJsonValue(JsonValue value, StringBuilder sb, bool pretty, int indent)
        {
            if (value == null)
            {
                sb.Append("null");
                return;
            }

            switch (value.Kind)
            {
                case JsonKind.Bool:
                    sb.Append(value.BoolValue ? "true" : "false");
                    return;
                case JsonKind.Number:
                    sb.Append(value.NumberValue.ToString("R", CultureInfo.InvariantCulture));
                    return;
                case JsonKind.String:
                    AppendEscaped(sb, value.StringValue);
                    return;
                case JsonKind.Array:
                    EmitArray(value, sb, pretty, indent);
                    return;
                case JsonKind.Object:
                    EmitObject(value, sb, pretty, indent);
                    return;
                default:
                    sb.Append("null");
                    return;
            }
        }

        static void EmitArray(JsonValue value, StringBuilder sb, bool pretty, int indent)
        {
            if (value.Count == 0)
            {
                sb.Append("[]");
                return;
            }

            sb.Append('[');
            for (int i = 0; i < value.Count; i++)
            {
                if (i > 0)
                {
                    sb.Append(',');
                }

                JsonNode.Indent(sb, pretty, indent + 1);
                EmitJsonValue(value[i], sb, pretty, indent + 1);
            }

            JsonNode.Indent(sb, pretty, indent);
            sb.Append(']');
        }

        static void EmitObject(JsonValue value, StringBuilder sb, bool pretty, int indent)
        {
            if (value.Count == 0)
            {
                sb.Append("{}");
                return;
            }

            var keys = new List<string>(value.Members.Keys);
            keys.Sort(System.StringComparer.Ordinal);

            sb.Append('{');
            for (int i = 0; i < keys.Count; i++)
            {
                if (i > 0)
                {
                    sb.Append(',');
                }

                JsonNode.Indent(sb, pretty, indent + 1);
                AppendEscaped(sb, keys[i]);
                sb.Append(pretty ? ": " : ":");
                EmitJsonValue(value[keys[i]], sb, pretty, indent + 1);
            }

            JsonNode.Indent(sb, pretty, indent);
            sb.Append('}');
        }

        public static string Escape(string text)
        {
            var sb = new StringBuilder((text?.Length ?? 0) + 2);
            AppendEscaped(sb, text);
            return sb.ToString();
        }

        /// <summary>
        /// Writes a quoted, escaped JSON string. Anything outside printable ASCII becomes a
        /// \uXXXX escape, which keeps mission names in Chinese readable by any parser.
        /// </summary>
        internal static void AppendEscaped(StringBuilder sb, string text)
        {
            sb.Append('"');
            if (string.IsNullOrEmpty(text))
            {
                sb.Append('"');
                return;
            }

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                switch (c)
                {
                    case '"':
                        sb.Append("\\\"");
                        continue;
                    case '\\':
                        sb.Append("\\\\");
                        continue;
                    case '\n':
                        sb.Append("\\n");
                        continue;
                    case '\r':
                        sb.Append("\\r");
                        continue;
                    case '\t':
                        sb.Append("\\t");
                        continue;
                    case '\b':
                        sb.Append("\\b");
                        continue;
                    case '\f':
                        sb.Append("\\f");
                        continue;
                    default:
                        if (c < 0x20 || c > 0x7E)
                        {
                            sb.Append("\\u");
                            sb.Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            sb.Append(c);
                        }

                        continue;
                }
            }

            sb.Append('"');
        }
    }
}
