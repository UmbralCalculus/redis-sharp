//
// Redis.cs: ECMA CLI Binding to the Redis key-value storage system
//
// Authors:
//   Miguel de Icaza (miguel@gnome.org)
//   Jonathan R. Steele (jrsteele@gmail.com)
//
// Copyright 2010 Novell, Inc.
//
// Licensed under the same terms of reddis: new BSD license.
//
using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;
using System.Linq;
using System.IO;

namespace RedisSharp {
	public class Redis : RedisBase {
		
		private Subscriber subscriptions;
		
			
		public Redis (string host, int port) : base(host, port)
		{  }
		
		public Redis (string host) : this (host, 6379)
		{ }
		
		public Redis () : this ("localhost", 6379) 
		{ }
		
		protected override void Dispose (bool disposing)
		{
			if (subscriptions != null) {
				subscriptions.Dispose();
			}
			
			base.Dispose (disposing);
		}
		
		~Redis() 
		{
			Dispose(false);
		}
	
		public string this [string key] {
			get { return GetString (key); }
			set { Set (key, value); }
		}
	
		public void Set (string key, string value)
		{
			if (key == null)
				throw new ArgumentNullException ("key");
			if (value == null)
				throw new ArgumentNullException ("value");
			
			Set (key, Encoding.UTF8.GetBytes (value));
		}
		
		public void Set (string key, byte [] value)
		{
			if (key == null)
				throw new ArgumentNullException ("key");
			if (value == null)
				throw new ArgumentNullException ("value");
	
			if (value.Length > 1073741824)
				throw new ArgumentException ("value exceeds 1G", "value");
	
			if (!SendDataCommand (value, "SET {0} ", key))
				throw new Exception ("Unable to connect");
			ExpectSuccess ("OK");
		}
	
		public bool SetNX (string key, string value)
		{
			if (key == null)
				throw new ArgumentNullException ("key");
			if (value == null)
				throw new ArgumentNullException ("value");
			
			return SetNX (key, Encoding.UTF8.GetBytes (value));
		}
		
		public bool SetNX (string key, byte [] value)
		{
			if (key == null)
				throw new ArgumentNullException ("key");
			if (value == null)
				throw new ArgumentNullException ("value");
	
			if (value.Length > 1073741824)
				throw new ArgumentException ("value exceeds 1G", "value");
	
			return SendDataExpectInt (value, "SETNX {0} {1}\r\n", key, value.Length) > 0 ? true : false;
		}
	
		public void Set (IDictionary<string,string> dict)
		{
		  Set(dict.ToDictionary(k => k.Key, v => Encoding.UTF8.GetBytes(v.Value)));
		}
	
		public void Set (IDictionary<string,byte []> dict)
		{
			if (dict == null)
				throw new ArgumentNullException ("dict");
	
			var nl = Encoding.UTF8.GetBytes ("\r\n");
	
			var ms = new MemoryStream ();
			foreach (var key in dict.Keys){
				var val = dict [key];
	
				var kLength = Encoding.UTF8.GetBytes ("$" + key.Length + "\r\n");
				var k = Encoding.UTF8.GetBytes (key + "\r\n");
				var vLength = Encoding.UTF8.GetBytes ("$" + val.Length + "\r\n");
				ms.Write (kLength, 0, kLength.Length);
				ms.Write (k, 0, k.Length);
				ms.Write (vLength, 0, vLength.Length);
				ms.Write (val, 0, val.Length);
				ms.Write (nl, 0, nl.Length);
			}
			
			SendDataCommand (ms.ToArray (), "*" + (dict.Count * 2 + 1) + "\r\n$4\r\nMSET\r\n");
			ExpectSuccess ();
		}
	
		public byte [] Get (string key)
		{
			if (key == null)
				throw new ArgumentNullException ("key");
			return SendExpectData (null, "GET " + key + "\r\n");
		}
	
		public string GetString (string key)
		{
			if (key == null)
				throw new ArgumentNullException ("key");
			return Encoding.UTF8.GetString (Get (key));
		}
	
		public byte[][] Sort (SortOptions options)
		{
			return SendDataCommandExpectMultiBulkReply(null, options.ToCommand() + "\r\n");
		}
		
		public byte [] GetSet (string key, byte [] value)
		{
			if (key == null)
				throw new ArgumentNullException ("key");
			if (value == null)
				throw new ArgumentNullException ("value");
			
			if (value.Length > 1073741824)
				throw new ArgumentException ("value exceeds 1G", "value");
	
			if (!SendDataCommand (value, "GETSET {0} ", key))
				throw new Exception ("Unable to connect");
	
			return ReadData ();
		}
	
		public string GetSet (string key, string value)
		{
			if (key == null)
				throw new ArgumentNullException ("key");
			if (value == null)
				throw new ArgumentNullException ("value");
			return Encoding.UTF8.GetString (GetSet (key, Encoding.UTF8.GetBytes (value)));
		}
		
	
		public bool ContainsKey (string key)
		{
			if (key == null)
				throw new ArgumentNullException ("key");
			return SendExpectInt ("EXISTS " + key + "\r\n") == 1;
		}
	
