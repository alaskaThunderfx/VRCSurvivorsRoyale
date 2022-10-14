using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Cyan.CT.Editor
{
    public class CyanTriggerCustomNodeInspectorUtilCommonOptionCache<TKey, TValue>
    {
        private const double CacheInvalidateTime = 5.0;
        
        private readonly Dictionary<TKey, (double, IList<TValue>)> _cache = 
            new Dictionary<TKey, (double, IList<TValue>)>();

        public void AddToCache(TKey key, IList<TValue> values)
        {
            if (key == null)
            {
                return;
            }
            
            _cache[key] = (EditorApplication.timeSinceStartup + Random.value, values);
        }

        public bool TryGetFromCache(TKey key, out IList<TValue> values)
        {
            if (key != null && _cache.TryGetValue(key, out var data))
            {
                if (data.Item1 + CacheInvalidateTime > EditorApplication.timeSinceStartup)
                {
                    values = data.Item2;
                    return true;
                }
                _cache.Remove(key);
            }
            values = null;
            return false;
        }

        public void ClearCache()
        {
            _cache.Clear();
        }
    }
}