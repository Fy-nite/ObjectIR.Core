// Compatibility shims for building against .NET Framework 4.7
// Included only for the 'net47' target via the csproj ItemGroup condition.

global using System;
global using System.IO;
global using System.Collections.Generic;
global using System.Linq;

namespace System
{
    public readonly struct Index
    {
        public Index(int value, bool fromEnd = false)
        {
            Value = value;
            FromEnd = fromEnd;
        }

        public Index(int value) : this(value, false) { }

        public int Value { get; }
        public bool FromEnd { get; }

        public int GetOffset(int length) => FromEnd ? length - Value : Value;

        public static implicit operator Index(int value) => new Index(value);

        public override string ToString() => FromEnd ? $"^{Value}" : Value.ToString();
    }

    public readonly struct Range
    {
        public Range(Index start, Index end)
        {
            Start = start;
            End = end;
        }

        public Index Start { get; }
        public Index End { get; }

        public override string ToString() => $"{Start}:{End}";
    }
}

namespace System.Runtime.CompilerServices
{
    public static class RuntimeHelpers
    {
        public static T[] GetSubArray<T>(T[] array, global::System.Range range)
        {
            if (array == null) throw new ArgumentNullException(nameof(array));
            var start = range.Start.GetOffset(array.Length);
            var end = range.End.GetOffset(array.Length);
            var len = Math.Max(0, end - start);
            var result = new T[len];
            Array.Copy(array, start, result, 0, len);
            return result;
        }
    }
}

namespace System.Collections.Generic
{
    public static class KeyValuePairExtensions
    {
        public static void Deconstruct<TKey, TValue>(this KeyValuePair<TKey, TValue> kvp, out TKey key, out TValue value)
        {
            key = kvp.Key;
            value = kvp.Value;
        }
    }

    public static class TupleDeconstructExtensions
    {
        public static void Deconstruct<T1, T2>(this Tuple<T1, T2> t, out T1 item1, out T2 item2)
        {
            item1 = t.Item1;
            item2 = t.Item2;
        }
    }
}
