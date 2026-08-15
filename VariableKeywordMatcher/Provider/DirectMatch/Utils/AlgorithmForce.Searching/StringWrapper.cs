using System;
using System.Collections;
using System.Collections.Generic;

namespace AlgorithmForce.Searching
{
    /// <summary>
    /// If one day string implements IReadOnlyList{T} we may dig out this class
    /// </summary>
    class StringWrapper : IReadOnlyList<char>
    {
        #region Fields

        private readonly string _str;

        #endregion

        #region Properties

        char IReadOnlyList<char>.this[int index] => _str[index];

        int IReadOnlyCollection<char>.Count => _str.Length;

        #endregion

        #region Constructor

        public StringWrapper(string str)
        {
            _str = str;
        }

        #endregion

        #region Methods

        IEnumerator IEnumerable.GetEnumerator()
        {
            return (_str as IEnumerable<char>).GetEnumerator();
        }

        IEnumerator<char> IEnumerable<char>.GetEnumerator()
        {
            return (_str as IEnumerable<char>).GetEnumerator();
        }

        #endregion

        #region Others

        public override string ToString()
        {
            return _str;
        }

        #endregion
    }
}
