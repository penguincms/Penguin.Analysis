using System;
using System.Collections.Generic;

public static class Combinations
{
    #region Methods

    public static IEnumerable<T[]> Flatten<T>(this T[][] input)
    {
        T[] result = new T[input.Length];
        int[] indices = new int[input.Length];
        for (int pos = 0, index = 0; ;)
        {
            for (; pos < input.Length; pos++, index = 0)
            {
                indices[pos] = index;
                result[pos] = input[pos][index];
            }
            yield return result;
            do
            {
                if (pos == 0)
                {
                    yield break;
                }

                index = indices[--pos] + 1;
            }
            while (index >= input[pos].Length);
        }
    }

    public static IEnumerable<IEnumerable<T>> Get<T>(IList<T> objs)
    {
        int i = 0;
        double c = Math.Pow(2, objs.Count);

        while (++i <= c)
        {
            List<T> set = new List<T>(objs.Count);

            int l;
            for (l = 0; l < objs.Count; l++)
            {
                if ((i >> l) % 2 == 1)
                {
                    set.Add(objs[l]);
                }
            }

            yield return set;
        }
    }

    #endregion Methods
}