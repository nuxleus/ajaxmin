// eval.cs
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

namespace Microsoft.Ajax.Utilities
{

    public sealed class EvaluateNode : AstNode
    {
        private AstNode m_operand;

        public EvaluateNode(Context context, JSParser parser, AstNode operand)
            : base(context, parser)
        {
            m_operand = operand;
            if (m_operand != null) operand.Parent = this;
        }

        public override AstNode Clone()
        {
            return new EvaluateNode(
            (Context == null ? null : Context.Clone()),
            Parser,
            (m_operand == null ? null : m_operand.Clone())
            );
        }

        internal override void AnalyzeNode()
        {
            // if the developer hasn't explicitly flagged eval statements as safe...
            if (Parser.Settings.EvalTreatment != EvalTreatment.Ignore)
            {
                // mark this scope as unknown so we don't
                // crunch out locals we might reference in the eval at runtime
                ActivationObject enclosingScope = ScopeStack.Peek();
                if (enclosingScope != null)
                {
                    enclosingScope.IsKnownAtCompileTime = false;
                }
            }
            // then just do the default analysis
            base.AnalyzeNode();
        }

        internal override string GetFunctionGuess(AstNode target)
        {
            return "eval";
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
            return "eval(" + m_operand.ToCode() + ")";
        }
    }
}