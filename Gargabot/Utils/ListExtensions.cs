using System;
using System.Collections.Generic;

public static class ListExtensions
{
    private static Random random = new Random();

    public static void Shuffle<T>(this IList<T> list)
    {
        int n = list.Count;
        for (int i = 0; i < n - 1; i++)
        {
            int j = random.Next(i, n);
            T temp = list[i];
            list[i] = list[j];
            list[j] = temp;
        }
    }
}
