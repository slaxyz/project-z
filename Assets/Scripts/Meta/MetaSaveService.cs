using System.IO;
using UnityEngine;

namespace ProjectZ.Meta
{
    public static class MetaSaveService
    {
        private const string FileName = "meta_save.json";

        private static string SavePath
        {
            get { return Path.Combine(Application.persistentDataPath, FileName); }
        }

        public static MetaData Load()
        {
            if (!File.Exists(SavePath))
            {
                return new MetaData();
            }

            var json = File.ReadAllText(SavePath);
            var loaded = JsonUtility.FromJson<MetaData>(json);
            return loaded ?? new MetaData();
        }

        public static void Save(MetaData data)
        {
            var json = JsonUtility.ToJson(data, true);
            File.WriteAllText(SavePath, json);
        }
    }
}
