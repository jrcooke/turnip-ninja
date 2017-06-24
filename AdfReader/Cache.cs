using System;
using System.Collections.Generic;
using System.Linq;

namespace AdfReader
{
    public class Cache<T, U> where U : class
    {
        private TimeSpan purgeAge;
        private Dictionary<T, LastAccessedHolder> chunkCache = new Dictionary<T, LastAccessedHolder>();
        private object locker = new object();

        public Cache(TimeSpan purgeAge)
        {
            this.purgeAge = purgeAge;
        }

        internal void Add(T key, U value)
        {
            lock (locker)
            {
                chunkCache.Add(key, new LastAccessedHolder(value));

                // Check to see if anything needs to be removed
                var keysToKeep = chunkCache.Where(p => p.Value.LastAccessed > DateTime.Now.Subtract(purgeAge)).Select(p => p.Key).ToArray();

                if (keysToKeep.Length < chunkCache.Count)
                {
                    Console.WriteLine("Removing " + (chunkCache.Count - keysToKeep.Length) + " items from cache");
                    // If any expired, make a new clone with those removed.
                    Dictionary<T, LastAccessedHolder> chunkCache2 = new Dictionary<T, LastAccessedHolder>();

                    foreach (var currentKey in keysToKeep)
                    {
                        chunkCache2.Add(currentKey, chunkCache[currentKey]);
                    }

                    // Now swap
                    chunkCache = chunkCache2;
                }
            }
        }

        public bool TryGetValue(T key, out U value)
        {
            LastAccessedHolder value2;
            bool success = chunkCache.TryGetValue(key, out value2);
            value = value2 == null ? null : value2.Value;
            return success;
        }

        private class LastAccessedHolder
        {
            private U value;

            public LastAccessedHolder(U value)
            {
                LastAccessed = DateTime.Now;
                this.value = value;
            }

            public DateTime LastAccessed { get; private set; }
            public U Value
            {
                get
                {
                    LastAccessed = DateTime.Now;
                    return value;
                }
            }
        }
    }

}
