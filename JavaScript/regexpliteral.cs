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
    public sealed class RegExpLiteral : Expression
    {
        public string Pattern { get; private set; }
        public string PatternSwitches { get; private set; }

        public RegExpLiteral(string pattern, string patternSwitches, Context context, JSParser parser)
            : base(context, parser)
        {
            Pattern = pattern;
            PatternSwitches = patternSwitches;
        }

        public override void Accept(IVisitor visitor)
        {
            if (visitor != null)
            {
                visitor.Visit(this);
            }
        }

        public override bool IsEquivalentTo(AstNode otherNode)
        {
            var otherRegExp = otherNode as RegExpLiteral;
            return otherRegExp != null
                && string.CompareOrdinal(Pattern, otherRegExp.Pattern) == 0
                && string.CompareOrdinal(PatternSwitches, otherRegExp.PatternSwitches) == 0;
        }

        public override string ToCode(ToCodeFormat format)
        {
            return string.Format(
              CultureInfo.InvariantCulture,
              "/{0}/{1}",
              Pattern,
              (PatternSwitches == null ? string.Empty : PatternSwitches)
              );
        }
    }
}
