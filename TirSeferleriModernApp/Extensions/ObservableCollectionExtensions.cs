using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace TirSeferleriModernApp.Extensions
{
    public static class ObservableCollectionExtensions
    {
        public static void ReplaceAll<T>(this ObservableCollection<T> collection, IEnumerable<T> newItems)
        {
            if (collection == null || newItems == null) return;

            collection.Clear();
            foreach (var item in newItems)
            {
                collection.Add(item);
            }
        }
    }
}