		public bool Remove (string key)
		{
			if (key == null)
				throw new ArgumentNullException ("key");
			return SendExpectInt ("DEL " + key + "\r\n", key) == 1;
		}
	
		public int Remove (params string [] args)
		{
			if (args == null)
				throw new ArgumentNullException ("args");
			return SendExpectInt ("DEL " + string.Join (" ", args) + "\r\n");
		}
	
		public int Increment (string key)
		{
			if (key == null)
				throw new ArgumentNullException ("key");
			return SendExpectInt ("INCR " + key + "\r\n");
		}
	
		public int Increment (string key, int count)
		{
			if (key == null)
				throw new ArgumentNullException ("key");
			return SendExpectInt ("INCRBY {0} {1}\r\n", key, count);
		}
	
		public int Decrement (string key)
		{
			if (key == null)
				throw new ArgumentNullException ("key");
			return SendExpectInt ("DECR " + key + "\r\n");
		}
	
		public int Decrement (string key, int count)
		{
			if (key == null)
				throw new ArgumentNullException ("key");
			return SendExpectInt ("DECRBY {0} {1}\r\n", key, count);
		}
	
		public KeyType TypeOf (string key)
		{
			if (key == null)
				throw new ArgumentNullException ("key");
			switch (SendExpectString ("TYPE {0}\r\n", key)){
			case "none":
				return KeyType.None;
			case "string":
				return KeyType.String;
			case "set":
				return KeyType.Set;
			case "list":
				return KeyType.List;
			}
			throw new ResponseException ("Invalid value");
		}
	
		public string RandomKey ()
		{
			return SendExpectString ("RANDOMKEY\r\n");
		}
	
		public bool Rename (string oldKeyname, string newKeyname)
		{
			if (oldKeyname == null)
				throw new ArgumentNullException ("oldKeyname");
			if (newKeyname == null)
				throw new ArgumentNullException ("newKeyname");
			return SendGetString ("RENAME {0} {1}\r\n", oldKeyname, newKeyname) [0] == '+';
		}
	
		public bool Expire (string key, int seconds)
		{
			if (key == null)
				throw new ArgumentNullException ("key");
			return SendExpectInt ("EXPIRE {0} {1}\r\n", key, seconds) == 1;
		}
	
		public bool ExpireAt (string key, int time)
		{
			if (key == null)
				throw new ArgumentNullException ("key");
			return SendExpectInt ("EXPIREAT {0} {1}\r\n", key, time) == 1;
		}
	
		public int TimeToLive (string key)
		{
			if (key == null)
				throw new ArgumentNullException ("key");
			return SendExpectInt ( "TTL {0}\r\n", key);
		}
		
		public int DbSize {
			get {
				return SendExpectInt ("DBSIZE\r\n");
			}
		}
	
		public string Save ()
		{
			return SendGetString ("SAVE\r\n");
		}
	
		public void BackgroundSave ()
		{
			SendGetString ("BGSAVE\r\n");
		}
	
		public void Shutdown ()
		{
			SendGetString ("SHUTDOWN\r\n");
		}
	
		public void FlushAll ()
		{
			SendGetString ("FLUSHALL\r\n");
		}
		
		public void FlushDb ()
		{
			SendGetString ("FLUSHDB\r\n");
		}
	
		const long UnixEpoch = 621355968000000000L;
		
		public DateTime LastSave {
			get {
				int t = SendExpectInt ("LASTSAVE\r\n");
				
				return new DateTime (UnixEpoch) + TimeSpan.FromSeconds (t);
			}
		}
		
		public string [] Keys {
			get {;
				return GetKeys("*");
			}
		}
	
		public string [] GetKeys (string pattern)
		{
		   if (pattern == null)
				throw new ArgumentNullException ("key");
			
			if (hostInformation["redis_version"][0] == '2') {
				byte[][] response = SendDataCommandExpectMultiBulkReply(null, "KEYS {0}\r\n", pattern);
				List<string> keys = new List<string>();
				
				foreach (byte[] b in response) {
					keys.Add(Encoding.UTF8.GetString(b));
				}
				
				return keys.ToArray();		
			} else {
				var keys = SendExpectData (null, "KEYS {0}\r\n", pattern);
				if (keys.Length == 0)
					return new string [0];
				return Encoding.UTF8.GetString (keys).Split (' ');
			}
		}
	
		public byte [][] GetKeys (params string [] keys)
		{
			if (keys == null)
				throw new ArgumentNullException ("key1");
			if (keys.Length == 0)
				throw new ArgumentException ("keys");
			
			return SendDataCommandExpectMultiBulkReply (null, "MGET {0}\r\n", string.Join (" ", keys));
		}
	
	
		
		#region List commands
				
		public RedisSharp.Collections.RedisList<T> GetListObject<T>(string key)
		{
			return new RedisSharp.Collections.RedisList<T>(key,this.Host,this.Port);			
		}
		
		#endregion
	
		#region Set commands
		public bool AddToSet (string key, byte[] member)
		{
			return SendDataExpectInt(member, "SADD {0} ", key) > 0;
		}
	
