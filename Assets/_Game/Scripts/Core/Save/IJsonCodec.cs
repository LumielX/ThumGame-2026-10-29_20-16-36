namespace SpiritMovers.Core.Save
{
    /// <summary>Turns save data into JSON and back; the game uses Unity's JsonUtility, which Core cannot reference.</summary>
    public interface IJsonCodec
    {
        string ToJson(object value);
        T FromJson<T>(string json);
        /// <summary>The "schema" field of a JSON document, or -1 when it cannot be read.</summary>
        int ReadSchema(string json);
    }
}
