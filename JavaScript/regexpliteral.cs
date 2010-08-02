// regexpliteral.cs
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

using System.Globalization;
using System.Text.RegularExpressions;

namespace Microsoft.Ajax.Utilities
{
    public sealed class RegExpLiteral : AstNode
    {
        private string m_pattern;
        private string m_patternSwitches;

        public RegExpLiteral(string pattern, string patternSwitches, Context context, JSParser parser)
            : base(context, parser)
        {
            m_pattern = pattern;
            m_patternSwitches = patternSwitches;
        }

        public override AstNode Clone()
        {
            return new RegExpLiteral(
              m_pattern,
              m_patternSwitches,
              (Context == null ? null : Context.Clone()),
              Parser
              );
        }

        internal override void AnalyzeNode()
        {
            // verify the syntax
            try
            {
                // just try instantiating a Regex object with this string.
                // if it's invalid, it will throw an exception.
                // we don't need to pass the flags -- we're just interested in the pattern
                Regex re = new Regex(m_pattern, RegexOptions.ECMAScript);

                // basically we have this test here so the re variable is referenced
                // and FxCop won't throw an error. There really aren't any cases where
                // the constructor will return null (other than out-of-memory)
                if (re == null)
                {
                    Context.HandleError(JSError.RegExpSyntax, true);
                }
            }
            catch (System.ArgumentException e)
            {
                System.Diagnostics.Debug.WriteLine(e.ToString());
                Context.HandleError(JSError.RegExpSyntax, true);
            }
            // don't bother calling the base -- there are no children
        }

        public override string ToCode(ToCodeFormat format)
        {
            return string.Format(
              CultureInfo.InvariantCulture,
              "/{0}/{1}",
              m_pattern,
              (m_patternSwitches == null ? string.Empty : m_patternSwitches)
              );
        }
    }
}
