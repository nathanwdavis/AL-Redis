using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web.SessionState;
using System.Collections.Specialized;
using System.Web;
using System.Web.Configuration;
using BookSleeve;
using System.IO;

namespace AngiesList.Redis
{
    public sealed class RedisSessionStateStore : SessionStateStoreProviderBase
    {
        private Bucket bucket;
        private SessionStateSection sessionStateConfig;

        public override void Initialize(string name, NameValueCollection config)
        {
            if (String.IsNullOrWhiteSpace(name))
            {
                name = "RedisAspNetSessionStateStore";
            }
            base.Initialize(name, config);

            bucket = KeyValueStore.Bucket(name);
            sessionStateConfig = (SessionStateSection)WebConfigurationManager.GetSection("system.web/sessionState");
        }

        public override bool SetItemExpireCallback(SessionStateItemExpireCallback expireCallback)
        {
            return false;
        }

        public override void SetAndReleaseItemExclusive(HttpContext context,
          string id,
          SessionStateStoreData item,
          object lockId,
          bool newItem)
        {
            var ms = new MemoryStream();
            var writer = new BinaryWriter(ms);

            if (item.Items as SessionStateItemCollection != null)
                ((SessionStateItemCollection)item.Items).Serialize(writer);

            writer.Close();

            byte[] sessionData = ms.ToArray();
            bucket.Set(id, sessionData, item.Timeout * 60);
        }

        public override SessionStateStoreData GetItem(HttpContext context, 
            string id, 
            out bool locked, 
            out TimeSpan lockAge, 
            out object lockId, 
            out SessionStateActions actions)
        {
            var sessionData = bucket.GetRawSync(id);
            locked = false;
            lockAge = new TimeSpan(0);
            lockId = null;
            actions = SessionStateActions.None;

            if (sessionData == null)
            {
                return null;
            }
            else
            {
                var ms = new MemoryStream(sessionData);

                var sessionItems = new SessionStateItemCollection();

                if (ms.Length > 0)
                {
                    BinaryReader reader = new BinaryReader(ms);
                    sessionItems = SessionStateItemCollection.Deserialize(reader);
                }
                return new SessionStateStoreData(sessionItems,
                    SessionStateUtility.GetSessionStaticObjects(context),
                    (int)sessionStateConfig.Timeout.TotalMinutes);
            }
        }

        public override SessionStateStoreData GetItemExclusive(HttpContext context, 
            string id, 
            out bool locked, 
            out TimeSpan lockAge, 
            out object lockId, 
            out SessionStateActions actions)
        {
            return GetItem(context, id, out locked, out lockAge, out lockId, out actions);
        }

        public override SessionStateStoreData CreateNewStoreData(HttpContext context, int timeout)
        {
            return new SessionStateStoreData(
                new SessionStateItemCollection(), 
                SessionStateUtility.GetSessionStaticObjects(context), 
                timeout
            );
        }

        public override void CreateUninitializedItem(HttpContext context, string id, int timeout)
        {
            var ms = new MemoryStream();
            var writer = new BinaryWriter(ms);
            (new SessionStateItemCollection()).Serialize(writer);
            writer.Close();
            byte[] sessionData = ms.ToArray();
            bucket.Set(id, sessionData, timeout);
        }

        public override void Dispose()
        {
            IDisposable disposable;
            if ((disposable = bucket as IDisposable) != null)
            {
                disposable.Dispose();
            }
        }

        public override void InitializeRequest(HttpContext context)
        {
        }

        public override void EndRequest(HttpContext context)
        {
        }

        public override void ReleaseItemExclusive(HttpContext context, string id, object lockId)
        {
        }

        public override void RemoveItem(HttpContext context, string id, object lockId, SessionStateStoreData item)
        {
            bucket.Del(id);
        }

        public override void ResetItemTimeout(HttpContext context, string id)
        {
        }
    }
}
