using System.Collections.Generic;

namespace Cyan.CT
{
    public class CyanTriggerTrie<TKey,TVal>
    {
        protected readonly Dictionary<TKey, CyanTriggerTrie<TKey,TVal>> Children = 
            new Dictionary<TKey, CyanTriggerTrie<TKey,TVal>>();
        private TVal _data;
        private bool _hasData;

        public CyanTriggerTrie<TKey, TVal> Get(TKey key)
        {
            Children.TryGetValue(key, out CyanTriggerTrie<TKey,TVal> value);
            return value;
        }

        private void Set(TKey key, CyanTriggerTrie<TKey, TVal> value)
        {
            Children.Add(key, value);
        }

        public bool TryGetData(out TVal data)
        {
            data = _data;
            return _hasData;
        }

        public void SetData(TVal data)
        {
            _data = data;
            _hasData = true;
        }

        public void ClearData()
        {
            _data = default;
            _hasData = false;
        }

        protected virtual CyanTriggerTrie<TKey, TVal> CreateChild(TKey key)
        {
            return new CyanTriggerTrie<TKey, TVal>();
        }
        
        public void AddToTrie(IEnumerable<TKey> stream, TVal data)
        {
            CyanTriggerTrie<TKey, TVal> cur = this;
            foreach (var element in stream)
            {
                var next = cur.Get(element);
                if (next == null)
                {
                    next = CreateChild(element);
                    cur.Set(element, next);
                }
                cur = next;
            }

            cur.SetData(data);
        }

        public bool GetFromTrie(IEnumerable<TKey> stream, out TVal data, bool startsWith = false)
        {
            CyanTriggerTrie<TKey, TVal> cur = this;
            foreach (var element in stream)
            {
                var next = cur.Get(element);
                if (next == null)
                {
                    if (startsWith)
                    {
                        return cur.TryGetData(out data);
                    }
                    data = default;
                    return false;
                }
                cur = next;
            }
            return cur.TryGetData(out data);
        }
    }
}