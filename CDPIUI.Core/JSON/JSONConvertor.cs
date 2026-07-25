using Newtonsoft.Json;

namespace CDPIUI.Core.JSON
{
    public static class JSONConvertor
    {
        public static T? LoadJson<T>(string filepath)
        {
            string json = File.ReadAllText(filepath);
            return DeserializeObject<T>(json);
        }

        public static T? DeserializeObject<T>(string @object) => JsonConvert.DeserializeObject<T>(@object);

        public static string SerializeObject<T>(T obj) => JsonConvert.SerializeObject(obj);
    }
}
