using System.Collections.Generic;
using UnityEngine;

namespace Cyan.CT.Editor
{
    public class CyanTriggerDependency<T>
    {
        public readonly T Data;
        public readonly List<CyanTriggerDependency<T>> Dependants = new List<CyanTriggerDependency<T>>();
        public readonly List<CyanTriggerDependency<T>> Dependencies = new List<CyanTriggerDependency<T>>();

        private int _index = -1;
        
        public CyanTriggerDependency(T data)
        {
            Data = data;
        }

        public void AddDependency(CyanTriggerDependency<T> dependency)
        {
            if (dependency == null)
            {
                return;
            }
            
            dependency.Dependants.Add(this);
            Dependencies.Add(dependency);
        }

        public void ClearDependencies()
        {
            Dependants.Clear();
            Dependencies.Clear();
        }

        public static void ClearAllDependencies(List<CyanTriggerDependency<T>> items)
        {
            foreach (var item in items)
            {
                item.ClearDependencies();
            }
        }

        // Assumes no loops or it will clear all if a loop is detected. 
        public static List<T> GetDependencyOrdering(List<CyanTriggerDependency<T>> unsortedItems)
        {
            if (!GetDependencyOrdering(unsortedItems, out var sorted, out _))
            {
                sorted.Clear();
            }
            return sorted;
        }
        
        // Topological sort
        public static bool GetDependencyOrdering(
            List<CyanTriggerDependency<T>> unsortedItems,
            out List<T> sortedItems,
            out List<T> failedToSort)
        {
            Queue<int> free = new Queue<int>();
            int[] depCount = new int[unsortedItems.Count];
            for (int index = 0; index < unsortedItems.Count; ++index)
            {
                var asset = unsortedItems[index];
                asset._index = index;
                int deps = asset.Dependencies.Count;
                depCount[index] = deps;
                if (deps == 0)
                {
                    free.Enqueue(index);
                }
            }

            sortedItems = new List<T>();

            while (free.Count > 0)
            {
                int elem = free.Dequeue();
                var asset = unsortedItems[elem];
                sortedItems.Add(asset.Data);
                Debug.Assert(depCount[elem] == 0, "Adding element to list but it still has dependencies!");

                foreach (var dep in asset.Dependants)
                {
                    int ind = dep._index;
                    --depCount[ind];
                    if (depCount[ind] == 0)
                    {
                        free.Enqueue(ind);
                    }
                }
            }
            
            if (unsortedItems.Count != sortedItems.Count)
            {
                Debug.LogError($"Could not properly sort all elements! {sortedItems.Count}/{unsortedItems.Count}");
                failedToSort = new List<T>();
                
                for (int index = 0; index < unsortedItems.Count; ++index)
                {
                    if (depCount[index] > 0)
                    {
                        failedToSort.Add(unsortedItems[index].Data);
                    }
                }
                return false;
            }

            failedToSort = null;
            return true;
        }
    }
}