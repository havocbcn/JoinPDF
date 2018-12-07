using System;
using System.IO;

namespace JoinPDF
{
    public static class aaa {
     public static T[] Slice<T>(this T[] arr, uint indexFrom, uint indexTo) {
            if (indexFrom > indexTo) {
                throw new ArgumentOutOfRangeException("indexFrom is bigger than indexTo!");
            }

            uint length = indexTo - indexFrom;
            T[] result = new T[length];
            Array.Copy(arr, indexFrom, result, 0, length);

            return result;
        }
    }
}