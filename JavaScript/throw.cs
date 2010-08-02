// throw.cs
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
    public sealed class ThrowNode : AstNode
    {
        private AstNode m_operand;

        public ThrowNode(Context context, JSParser parser, AstNode operand)
            : base(context, parser)
        {
            m_operand = operand;
            if (m_operand != null) m_operand.Parent = this;
        }

        public override AstNode Clone()
        {
            return new ThrowNode(
              (Context == null ? null : Context.Clone()),
              Parser,
              (m_operand == null ? null : m_operand.Clone())
              );
        }

        public override IEnumerable<AstNode> Children
        {
            get
            {
                if (m_operand != null)
                {
                    yield return m_operand;
                }
            }
        }

        public override bool ReplaceChild(AstNode oldNode, AstNode newNode)
        {
            if (m_operand == oldNode)
            {
                m_operand = newNode;
                if (newNode != null) { newNode.Parent = this; }
                return true;
            }
            return false;
        }

        internal override bool RequiresSeparator
        {
            get
            {
                // if MacSafariQuirks is true, then we will be adding the semicolon
                // ourselves every single time and won't need outside code to add it.
                // otherwise we won't be adding it, but it will need it if there's something
                // to separate it from.
                return !Parser.Settings.MacSafariQuirks;
            }
        }

        public override string ToCode(ToCodeFormat format)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("throw");
            string exprString = (
              m_operand == null
              ? string.Empty
              : m_operand.ToCode()
              );
            if (exprString.Length > 0)
            {
                if (JSScanner.StartsWithIdentifierPart(exprString))
                {
                    sb.Append(' ');
                }
                sb.Append(exprString);
            }
            if (Parser.Settings.MacSafariQuirks)
            {
                sb.Append(';');
            }
            return sb.ToString();
        }
    }
}