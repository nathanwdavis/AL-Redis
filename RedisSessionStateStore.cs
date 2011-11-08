using System;
using System.Collections.Generic;
using System.Configuration;
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
        private RedisConnection redis;
        private SessionStateSection sessionStateConfig;
		private string lockHashKey;

        public override void Initialize(string name, NameValueCollection config)
        {
            if (String.IsNullOrWhiteSpace(name))
            {
                name = "RedisAspNetSessionStateStore";
            }
            base.Initialize(name, config);
			
			lockHashKey = name+":LockedSessions";
			
            sessionStateConfig = (SessionStateSection)WebConfigurationManager.GetSection("system.web/sessionState");
			var stateConnection = sessionStateConfig.StateConnectionString;
			
			if (!String.IsNullOrWhiteSpace(stateConnection)) {
				var stateConnectionParts = sessionStateConfig.StateConnectionString.Split('=',':');
				string host = stateConnectionParts.ElementAtOrDefault(1) ?? "localhost",
					portAsString = stateConnectionParts.ElementAtOrDefault(2) ?? "6379";
				var port = Int32.Parse(portAsString);
				
				redis = new RedisConnection(host, port);
			}
			else {
				redis = new RedisConnection("localhost", 6379);
			}
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
            var getLock = redis.Hashes.GetString(0, lockHashKey, id);
			var lockIdAsString = (string)lockId;
			var ms = new MemoryStream();
            var writer = new BinaryWriter(ms);

            if (item.Items as SessionStateItemCollection != null)
                ((SessionStateItemCollection)item.Items).Serialize(writer);
			
            writer.Close();

            byte[] sessionData = ms.ToArray();
			var sessionItemHash = new Dictionary<string, byte[]>();
			sessionItemHash.Add("initialize", new byte[] {0});
			sessionItemHash.Add("data", sessionData);
			
			if (!String.IsNullOrEmpty(getLock.Result) && getLock.Result == lockIdAsString) {
				redis.Hashes.Set(0, GetKeyForSessionId(id), sessionItemHash, false);
				redis.Hashes.Remove(0, lockHashKey, id);
			}
        }

        public override SessionStateStoreData GetItem(HttpContext context, 
            string id, 
            out bool locked, 
            out TimeSpan lockAge, 
            out object lockId, 
            out SessionStateActions actions)
        {
            var sessionData = redis.Hashes.GetAll(0, GetKeyForSessionId(id)).Result;
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
                var ms = new MemoryStream(sessionData["data"]);
                var sessionItems = new SessionStateItemCollection();

                if (ms.Length > 0)
                {
                    var reader = new BinaryReader(ms);
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
            
			//TODO
			//sessionItemHash.Add("lockedTime", BitConverter.GetBytes(DateTime.Now.Ticks/TimeSpan.TicksPerMillisecond));
			//
			
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
			var newItemHash = new Dictionary<string, byte[]>();
			newItemHash.Add("data", sessionData);
			newItemHash.Add("initialize", new byte[] {1});
			redis.Hashes.Set(0, GetKeyForSessionId(id), newItemHash, false);
        }

        public override void Dispose()
        {
            IDisposable disposable;
            if ((disposable = redis as IDisposable) != null)
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
			var getLock = redis.Hashes.GetString(0, lockHashKey, id);
			var lockIdAsString = (string)lockId;
			if (!String.IsNullOrEmpty(getLock.Result) && getLock.Result == lockIdAsString) {
				redis.Hashes.Remove(0, lockHashKey, id);
			}
        }

        public override void RemoveItem(HttpContext context, string id, object lockId, SessionStateStoreData item)
        {
            var getLock = redis.Hashes.GetString(0, lockHashKey, id);
			var lockIdAsString = (string)lockId;
			if (!String.IsNullOrEmpty(getLock.Result) && getLock.Result == lockIdAsString) {
				redis.Keys.Remove(0, GetKeyForSessionId(id));
				redis.Hashes.Remove(0, lockHashKey, id);
			}
		}
		
		public override void ResetItemTimeout(HttpContext context, string id)
        {
			//TODO
        }
			
		private string GetKeyForSessionId(string id) {
			return this.Name + ":" + id;	
		}
	}
}
