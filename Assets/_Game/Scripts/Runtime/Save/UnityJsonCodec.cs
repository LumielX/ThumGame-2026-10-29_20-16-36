using System;
using SpiritMovers.Core.Save;
using UnityEngine;

namespace SpiritMovers.Save
{
    /// <summary>Save files through Unity's JsonUtility (no extra package, M4B plan).</summary>
    public sealed class UnityJsonCodec : IJsonCodec
    {
        [Serializable]
        private sealed class SchemaProbe
        {
            public int schema = -1;
        }

        public string ToJson(object value) => JsonUtility.ToJson(value, true);

        public T FromJson<T>(string json) => JsonUtility.FromJson<T>(json);

        public int ReadSchema(string json)
        {
            try
            {
                return JsonUtility.FromJson<SchemaProbe>(json)?.schema ?? -1;
            }
            catch (ArgumentException)
            {
                return -1;
            }
        }
    }
}
