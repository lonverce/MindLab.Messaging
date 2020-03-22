using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace MindLab.Messaging.Internals
{
    class SortedListSlim<T> :IReadOnlyCollection<T>
    {
        private readonly List<T> m_sortedList;
        private readonly IComparer<T> m_valueComparer;

        public SortedListSlim(T firstItem, IComparer<T> comparer) 
            :this(new List<T>{firstItem}, comparer)
        {
        }

        public SortedListSlim(IComparer<T> comparer)
            : this(new List<T>(1), comparer)
        {
        }

        private SortedListSlim(List<T> list, IComparer<T> comparer)
        {
            m_sortedList = list;
            Count = m_sortedList.Count;
            m_valueComparer = comparer ?? throw new ArgumentNullException(nameof(comparer));
        }

        public bool Contains(T item)
        {
            var index = m_sortedList.BinarySearch(item, m_valueComparer);
            return index >= 0;
        }

        public bool TryAppend(T item, out SortedListSlim<T> newSortedList)
        {
            newSortedList = null;
            var index = m_sortedList.BinarySearch(item, m_valueComparer);

            if (index >= 0)
            {
                return false;
            }

            var largerIndex = ~index;
            var newList = new List<T>(m_sortedList.Count+1);

            if (largerIndex > 0)
            {
                newList.AddRange(m_sortedList.Take(largerIndex));
            }
                
            newList.Add(item);

            if (largerIndex < m_sortedList.Count)
            {
                newList.AddRange(m_sortedList.Skip(largerIndex));
            }
                
            newSortedList = new SortedListSlim<T>(newList, m_valueComparer);
            return true;
        }

        public bool TryRemove(T item, out SortedListSlim<T> newSortedList)
        {
            newSortedList = null;
            var index = m_sortedList.BinarySearch(item, m_valueComparer);

            if (index < 0)
            {
                return false;
            }

            var newList = new List<T>(Math.Max(1, m_sortedList.Count - 1));

            if (index > 0)
            {
                newList.AddRange(m_sortedList.Take(index));
            }

            index++;

            if (index < m_sortedList.Count)
            {
                newList.AddRange(m_sortedList.Skip(index));
            }

            newSortedList = new SortedListSlim<T>(newList, m_valueComparer);
            return true;
        }

        public IEnumerator<T> GetEnumerator()
        {
            return m_sortedList.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public int Count { get; }
    }
}
