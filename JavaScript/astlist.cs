// astlist.cs
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
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Ajax.Utilities
{

    public sealed class AstNodeList : AstNode
    {
        private List<AstNode> m_list;

        public AstNodeList(Context context, JSParser parser)
            : base(context, parser)
        {
            m_list = new List<AstNode>();
        }

        public override AstNode Clone()
        {
            // create a new empty list
            AstNodeList newList = new AstNodeList((Context == null ? null : Context.Clone()), Parser);
            // and clone all the items into it (skipping nulls)
            for (int ndx = 0; ndx < m_list.Count; ++ndx)
            {
                if (m_list[ndx] != null)
                {
                    newList.Append(m_list[ndx].Clone());
                }
            }
            return newList;
        }

        public int Count
        {
            get { return m_list.Count; }
        }

        public override IEnumerable<AstNode> Children
        {
            get
            {
                return EnumerateNonNullNodes(m_list);
            }
        }

        public override bool ReplaceChild(AstNode oldNode, AstNode newNode)
        {
            for (int ndx = 0; ndx < m_list.Count; ++ndx)
            {
                if (m_list[ndx] == oldNode)
                {
                    if (newNode == null)
                    {
                        // remove it
                        m_list.RemoveAt(ndx);
                    }
                    else
                    {
                        // replace with the new node
                        m_list[ndx] = newNode;
                        newNode.Parent = this;
                    }
                    return true;
                }
            }
            return false;
        }

        public override string ToCode(ToCodeFormat format)
        {
            bool requiresSeparator = false;
            string separator;
            switch (format)
            {
                case ToCodeFormat.Commas:
                    separator = ",";
                    break;

                case ToCodeFormat.Semicolons:
                    separator = ";";
                    break;

                default:
                    separator = string.Empty;
                    break;
            }

            StringBuilder sb = new StringBuilder();
            for (int ndx = 0; ndx < m_list.Count; ++ndx)
            {
                // the crunched code for the item, passing in the separator
                // we are using (in case it affects the code by needing to add parens or something)
                string itemText = m_list[ndx].ToCode(format);

                // see if we need to add the separator
                if (separator.Length > 0)
                {
                    // don't add it if this is the first item; we only need it between them
                    // (and only then if it's required)
                    if (ndx > 0 && requiresSeparator)
                    {
                        sb.Append(separator);
                    }

                    // see if we'll need one for the next iteration (if any).
                    // if we're separating with commas, then we will always add a comma unless there already is one.
                    // otherwise we'll only add a separator if there isn't already one AND we require a separator.
                    requiresSeparator = !itemText.EndsWith(separator, StringComparison.Ordinal)
                      && (format == ToCodeFormat.Commas || m_list[ndx].RequiresSeparator);
                }

                // add the item to the stream
                sb.Append(itemText);
            }
            return sb.ToString();
        }

        internal AstNodeList Append(AstNode astNode)
        {
            astNode.Parent = this;
            m_list.Add(astNode);
            Context.UpdateWith(astNode.Context);
            return this;
        }

        internal void RemoveAt(int position)
        {
            m_list.RemoveAt(position);
        }

        public AstNode this[int index]
        {
            get
            {
                return m_list[index];
            }
        }

        public bool IsSingleConstantArgument(string argumentValue)
        {
            if (m_list.Count == 1)
            {
                ConstantWrapper constantWrapper = m_list[0] as ConstantWrapper;
                if (constantWrapper != null 
                    && string.CompareOrdinal(constantWrapper.Value.ToString(), argumentValue) == 0)
                {
                    return true;
                }
            }
            return false;
        }

        public string SingleConstantArgument
        {
            get
            {
                string constantValue = null;
                if (m_list.Count == 1)
                {
                    ConstantWrapper constantWrapper = m_list[0] as ConstantWrapper;
                    if (constantWrapper != null)
                    {
                        constantValue = constantWrapper.ToString();
                    }
                }
                return constantValue;
            }
        }
    }
}
