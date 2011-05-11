// forin.cs
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
    public sealed class ForIn : AstNode
    {
        private AstNode m_var;
        private AstNode m_collection;
        private Block m_body;

        public ForIn(Context context, JSParser parser, AstNode var, AstNode collection, AstNode body)
            : base(context, parser)
        {
            m_var = var;
            m_collection = collection;
            m_body = ForceToBlock(body);
            if (m_body != null) m_body.Parent = this;
            if (m_var != null) m_var.Parent = this;
            if (m_collection != null) m_collection.Parent = this;
        }

        public override AstNode Clone()
        {
            return new ForIn(
                (Context == null ? null : Context.Clone()),
                Parser,
                (m_var == null ? null : m_var.Clone()),
                (m_collection == null ? null : m_collection.Clone()),
                (m_body == null ? null : m_body.Clone())
                );
        }

        internal override void AnalyzeNode()
        {
            // if we are stripping debugger statements and the body is
            // just a debugger statement, replace it with a null
            if (Parser.Settings.StripDebugStatements
                 && Parser.Settings.IsModificationAllowed(TreeModifications.StripDebugStatements) 
                 && m_body != null 
                 && m_body.IsDebuggerStatement)
            {
                m_body = null;
            }

            // recurse
            base.AnalyzeNode();

            // if the body is now empty, make it null
            if (m_body != null && m_body.Count == 0)
            {
                m_body = null;
            }
        }

        public override IEnumerable<AstNode> Children
        {
            get
            {
                return EnumerateNonNullNodes(m_var, m_collection, m_body);
            }
        }

        public override bool ReplaceChild(AstNode oldNode, AstNode newNode)
        {
            if (m_var == oldNode)
            {
                m_var = newNode;
                if (newNode != null) { newNode.Parent = this; }
                return true;
            }
            if (m_collection == oldNode)
            {
                m_collection = newNode;
                if (newNode != null) { newNode.Parent = this; }
                return true;
            }
            if (m_body == oldNode)
            {
                m_body = ForceToBlock(newNode);
                if (m_body != null) { m_body.Parent = this; }
                return true;
            }
            return false;
        }

        internal override bool EncloseBlock(EncloseBlockType type)
        {
            // pass the query on to the body
            return m_body == null ? false : m_body.EncloseBlock(type);
        }

        internal override bool RequiresSeparator
        {
            get
            {
                // requires a separator if the body does
                return m_body == null ? true : m_body.RequiresSeparator;
            }
        }

        internal override bool EndsWithEmptyBlock
        {
            get
            {
                return m_body == null ? true : m_body.EndsWithEmptyBlock;
            }
        }

        public override string ToCode(ToCodeFormat format)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("for(");

            string var = m_var.ToCode();
            sb.Append(var);
            if (JSScanner.EndsWithIdentifierPart(var))
            {
                sb.Append(' ');
            }
            sb.Append("in");

            string collection = m_collection.ToCode();
            if (JSScanner.StartsWithIdentifierPart(collection))
            {
                sb.Append(' ');
            }
            sb.Append(m_collection.ToCode());
            sb.Append(')');

            string bodyString = (
              m_body == null
              ? string.Empty
              : m_body.ToCode()
              );
            sb.Append(bodyString);
            return sb.ToString();
        }
    }
}
