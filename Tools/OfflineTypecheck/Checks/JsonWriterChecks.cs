namespace ParcelSort.Offline
{
    /// <summary>JsonWriter escaping and float round-trip through MiniJson.</summary>
    public static class JsonWriterChecks
    {
        public static void Run(CheckRunner r)
        {
            r.Section("JsonWriter");

            r.AreEqual("\"a\\\"b\"", JsonWriter.Escape("a\"b"), "double quote is escaped");
            r.AreEqual("\"a\\\\b\"", JsonWriter.Escape("a\\b"), "backslash is escaped");
            r.AreEqual("\"a\\nb\"", JsonWriter.Escape("a\nb"), "newline is escaped");
            r.AreEqual("\"a\\tb\"", JsonWriter.Escape("a\tb"), "tab is escaped");
            r.AreEqual("\"\\u591c\\u73ed\"", JsonWriter.Escape("夜班"), "non-ASCII becomes \\uXXXX");

            // Round-trip a nested structure through the reader.
            string json = JsonWriter.Object()
                .Add("name", "夜班盲件 \"M3\"\n")
                .Add("ratio", 0.35f)
                .Add("count", 108)
                .Add("on", true)
                .Add("nested", JsonWriter.Object().Add("deep", JsonWriter.Array().Add(1).Add(2).Add(3)))
                .Add("empty", JsonWriter.Array())
                .ToJson();

            JsonValue parsed = MiniJson.Parse(json);
            r.AreEqual("夜班盲件 \"M3\"\n", parsed["name"].AsString(), "string survives the round trip");
            r.Check(parsed["ratio"].AsFloat() == 0.35f, "0.35f survives the round trip exactly");
            r.AreEqual(108, parsed["count"].AsInt(), "int survives the round trip");
            r.Check(parsed["on"].AsBool(), "bool survives the round trip");
            r.AreEqual(3, parsed["nested"]["deep"].Count, "nested array survives the round trip");
            r.AreEqual(2, parsed["nested"]["deep"][1].AsInt(), "nested array values are in order");
            r.Check(parsed["empty"].Kind == JsonKind.Array && parsed["empty"].Count == 0,
                "an empty array stays an empty array");

            // A spread of floats, including awkward ones.
            float[] samples = { 0f, 1f, -1f, 0.1f, 0.35f, 1.15f, 1.6f, 2.8644f, 150f, 1e-5f, 1e7f, 81.6f };
            bool allExact = true;
            for (int i = 0; i < samples.Length; i++)
            {
                string one = JsonWriter.Object().Add("v", samples[i]).ToJson(false);
                if (MiniJson.Parse(one)["v"].AsFloat() != samples[i])
                {
                    allExact = false;
                    r.Info("float round-trip drifted for " + samples[i]);
                }
            }

            r.Check(allExact, "every sampled float round-trips bit exactly");

            r.AreEqual("{}", JsonWriter.Object().ToJson(false), "an empty object writes as {}");
            r.AreEqual("[]", JsonWriter.Array().ToJson(false), "an empty array writes as []");
        }
    }
}
