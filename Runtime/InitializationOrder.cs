using System;
using System.Collections.Generic;

namespace Kott.Scopes
{
    /// <summary>
    ///     Orders the managers of one scope so that every manager initializes after the same-scope managers it
    ///     injects. Among managers that are ready, list order decides.
    /// </summary>
    internal static class InitializationOrder
    {
        /// <summary>
        ///     For each manager, the indices of the other managers in the list that provide a contract it injects.
        ///     If a contract is provided twice, the first provider counts.
        /// </summary>
        public static List<int>[] GetDependencies(IReadOnlyList<Type> managerTypes)
        {
            Dictionary<Type, int> providers = new();
            for (int i = 0; i < managerTypes.Count; i++)
            {
                foreach (Type contract in Injector.GetContracts(managerTypes[i]))
                {
                    providers.TryAdd(contract, i);
                }
            }

            List<int>[] dependencies = new List<int>[managerTypes.Count];
            for (int i = 0; i < managerTypes.Count; i++)
            {
                dependencies[i] = new List<int>();
                foreach (InjectedField field in Injector.GetInfo(managerTypes[i]).Fields)
                {
                    if (providers.TryGetValue(field.Field.FieldType, out int provider) && provider != i &&
                        !dependencies[i].Contains(provider))
                    {
                        dependencies[i].Add(provider);
                    }
                }
            }

            return dependencies;
        }

        /// <summary>Computes the initialization order of <paramref name="managerTypes" />.</summary>
        /// <param name="managerTypes">The manager types in list order.</param>
        /// <param name="order">Indices into <paramref name="managerTypes" /> in initialization order; <c>null</c> on a cycle.</param>
        /// <param name="cycle">On failure, the indices forming a dependency cycle, starting and ending with the same index.</param>
        /// <returns>Whether the managers could be ordered (no dependency cycle).</returns>
        public static bool TryCompute(IReadOnlyList<Type> managerTypes, out List<int> order, out List<int> cycle)
        {
            List<int>[] dependencies = GetDependencies(managerTypes);
            bool[] done = new bool[managerTypes.Count];
            order = new List<int>(managerTypes.Count);

            while (order.Count < managerTypes.Count)
            {
                int next = -1;
                for (int i = 0; i < managerTypes.Count && next < 0; i++)
                {
                    if (!done[i] && dependencies[i].TrueForAll(dependency => done[dependency]))
                    {
                        next = i;
                    }
                }

                if (next < 0)
                {
                    order = null;
                    cycle = FindCycle(dependencies, done);
                    return false;
                }

                done[next] = true;
                order.Add(next);
            }

            cycle = null;
            return true;
        }

        /// <summary>Formats a cycle from <see cref="TryCompute" /> as "A → B → A".</summary>
        public static string DescribeCycle(IReadOnlyList<Type> managerTypes, List<int> cycle)
        {
            return string.Join(" → ", cycle.ConvertAll(index => managerTypes[index].Name));
        }

        private static List<int> FindCycle(List<int>[] dependencies, bool[] done)
        {
            // every remaining manager waits for another remaining one, so following those edges must loop
            List<int> path = new();
            int current = Array.IndexOf(done, false);
            while (!path.Contains(current))
            {
                path.Add(current);
                current = dependencies[current].Find(dependency => !done[dependency]);
            }

            List<int> cycle = path.GetRange(path.IndexOf(current), path.Count - path.IndexOf(current));
            cycle.Add(current);
            return cycle;
        }
    }
}
