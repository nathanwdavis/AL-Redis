using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;

namespace AngiesList.Redis
{
    public sealed class KeyValueStore
    {
        private static ConcurrentDictionary<string, Bucket> bucketsPool = new ConcurrentDictionary<string, Bucket>();
        private static readonly object locker = new Object();

        private KeyValueStore() { }

        public static Bucket Bucket(string name, string host, int? port)
        {
            if (String.IsNullOrEmpty(host) && !port.HasValue)
            {
                var config = KeyValueStoreConfiguration.GetConfig();
                host = config.Host;
                port = config.Port;
            }

            var poolKey = name + host + port;
            if (!bucketsPool.ContainsKey(poolKey))
            {
                lock (locker)
                {
                    bucketsPool.TryAdd(poolKey, new RedisBucket(name, host, port));
                }
            }
            return bucketsPool[poolKey];
        }

        public static Bucket Bucket(string name)
        {
            return Bucket(name, null, null);
        }
    }
}
