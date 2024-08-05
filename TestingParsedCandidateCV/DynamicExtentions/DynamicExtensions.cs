using System.Collections.Generic;
using System.Dynamic;

public static class DynamicExtensions
{
    public static object GetProperty(dynamic obj, string propertyName)
    {
        if (obj is ExpandoObject)
        {
            var dictionary = (IDictionary<string, object>)obj;
            dictionary.TryGetValue(propertyName, out var value);
            return value;
        }
        else if (obj is IDictionary<string, object> dictionary)
        {
            dictionary.TryGetValue(propertyName, out var value);
            return value;
        }

        return null;
    }

    public static IEnumerable<object> GetEnumerable(dynamic obj)
    {
        if (obj is IEnumerable<object> enumerable)
        {
            return enumerable;
        }
        else if (obj is IEnumerable<object> list)
        {
            return list;
        }

        return Enumerable.Empty<object>();
    }
}
