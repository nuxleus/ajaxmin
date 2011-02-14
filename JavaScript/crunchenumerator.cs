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

        private static string s_varLetters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";

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
            }
            while (m_skipNames.ContainsKey(name) || JSScanner.IsKeyword(name));
            return name;
        }

        private string CurrentName
        {
            get
            {
                int n = m_currentName;
                StringBuilder sb = new StringBuilder();
                do
                {
                    sb.Append(s_varLetters[n % s_varLetters.Length]);
                    n = n / s_varLetters.Length;
                }
                while (n > 0);

                return sb.ToString();
            }
        }

        public static string CrunchedLabel(int nestLevel)
        {
            StringBuilder sb = new StringBuilder();
            while (nestLevel > 0)
            {
                // nestCount is 1-based, so subtract one to make the character index 0-based
                int ndx = (nestLevel % s_varLetters.Length) - 1;
                nestLevel /= s_varLetters.Length;
                sb.Append(s_varLetters[ndx]);
            }
            return sb.ToString();
        }
    }
}
