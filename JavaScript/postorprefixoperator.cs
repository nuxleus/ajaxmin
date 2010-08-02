// postorprefixoperator.cs
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

namespace Microsoft.Ajax.Utilities
{
    public enum PostOrPrefix { PostfixDecrement, PostfixIncrement, PrefixDecrement, PrefixIncrement };

    public sealed class PostOrPrefixOperator : UnaryOperator
    {
        private PostOrPrefix m_postOrPrefixOperator;

        public PostOrPrefixOperator(Context context, JSParser parser, AstNode operand, JSToken operatorToken, PostOrPrefix postOrPrefixOperator)
            : base(context, parser, operand, operatorToken)
        {
            m_postOrPrefixOperator = postOrPrefixOperator;
        }

        public override AstNode Clone()
        {
            return new PostOrPrefixOperator(
                Context.Clone(),
                Parser,
                (Operand == null ? null : Operand.Clone()),
                OperatorToken,
                m_postOrPrefixOperator
                );
        }

        public override string ToCode(ToCodeFormat format)
        {
            string operandString = Operand.ToCode(format);
            if (NeedsParentheses)
            {
                operandString = "(" + operandString + ")";
            }
            switch (m_postOrPrefixOperator)
            {
                case PostOrPrefix.PostfixDecrement:
                    return operandString + "--";

                case PostOrPrefix.PostfixIncrement:
                    return operandString + "++";

                case PostOrPrefix.PrefixDecrement:
                    return "--" + operandString;

                case PostOrPrefix.PrefixIncrement:
                    return "++" + operandString;

                default:
                    throw new UnexpectedTokenException();
            }
        }

        public override AstNode LeftHandSide
        {
            get
            {
                if (m_postOrPrefixOperator == PostOrPrefix.PostfixDecrement
                  || m_postOrPrefixOperator == PostOrPrefix.PostfixIncrement)
                {
                    // postfix -- the operand is on the left
                    return Operand.LeftHandSide;
                }
                else
                {
                    // prefix operator -- we are on the left
                    return this;
                }
            }
        }
    }
}