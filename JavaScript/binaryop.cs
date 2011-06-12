// binaryop.cs
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
using System.Globalization;
using System.Text;

namespace Microsoft.Ajax.Utilities
{

    public sealed class BinaryOperator : Expression
    {
        public AstNode Operand1 { get; private set; }
        public AstNode Operand2 { get; private set; }
        public JSToken OperatorToken { get; set; }

        public BinaryOperator(Context context, JSParser parser, AstNode operand1, AstNode operand2, JSToken operatorToken)
            : base(context, parser)
        {
            if (operand1 != null) operand1.Parent = this;
            if (operand2 != null) operand2.Parent = this;
            Operand1 = operand1;
            Operand2 = operand2;
            OperatorToken = operatorToken;
        }

        public override PrimitiveType FindPrimitiveType()
        {
            PrimitiveType leftType;
            PrimitiveType rightType;

            switch (OperatorToken)
            {
                case JSToken.Assign:
                case JSToken.Comma:
                    // returns whatever type the right operand is
                    return Operand2.FindPrimitiveType();

                case JSToken.BitwiseAnd:
                case JSToken.BitwiseAndAssign:
                case JSToken.BitwiseOr:
                case JSToken.BitwiseOrAssign:
                case JSToken.BitwiseXor:
                case JSToken.BitwiseXorAssign:
                case JSToken.Divide:
                case JSToken.DivideAssign:
                case JSToken.LeftShift:
                case JSToken.LeftShiftAssign:
                case JSToken.Minus:
                case JSToken.MinusAssign:
                case JSToken.Modulo:
                case JSToken.ModuloAssign:
                case JSToken.Multiply:
                case JSToken.MultiplyAssign:
                case JSToken.RightShift:
                case JSToken.RightShiftAssign:
                case JSToken.UnsignedRightShift:
                case JSToken.UnsignedRightShiftAssign:
                    // always returns a number
                    return PrimitiveType.Number;

                case JSToken.Equal:
                case JSToken.GreaterThan:
                case JSToken.GreaterThanEqual:
                case JSToken.In:
                case JSToken.InstanceOf:
                case JSToken.LessThan:
                case JSToken.LessThanEqual:
                case JSToken.NotEqual:
                case JSToken.StrictEqual:
                case JSToken.StrictNotEqual:
                    // always returns a boolean
                    return PrimitiveType.Boolean;

                case JSToken.PlusAssign:
                case JSToken.Plus:
                    // if either operand is known to be a string, then the result type is a string.
                    // otherwise the result is numeric if both types are known.
                    leftType = Operand1.FindPrimitiveType();
                    rightType = Operand2.FindPrimitiveType();

                    return (leftType == PrimitiveType.String || rightType == PrimitiveType.String)
                        ? PrimitiveType.String
                        : (leftType != PrimitiveType.Other && rightType != PrimitiveType.Other
                            ? PrimitiveType.Number
                            : PrimitiveType.Other);

                case JSToken.LogicalAnd:
                case JSToken.LogicalOr:
                    // these two are special. They return either the left or the right operand
                    // (depending on their values), so unless they are both known types AND the same,
                    // then we can't know for sure.
                    leftType = Operand1.FindPrimitiveType();
                    if (leftType != PrimitiveType.Other)
                    {
                        if (leftType == Operand2.FindPrimitiveType())
                        {
                            // they are both the same and neither is unknown
                            return leftType;
                        }
                    }

                    // if we get here, then we don't know the type
                    return PrimitiveType.Other;

                default:
                    // shouldn't get here....
                    return PrimitiveType.Other;
            }
        }

        public override IEnumerable<AstNode> Children
        {
            get
            {
                return EnumerateNonNullNodes(Operand1, Operand2);
            }
        }

        public override void Accept(IVisitor visitor)
        {
            if (visitor != null)
            {
                visitor.Visit(this);
            }
        }

        public override bool ReplaceChild(AstNode oldNode, AstNode newNode)
        {
            if (Operand1 == oldNode)
            {
                Operand1 = newNode;
                if (newNode != null) { newNode.Parent = this; }
                return true;
            }
            if (Operand2 == oldNode)
            {
                Operand2 = newNode;
                if (newNode != null) { newNode.Parent = this; }
                return true;
            }
            return false;
        }

