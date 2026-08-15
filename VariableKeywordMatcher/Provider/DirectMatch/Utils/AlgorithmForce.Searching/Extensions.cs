using System;
using System.Collections.Generic;

namespace AlgorithmForce.Searching
{
    /// <summary>
    /// Provides a set of extensions for searching specified collection in another collection.
    /// </summary>
    public static class Extensions
    {
        #region IReadOnlyList(T) (IndexesOf)

        /// <summary>
        /// Enumerates each zero-based index of all occurrences of the specified collection in this instance
        /// and uses the specified <see cref="IEqualityComparer{T}"/>.
        /// The search starts at a specified position.
        /// </summary>
        /// <param name="s">The current collection.</param>
        /// <param name="t">The collection to seek.</param>
        /// <param name="startIndex">The search starting position.</param>
        /// <param name="comparer">The specified <see cref="IEqualityComparer{T}"/> instance.</param>
        /// <typeparam name="T">The type of element in the collection.</typeparam>
        /// <returns>
        /// The zero-based index positions of value if one or more <paramref name="t"/> are found. 
        /// If <paramref name="t"/> is empty, no indexes will be enumerated.
        /// </returns>
        /// <exception cref="ArgumentNullException"><paramref name="s"/> or <paramref name="t"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="startIndex"/> is less than zero or greater than <see cref="IReadOnlyCollection{T}.Count">Count</see> of <paramref name="s"/>.
        /// </exception>
        public static IEnumerable<int> IndexesOf<T>(this IReadOnlyList<T> s, IReadOnlyList<T> t, int startIndex, IEqualityComparer<T> comparer)
            where T : IEquatable<T>
        {
            Validate(s, t, startIndex);

            if (comparer == null) comparer = EqualityComparer<T>.Default;
            if (t.Count == 1)
                return EnumerateIndexes(s, t[0], startIndex, comparer);
            else
                return EnumerateIndexes(s, t, startIndex, comparer);
        }

        internal static IEnumerable<int> EnumerateIndexes<T>(IReadOnlyList<T> s, IReadOnlyList<T> t, int startIndex, IEqualityComparer<T> comparer)
            where T : IEquatable<T>
        {
            var table = TableBuilder.BuildTable(t, comparer);
            var i = 0;

            while (startIndex + i < s.Count)
            {
                if (comparer.Equals(t[i], s[startIndex + i]))
                {
                    if (i == t.Count - 1)
                    {
                        yield return startIndex;

                        startIndex++;
                        i = 0;
                    }
                    else
                    {
                        i++;
                    }
                }
                else
                {
                    if (table[i] > -1)
                    {
                        startIndex += i;
                        i = table[i];
                    }
                    else
                    {
                        startIndex++;
                        i = 0;
                    }
                }
            }
        }

        #endregion

        #region String (IndexesOf)

        /// <summary>
        /// Enumerates each zero-based index of all occurrences of the string in this instance.
        /// </summary>
        /// <param name="s">The string instance.</param>
        /// <param name="t">The character collection to seek.</param>
        /// <returns>
        /// The zero-based index positions of value if one or more <paramref name="t"/> are found. 
        /// If <paramref name="t"/> is empty, no indexes will be enumerated.
        /// </returns>
        /// <exception cref="ArgumentNullException"><paramref name="s"/> or <paramref name="t"/> is null.</exception>
        public static IEnumerable<int> IndexesOf(this string s, IReadOnlyList<char> t)
        {
            return s.AsReadOnlyList().IndexesOf(t, 0, EqualityComparer<char>.Default);
        }

        #endregion


        #region Wrapper

        /// <summary>
        /// Wrap a string instance as a read-only character collection.
        /// </summary>
        /// <param name="str">The string to be wrapped.</param>
        /// <returns>A wrapped string.</returns>
        public static IReadOnlyList<char> AsReadOnlyList(this string str)
        {
            return str == null ? default(IReadOnlyList<char>) : new StringWrapper(str);
        }

        #endregion

        #region Others

        internal static void Validate<T>(IReadOnlyList<T> s, IReadOnlyList<T> t, int startIndex)
        {
            if (s == null) throw new ArgumentNullException(nameof(s));
            if (t == null) throw new ArgumentNullException(nameof(t));

            if (startIndex < 0)
                throw new ArgumentOutOfRangeException(nameof(startIndex), "Value is less than zero.");

            if (startIndex >= s.Count)
                throw new ArgumentOutOfRangeException(nameof(startIndex), "Value is greater than the length of s.");
        }

        internal static IEnumerable<int> EnumerateIndexes<T>(IReadOnlyList<T> s, T t, int startIndex, IEqualityComparer<T> comparer)
            where T : IEquatable<T>
        {
            for (var i = startIndex; i < s.Count; i++)
            {
                if (comparer.Equals(s[i], t))
                    yield return i;
            }
        }

        #endregion
    }
}