		public bool AddToSet (string key, string member)
		{
			return AddToSet (key, Encoding.UTF8.GetBytes(member));
		}
		
		public int CardinalityOfSet (string key)
		{
			return SendDataExpectInt (null, "SCARD {0}\r\n", key);
		}
	
		public bool IsMemberOfSet (string key, byte[] member)
		{
			return SendDataExpectInt (member, "SISMEMBER {0} ", key) > 0;
		}
	
		public bool IsMemberOfSet(string key, string member)
		{
			return IsMemberOfSet(key, Encoding.UTF8.GetBytes(member));
		}
		
		public byte[][] GetMembersOfSet (string key)
		{
			return SendDataCommandExpectMultiBulkReply (null, "SMEMBERS {0}\r\n", key);
		}
		
		public byte[] GetRandomMemberOfSet (string key)
		{
			return SendExpectData (null, "SRANDMEMBER {0}\r\n", key);
		}
		
		public byte[] PopRandomMemberOfSet (string key)
		{
			return SendExpectData (null, "SPOP {0}\r\n", key);
		}
	
		public bool RemoveFromSet (string key, byte[] member)
		{
			return SendDataExpectInt (member, "SREM {0} ", key) > 0;
		}
	
		public bool RemoveFromSet (string key, string member)
		{
			return RemoveFromSet (key, Encoding.UTF8.GetBytes(member));
		}
			
		public byte[][] GetUnionOfSets (params string[] keys)
		{
			if (keys == null)
				throw new ArgumentNullException();
			
			return SendDataCommandExpectMultiBulkReply (null, "SUNION " + string.Join (" ", keys) + "\r\n");
			
		}
		
		void StoreSetCommands (string cmd, string destKey, params string[] keys)
		{
			if (String.IsNullOrEmpty(cmd))
				throw new ArgumentNullException ("cmd");
			
			if (String.IsNullOrEmpty(destKey))
				throw new ArgumentNullException ("destKey");
			
			if (keys == null)
				throw new ArgumentNullException ("keys");
			
			SendExpectSuccess ("{0} {1} {2}\r\n", cmd, destKey, String.Join(" ", keys));
		}
		
		public void StoreUnionOfSets (string destKey, params string[] keys)
		{
			StoreSetCommands ("SUNIONSTORE", destKey, keys);
		}
		
		public byte[][] GetIntersectionOfSets (params string[] keys)
		{
			if (keys == null)
				throw new ArgumentNullException();
			
			return SendDataCommandExpectMultiBulkReply (null, "SINTER " + string.Join(" ", keys) + "\r\n");
		}
		
		public void StoreIntersectionOfSets (string destKey, params string[] keys)
		{
			StoreSetCommands ("SINTERSTORE", destKey, keys);		                 
		}
		
		public byte[][] GetDifferenceOfSets (params string[] keys)
		{
			if (keys == null)
				throw new ArgumentNullException();
			
			return SendDataCommandExpectMultiBulkReply (null, "SDIFF " + string.Join (" ", keys) + "\r\n");
		}
		
		public void StoreDifferenceOfSets (string destKey, params string[] keys)
		{
			StoreSetCommands("SDIFFSTORE", destKey, keys);
		}
		
		public bool MoveMemberToSet (string srcKey, string destKey, byte[] member)
		{
			return SendDataExpectInt(member, "SMOVE {0} {1} ", srcKey, destKey) > 0;
		}
		#endregion
		
		
		#region Publish / Subscribe methods
		
		/// <summary>
		/// Publish data to a given channel
		/// </summary>
		public int Publish (string channel, byte[] data)
		{
			RequireMinimumVersion("2.0.0");
			
			if (channel == null)
				throw new ArgumentNullException();
			
			/* JS (09/26/2010): The result of PUBLISH is the number of clients that receive the data */
			return SendDataExpectInt(data, "PUBLISH {0} {1}\r\n", channel, data.Length);
		}
		
		public int Publish(string channel, string data)
		{
			if (channel == null || data == null)
			    throw new ArgumentNullException();
			
			return Publish(channel, Encoding.UTF8.GetBytes(data));
		}
	
		public void Subscribe(string channel, Action<byte[]> callBack)
		{
			RequireMinimumVersion("2.0.0");
			
			if (subscriptions == null)
				subscriptions = new Subscriber(this.Host, this.Port);
			
			subscriptions.Add(channel,callBack);
						
		}
		
			
		/// <summary>
		/// Unsubscribe from all channels
		/// </summary>
		public void Unsubscribe()
		{
			RequireMinimumVersion("2.0.0");
			
			if (subscriptions == null) 
				return;
			
			subscriptions.RemoveAll();
	
		}
	
		public void PUnsubscribe(string channel)  { Unsubscribe(channel); }
		
		public void Unsubscribe(string channel) 
		{
			RequireMinimumVersion("2.0.0");
			
			if (subscriptions == null) 
				return;
			
			subscriptions.Remove(channel);
		}
		
		#endregion
	
		
	}

}