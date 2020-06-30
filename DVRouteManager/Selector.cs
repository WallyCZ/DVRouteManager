using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DVRouteManager
{
    public class Selector<T> : IEnumerator<T>
    {
        private List<T> _items;

        public Selector(List<T> list)
        {
            this._items = list;
        }

        public T Current => _items.ElementAt(Index);

        public int Index { get; private set; } = -1;

        object IEnumerator.Current => Current;

        public void Dispose()
        {
        }

        public bool MoveNext()
        {
            if (_items.Count == 0 || _items.Count <= Index)
            {
                return false;
            }

            Index++;

            return true;
        }
        public void MoveNextRewind()
        {
            Index++;

            if (_items.Count <= Index)
            {
                Index = 0;
            }
        }

        public void MovePrevRewind()
        {
            Index--;

            if (Index < 0)
            {
                Index = _items.Count - 1;
            }
        }

        public void Reset()
        {
            Index = -1;
        }
    }

}
