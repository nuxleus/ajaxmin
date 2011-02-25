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

        public override AstNode Clone()
        {
            return new NumericUnary(
                (Context == null ? null : Context.Clone()),
                Parser,
                (Operand == null ? null : Operand.Clone()),
                OperatorToken
                );
        }

        internal override void AnalyzeNode()
        {
            // recurse first, then check to see if the unary is still needed
            base.AnalyzeNode();

            // if the operand is a numeric literal
            ConstantWrapper constantWrapper = Operand as ConstantWrapper;
            if (constantWrapper != null
                && constantWrapper.IsNumericLiteral)
            {
                // get the value of the constant. We've already screened it for numeric, so
                // we don't have to worry about catching any errors
                double doubleValue = constantWrapper.ToNumber();

                // if this is a unary minus...
                if (OperatorToken == JSToken.Minus
                    && Parser.Settings.IsModificationAllowed(TreeModifications.ApplyUnaryMinusToNumericLiteral))
                {
                    // negate the value
                    constantWrapper.Value = -doubleValue;

                    // replace us with the negated constant
                    if (Parent.ReplaceChild(this, constantWrapper))
                    {
                        // the context for the minus will include the number (its operand),
                        // but the constant will just be the number. Update the context on
                        // the constant to be a copy of the context on the operator
                        constantWrapper.Context = Context.Clone();
                        return;
                    }
                }
                else if (OperatorToken == JSToken.Plus
                    && Parser.Settings.IsModificationAllowed(TreeModifications.RemoveUnaryPlusOnNumericLiteral))
                {
                    // +NEG is still negative, +POS is still positive, and +0 is still 0.
                    // so just get rid of the unary operator altogether
                    if (Parent.ReplaceChild(this, constantWrapper))
                    {
                        // the context for the unary will include the number (its operand),
                        // but the constant will just be the number. Update the context on
                        // the constant to be a copy of the context on the operator
                        constantWrapper.Context = Context.Clone();
                        return;
                    }
                }
            }
        }

        public override void CleanupNodes()
        {
            base.CleanupNodes();

            if (Parser.Settings.EvalLiteralExpressions
                && Parser.Settings.IsModificationAllowed(TreeModifications.EvaluateNumericExpressions))
            {
                // see if our operand is a ConstantWrapper
                ConstantWrapper literalOperand = Operand as ConstantWrapper;
                if (literalOperand != null)
                {
                    // must be number, boolean, string, or null
                    switch (OperatorToken)
                    {
                        case JSToken.Plus:
                            try
                            {
                                // replace with a constant representing operand.ToNumber,
                                Parent.ReplaceChild(this, new ConstantWrapper(literalOperand.ToNumber(), PrimitiveType.Number, Context, Parser));
                            }
                            catch (InvalidCastException)
                            {
                                // some kind of casting in ToNumber caused a situation where we don't want
                                // to perform the combination on these operands
                            }
                            break;

                        case JSToken.Minus:
                            try
                            {
                                // replace with a constant representing the negative of operand.ToNumber
                                Parent.ReplaceChild(this, new ConstantWrapper(-literalOperand.ToNumber(), PrimitiveType.Number, Context, Parser));
                            }
                            catch (InvalidCastException)
                            {
                                // some kind of casting in ToNumber caused a situation where we don't want
                                // to perform the combination on these operands
                            }
                            break;

                        case JSToken.BitwiseNot:
                            try
                            {
                                // replace with a constant representing the bitwise-not of operant.ToInt32
                                Parent.ReplaceChild(this, new ConstantWrapper(Convert.ToDouble(~literalOperand.ToInt32()), PrimitiveType.Number, Context, Parser));
                            }
                            catch (InvalidCastException)
                            {
                                // some kind of casting in ToNumber caused a situation where we don't want
                                // to perform the combination on these operands
                            }
                            break;

                        case JSToken.LogicalNot:
                            // replace with a constant representing the opposite of operand.ToBoolean
                            try
                            {
                                Parent.ReplaceChild(this, new ConstantWrapper(!literalOperand.ToBoolean(), PrimitiveType.Boolean, Context, Parser));
                            }
                            catch (InvalidCastException)
                            {
                                // ignore any invalid cast exceptions
                            }
                            break;
                    }
                }
            }
        }

        internal override AstNode LogicalNot()
        {
            return (OperatorToken == JSToken.LogicalNot
              ? Operand // the logical-not of a logical-not is just the operand
              : null);
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
