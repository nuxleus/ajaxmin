// unaryop.cs
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
    public abstract class UnaryOperator : Expression
    {
        public AstNode Operand { get; private set; }

        public JSToken OperatorToken { get; private set; }

        protected UnaryOperator(Context context, JSParser parser, AstNode operand, JSToken operatorToken)
            : base(context, parser)
        {
            Operand = operand;
            OperatorToken = operatorToken;
            if (Operand != null) Operand.Parent = this;
        }

        internal override string GetFunctionGuess(AstNode target)
        {
            return Operand.GetFunctionGuess(target);
        }

        public override IEnumerable<AstNode> Children
        {
            get
            {
                return EnumerateNonNullNodes(Operand);
            }
        }

        public override bool ReplaceChild(AstNode oldNode, AstNode newNode)
        {
            if (Operand == oldNode)
            {
                Operand = newNode;
                if (newNode != null) { newNode.Parent = this; }
                return true;
            }
            return false;
        }

        protected bool NeedsParentheses
        {
            // binary and conditional operators are all lesser-precedence than unaries
            get
            {
                // we only need parens if the operand is a binary op or a conditional op
                return (Operand is BinaryOperator || Operand is Conditional);
            }
        }
    }
}