        public override AstNode LeftHandSide
        {
            get
            {
                // the operand1 is on the left
                return Operand1.LeftHandSide;
            }
        }

        public void SwapOperands()
        {
            // swap the operands -- we don't need to go through ReplaceChild 
            // because we don't need to change the Parent pointers 
            // or anything like that.
            AstNode temp = Operand1;
            Operand1 = Operand2;
            Operand2 = temp;
        }

        public override bool IsEquivalentTo(AstNode otherNode)
        {
            // a binary operator is equivalent to another binary operator if the operator is the same and
            // both operands are also equivalent
            var otherBinary = otherNode as BinaryOperator;
            return otherBinary != null
                && OperatorToken == otherBinary.OperatorToken
                && Operand1.IsEquivalentTo(otherBinary.Operand1)
                && Operand2.IsEquivalentTo(otherBinary.Operand2);
        }

        public bool IsAssign
        {
            get
            {
                switch(OperatorToken)
                {
                    case JSToken.Assign:
                    case JSToken.PlusAssign:
                    case JSToken.MinusAssign:
                    case JSToken.MultiplyAssign:
                    case JSToken.DivideAssign:
                    case JSToken.ModuloAssign:
                    case JSToken.BitwiseAndAssign:
                    case JSToken.BitwiseOrAssign:
                    case JSToken.BitwiseXorAssign:
                    case JSToken.LeftShiftAssign:
                    case JSToken.RightShiftAssign:
                    case JSToken.UnsignedRightShiftAssign:
                        return true;

                    default:
                        return false;
                }
            }
        }

        internal override string GetFunctionGuess(AstNode target)
        {
            return Operand1.GetFunctionGuess(target);
        }

        internal override AstNode LogicalNot()
        {
            // depending on the operator, we either can't logical-not this
            // node at all, OR we can just tweak the operator and it's notted.
            switch (OperatorToken)
            {
                case JSToken.Equal:
                    OperatorToken = JSToken.NotEqual;
                    return this;

                case JSToken.NotEqual:
                    OperatorToken = JSToken.Equal;
                    return this;

                case JSToken.StrictEqual:
                    OperatorToken = JSToken.StrictNotEqual;
                    return this;

                case JSToken.StrictNotEqual:
                    OperatorToken = JSToken.StrictEqual;
                    return this;

                case JSToken.LessThan:
                    OperatorToken = JSToken.GreaterThanEqual;
                    return this;

                case JSToken.LessThanEqual:
                    OperatorToken = JSToken.GreaterThan;
                    return this;

                case JSToken.GreaterThan:
                    OperatorToken = JSToken.LessThanEqual;
                    return this;

                case JSToken.GreaterThanEqual:
                    OperatorToken = JSToken.LessThan;
                    return this;

            }

            // don't change anything else
            return null;
        }

        public override string ToCode(ToCodeFormat format)
        {
            string lhs = OptionalParens(Operand1, false, (format == ToCodeFormat.Preprocessor));
            string rhs = OptionalParens(Operand2, true, (format == ToCodeFormat.Preprocessor));
            string op = JSScanner.GetOperatorString(OperatorToken);

            StringBuilder sb = new StringBuilder();
            // if this is a comma operator and we are passed the comma-delimeter format,
            // then we need to wrap the entire operator in parens to preserve the
            // operator precedence. Otherwise our operator comma will just seem like
            // the containing list's separator
            bool wrapInParens = (OperatorToken == JSToken.Comma && format == ToCodeFormat.Commas);
            if (wrapInParens)
            {
                sb.Append('(');
            }

            sb.Append(lhs);
            CodeSettings codeSettings = Parser.Settings;
            // don't put a space before a comma operator in pretty-print-spacing mode
            if (codeSettings.OutputMode == OutputMode.MultipleLines
              && codeSettings.IndentSize > 0
              && OperatorToken != JSToken.Comma)
            {
                sb.Append(' ');
            }
            else if (CrunchNeedsSpace(lhs, op))
            {
                sb.Append(' ');
            }
            sb.Append(op);
            if (codeSettings.OutputMode == OutputMode.MultipleLines && codeSettings.IndentSize > 0)
            {
                sb.Append(' ');
            }
            else if (CrunchNeedsSpace(op, rhs))
            {
                sb.Append(' ');
            }
            sb.Append(rhs);
            if (wrapInParens)
            {
                sb.Append(')');
            }
            return sb.ToString();
        }

