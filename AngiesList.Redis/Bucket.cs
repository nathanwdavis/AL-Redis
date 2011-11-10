using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AngiesList.Redis
{
    public abstract class Bucket
    {
        public string Name { get; private set; }

        public Bucket(string name)
        {
            Name = name;
        }

        public abstract void Set(string key, object value, int? expireSeconds = null);
        public abstract void Del(string key);
        public abstract void Del(string[] keys);
        public abstract void Expire(string key, int expireSeconds);
        public abstract void GetString(string key, Action<string, Exception> cb);
        public abstract string GetStringSync(string key);
        public abstract void Get<T>(string key, Action<T, Exception> cb);
        public abstract T GetSync<T>(string key);
        public abstract void GetRaw(string key, Action<byte[], Exception> cb);
        public abstract byte[] GetRawSync(string key);
    }
}
