// objectliteral.cs
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
    public sealed class ObjectLiteral : AstNode
    {
        private ObjectLiteralField[] m_keys;
        //public ObjectLiteralField[] Keys { get { return m_keys; } }

        private AstNode[] m_values;
        //public AstNode[] Values { get { return m_values; } }

        // return the length of the keys, since we can't set the keys or values with differing lengths,
        // returning one should be the same for both
        public int Count { get { return m_keys.Length; } }

        public ObjectLiteral(Context context, JSParser parser, ObjectLiteralField[] keys, AstNode[] values)
            : base(context, parser)
        {
            // the length of keys and values should be identical.
            // if either is null, or if the lengths don't match, we ignore both!
            if (keys == null || values == null || keys.Length != values.Length)
            {
                // allocate EMPTY arrays so we don't have to keep checking for nulls
                m_keys = new ObjectLiteralField[0];
                m_values = new AstNode[0];
            }
            else
            {
                // copy the arrays
                m_keys = keys;
                m_values = values;

                // make sure the parents are set properly
                foreach (AstNode astNode in keys)
                {
                    astNode.Parent = this;
                }
                foreach (AstNode astNode in values)
                {
                    astNode.Parent = this;
                }
                // because we don't ensure that the arrays are the same length, we'll need to
                // check for the minimum length every time we iterate over them
            }
        }

        public override AstNode Clone()
        {
            // we need to manually clone the keys and values, so determine
            // the minimum length, allocate the arrays, and clone each item
            int count = (m_keys.Length < m_values.Length ? m_keys.Length : m_values.Length);
            ObjectLiteralField[] clonedKeys = new ObjectLiteralField[count];
            AstNode[] clonedValues = new AstNode[count];
            for (int ndx = 0; ndx < count; ++ndx)
            {
                clonedKeys[ndx] = (ObjectLiteralField)m_keys[ndx].Clone();
                clonedValues[ndx] = m_values[ndx].Clone();
            }

            return new ObjectLiteral(
                (Context == null ? null : Context.Clone()),
                Parser,
                clonedKeys,
                clonedValues
                );
        }

        public override IEnumerable<AstNode> Children
        {
            get
            {
                // the lengths should be the same, but just in case, use the minimum
                int count = (m_keys.Length < m_values.Length ? m_keys.Length : m_values.Length);
                for (int ndx = 0; ndx < count; ++ndx)
                {
                    yield return m_keys[ndx];
                    yield return m_values[ndx];
                }
            }
        }

        public override bool ReplaceChild(AstNode oldNode, AstNode newNode)
        {
            // they're both created at the same time, so they should both be non-null.
            // assumption: they should also have the same number of members in them!
            int count = (m_keys.Length < m_values.Length ? m_keys.Length : m_values.Length);
            for (int ndx = 0; ndx < count; ++ndx)
            {
                if (m_keys[ndx] == oldNode)
                {
                    m_keys[ndx] = newNode as ObjectLiteralField;
                    if (newNode != null) { newNode.Parent = this; }
                    return true;
                }
                if (m_values[ndx] == oldNode)
                {
                    m_values[ndx] = newNode;
                    if (newNode != null) { newNode.Parent = this; }
                    return true;
                }
            }
            return false;
        }

        public override string ToCode(ToCodeFormat format)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append('{');

            int count = (m_keys.Length < m_values.Length ? m_keys.Length : m_values.Length);
            if (count > 0)
            {
                Parser.Settings.Indent();
                for (int ndx = 0; ndx < count; ++ndx)
                {
                    if (ndx > 0)
                    {
                        sb.Append(',');
                    }

                    Parser.Settings.NewLine(sb);
                    sb.Append(m_keys[ndx].ToCode());
                    if (m_keys[ndx] is GetterSetter)
                    {
                        sb.Append(m_values[ndx].ToCode(ToCodeFormat.NoFunction));
                    }
                    else
                    {
                        // the key is always an identifier, string or numeric literal
                        sb.Append(':');
                        sb.Append(m_values[ndx].ToCode());
                    }
                }

                Parser.Settings.Unindent();
                Parser.Settings.NewLine(sb);
            }

            sb.Append('}');
            return sb.ToString();
        }

        internal override string GetFunctionGuess(AstNode target)
        {
            // walk the values until we find the target, then return the key
            int count = (m_keys.Length < m_values.Length ? m_keys.Length : m_values.Length);
            for (int ndx = 0; ndx < count; ++ndx)
            {
                if (m_values[ndx] == target)
                {
                    // we found it -- return the corresponding key (converted to a string)
                    return m_keys[ndx].ToString();
                }
            }
            // if we get this far, we didn't find it
            return string.Empty;
        }
    }
}