        private static bool CrunchNeedsSpace(string left, string right)
        {
            // if left ends in an identifier part and right starts with one, then we need a space.
            // also, if the left is a minus sign and the right starts with another one, or 
            // if the right is a plus sign and the right starts with another one, we also need a space
            // (the keep the two operators from being read as -- or ++ increment/decrement operators)
            return ((JSScanner.EndsWithIdentifierPart(left) && JSScanner.StartsWithIdentifierPart(right))
              ||
              ((left == "-" && right.Length > 0 && right[0] == '-')
              || (left == "+" && right.Length > 0 && right[0] == '+')));
        }

        private string OptionalParens(AstNode operand, bool isRhs, bool preserveProprocessor)
        {
            bool wrapInParen = false;
            JSToken operandToken = JSToken.Null;

            BinaryOperator binaryOp = operand as BinaryOperator;
            if (binaryOp != null)
            {
                operandToken = binaryOp.OperatorToken;
            }

            if (operandToken != JSToken.Null)
            {
                OpPrec operandPrec = JSScanner.GetOperatorPrecedence(operandToken);
                OpPrec thisPrec = JSScanner.GetOperatorPrecedence(OperatorToken);
                if (operandPrec < thisPrec)
                {
                    // lesser precedence gets parenthesis
                    wrapInParen = true;
                }
                else if (operandPrec == thisPrec
                  && isRhs
                  && operandPrec != OpPrec.precAssignment
                  && operandPrec != OpPrec.precComma)
                {
                    // because we evaluate from left to right (except for assignments and commas),
                    // we'll wrap the same precedence in parens if this is for the right-hand-side.
                    // we don't need to do this if BOTH operators are the same and they are associative.
                    // EG: addition, multiplication, logical-AND, logical-OR, bitwise-AND, bitwise-OR, bitwise-XOR
                    if (operandToken != OperatorToken)
                    {
                        // the operators are different, so always wrap the right-hand pair in parens
                        // to make sure the resulting code has the proper operator precedence
                        wrapInParen = true;
                    }
                    else
                    {
                        // same operators. If the operator is associative, it really doesn't matter
                        // whether the output code is (A+B)+C or A+(B+C). If the operators are NOT
                        // associate, it does make a difference (A/B)/C != A/(B/C)
                        switch(operandToken)
                        {
                            // ACTUALLY, we shouldn't use this rule on the plus operator. Yes, it would
                            // work IF the operator was just a series of additions, or just a series of
                            // string concatenations. But if it's a COMBINATION of the two, then the 
                            // developer knows best. For instance: 
                            //      "foo" + (1 + 2) ==> "foo3"
                            //      "foo" + 1 + 2 == > "foo12"  NOT THE SAME!!!!
                            // I think this is just because of the dual-nature of the + operator
                            // (addition OR string concat), and that we can still use this rule for
                            // the other operators, since they all do only one type of operation.
                            // I might have to pull this feature entirely, though. 
                            //case JSToken.Plus:
                            case JSToken.Multiply:
                            case JSToken.BitwiseAnd:
                            case JSToken.BitwiseOr:
                            case JSToken.BitwiseXor:
                            case JSToken.LogicalAnd:
                            case JSToken.LogicalOr:
                                // these binary operators are associative and don't need to
                                // wrap the right-hand-side in parens if they are the same operands
                                break;

                            default:
                                // I'm pretty sure all the others are NOT associative, so we should
                                // wrap the right-hand-side in parens to make sure we keep the same
                                // operator precedence in the resulting code
                                wrapInParen = true;
                                break;
                        }
                    }
                }
            }
            else if (operand is Conditional && JSScanner.GetOperatorPrecedence(OperatorToken) > OpPrec.precConditional)
            {
                // the operand is a conditional, so wrap it in parens when this binary operator's 
                // precedence is greater than the conditional precedence
                wrapInParen = true;
            }

            string operandCode = operand.ToCode(preserveProprocessor ? ToCodeFormat.Preprocessor : ToCodeFormat.Normal);
            if (wrapInParen)
            {
                return '(' + operandCode + ')';
            }
            return operandCode;
        }
    }
}
