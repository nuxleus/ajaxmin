// crunchenumerator.cs
//
// Copyright 2010 Microsoft Corporation
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System.Collections.Generic;
using System.Text;

namespace Microsoft.Ajax.Utilities
{
    internal class CrunchEnumerator
    {
        private Dictionary<string, string> m_skipNames;
        private int m_currentName = -1;

        // we essentially take a zero-based integer and turn it into a string identifier by using a base-54
        // digit for the "ones," and base-64 for all digits after that -- then reverse the string (ones first).
        // the first letter is zero-based, so we start with the "a" character for the first variable (index 0)
        private static string s_varFirstLetters = "ntirufeoshclavypwbkdg";//"abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ_$";//"ntirufeoshclavypwbkdgjmqxz";//

        // we don't display "leading zeros" for subsequent letters, so they are 1-based. We only hit the zero position
        // when we roll over into another letter (which also starts at 1). So put the "last" digit we want to use FIRST,
        // and the first one second (and go up from there)
        private static string s_varPartLetters = "ntirufeoshclavypwbkdg";//"$abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789_";//"ntirufeoshclavypwbkdgjmqxz";//

        internal CrunchEnumerator(Dictionary<string, string> avoidNames)
        {
            // just use the dictionary we were passed
            m_skipNames = avoidNames;
        }

        internal CrunchEnumerator(Dictionary<JSVariableField, JSVariableField> verboten)
        {
            // empty hashtable
            m_skipNames = new Dictionary<string, string>(verboten.Count);

            // walk through all the items in verboten
            foreach (var variableField in verboten.Keys)
            {
                string name = variableField.ToString();
                // if the name isn't already in the skipnames hash, add it
                if (!m_skipNames.ContainsKey(name))
                {
                    m_skipNames[name] = name;
                }
            }
        }

        internal string NextName()
        {
            string name;
            do
            {
                // advance to the next name
                ++m_currentName;
                name = CurrentName;
                // keep advancing until we find one that isn't in the skip list or a keyword
                // (use strict mode to be safe)
            }
            while (m_skipNames.ContainsKey(name) || JSScanner.IsKeyword(name, true));
            return name;
        }

        private string CurrentName
        {
            get
            {
                return GenerateNameFromNumber(m_currentName);
            }
        }

        public static string CrunchedLabel(int nestLevel)
        {
            // nestCount is 1-based, so subtract one to make the character index 0-based
            // TODO: make sure the generated name isn't a keyword!
            return GenerateNameFromNumber(nestLevel - 1);
        }

        /// <summary>
        /// get the algorithmically-generated minified variable name based on the given number
        /// zero is the first name, 1 is the next, etc. This method needs to be tuned to
        /// get better gzip results.
        /// </summary>
        /// <param name="num">integer position of the name to retrieve</param>
        /// <returns>minified variable name</returns>
        public static string GenerateNameFromNumber(int num)
        {
            StringBuilder sb = new StringBuilder();

            // get the first number
            sb.Append(s_varFirstLetters[num % s_varFirstLetters.Length]);
            num /= s_varFirstLetters.Length;

            // if we need more than one number, generate them now
            while (num > 0)
            {
                sb.Append(s_varPartLetters[num % s_varPartLetters.Length]);
                num /= s_varPartLetters.Length;
            }

            return sb.ToString();
        }
    }

    /// <summary>
    /// this class is used to sort the crunchable local fields in a scope so that the fields
    /// most in need of crunching get crunched first, therefore having the smallest-length
    /// crunched variable name.
    /// Highest priority are the fields most-often referenced.
    /// Among fields with the same reference count, the longest fields get priority.
    /// Lastly, alphabetize.
    /// </summary>
    internal class ReferenceComparer : IComparer<JSVariableField>
    {
        // singleton instance
        public static readonly IComparer<JSVariableField> Instance = new ReferenceComparer();
        // never instantiate outside this class
        private ReferenceComparer() { }

        #region IComparer<JSVariableField> Members

        /// <summary>
        /// sorting method for fields that will be renamed in the minification process.
        /// The order of the fields determines which minified name it will receive --
        /// the earlier in the list, typically the smaller, more-common the minified name.
        /// Tune this method to get better gzip results.
        /// </summary>
        /// <param name="left"></param>
        /// <param name="right"></param>
        /// <returns></returns>
        public int Compare(JSVariableField left, JSVariableField right)
        {
            /*
            int comparison = 0;
            if (left != right && left != null && right != null)
            {
                comparison = right.RefCount - left.RefCount;
                if (comparison == 0)
                {
                    comparison = right.Name.Length - left.Name.Length;
                    if (comparison == 0)
                    {
                        comparison = string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase);
                    }
                }
            }
            return comparison;
            */

            // same field (or both are null)?
            if (left == right || (left == null && right == null)) return 0;

            // if the left field is null, we want if AFTER the right field (which isn't null)
            if (left == null) return 1;

            // if the right field is null, we want it AFTER the left field (which isn't null)
            if (right == null) return -1;

            // arguments come first, ordered by position. This is an effort to try and make the
            // argument lists for the functions come out in a more repeatable pattern so gzip will
            // compress the file better.
            JSArgumentField leftArg = left as JSArgumentField;
            JSArgumentField rightArg = right as JSArgumentField;
            if (leftArg != null && rightArg != null)
            {
                return leftArg.Position - rightArg.Position;
            }
            if (leftArg != null)
            {
                return -1;
            }
            if (rightArg != null)
            {
                return 1;
            }

            // everything other than args comes next, ordered by refcount
            // (the number of times it's referenced in the code) in DECREASING
            // order. So the variables used most often get named first (presumably
            // with smaller names)
            return right.RefCount - left.RefCount;
        }

        #endregion
    }
}
