/**
 * @copyright wesley wu 
 * @email jie1975.wu@gmail.com
 */
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace gcsfuse_win
{
    /// <summary>
    /// 缓存的对象
    /// </summary>
    public class CachedObject<T>
    {
        public DateTime expire;
        public T cachedObject;
        public CachedObject(T cachedObject)
        {
            this.cachedObject = cachedObject;
        }
        public CachedObject()
        {
        }
        public CachedObject(T cachedObject, DateTime expire)
        {
            this.cachedObject = cachedObject;
            this.expire = expire;
        }
    }

    /// <summary>
    /// 通用的缓冲类
    /// </summary>
    public class Cache<T> : IDisposable
    {
        //并发缓存store
        private ConcurrentDictionary<string, CachedObject<T>> cacheStore = new ConcurrentDictionary<string, CachedObject<T>>();
        //缓存的容量
        private int Capacity;
        //缓存的Item的过期时间
        private int ExpireTime;
        private bool disposed = false;

        /// <summary>
        /// 初始化
        /// </summary>
        public Cache(int capacity, int expireTime)
        {
            this.Capacity = capacity;
            this.ExpireTime = expireTime;
        }


        /// <summary>
        /// Dispose
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Dispose
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (!disposed)
            {
                disposed = true;

                if (disposing)
                {
                    // Dispose managed resources.
                    cacheStore.Clear();
                }
                // Dispose unmanaged resources
            }
        }

        /// <summary>
        /// 保证缓存的容量
        /// </summary>
        /// <returns></returns>
        private bool trimToCapcity()
        {
            bool result = false;
            if (cacheStore.Count <= Capacity || cacheStore.Count == 0)
            {
                return true;
            }
            foreach (KeyValuePair<string, CachedObject<T>> toRemove in cacheStore)
            {
                CachedObject<T> cachedObject = (CachedObject<T>)toRemove.Value;
                if (cachedObject.expire != null)
                {
                    if (DateTime.Now > cachedObject.expire)
                    {
                        CachedObject<T> ignored;
                        cacheStore.TryRemove(toRemove.Key, out ignored);
                    }
                }
            }
            if (cacheStore.Count <= Capacity)
            {
                result = true;
            }
            return result;

        }

        /// <summary>
        /// 增加或更新缓冲内容
        /// </summary>
        /// <param name="key"></param>
        /// <param name="rawObject"></param>
        public void AddOrUpdate(string key, T rawObject)
        {
            if (disposed) return;
            if (!trimToCapcity())
            {
                throw new ArgumentOutOfRangeException($"Cache is full when puting the key:{key}.");
            }
            DateTime itemEPTime = DateTime.Now.AddSeconds(this.ExpireTime);
            CachedObject<T> cacheObject = new CachedObject<T>(rawObject, itemEPTime);
            if (cacheStore.ContainsKey(key))
            {
                CachedObject<T> ignored;
                cacheStore.TryRemove(key, out ignored);
            }
            cacheStore.TryAdd(key, cacheObject);
        }

        /// <summary>
        /// 获取某项值
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        public void TryGet(string key, out T value)
        {
            if (disposed)
            {
                value = default(T);
                return;
            }
            if (cacheStore.ContainsKey(key))
            {
                CachedObject<T> cachedObject;
                cacheStore.TryGetValue(key, out cachedObject);
                if (cachedObject.expire != null)
                {
                    if (DateTime.Now < cachedObject.expire)
                    {
                        value = cachedObject.cachedObject;
                        return;
                    }
                    else
                    {
                        CachedObject<T> ignored;
                        cacheStore.TryRemove(key, out ignored);
                    }
                }
            }
            value = default(T);
            return;
        }

        /// <summary>
        /// 删除某项
        /// </summary>
        /// <param name="key"></param>
        public void Remove(string key)
        {
            if (disposed) return;
            CachedObject<T> ignored;
            cacheStore.TryRemove(key, out ignored);

        }

        /// <summary>
        /// 是否存在key
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public bool Exists(string key)
        {
            if (disposed) return false;
            if (cacheStore.ContainsKey(key))
            {
                CachedObject<T> cachedObject;
                cacheStore.TryGetValue(key, out cachedObject);
                if (cachedObject.expire != null)
                {
                    if (DateTime.Now < cachedObject.expire)
                    {
                        return true;
                    }
                    else
                    {
                        CachedObject<T> ignored;
                        cacheStore.TryRemove(key, out ignored);
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// 容量
        /// </summary>
        /// <returns></returns>
        public int Count()
        {
            return cacheStore.Count;
        }
    }
}
