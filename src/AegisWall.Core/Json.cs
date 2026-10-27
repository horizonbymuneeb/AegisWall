using System;
using System.Collections.Generic;
using System.Web.Script.Serialization;

namespace AegisWall.Core
{
    public static class Json
    {
        private static readonly object Gate = new object();
        private static readonly JavaScriptSerializer Serializer = new JavaScriptSerializer();

        static Json()
        {
            Serializer.MaxJsonLength = 16 * 1024 * 1024;
            Serializer.RecursionLimit = 100;
        }

        public static string Serialize(object value)
        {
            lock (Gate)
            {
                return Serializer.Serialize(value);
            }
        }

        public static T Deserialize<T>(string json)
        {
            lock (Gate)
            {
                return Serializer.Deserialize<T>(json);
            }
        }

        public static Dictionary<string, object> DeserializeObject(string json)
        {
            lock (Gate)
            {
                return Serializer.Deserialize<Dictionary<string, object>>(json);
            }
        }
    }
}
