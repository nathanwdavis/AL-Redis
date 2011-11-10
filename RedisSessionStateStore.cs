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

		public override void Initialize (string name, NameValueCollection config)
		{
			if (String.IsNullOrWhiteSpace (name)) {
				name = "RedisAspNetSessionStateStore";
			}
			base.Initialize (name, config);
			
			lockHashKey = name + ":LockedSessions";
			
			sessionStateConfig = (SessionStateSection)WebConfigurationManager.GetSection ("system.web/sessionState");
			var stateConnection = sessionStateConfig.StateConnectionString;
			
			if (!String.IsNullOrWhiteSpace (stateConnection)) {
				var stateConnectionParts = sessionStateConfig.StateConnectionString.Split ('=', ':');
				string host = stateConnectionParts.ElementAtOrDefault (1) ?? "localhost",
					portAsString = stateConnectionParts.ElementAtOrDefault (2) ?? "6379";
				var port = Int32.Parse (portAsString);
				
				redis = new RedisConnection (host, port);
			} else {
				redis = new RedisConnection ("localhost", 6379);
			}
		}

		public override bool SetItemExpireCallback (SessionStateItemExpireCallback expireCallback)
		{
			return false;
		}

		public override void SetAndReleaseItemExclusive (HttpContext context,
          string id,
          SessionStateStoreData item,
          object lockId,
          bool newItem)
		{
			var getLock = redis.Hashes.GetString (0, lockHashKey, id);
			var lockIdAsString = (string)lockId;
			var ms = new MemoryStream ();
			var writer = new BinaryWriter (ms);

			if (item.Items as SessionStateItemCollection != null)
				((SessionStateItemCollection)item.Items).Serialize (writer);
			
			writer.Close ();

			byte[] sessionData = ms.ToArray ();
			var sessionItemHash = new Dictionary<string, byte[]> ();
			sessionItemHash.Add ("initialize", new byte[] {0});
			sessionItemHash.Add ("data", sessionData);
			
			if (!String.IsNullOrEmpty (getLock.Result) && getLock.Result == lockIdAsString) {
				redis.Hashes.Set (0, GetKeyForSessionId (id), sessionItemHash, false);
				redis.Hashes.Remove (0, lockHashKey, id);
			}
		}

		public override SessionStateStoreData GetItem (HttpContext context, 
            string id, 
            out bool locked, 
            out TimeSpan lockAge, 
            out object lockId, 
            out SessionStateActions actions)
		{
			var getSessionData = redis.Hashes.GetAll (0, GetKeyForSessionId (id));
			locked = false;
			lockAge = new TimeSpan (0);
			lockId = null;
			actions = SessionStateActions.None;
			
			if (getSessionData.Result == null) {
				return null;
			} else {
				var ms = new MemoryStream (getSessionData.Result ["data"]);
				var sessionItems = new SessionStateItemCollection ();

				if (ms.Length > 0) {
					var reader = new BinaryReader (ms);
					sessionItems = SessionStateItemCollection.Deserialize (reader);
				}
				return new SessionStateStoreData (sessionItems,
                    SessionStateUtility.GetSessionStaticObjects (context),
                    (int)sessionStateConfig.Timeout.TotalMinutes);
			}
		}

		public override SessionStateStoreData GetItemExclusive (HttpContext context, 
            string id, 
            out bool locked, 
            out TimeSpan lockAge, 
            out object lockId, 
            out SessionStateActions actions)
		{
			var rawLockData = redis.Hashes.Get (0, lockHashKey, id).Result;
			
			actions = SessionStateActions.None;
			locked = false;
			lockId = null;
			lockAge = TimeSpan.MinValue;
			
			if (rawLockData == null) {
				var lockData = LockData.New ();
				using (var trans = redis.CreateTransaction()) {
					var setLock = trans.Hashes.SetIfNotExists (0, lockHashKey, id, lockData.ToString ());
					var getSessionData = redis.Hashes.GetAll (0, GetKeyForSessionId (id));
					trans.Execute ();
					
					if (setLock.Result) {
						locked = true;
						lockAge = new TimeSpan (0);
						lockId = lockData.LockId;
						var sessionDataHash = getSessionData.Result;
						actions = sessionDataHash ["initialize"] [0] == 1 ? 
								SessionStateActions.InitializeItem : SessionStateActions.None;
						
						var ms = new MemoryStream (sessionDataHash ["data"]);
						var sessionItems = new SessionStateItemCollection ();
		
						if (ms.Length > 0) {
							var reader = new BinaryReader (ms);
							sessionItems = SessionStateItemCollection.Deserialize (reader);
						}
						return new SessionStateStoreData (sessionItems,
		                    SessionStateUtility.GetSessionStaticObjects (context),
		                    (int)sessionStateConfig.Timeout.TotalMinutes);
					} else {
						//TODO
						rawLockData = redis.Hashes.Get (0, lockHashKey, id).Result;
						if (rawLockData != null) {
							if (LockData.TryParse (rawLockData, out lockData)) {
								locked = false;
								lockId = lockData.LockId;
								lockAge = DateTime.UtcNow - lockData.LockUtcTime;
							}
						}
						return null;
					}
				}
			} else {
				LockData lockData;
				if (LockData.TryParse (rawLockData, out lockData)) {
					locked = false;
					lockId = lockData.LockId;
					lockAge = DateTime.UtcNow - lockData.LockUtcTime;
				}
				//Big FAIL
				return null;
			}
			
			//TODO
			//sessionItemHash.Add("lockedTime", BitConverter.GetBytes(DateTime.Now.Ticks/TimeSpan.TicksPerMillisecond));
			//
			
			
			
			//return GetItem(context, id, out locked, out lockAge, out lockId, out actions);
		}
		
		internal struct LockData
		{
			static readonly byte SEPERATOR = Encoding.ASCII.GetBytes (";") [0];
			
			public static LockData New ()
			{
				var data = new LockData ();
				data.LockId = Guid.NewGuid ().ToByteArray ();
				data.LockUtcTime = DateTime.UtcNow;
				return data;
			}

			public static bool TryParse (byte[] raw, out LockData data)
			{
				if (raw.Length > 1) {
					var lockId = raw.TakeWhile (b => b != SEPERATOR).ToArray ();
					var lockTicks = BitConverter.ToInt64(raw.Skip(lockId.Length + 1).ToArray(), 0);
					data = new LockData {
						LockId = lockId,
						LockUtcTime = new DateTime (lockTicks)
					};
					return true;
				}
				data = new LockData ();
				return false;
			}

			public byte[] LockId;
			public DateTime LockUtcTime;

			public override string ToString ()
			{
				return BitConverter.ToString (LockId) + ";" + LockUtcTime.Ticks;
			}

			public byte[] ToByteArray ()
			{
				return LockId.Concat (new byte[] {SEPERATOR}.Concat (BitConverter.GetBytes (LockUtcTime.Ticks))).ToArray ();
			}
		}

		public override SessionStateStoreData CreateNewStoreData (HttpContext context, int timeout)
		{
			return new SessionStateStoreData (
                new SessionStateItemCollection (), 
                SessionStateUtility.GetSessionStaticObjects (context), 
                timeout
            );
		}

		public override void CreateUninitializedItem (HttpContext context, string id, int timeout)
		{
			var ms = new MemoryStream ();
			var writer = new BinaryWriter (ms);
			(new SessionStateItemCollection ()).Serialize (writer);
			writer.Close ();
			byte[] sessionData = ms.ToArray ();
			var newItemHash = new Dictionary<string, byte[]> ();
			newItemHash.Add ("data", sessionData);
			newItemHash.Add ("initialize", new byte[] {1});
			redis.Hashes.Set (0, GetKeyForSessionId (id), newItemHash, false);
		}

		public override void Dispose ()
		{
			IDisposable disposable;
			if ((disposable = redis as IDisposable) != null) {
				disposable.Dispose ();
			}
		}

		public override void InitializeRequest (HttpContext context)
		{
		}

		public override void EndRequest (HttpContext context)
		{
		}

		public override void ReleaseItemExclusive (HttpContext context, string id, object lockId)
		{
			var getLock = redis.Hashes.GetString (0, lockHashKey, id);
			var lockIdAsString = (string)lockId;
			if (!String.IsNullOrEmpty (getLock.Result) && getLock.Result == lockIdAsString) {
				redis.Hashes.Remove (0, lockHashKey, id);
			}
		}

		public override void RemoveItem (HttpContext context, string id, object lockId, SessionStateStoreData item)
		{
			var getLock = redis.Hashes.GetString (0, lockHashKey, id);
			var lockIdAsString = (string)lockId;
			if (!String.IsNullOrEmpty (getLock.Result) && getLock.Result == lockIdAsString) {
				redis.Keys.Remove (0, GetKeyForSessionId (id));
				redis.Hashes.Remove (0, lockHashKey, id);
			}
		}
		
		public override void ResetItemTimeout (HttpContext context, string id)
		{
			//TODO
		}
			
		private string GetKeyForSessionId (string id)
		{
			return this.Name + ":" + id;	
		}
	}
}
