using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace MindLab.Messaging.Internals
{
    internal class SortedListSlim<T> :IReadOnlyCollection<T>
    {
        private readonly List<KeyValuePair<int, T>> m_sortedList;
        private readonly IEqualityComparer<T> m_valueComparer;
        private static readonly HashPairComparer _pairComparer = new HashPairComparer();
        
        private readonly IHashCodeGenerator<T> m_codeGenerator;

        private class HashPairComparer : IComparer<KeyValuePair<int, T>>
        {
            public int Compare(KeyValuePair<int, T> x, KeyValuePair<int, T> y)
            {
                return x.Key - y.Key;
            }
        }

        public SortedListSlim(T firstItem, 
            IEqualityComparer<T> comparer,
            IHashCodeGenerator<T> hashCodeGenerator) 
            :this(new List<KeyValuePair<int, T>>
            {
                new KeyValuePair<int, T>(hashCodeGenerator.GetHashCode(firstItem), firstItem)
            }, comparer, hashCodeGenerator)
        {
        }

        public SortedListSlim(
            IEqualityComparer<T> comparer,
            IHashCodeGenerator<T> hashCodeGenerator)
            : this(new List<KeyValuePair<int, T>>(1), comparer, hashCodeGenerator)
        {
        }

        private SortedListSlim(
            List<KeyValuePair<int, T>> list, 
            IEqualityComparer<T> comparer, 
            IHashCodeGenerator<T> hashCodeGenerator)
        {
            m_sortedList = list;
            Count = m_sortedList.Count;
            m_codeGenerator = hashCodeGenerator ?? throw new ArgumentNullException(nameof(comparer));
            m_valueComparer = comparer ?? throw new ArgumentNullException(nameof(comparer));
        }

        private bool IsExists(KeyValuePair<int, T> item, ref int index)
        {
            for (int i = index; i < m_sortedList.Count; i++)
            {
                var p = m_sortedList[i];
                if (item.Key != p.Key)
                {
                    break;
                }

                if (m_valueComparer.Equals(p.Value, item.Value))
                {
                    index = i;
                    return true;
                }
            }

            for (int i = index - 1; i > -1; i--)
            {
                var p = m_sortedList[i];
                if (item.Key != p.Key)
                {
                    break;
                }

                if (m_valueComparer.Equals(p.Value, item.Value))
                {
                    index = i;
                    return true;
                }
            }

            return false;
        }

        public bool TryAppend(T item, out SortedListSlim<T> newSortedList)
        {
            newSortedList = null;
            var pair = new KeyValuePair<int,T>(m_codeGenerator.GetHashCode(item), item);
            var index = m_sortedList.BinarySearch(pair, _pairComparer);

            if (index >= 0)
            {
                // 有可能是哈希冲突
                if (IsExists(pair, ref index))
                {
                    return false;
                }

                index = ~index;
            }

            var largerIndex = ~index;
            var newList = new List<KeyValuePair<int, T>>(m_sortedList.Count+1);

            if (largerIndex > 0)
            {
                newList.AddRange(m_sortedList.Take(largerIndex));
            }
                
            newList.Add(pair);

            if (largerIndex < m_sortedList.Count)
            {
                newList.AddRange(m_sortedList.Skip(largerIndex));
            }
                
            newSortedList = new SortedListSlim<T>(newList, m_valueComparer, m_codeGenerator);
            return true;
        }

        public bool TryRemove(T item, out SortedListSlim<T> newSortedList)
        {
            newSortedList = null;
            var pair = new KeyValuePair<int, T>(m_codeGenerator.GetHashCode(item), item);
            var index = m_sortedList.BinarySearch(pair, _pairComparer);

            if (index < 0)
            {
                return false;
            }

            // 有可能是哈希冲突
            if (!IsExists(pair, ref index))
            {
                return false;
            }

            var newList = new List<KeyValuePair<int, T>>(Math.Max(1, m_sortedList.Count - 1));

            if (index > 0)
            {
                newList.AddRange(m_sortedList.Take(index));
            }

            index++;

            if (index < m_sortedList.Count)
            {
                newList.AddRange(m_sortedList.Skip(index));
            }

            newSortedList = new SortedListSlim<T>(newList, m_valueComparer, m_codeGenerator);
            return true;
        }

        public IEnumerator<T> GetEnumerator()
        {
            return m_sortedList.Select(pair => pair.Value).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public int Count { get; }
    }
}
