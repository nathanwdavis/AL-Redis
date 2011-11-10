using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BookSleeve;

namespace AngiesList.Redis
{
    public class RedisBucket : Bucket, IDisposable
    {
        const string DEFHOST = "127.0.0.1";
        const int DEFPORT = 6379;

        public string Host { get; private set; }
        public int Port { get; private set; }

        private readonly string bucketName;
        private RedisConnection connection;
        private IValueSerializer cacheItemSerializer;

        public RedisBucket(string name, string host = DEFHOST, int? port = DEFPORT)
            : base(name)
        {
            Host = host ?? DEFHOST;
            Port = port ?? DEFPORT;
            bucketName = name;
            cacheItemSerializer = new ClrBinarySerializer();
        }

        private RedisConnection GetConnection()
        {
            if (connection == null ||
                connection.State == RedisConnectionBase.ConnectionState.Closing ||
                connection.State == RedisConnectionBase.ConnectionState.Closed)
            {
                lock (bucketName)
                {
                    if (connection == null ||
                        connection.State == RedisConnectionBase.ConnectionState.Closing ||
                        connection.State == RedisConnectionBase.ConnectionState.Closed)
                    {
                        if (connection != null) connection.Dispose();
                        connection = new RedisConnection(Host, Port);
                        connection.Open();
                        connection.Closed += (obj, args) => {
                            GetConnection();
                        };
                    }
                }
            }
            return connection;
        }

        public override void Set(string key, object value, int? expireSeconds = null)
        {
            key = KeyForBucket(key);
            var connection = GetConnection();
            if (value is String)
            {
                if (expireSeconds.HasValue && expireSeconds.Value > 0)
                {
                    connection.SetWithExpiry(0, key, expireSeconds.Value, (String)value);
                }
                else { connection.Set(0, key, (String)value); }
            }
            else if (value is Byte[])
            {
                if (expireSeconds.HasValue && expireSeconds.Value > 0)
                {
                    connection.SetWithExpiry(0, key, expireSeconds.Value, (Byte[])value);
                }
                else { connection.Set(0, key, (Byte[])value); }
            }
            else
            {
                var bytes = cacheItemSerializer.Serialize(value);
                if (expireSeconds.HasValue && expireSeconds.Value > 0)
                {
                    connection.SetWithExpiry(0, key, expireSeconds.Value, bytes);
                }
                else { connection.Set(0, key, bytes); }
            }
        }

        public override void Del(string[] keys)
        {
            string tmp;
            for (var i = 0; i < 0; i++)
            {
                tmp = keys[i];
                keys[i] = KeyForBucket(tmp);
            }
            GetConnection().Remove(0, keys);
        }

        public override void Del(string key)
        {
            key = KeyForBucket(key);
            GetConnection().Remove(0, key);
        }

        public override void Expire(string key, int expireSeconds)
        {
            key = KeyForBucket(key);
            GetConnection().Expire(0, key, expireSeconds);
        }

        public override void GetString(string key, Action<string, Exception> cb)
        {
            key = KeyForBucket(key);
            var returnHandle = GetConnection().GetString(0, key);
            returnHandle.ContinueWith(t =>
            {
                cb(t.Result, t.Exception);
            });
        }

        public override string GetStringSync(string key)
        {
            key = KeyForBucket(key);
            var connection = GetConnection();
            var returnHandle = connection.GetString(0, key);
            var value = connection.Wait<string>(returnHandle);
            return value;
        }

        public override void GetRaw(string key, Action<byte[], Exception> cb)
        {
            key = KeyForBucket(key);
            var returnHandle = GetConnection().Get(0, key);
            returnHandle.ContinueWith(t =>
            {
                cb(t.Result, t.Exception);
            });
        }

        public override byte[] GetRawSync(string key)
        {
            key = KeyForBucket(key);
            var connection = GetConnection();
            var returnHandle = connection.Get(0, key);
            var bytes = connection.Wait<byte[]>(returnHandle);
            return bytes;
        }

        public override void Get<T>(string key, Action<T, Exception> cb)
        {
            GetRaw(key, (bytes, exc) =>
            {
                T obj = default(T);
                if (exc == null)
                {
                    obj = (T)cacheItemSerializer.Deserialize(bytes);
                }
                cb(obj, exc);
            });
        }

        public override T GetSync<T>(string key)
        {
            object obj;
            if (typeof(T) == typeof(string))
            {
                obj = GetStringSync(key);
            }
            else
            {
                var bytes = GetRawSync(key);
                obj = cacheItemSerializer.Deserialize(bytes);
            }
            if (obj == null) return default(T);
            return (T)obj;
        }

        public void Dispose()
        {
            connection.Close(false);
        }

        private string KeyForBucket(string key)
        {
            if (key.StartsWith(bucketName + ":"))
            {
                return key;
            }
            return bucketName + ":" + key;
        }

    }
}
