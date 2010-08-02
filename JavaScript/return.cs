// return.cs
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
    public sealed class ReturnNode : AstNode
    {
        private AstNode m_operand;
        public AstNode Operand { get { return m_operand; } }

        public ReturnNode(Context context, JSParser parser, AstNode operand)
            : base(context, parser)
        {
            m_operand = operand;
            if (m_operand != null) { m_operand.Parent = this; }
        }

        public override AstNode Clone()
        {
            return new ReturnNode(
                (Context == null ? null : Context.Clone()),
                Parser,
                (m_operand == null ? null : m_operand.Clone())
                );
        }

        internal override string GetFunctionGuess(AstNode target)
        {
            return "return";
        }

        internal override void AnalyzeNode()
        {
            // first we want to make sure that we are indeed within a function scope.
            // it makes no sense to have a return outside of a function
            ActivationObject scope = ScopeStack.Peek();
            while (scope != null && !(scope is FunctionScope))
            {
                scope = scope.Parent;
            }
            if (scope == null)
            {
                Context.HandleError(JSError.BadReturn);
            }

            // now just do the default analyze
            base.AnalyzeNode();
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

        public override string ToCode(ToCodeFormat format)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("return");
            if (m_operand != null)
            {
                string operandText = m_operand.ToCode();
                if (operandText.Length > 0 && JSScanner.StartsWithIdentifierPart(operandText))
                {
                    sb.Append(' ');
                }
                sb.Append(operandText);
            }
            return sb.ToString();
        }
    }
}
