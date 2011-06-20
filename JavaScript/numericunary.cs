// numericunary.cs
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
using System.Text;

namespace Microsoft.Ajax.Utilities
{
    public sealed class NumericUnary : UnaryOperator
    {

        public NumericUnary(Context context, JSParser parser, AstNode operand, JSToken operatorToken)
            : base(context, parser, operand, operatorToken)
        {
        }

        public override PrimitiveType FindPrimitiveType()
        {
            // if this is a logical not, it always returns boolean. otherwise it always returns a number
            return OperatorToken == JSToken.LogicalNot ? PrimitiveType.Boolean : PrimitiveType.Number;
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
            var otherUnary = otherNode as NumericUnary;
            return otherUnary != null
                && this.OperatorToken == otherUnary.OperatorToken
                && this.Operand.IsEquivalentTo(otherUnary.Operand);
        }

        public override string ToCode(ToCodeFormat format)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(JSScanner.GetOperatorString(OperatorToken));
            if (Operand != null)
            {
                string operandString = Operand.ToCode(format);
                if (NeedsParentheses)
                {
                    sb.Append('(');
                    sb.Append(operandString);
                    sb.Append(')');
                }
                else
                {
                    if (operandString.Length > 0)
                    {
                        // make sure that - - or + + doesn't get crunched to -- or ++
                        // (which would totally change the meaning of the code)
                        if ((OperatorToken == JSToken.Minus && operandString[0] == '-')
                          || (OperatorToken == JSToken.Plus && operandString[0] == '+'))
                        {
                            sb.Append(' ');
                        }
                    }
                    sb.Append(operandString);
                }
            }
            return sb.ToString();
        }
    }
}
