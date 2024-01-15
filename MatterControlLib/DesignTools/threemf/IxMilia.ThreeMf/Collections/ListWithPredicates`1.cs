using System;
using System.Collections;
using System.Collections.Generic;

namespace IxMilia.ThreeMf.Collections
{
    internal class ListNonNull<T> : ListWithPredicates<T>
    {
        public ListNonNull()
            : base(item => item != null, 0)
        {
        }
    }

    internal class ListNonNullWithMinimum<T> : ListWithPredicates<T>
    {
        public ListNonNullWithMinimum(int minimum)
            : base(item => item != null, minimum, false)
        {
        }
    }

    internal class ListWithPredicates<T> : IList<T>
    {
        private List<T> _items = new List<T>();
        public Func<T, bool> ItemPredicate { get; }
        public int MinimumCount { get; }

        public ListWithPredicates(Func<T, bool> itemPredicate, int minimumCount, params T[] initialItems)
            : this(itemPredicate, minimumCount, true, initialItems)
        {
        }

        public ListWithPredicates(Func<T, bool> itemPredicate, int minimumCount, bool validateInitialCount, params T[] initialItems)
        {
            ItemPredicate = itemPredicate;
            MinimumCount = minimumCount;
            foreach (var item in initialItems)
            {
                Add(item);
            }

            if (validateInitialCount)
            {
                ValidateCount();
            }
        }

        private void ValidatePredicate(T item)
        {
            if (ItemPredicate != null && !ItemPredicate(item))
            {
                throw new InvalidOperationException("Item does not meet the criteria to be added to this collection.");
            }
        }

        public void ValidateCount()
        {
            if (Count < MinimumCount)
            {
                throw new InvalidOperationException($"This collection must contain at least {MinimumCount} items at all times.");
            }
        }

        public T this[int index]
        {
            get { return _items[index]; }
            set
            {
                ValidatePredicate(value);
                _items[index] = value;
            }
        }

        public int Count => _items.Count;
        public bool IsReadOnly => false;

        public void Add(T item)
        {
            ValidatePredicate(item);
            _items.Add(item);
        }

        public void Clear()
        {
            _items.Clear();
            ValidateCount();
        }

        public bool Contains(T item) => _items.Contains(item);
        public void CopyTo(T[] array, int arrayIndex) => _items.CopyTo(array, arrayIndex);
        public IEnumerator<T> GetEnumerator() => _items.GetEnumerator();
        public int IndexOf(T item) => _items.IndexOf(item);

        public void Insert(int index, T item)
        {
            ValidatePredicate(item);
            _items.Insert(index, item);
        }

        public bool Remove(T item)
        {
            var result = _items.Remove(item);
            ValidateCount();
            return result;
        }

        public void RemoveAt(int index)
        {
            _items.RemoveAt(index);
            ValidateCount();
        }

        IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)_items).GetEnumerator();
    }
}
