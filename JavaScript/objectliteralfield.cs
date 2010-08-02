// objectliteralfield.cs
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

using System;

namespace Microsoft.Ajax.Utilities
{
    public class ObjectLiteralField : ConstantWrapper
    {
        private bool m_canBeIdentifier;// = false;

        // really basic identifier format. JavaScript identifiers typically have the format:
        // first character is ASCII letter, underscore, or dollar sign.
        // subsequent characters can also contain numbers.
        // they can have many more characters in them, but this is just a simple pattern.
        // anything that doesn't match this pattern will just have quotes put around it.
        //private static Regex s_identifierFormat = new Regex(@"^[a-zA-Z_\$][a-zA-Z_\$0-9]*$");

        public ObjectLiteralField(Object value, bool isNumericLiteral, Context context, JSParser parser)
            : base(value, isNumericLiteral, context, parser)
        {
            // if the value is a string, then we might be able to remove the quotes
            // if it's a simple identifier format and not a keyword.
            if (value is string)
            {
                // get the string object
                string stringValue = value.ToString();
                // if it's a simple format and not a keyword...
                if (JSScanner.IsValidIdentifier(stringValue)/*s_identifierFormat.IsMatch(stringValue)*/ && !JSScanner.IsKeyword(stringValue))
                {
                    // then the string literal could be treated like an identifier
                    m_canBeIdentifier = true;
                }
            }
        }

        internal override void AnalyzeNode()
        {
            // don't call the base -- we don't want to add the literal to
            // the combination logic, which is what the ConstantWrapper (base class) does
            //base.AnalyzeNode();
        }

        public override string ToString()
        {
            if (m_canBeIdentifier)
            {
                // just return the string as an identifier (no quotes)
                return Value.ToString();
            }
            else
            {
                // doing the normal base class call puts quotes around the strings
                return base.ToString();
            }
        }
    }
}
