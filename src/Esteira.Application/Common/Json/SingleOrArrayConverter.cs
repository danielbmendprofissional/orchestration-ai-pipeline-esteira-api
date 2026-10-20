using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Esteira.Application.Common.Json
{
    public class SingleOrArrayConverter<T> : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(List<T>);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            JToken token = JToken.Load(reader);
            if (token.Type == JTokenType.String)
            {
                // If it's a single string, wrap it in a new list
                return new List<T> { token.ToObject<T>() };
            }
            if (token.Type == JTokenType.Array)
            {
                // If it's an array, deserialize it as a list
                return token.ToObject<List<T>>();
            }
            // Handle other cases or nulls as needed
            return null;
        }

        public override bool CanWrite
        {
            get { return false; }
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }


    }
}
