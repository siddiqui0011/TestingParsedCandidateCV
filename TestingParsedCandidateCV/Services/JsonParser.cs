using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System.Dynamic;

public class JsonParser
{
    public dynamic ParseJsonData(string jsonData)
    {
        return JsonConvert.DeserializeObject<ExpandoObject>(jsonData, new ExpandoObjectConverter());
    }
}
