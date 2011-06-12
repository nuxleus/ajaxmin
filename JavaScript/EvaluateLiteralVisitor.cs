// EvaluateLiteralVisitor.cs
//
// Copyright 2011 Microsoft Corporation
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
    internal class EvaluateLiteralVisitor : TreeVisitor
    {
        private JSParser m_parser;

        public EvaluateLiteralVisitor(JSParser parser) 
        {
            m_parser = parser;
        }

        #region BinaryOperator helper methods

        /// <summary>
        /// If the new literal is a string literal, then we need to check to see if our
        /// parent is a CallNode. If it is, and if the string literal can be an identifier,
        /// we'll replace it with a Member-Dot operation.
        /// </summary>
        /// <param name="newLiteral">newLiteral we intend to replace this binaryop node with</param>
        /// <returns>true if we replaced the parent callnode with a member-dot operation</returns>
        private bool ReplaceMemberBracketWithDot(BinaryOperator node, ConstantWrapper newLiteral)
        {
            if (newLiteral.IsStringLiteral)
            {
                // see if this newly-combined string is the sole argument to a 
                // call-brackets node. If it is and the combined string is a valid
                // identifier (and not a keyword), then we can replace the call
                // with a member operator.
                // remember that the parent of the argument won't be the call node -- it
                // will be the ast node list representing the arguments, whose parent will
                // be the node list. 
                CallNode parentCall = (node.Parent is AstNodeList ? node.Parent.Parent as CallNode : null);
                if (parentCall != null && parentCall.InBrackets)
                {
                    // get the newly-combined string
                    string combinedString = newLiteral.ToString();

                    // see if this new string is the target of a replacement operation
                    string newName;
                    if (m_parser.Settings.HasRenamePairs && m_parser.Settings.ManualRenamesProperties
                        && m_parser.Settings.IsModificationAllowed(TreeModifications.PropertyRenaming)
                        && !string.IsNullOrEmpty(newName = m_parser.Settings.GetNewName(combinedString)))
                    {
                        // yes, it is. Now see if the new name is safe to be converted to a dot-operation.
                        if (m_parser.Settings.IsModificationAllowed(TreeModifications.BracketMemberToDotMember)
                            && JSScanner.IsSafeIdentifier(newName)
                            && !JSScanner.IsKeyword(newName))
                        {
                            // we want to replace the call with operator with a new member dot operation, and
                            // since we won't be analyzing it (we're past the analyze phase, we're going to need
                            // to use the new string value
                            Member replacementMember = new Member(parentCall.Context, m_parser, parentCall.Function, newName);
                            parentCall.Parent.ReplaceChild(parentCall, replacementMember);
                            return true;
                        }
                        else
                        {
                            // nope, can't be changed to a dot-operator for whatever reason.
                            // just replace the value on this new literal. The old operation will
                            // get replaced with this new literal
                            newLiteral.Value = newName;

                            // and make sure it's type is string
                            newLiteral.PrimitiveType = PrimitiveType.String;
                        }
                    }
                    else if (m_parser.Settings.IsModificationAllowed(TreeModifications.BracketMemberToDotMember))
                    {
                        // our parent is a call-bracket -- now we just need to see if the newly-combined
                        // string can be an identifier
                        if (JSScanner.IsSafeIdentifier(combinedString) && !JSScanner.IsKeyword(combinedString))
                        {
                            // yes -- replace the parent call with a new member node using the newly-combined string
                            Member replacementMember = new Member(parentCall.Context, m_parser, parentCall.Function, combinedString);
                            parentCall.Parent.ReplaceChild(parentCall, replacementMember);
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// Both the operands of this operator are constants. See if we can evaluate them
        /// </summary>
        /// <param name="left">left-side operand</param>
        /// <param name="right">right-side operand</param>
        private void EvalThisOperator(BinaryOperator node, ConstantWrapper left, ConstantWrapper right)
        {
            // we can evaluate these operators if we know both operands are literal
            // number, boolean, string or null
            ConstantWrapper newLiteral = null;
            switch (node.OperatorToken)
            {
                case JSToken.Multiply:
                    newLiteral = Multiply(left, right);
                    break;

                case JSToken.Divide:
                    newLiteral = Divide(left, right);
                    if (newLiteral != null && newLiteral.ToCode().Length > node.ToCode().Length)
                    {
                        // the result is bigger than the expression.
                        // eg: 1/3 is smaller than .333333333333333
                        // never mind.
                        newLiteral = null;
                    }
                    break;

                case JSToken.Modulo:
                    newLiteral = Modulo(left, right);
                    if (newLiteral != null && newLiteral.ToCode().Length > node.ToCode().Length)
                    {
                        // the result is bigger than the expression.
                        // eg: 46.5%6.3 is smaller than 2.4000000000000012
                        // never mind.
                        newLiteral = null;
                    }
                    break;

                case JSToken.Plus:
                    newLiteral = Plus(left, right);
                    break;

                case JSToken.Minus:
                    newLiteral = Minus(left, right);
                    break;

                case JSToken.LeftShift:
                    newLiteral = LeftShift(left, right);
                    break;

                case JSToken.RightShift:
                    newLiteral = RightShift(left, right);
                    break;

                case JSToken.UnsignedRightShift:
                    newLiteral = UnsignedRightShift(left, right);
                    break;

                case JSToken.LessThan:
                    newLiteral = LessThan(left, right);
                    break;

                case JSToken.LessThanEqual:
                    newLiteral = LessThanOrEqual(left, right);
                    break;

                case JSToken.GreaterThan:
                    newLiteral = GreaterThan(left, right);
                    break;

                case JSToken.GreaterThanEqual:
                    newLiteral = GreaterThanOrEqual(left, right);
                    break;

                case JSToken.Equal:
                    newLiteral = Equal(left, right);
                    break;

                case JSToken.NotEqual:
                    newLiteral = NotEqual(left, right);
                    break;

                case JSToken.StrictEqual:
                    newLiteral = StrictEqual(left, right);
                    break;

                case JSToken.StrictNotEqual:
                    newLiteral = StrictNotEqual(left, right);
                    break;

                case JSToken.BitwiseAnd:
                    newLiteral = BitwiseAnd(left, right);
                    break;

                case JSToken.BitwiseOr:
                    newLiteral = BitwiseOr(left, right);
                    break;

                case JSToken.BitwiseXor:
                    newLiteral = BitwiseXor(left, right);
                    break;

                case JSToken.LogicalAnd:
                    newLiteral = LogicalAnd(left, right);
                    break;

                case JSToken.LogicalOr:
                    newLiteral = LogicalOr(left, right);
                    break;

                default:
                    // an operator we don't want to evaluate
                    break;
            }

            // if we can combine them...
            if (newLiteral != null)
            {
                // first we want to check if the new combination is a string literal, and if so, whether 
                // it's now the sole parameter of a member-bracket call operator. If so, instead of replacing our
                // binary operation with the new constant, we'll replace the entire call with a member-dot
                // expression
                if (!ReplaceMemberBracketWithDot(node, newLiteral))
                {
                    node.Parent.ReplaceChild(node, newLiteral);
                }
            }
        }

        /// <summary>
        /// We have determined that our left-hand operand is another binary operator, and its
        /// right-hand operand is a constant that can be combined with our right-hand operand.
        /// Now we want to set the right-hand operand of that other operator to the newly-
        /// combined constant value, and then rotate it up -- replace our binary operator
        /// with this newly-modified binary operator, and then attempt to re-evaluate it.
        /// </summary>
        /// <param name="binaryOp">the binary operator that is our left-hand operand</param>
        /// <param name="newLiteral">the newly-combined literal</param>
        private void RotateFromLeft(BinaryOperator node, BinaryOperator binaryOp, ConstantWrapper newLiteral)
        {
            // replace our node with the binary operator
            binaryOp.ReplaceChild(binaryOp.Operand2, newLiteral);
            node.Parent.ReplaceChild(node, binaryOp);

            // and just for good measure.. revisit the node that's taking our place, since
            // we just changed a constant value. Assuming the other operand is a constant, too.
            ConstantWrapper otherConstant = binaryOp.Operand1 as ConstantWrapper;
            if (otherConstant != null)
            {
                EvalThisOperator(binaryOp, otherConstant, newLiteral);
            }
        }

        /// <summary>
        /// We have determined that our right-hand operand is another binary operator, and its
        /// left-hand operand is a constant that can be combined with our left-hand operand.
        /// Now we want to set the left-hand operand of that other operator to the newly-
        /// combined constant value, and then rotate it up -- replace our binary operator
        /// with this newly-modified binary operator, and then attempt to re-evaluate it.
        /// </summary>
        /// <param name="binaryOp">the binary operator that is our right-hand operand</param>
        /// <param name="newLiteral">the newly-combined literal</param>
        private void RotateFromRight(BinaryOperator node, BinaryOperator binaryOp, ConstantWrapper newLiteral)
        {
            // replace our node with the binary operator
            binaryOp.ReplaceChild(binaryOp.Operand1, newLiteral);
            node.Parent.ReplaceChild(node, binaryOp);

            // and just for good measure.. revisit the node that's taking our place, since
            // we just changed a constant value. Assuming the other operand is a constant, too.
            ConstantWrapper otherConstant = binaryOp.Operand2 as ConstantWrapper;
            if (otherConstant != null)
            {
                EvalThisOperator(binaryOp, newLiteral, otherConstant);
            }
        }

        /// <summary>
        /// Return true is not an overflow or underflow, for multiplication operations
        /// </summary>
        /// <param name="left">left operand</param>
        /// <param name="right">right operand</param>
        /// <param name="result">result</param>
        /// <returns>true if result not overflow or underflow; false if it is</returns>
        private static bool NoMultiplicativeOverOrUnderFlow(ConstantWrapper left, ConstantWrapper right, ConstantWrapper result)
        {
            // check for overflow
            bool okayToProceed = !result.IsInfinity;

            // if we still might be good, check for possible underflow
            if (okayToProceed)
            {
                // if the result is zero, we might have an underflow. But if one of the operands
                // was zero, then it's okay.
                // Inverse: if neither operand is zero, then a zero result is not okay
                okayToProceed = !result.IsZero || (left.IsZero || right.IsZero);
            }
            return okayToProceed;
        }

        /// <summary>
        /// Return true if the result isn't an overflow condition
        /// </summary>
        /// <param name="result">result constant</param>
        /// <returns>true is not an overflow; false if it is</returns>
        private static bool NoOverflow(ConstantWrapper result)
        {
            return !result.IsInfinity;
        }

        /// <summary>
        /// Evaluate: (OTHER [op] CONST) [op] CONST
        /// </summary>
        /// <param name="thisConstant">second constant</param>
        /// <param name="otherConstant">first constant</param>
        /// <param name="leftOperator">first operator</param>
        private void EvalToTheLeft(BinaryOperator node, ConstantWrapper thisConstant, ConstantWrapper otherConstant, BinaryOperator leftOperator)
        {
            if (leftOperator.OperatorToken == JSToken.Plus && node.OperatorToken == JSToken.Plus)
            {
                // plus-plus
                // the other operation goes first, so if the other constant is a string, then we know that
                // operation will do a string concatenation, which will force our operation to be a string
                // concatenation. If the other constant is not a string, then we won't know until runtime and
                // we can't combine them.
                if (otherConstant.IsStringLiteral)
                {
                    // the other constant is a string -- so we can do the string concat and combine them
                    ConstantWrapper newLiteral = StringConcat(otherConstant, thisConstant);
                    if (newLiteral != null)
                    {
                        RotateFromLeft(node, leftOperator, newLiteral);
                    }
                }
            }
            else if (leftOperator.OperatorToken == JSToken.Minus)
            {
                if (node.OperatorToken == JSToken.Plus)
                {
                    // minus-plus
                    // the minus operator goes first and will always convert to number.
                    // if our constant is not a string, then it will be a numeric addition and we can combine them.
                    // if our constant is a string, then we'll end up doing a string concat, so we can't combine
                    if (!thisConstant.IsStringLiteral)
                    {
                        // two numeric operators. a-n1+n2 is the same as a-(n1-n2)
                        ConstantWrapper newLiteral = Minus(otherConstant, thisConstant);
                        if (newLiteral != null && NoOverflow(newLiteral))
                        {
                            // a-(-n) is numerically equivalent as a+n -- and takes fewer characters to represent.
                            // BUT we can't do that because that might change a numeric operation (the original minus)
                            // to a string concatenation if the unknown operand turns out to be a string!

                            RotateFromLeft(node, leftOperator, newLiteral);
                        }
                        else
                        {
                            // if the left-left is a constant, then we can try combining with it
                            ConstantWrapper leftLeft = leftOperator.Operand1 as ConstantWrapper;
                            if (leftLeft != null)
                            {
                                EvalFarToTheLeft(node, thisConstant, leftLeft, leftOperator);
                            }
                        }
                    }
                }
                else if (node.OperatorToken == JSToken.Minus)
                {
                    // minus-minus. Both operations are numeric.
                    // a-n1-n2 => a-(n1+n2), so we can add the two constants and subtract from 
                    // the left-hand non-constant. 
                    ConstantWrapper newLiteral = NumericAddition(otherConstant, thisConstant);
                    if (newLiteral != null && NoOverflow(newLiteral))
                    {
                        // make it the new right-hand literal for the left-hand operator
                        // and make the left-hand operator replace our operator
                        RotateFromLeft(node, leftOperator, newLiteral);
                    }
                    else
                    {
                        // if the left-left is a constant, then we can try combining with it
                        ConstantWrapper leftLeft = leftOperator.Operand1 as ConstantWrapper;
                        if (leftLeft != null)
                        {
                            EvalFarToTheLeft(node, thisConstant, leftLeft, leftOperator);
                        }
                    }
                }
            }
            else if (leftOperator.OperatorToken == node.OperatorToken
                && (node.OperatorToken == JSToken.Multiply || node.OperatorToken == JSToken.Divide))
            {
                // either multiply-multiply or divide-divide
                // either way, we use the other operand and the product of the two constants.
                // if the product blows up to an infinte value, then don't combine them because that
                // could change the way the program goes at runtime, depending on the unknown value.
                ConstantWrapper newLiteral = Multiply(otherConstant, thisConstant);
                if (newLiteral != null && NoMultiplicativeOverOrUnderFlow(otherConstant, thisConstant, newLiteral))
                {
                    RotateFromLeft(node, leftOperator, newLiteral);
                }
            }
            else if ((leftOperator.OperatorToken == JSToken.Multiply && node.OperatorToken == JSToken.Divide)
                || (leftOperator.OperatorToken == JSToken.Divide && node.OperatorToken == JSToken.Multiply))
            {
                if (m_parser.Settings.IsModificationAllowed(TreeModifications.EvaluateNumericExpressions))
                {
                    // get the two division operators
                    ConstantWrapper otherOverThis = Divide(otherConstant, thisConstant);
                    ConstantWrapper thisOverOther = Divide(thisConstant, otherConstant);

                    // get the lengths
                    int otherOverThisLength = otherOverThis != null ? otherOverThis.ToCode().Length : int.MaxValue;
                    int thisOverOtherLength = thisOverOther != null ? thisOverOther.ToCode().Length : int.MaxValue;

                    // we'll want to use whichever one is shorter, and whichever one does NOT involve an overflow 
                    // or possible underflow
                    if (otherOverThis != null && NoMultiplicativeOverOrUnderFlow(otherConstant, thisConstant, otherOverThis)
                        && (thisOverOther == null || otherOverThisLength < thisOverOtherLength))
                    {
                        // but only if it's smaller than the original expression
                        if (otherOverThisLength <= otherConstant.ToCode().Length + thisConstant.ToCode().Length + 1)
                        {
                            // same operator
                            RotateFromLeft(node, leftOperator, otherOverThis);
                        }
                    }
                    else if (thisOverOther != null && NoMultiplicativeOverOrUnderFlow(thisConstant, otherConstant, thisOverOther))
                    {
                        // but only if it's smaller than the original expression
                        if (thisOverOtherLength <= otherConstant.ToCode().Length + thisConstant.ToCode().Length + 1)
                        {
                            // opposite operator
                            leftOperator.OperatorToken = leftOperator.OperatorToken == JSToken.Multiply ? JSToken.Divide : JSToken.Multiply;
                            RotateFromLeft(node, leftOperator, thisOverOther);
                        }
                    }
                }
            }
            else if (node.OperatorToken == leftOperator.OperatorToken
                && (node.OperatorToken == JSToken.BitwiseAnd || node.OperatorToken == JSToken.BitwiseOr || node.OperatorToken == JSToken.BitwiseXor))
            {
                // identical bitwise operators can be combined
                ConstantWrapper newLiteral = null;
                switch (node.OperatorToken)
                {
                    case JSToken.BitwiseAnd:
                        newLiteral = BitwiseAnd(otherConstant, thisConstant);
                        break;

                    case JSToken.BitwiseOr:
                        newLiteral = BitwiseOr(otherConstant, thisConstant);
                        break;

                    case JSToken.BitwiseXor:
                        newLiteral = BitwiseXor(otherConstant, thisConstant);
                        break;
                }
                if (newLiteral != null)
                {
                    RotateFromLeft(node, leftOperator, newLiteral);
                }
            }
        }

        /// <summary>
        /// Evaluate: (CONST [op] OTHER) [op] CONST
        /// </summary>
        /// <param name="thisConstant">second constant</param>
        /// <param name="otherConstant">first constant</param>
        /// <param name="leftOperator">first operator</param>
        private void EvalFarToTheLeft(BinaryOperator node, ConstantWrapper thisConstant, ConstantWrapper otherConstant, BinaryOperator leftOperator)
        {
            if (leftOperator.OperatorToken == JSToken.Minus)
            {
                if (node.OperatorToken == JSToken.Plus)
                {
                    // minus-plus
                    ConstantWrapper newLiteral = NumericAddition(otherConstant, thisConstant);
                    if (newLiteral != null && NoOverflow(newLiteral))
                    {
                        RotateFromRight(node, leftOperator, newLiteral);
                    }
                }
                else if (node.OperatorToken == JSToken.Minus)
                {
                    // minus-minus
                    ConstantWrapper newLiteral = Minus(otherConstant, thisConstant);
                    if (newLiteral != null && NoOverflow(newLiteral))
                    {
                        RotateFromRight(node, leftOperator, newLiteral);
                    }
                }
            }
            else if (node.OperatorToken == JSToken.Multiply)
            {
                if (leftOperator.OperatorToken == JSToken.Multiply || leftOperator.OperatorToken == JSToken.Divide)
                {
                    ConstantWrapper newLiteral = Multiply(otherConstant, thisConstant);
                    if (newLiteral != null && NoMultiplicativeOverOrUnderFlow(otherConstant, thisConstant, newLiteral))
                    {
                        RotateFromRight(node, leftOperator, newLiteral);
                    }
                }
            }
            else if (node.OperatorToken == JSToken.Divide)
            {
                if (leftOperator.OperatorToken == JSToken.Divide)
                {
                    // divide-divide
                    ConstantWrapper newLiteral = Divide(otherConstant, thisConstant);
                    if (newLiteral != null && NoMultiplicativeOverOrUnderFlow(otherConstant, thisConstant, newLiteral)
                        && newLiteral.ToCode().Length <= thisConstant.ToCode().Length + otherConstant.ToCode().Length + 1)
                    {
                        RotateFromRight(node, leftOperator, newLiteral);
                    }
                }
                else if (leftOperator.OperatorToken == JSToken.Multiply)
                {
                    // mult-divide
                    ConstantWrapper otherOverThis = Divide(otherConstant, thisConstant);
                    ConstantWrapper thisOverOther = Divide(thisConstant, otherConstant);

                    int otherOverThisLength = otherOverThis != null ? otherOverThis.ToCode().Length : int.MaxValue;
                    int thisOverOtherLength = thisOverOther != null ? thisOverOther.ToCode().Length : int.MaxValue;

                    if (otherOverThis != null && NoMultiplicativeOverOrUnderFlow(otherConstant, thisConstant, otherOverThis)
                        && (thisOverOther == null || otherOverThisLength < thisOverOtherLength))
                    {
                        if (otherOverThisLength <= thisConstant.ToCode().Length + otherConstant.ToCode().Length + 1)
                        {
                            RotateFromRight(node, leftOperator, otherOverThis);
                        }
                    }
                    else if (thisOverOther != null && NoMultiplicativeOverOrUnderFlow(thisConstant, otherConstant, thisOverOther))
                    {
                        if (thisOverOtherLength <= thisConstant.ToCode().Length + otherConstant.ToCode().Length + 1)
                        {
                            // swap the operands
                            leftOperator.SwapOperands();

                            // operator is the opposite
                            leftOperator.OperatorToken = JSToken.Divide;
                            RotateFromLeft(node, leftOperator, thisOverOther);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Evaluate: CONST [op] (CONST [op] OTHER)
        /// </summary>
        /// <param name="thisConstant">first constant</param>
        /// <param name="otherConstant">second constant</param>
        /// <param name="leftOperator">second operator</param>
        private void EvalToTheRight(BinaryOperator node, ConstantWrapper thisConstant, ConstantWrapper otherConstant, BinaryOperator rightOperator)
        {
            if (node.OperatorToken == JSToken.Plus)
            {
                if (rightOperator.OperatorToken == JSToken.Plus && otherConstant.IsStringLiteral)
                {
                    // plus-plus, and the other constant is a string. So the right operator will be a string-concat
                    // that generates a string. And since this is a plus-operator, then this operator will be a string-
                    // concat as well. So we can just combine the strings now and replace our node with the right-hand 
                    // operation
                    ConstantWrapper newLiteral = StringConcat(thisConstant, otherConstant);
                    if (newLiteral != null)
                    {
                        RotateFromRight(node, rightOperator, newLiteral);
                    }
                }
                else if (rightOperator.OperatorToken == JSToken.Minus && !thisConstant.IsStringLiteral)
                {
                    // plus-minus. Now, the minus operation happens first, and it will perform a numeric
                    // operation. The plus is NOT string, so that means it will also be a numeric operation
                    // and we can combine the operators numericly. 
                    ConstantWrapper newLiteral = NumericAddition(thisConstant, otherConstant);
                    if (newLiteral != null && NoOverflow(newLiteral))
                    {
                        RotateFromRight(node, rightOperator, newLiteral);
                    }
                    else
                    {
                        ConstantWrapper rightRight = rightOperator.Operand2 as ConstantWrapper;
                        if (rightRight != null)
                        {
                            EvalFarToTheRight(node, thisConstant, rightRight, rightOperator);
                        }
                    }
                }
            }
            else if (node.OperatorToken == JSToken.Minus && rightOperator.OperatorToken == JSToken.Minus)
            {
                // minus-minus
                // both operations are numeric, so we can combine the constant operands. However, we 
                // can't combine them into a plus, so make sure we do the minus in the opposite direction
                ConstantWrapper newLiteral = Minus(otherConstant, thisConstant);
                if (newLiteral != null && NoOverflow(newLiteral))
                {
                    rightOperator.SwapOperands();
                    RotateFromLeft(node, rightOperator, newLiteral);
                }
                else
                {
                    ConstantWrapper rightRight = rightOperator.Operand2 as ConstantWrapper;
                    if (rightRight != null)
                    {
                        EvalFarToTheRight(node, thisConstant, rightRight, rightOperator);
                    }
                }
            }
            else if (node.OperatorToken == JSToken.Multiply
                && (rightOperator.OperatorToken == JSToken.Multiply || rightOperator.OperatorToken == JSToken.Divide))
            {
                // multiply-divide or multiply-multiply
                // multiply the operands and use the right-hand operator
                ConstantWrapper newLiteral = Multiply(thisConstant, otherConstant);
                if (newLiteral != null && NoMultiplicativeOverOrUnderFlow(thisConstant, otherConstant, newLiteral))
                {
                    RotateFromRight(node, rightOperator, newLiteral);
                }
            }
            else if (node.OperatorToken == JSToken.Divide)
            {
                if (rightOperator.OperatorToken == JSToken.Multiply)
                {
                    // divide-multiply
                    ConstantWrapper newLiteral = Divide(thisConstant, otherConstant);
                    if (newLiteral != null && NoMultiplicativeOverOrUnderFlow(thisConstant, otherConstant, newLiteral)
                        && newLiteral.ToCode().Length < thisConstant.ToCode().Length + otherConstant.ToCode().Length + 1)
                    {
                        // flip the operator: multiply becomes divide; devide becomes multiply
                        rightOperator.OperatorToken = JSToken.Divide;

                        RotateFromRight(node, rightOperator, newLiteral);
                    }
                }
                else if (rightOperator.OperatorToken == JSToken.Divide)
                {
                    // divide-divide
                    // get constants for left/right and for right/left
                    ConstantWrapper leftOverRight = Divide(thisConstant, otherConstant);
                    ConstantWrapper rightOverLeft = Divide(otherConstant, thisConstant);

                    // get the lengths of the resulting code
                    int leftOverRightLength = leftOverRight != null ? leftOverRight.ToCode().Length : int.MaxValue;
                    int rightOverLeftLength = rightOverLeft != null ? rightOverLeft.ToCode().Length : int.MaxValue;

                    // try whichever is smaller
                    if (leftOverRight != null && NoMultiplicativeOverOrUnderFlow(thisConstant, otherConstant, leftOverRight)
                        && (rightOverLeft == null || leftOverRightLength < rightOverLeftLength))
                    {
                        // use left-over-right. 
                        // but only if the resulting value is smaller than the original expression
                        if (leftOverRightLength <= thisConstant.ToCode().Length + otherConstant.ToCode().Length + 1)
                        {
                            // We don't need to swap the operands, but we do need to switch the operator
                            rightOperator.OperatorToken = JSToken.Multiply;
                            RotateFromRight(node, rightOperator, leftOverRight);
                        }
                    }
                    else if (rightOverLeft != null && NoMultiplicativeOverOrUnderFlow(otherConstant, thisConstant, rightOverLeft))
                    {
                        // but only if the resulting value is smaller than the original expression
                        if (rightOverLeftLength <= thisConstant.ToCode().Length + otherConstant.ToCode().Length + 1)
                        {
                            // use right-over-left. Keep the operator, but swap the operands
                            rightOperator.SwapOperands();
                            RotateFromLeft(node, rightOperator, rightOverLeft);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Eval the two constants: CONST [op] (OTHER [op] CONST)
        /// </summary>
        /// <param name="thisConstant">first constant</param>
        /// <param name="otherConstant">second constant</param>
        /// <param name="rightOperator">second operator</param>
        private void EvalFarToTheRight(BinaryOperator node, ConstantWrapper thisConstant, ConstantWrapper otherConstant, BinaryOperator rightOperator)
        {
            if (rightOperator.OperatorToken == JSToken.Minus)
            {
                if (node.OperatorToken == JSToken.Plus)
                {
                    // plus-minus
                    // our constant cannot be a string, though
                    if (!thisConstant.IsStringLiteral)
                    {
                        ConstantWrapper newLiteral = Minus(otherConstant, thisConstant);
                        if (newLiteral != null && NoOverflow(newLiteral))
                        {
                            RotateFromLeft(node, rightOperator, newLiteral);
                        }
                    }
                }
                else if (node.OperatorToken == JSToken.Minus)
                {
                    // minus-minus
                    ConstantWrapper newLiteral = NumericAddition(thisConstant, otherConstant);
                    if (newLiteral != null && NoOverflow(newLiteral))
                    {
                        // but we need to swap the left and right operands first
                        rightOperator.SwapOperands();

                        // then rotate the node up after replacing old with new
                        RotateFromRight(node, rightOperator, newLiteral);
                    }
                }
            }
            else if (node.OperatorToken == JSToken.Multiply)
            {
                if (rightOperator.OperatorToken == JSToken.Multiply)
                {
                    // mult-mult
                    ConstantWrapper newLiteral = Multiply(thisConstant, otherConstant);
                    if (newLiteral != null && NoMultiplicativeOverOrUnderFlow(thisConstant, otherConstant, newLiteral))
                    {
                        RotateFromLeft(node, rightOperator, newLiteral);
                    }
                }
                else if (rightOperator.OperatorToken == JSToken.Divide)
                {
                    // mult-divide
                    ConstantWrapper otherOverThis = Divide(otherConstant, thisConstant);
                    ConstantWrapper thisOverOther = Divide(thisConstant, otherConstant);

                    int otherOverThisLength = otherOverThis != null ? otherOverThis.ToCode().Length : int.MaxValue;
                    int thisOverOtherLength = thisOverOther != null ? thisOverOther.ToCode().Length : int.MaxValue;

                    if (otherOverThis != null && NoMultiplicativeOverOrUnderFlow(otherConstant, thisConstant, otherOverThis)
                        && (thisOverOther == null || otherOverThisLength < thisOverOtherLength))
                    {
                        if (otherOverThisLength <= thisConstant.ToCode().Length + otherConstant.ToCode().Length + 1)
                        {
                            // swap the operands, but keep the operator
                            RotateFromLeft(node, rightOperator, otherOverThis);
                        }
                    }
                    else if (thisOverOther != null && NoMultiplicativeOverOrUnderFlow(thisConstant, otherConstant, thisOverOther))
                    {
                        if (thisOverOtherLength <= thisConstant.ToCode().Length + otherConstant.ToCode().Length + 1)
                        {
                            // keep the order, but opposite operator
                            rightOperator.OperatorToken = JSToken.Multiply;
                            RotateFromRight(node, rightOperator, thisOverOther);
                        }
                    }
                }
            }
            else if (node.OperatorToken == JSToken.Divide)
            {
                if (rightOperator.OperatorToken == JSToken.Multiply)
                {
                    // divide-mult
                    ConstantWrapper newLiteral = Divide(thisConstant, otherConstant);
                    if (newLiteral != null && NoMultiplicativeOverOrUnderFlow(thisConstant, otherConstant, newLiteral)
                        && newLiteral.ToCode().Length <= thisConstant.ToCode().Length + otherConstant.ToCode().Length + 1)
                    {
                        // swap the operands
                        rightOperator.SwapOperands();

                        // change the operator
                        rightOperator.OperatorToken = JSToken.Divide;
                        RotateFromRight(node, rightOperator, newLiteral);
                    }
                }
                else if (rightOperator.OperatorToken == JSToken.Divide)
                {
                    // divide-divide
                    ConstantWrapper newLiteral = Multiply(thisConstant, otherConstant);
                    if (newLiteral != null && NoMultiplicativeOverOrUnderFlow(thisConstant, otherConstant, newLiteral))
                    {
                        // but we need to swap the left and right operands first
                        rightOperator.SwapOperands();

                        // then rotate the node up after replacing old with new
                        RotateFromRight(node, rightOperator, newLiteral);
                    }
                }
            }
        }

        #endregion

        #region Constant operation methods

        private ConstantWrapper Multiply(ConstantWrapper left, ConstantWrapper right)
        {
            ConstantWrapper newLiteral = null;

            if (left.IsOkayToCombine && right.IsOkayToCombine
                && m_parser.Settings.IsModificationAllowed(TreeModifications.EvaluateNumericExpressions))
            {
                try
                {
                    double leftValue = left.ToNumber();
                    double rightValue = right.ToNumber();
                    double result = leftValue * rightValue;

                    if (ConstantWrapper.NumberIsOkayToCombine(result))
                    {
                        newLiteral = new ConstantWrapper(result, PrimitiveType.Number, null, m_parser);
                    }
                    else
                    {
                        if (!left.IsNumericLiteral && ConstantWrapper.NumberIsOkayToCombine(leftValue))
                        {
                            left.Parent.ReplaceChild(left, new ConstantWrapper(leftValue, PrimitiveType.Number, left.Context, m_parser));
                        }
                        if (!right.IsNumericLiteral && ConstantWrapper.NumberIsOkayToCombine(rightValue))
                        {
                            right.Parent.ReplaceChild(right, new ConstantWrapper(rightValue, PrimitiveType.Number, right.Context, m_parser));
                        }
                    }
                }
                catch (InvalidCastException)
                {
                    // some kind of casting in ToNumber caused a situation where we don't want
                    // to perform the combination on these operands
                }
            }

            return newLiteral;
        }

        private ConstantWrapper Divide(ConstantWrapper left, ConstantWrapper right)
        {
            ConstantWrapper newLiteral = null;

            if (left.IsOkayToCombine && right.IsOkayToCombine
                && m_parser.Settings.IsModificationAllowed(TreeModifications.EvaluateNumericExpressions))
            {
                try
                {
                    double leftValue = left.ToNumber();
                    double rightValue = right.ToNumber();
                    double result = leftValue / rightValue;

                    if (ConstantWrapper.NumberIsOkayToCombine(result))
                    {
                        newLiteral = new ConstantWrapper(result, PrimitiveType.Number, null, m_parser);
                    }
                    else
                    {
                        if (!left.IsNumericLiteral && ConstantWrapper.NumberIsOkayToCombine(leftValue))
                        {
                            left.Parent.ReplaceChild(left, new ConstantWrapper(leftValue, PrimitiveType.Number, left.Context, m_parser));
                        }
                        if (!right.IsNumericLiteral && ConstantWrapper.NumberIsOkayToCombine(rightValue))
                        {
                            right.Parent.ReplaceChild(right, new ConstantWrapper(rightValue, PrimitiveType.Number, right.Context, m_parser));
                        }
                    }
                }
                catch (InvalidCastException)
                {
                    // some kind of casting in ToNumber caused a situation where we don't want
                    // to perform the combination on these operands
                }
            }

            return newLiteral;
        }

        private ConstantWrapper Modulo(ConstantWrapper left, ConstantWrapper right)
        {
            ConstantWrapper newLiteral = null;

            if (left.IsOkayToCombine && right.IsOkayToCombine
                && m_parser.Settings.IsModificationAllowed(TreeModifications.EvaluateNumericExpressions))
            {
                try
                {
                    double leftValue = left.ToNumber();
                    double rightValue = right.ToNumber();
                    double result = leftValue % rightValue;

                    if (ConstantWrapper.NumberIsOkayToCombine(result))
                    {
                        newLiteral = new ConstantWrapper(result, PrimitiveType.Number, null, m_parser);
                    }
                    else
                    {
                        if (!left.IsNumericLiteral && ConstantWrapper.NumberIsOkayToCombine(leftValue))
                        {
                            left.Parent.ReplaceChild(left, new ConstantWrapper(leftValue, PrimitiveType.Number, left.Context, m_parser));
                        }
                        if (!right.IsNumericLiteral && ConstantWrapper.NumberIsOkayToCombine(rightValue))
                        {
                            right.Parent.ReplaceChild(right, new ConstantWrapper(rightValue, PrimitiveType.Number, right.Context, m_parser));
                        }
                    }
                }
                catch (InvalidCastException)
                {
                    // some kind of casting in ToNumber caused a situation where we don't want
                    // to perform the combination on these operands
                }
            }

            return newLiteral;
        }

        private ConstantWrapper Plus(ConstantWrapper left, ConstantWrapper right)
        {
            ConstantWrapper newLiteral = null;

            if (left.IsStringLiteral || right.IsStringLiteral)
            {
                // one or both are strings -- this is a strng concat operation
                newLiteral = StringConcat(left, right);
            }
            else
            {
                // neither are strings -- this is a numeric addition operation
                newLiteral = NumericAddition(left, right);
            }
            return newLiteral;
        }

        private ConstantWrapper NumericAddition(ConstantWrapper left, ConstantWrapper right)
        {
            ConstantWrapper newLiteral = null;

            if (left.IsOkayToCombine && right.IsOkayToCombine
                && m_parser.Settings.IsModificationAllowed(TreeModifications.EvaluateNumericExpressions))
            {
                try
                {
                    double leftValue = left.ToNumber();
                    double rightValue = right.ToNumber();
                    double result = leftValue + rightValue;

                    if (ConstantWrapper.NumberIsOkayToCombine(result))
                    {
                        newLiteral = new ConstantWrapper(result, PrimitiveType.Number, null, m_parser);
                    }
                    else
                    {
                        if (!left.IsNumericLiteral && ConstantWrapper.NumberIsOkayToCombine(leftValue))
                        {
                            left.Parent.ReplaceChild(left, new ConstantWrapper(leftValue, PrimitiveType.Number, left.Context, m_parser));
                        }
                        if (!right.IsNumericLiteral && ConstantWrapper.NumberIsOkayToCombine(rightValue))
                        {
                            right.Parent.ReplaceChild(right, new ConstantWrapper(rightValue, PrimitiveType.Number, right.Context, m_parser));
                        }
                    }
                }
                catch (InvalidCastException)
                {
                    // some kind of casting in ToNumber caused a situation where we don't want
                    // to perform the combination on these operands
                }
            }

            return newLiteral;
        }

        private ConstantWrapper StringConcat(ConstantWrapper left, ConstantWrapper right)
        {
            ConstantWrapper newLiteral = null;

            // if we don't want to combine adjacent string literals, then we know we don't want to do
            // anything here.
            if (m_parser.Settings.IsModificationAllowed(TreeModifications.CombineAdjacentStringLiterals))
            {
                // if either one of the operands is not a string literal, then check to see if we allow
                // evaluation of numeric expression; if not, then no-go. IF they are both string literals,
                // then it doesn't matter what the numeric flag says.
                if ((left.IsStringLiteral && right.IsStringLiteral)
                    || m_parser.Settings.IsModificationAllowed(TreeModifications.EvaluateNumericExpressions))
                {
                    // if either value is a floating-point number (a number, not NaN, not Infinite, not an Integer),
                    // then we won't do the string concatenation because different browsers may have subtle differences
                    // in their double-to-string conversion algorithms.
                    // so if neither is a numeric literal, or if one or both are, if they are both integer literals
                    // in the range that we can EXACTLY represent them in a double, then we can proceed.
                    // NaN, +Infinity and -Infinity are also acceptable
                    if ((!left.IsNumericLiteral || left.IsExactInteger || left.IsNaN || left.IsInfinity)
                        && (!right.IsNumericLiteral || right.IsExactInteger || right.IsNaN || right.IsInfinity))
                    {
                        // they are both either string, bool, null, or integer 
                        newLiteral = new ConstantWrapper(left.ToString() + right.ToString(), PrimitiveType.String, null, m_parser);
                    }
                }
            }

            return newLiteral;
        }

        private ConstantWrapper Minus(ConstantWrapper left, ConstantWrapper right)
        {
            ConstantWrapper newLiteral = null;

            if (left.IsOkayToCombine && right.IsOkayToCombine
                && m_parser.Settings.IsModificationAllowed(TreeModifications.EvaluateNumericExpressions))
            {
                try
                {
                    double leftValue = left.ToNumber();
                    double rightValue = right.ToNumber();
                    double result = leftValue - rightValue;

                    if (ConstantWrapper.NumberIsOkayToCombine(result))
                    {
                        newLiteral = new ConstantWrapper(result, PrimitiveType.Number, null, m_parser);
                    }
                    else
                    {
                        if (!left.IsNumericLiteral && ConstantWrapper.NumberIsOkayToCombine(leftValue))
                        {
                            left.Parent.ReplaceChild(left, new ConstantWrapper(leftValue, PrimitiveType.Number, left.Context, m_parser));
                        }
                        if (!right.IsNumericLiteral && ConstantWrapper.NumberIsOkayToCombine(rightValue))
                        {
                            right.Parent.ReplaceChild(right, new ConstantWrapper(rightValue, PrimitiveType.Number, right.Context, m_parser));
                        }
                    }
                }
                catch (InvalidCastException)
                {
                    // some kind of casting in ToNumber caused a situation where we don't want
                    // to perform the combination on these operands
                }
            }

            return newLiteral;
        }

        private ConstantWrapper LeftShift(ConstantWrapper left, ConstantWrapper right)
        {
            ConstantWrapper newLiteral = null;

            if (m_parser.Settings.IsModificationAllowed(TreeModifications.EvaluateNumericExpressions))
            {
                try
                {
                    // left-hand value is a 32-bit signed integer
                    Int32 lvalue = left.ToInt32();

                    // mask only the bottom 5 bits of the right-hand value
                    int rvalue = (int)(right.ToUInt32() & 0x1F);

                    // convert the result to a double
                    double result = Convert.ToDouble(lvalue << rvalue);
                    newLiteral = new ConstantWrapper(result, PrimitiveType.Number, null, m_parser);
                }
                catch (InvalidCastException)
                {
                    // some kind of casting in ToNumber caused a situation where we don't want
                    // to perform the combination on these operands
                }
            }
            return newLiteral;
        }

        private ConstantWrapper RightShift(ConstantWrapper left, ConstantWrapper right)
        {
            ConstantWrapper newLiteral = null;

            if (m_parser.Settings.IsModificationAllowed(TreeModifications.EvaluateNumericExpressions))
            {
                try
                {
                    // left-hand value is a 32-bit signed integer
                    Int32 lvalue = left.ToInt32();

                    // mask only the bottom 5 bits of the right-hand value
                    int rvalue = (int)(right.ToUInt32() & 0x1F);

                    // convert the result to a double
                    double result = Convert.ToDouble(lvalue >> rvalue);
                    newLiteral = new ConstantWrapper(result, PrimitiveType.Number, null, m_parser);
                }
                catch (InvalidCastException)
                {
                    // some kind of casting in ToNumber caused a situation where we don't want
                    // to perform the combination on these operands
                }
            }

            return newLiteral;
        }

        private ConstantWrapper UnsignedRightShift(ConstantWrapper left, ConstantWrapper right)
        {
            ConstantWrapper newLiteral = null;

            if (m_parser.Settings.IsModificationAllowed(TreeModifications.EvaluateNumericExpressions))
            {
                try
                {
                    // left-hand value is a 32-bit signed integer
                    UInt32 lvalue = left.ToUInt32();

                    // mask only the bottom 5 bits of the right-hand value
                    int rvalue = (int)(right.ToUInt32() & 0x1F);

                    // convert the result to a double
                    double result = Convert.ToDouble(lvalue >> rvalue);
                    newLiteral = new ConstantWrapper(result, PrimitiveType.Number, null, m_parser);
                }
                catch (InvalidCastException)
                {
                    // some kind of casting in ToNumber caused a situation where we don't want
                    // to perform the combination on these operands
                }
            }

            return newLiteral;
        }

        private ConstantWrapper LessThan(ConstantWrapper left, ConstantWrapper right)
        {
            ConstantWrapper newLiteral = null;

            if (m_parser.Settings.IsModificationAllowed(TreeModifications.EvaluateNumericExpressions))
            {
                if (left.IsStringLiteral && right.IsStringLiteral)
                {
                    // do a straight ordinal comparison of the strings
                    newLiteral = new ConstantWrapper(string.CompareOrdinal(left.ToString(), right.ToString()) < 0, PrimitiveType.Boolean, null, m_parser);
                }
                else
                {
                    try
                    {
                        // either one or both are NOT a string -- numeric comparison
                        if (left.IsOkayToCombine && right.IsOkayToCombine)
                        {
                            newLiteral = new ConstantWrapper(left.ToNumber() < right.ToNumber(), PrimitiveType.Boolean, null, m_parser);
                        }
                    }
                    catch (InvalidCastException)
                    {
                        // some kind of casting in ToNumber caused a situation where we don't want
                        // to perform the combination on these operands
                    }
                }
            }
            return newLiteral;
        }

        private ConstantWrapper LessThanOrEqual(ConstantWrapper left, ConstantWrapper right)
        {
            ConstantWrapper newLiteral = null;

            if (m_parser.Settings.IsModificationAllowed(TreeModifications.EvaluateNumericExpressions))
            {
                if (left.IsStringLiteral && right.IsStringLiteral)
                {
                    // do a straight ordinal comparison of the strings
                    newLiteral = new ConstantWrapper(string.CompareOrdinal(left.ToString(), right.ToString()) <= 0, PrimitiveType.Boolean, null, m_parser);
                }
                else
                {
                    try
                    {
                        // either one or both are NOT a string -- numeric comparison
                        if (left.IsOkayToCombine && right.IsOkayToCombine)
                        {
                            newLiteral = new ConstantWrapper(left.ToNumber() <= right.ToNumber(), PrimitiveType.Boolean, null, m_parser);
                        }
                    }
                    catch (InvalidCastException)
                    {
                        // some kind of casting in ToNumber caused a situation where we don't want
                        // to perform the combination on these operands
                    }
                }
            }

            return newLiteral;
        }

        private ConstantWrapper GreaterThan(ConstantWrapper left, ConstantWrapper right)
        {
            ConstantWrapper newLiteral = null;

            if (m_parser.Settings.IsModificationAllowed(TreeModifications.EvaluateNumericExpressions))
            {
                if (left.IsStringLiteral && right.IsStringLiteral)
                {
                    // do a straight ordinal comparison of the strings
                    newLiteral = new ConstantWrapper(string.CompareOrdinal(left.ToString(), right.ToString()) > 0, PrimitiveType.Boolean, null, m_parser);
                }
                else
                {
                    try
                    {
                        // either one or both are NOT a string -- numeric comparison
                        if (left.IsOkayToCombine && right.IsOkayToCombine)
                        {
                            newLiteral = new ConstantWrapper(left.ToNumber() > right.ToNumber(), PrimitiveType.Boolean, null, m_parser);
                        }
                    }
                    catch (InvalidCastException)
                    {
                        // some kind of casting in ToNumber caused a situation where we don't want
                        // to perform the combination on these operands
                    }
                }
            }

            return newLiteral;
        }

        private ConstantWrapper GreaterThanOrEqual(ConstantWrapper left, ConstantWrapper right)
        {
            ConstantWrapper newLiteral = null;

            if (m_parser.Settings.IsModificationAllowed(TreeModifications.EvaluateNumericExpressions))
            {
                if (left.IsStringLiteral && right.IsStringLiteral)
                {
                    // do a straight ordinal comparison of the strings
                    newLiteral = new ConstantWrapper(string.CompareOrdinal(left.ToString(), right.ToString()) >= 0, PrimitiveType.Boolean, null, m_parser);
                }
                else
                {
                    try
                    {
                        // either one or both are NOT a string -- numeric comparison
                        if (left.IsOkayToCombine && right.IsOkayToCombine)
                        {
                            newLiteral = new ConstantWrapper(left.ToNumber() >= right.ToNumber(), PrimitiveType.Boolean, null, m_parser);
                        }
                    }
                    catch (InvalidCastException)
                    {
                        // some kind of casting in ToNumber caused a situation where we don't want
                        // to perform the combination on these operands
                    }
                }
            }

            return newLiteral;
        }

        private ConstantWrapper Equal(ConstantWrapper left, ConstantWrapper right)
        {
            ConstantWrapper newLiteral = null;

            if (m_parser.Settings.IsModificationAllowed(TreeModifications.EvaluateNumericExpressions))
            {
                PrimitiveType leftType = left.PrimitiveType;
                if (leftType == right.PrimitiveType)
                {
                    // the values are the same type
                    switch (leftType)
                    {
                        case PrimitiveType.Null:
                            // null == null is true
                            newLiteral = new ConstantWrapper(true, PrimitiveType.Boolean, null, m_parser);
                            break;

                        case PrimitiveType.Boolean:
                            // compare boolean values
                            newLiteral = new ConstantWrapper(left.ToBoolean() == right.ToBoolean(), PrimitiveType.Boolean, null, m_parser);
                            break;

                        case PrimitiveType.String:
                            // compare string ordinally
                            newLiteral = new ConstantWrapper(string.CompareOrdinal(left.ToString(), right.ToString()) == 0, PrimitiveType.Boolean, null, m_parser);
                            break;

                        case PrimitiveType.Number:
                            try
                            {
                                // compare the values
                                // +0 and -0 are treated as "equal" in C#, so we don't need to test them separately.
                                // and NaN is always unequal to everything else, including itself.
                                if (left.IsOkayToCombine && right.IsOkayToCombine)
                                {
                                    newLiteral = new ConstantWrapper(left.ToNumber() == right.ToNumber(), PrimitiveType.Boolean, null, m_parser);
                                }
                            }
                            catch (InvalidCastException)
                            {
                                // some kind of casting in ToNumber caused a situation where we don't want
                                // to perform the combination on these operands
                            }
                            break;
                    }
                }
                else if (left.IsOkayToCombine && right.IsOkayToCombine)
                {
                    try
                    {
                        // numeric comparison
                        // +0 and -0 are treated as "equal" in C#, so we don't need to test them separately.
                        // and NaN is always unequal to everything else, including itself.
                        newLiteral = new ConstantWrapper(left.ToNumber() == right.ToNumber(), PrimitiveType.Boolean, null, m_parser);
                    }
                    catch (InvalidCastException)
                    {
                        // some kind of casting in ToNumber caused a situation where we don't want
                        // to perform the combination on these operands
                    }
                }
            }

            return newLiteral;
        }

        private ConstantWrapper NotEqual(ConstantWrapper left, ConstantWrapper right)
        {
            ConstantWrapper newLiteral = null;

            if (m_parser.Settings.IsModificationAllowed(TreeModifications.EvaluateNumericExpressions))
            {
                PrimitiveType leftType = left.PrimitiveType;
                if (leftType == right.PrimitiveType)
                {
                    // the values are the same type
                    switch (leftType)
                    {
                        case PrimitiveType.Null:
                            // null != null is false
                            newLiteral = new ConstantWrapper(false, PrimitiveType.Boolean, null, m_parser);
                            break;

                        case PrimitiveType.Boolean:
                            // compare boolean values
                            newLiteral = new ConstantWrapper(left.ToBoolean() != right.ToBoolean(), PrimitiveType.Boolean, null, m_parser);
                            break;

                        case PrimitiveType.String:
                            // compare string ordinally
                            newLiteral = new ConstantWrapper(string.CompareOrdinal(left.ToString(), right.ToString()) != 0, PrimitiveType.Boolean, null, m_parser);
                            break;

                        case PrimitiveType.Number:
                            try
                            {
                                // compare the values
                                // +0 and -0 are treated as "equal" in C#, so we don't need to test them separately.
                                // and NaN is always unequal to everything else, including itself.
                                if (left.IsOkayToCombine && right.IsOkayToCombine)
                                {
                                    newLiteral = new ConstantWrapper(left.ToNumber() != right.ToNumber(), PrimitiveType.Boolean, null, m_parser);
                                }
                            }
                            catch (InvalidCastException)
                            {
                                // some kind of casting in ToNumber caused a situation where we don't want
                                // to perform the combination on these operands
                            }
                            break;
                    }
                }
                else if (left.IsOkayToCombine && right.IsOkayToCombine)
                {
                    try
                    {
                        // numeric comparison
                        // +0 and -0 are treated as "equal" in C#, so we don't need to test them separately.
                        // and NaN is always unequal to everything else, including itself.
                        newLiteral = new ConstantWrapper(left.ToNumber() != right.ToNumber(), PrimitiveType.Boolean, null, m_parser);
                    }
                    catch (InvalidCastException)
                    {
                        // some kind of casting in ToNumber caused a situation where we don't want
                        // to perform the combination on these operands
                    }
                }
            }

            return newLiteral;
        }

        private ConstantWrapper StrictEqual(ConstantWrapper left, ConstantWrapper right)
        {
            ConstantWrapper newLiteral = null;

            if (m_parser.Settings.IsModificationAllowed(TreeModifications.EvaluateNumericExpressions))
            {
                PrimitiveType leftType = left.PrimitiveType;
                if (leftType == right.PrimitiveType)
                {
                    // the values are the same type
                    switch (leftType)
                    {
                        case PrimitiveType.Null:
                            // null === null is true
                            newLiteral = new ConstantWrapper(true, PrimitiveType.Boolean, null, m_parser);
                            break;

                        case PrimitiveType.Boolean:
                            // compare boolean values
                            newLiteral = new ConstantWrapper(left.ToBoolean() == right.ToBoolean(), PrimitiveType.Boolean, null, m_parser);
                            break;

                        case PrimitiveType.String:
                            // compare string ordinally
                            newLiteral = new ConstantWrapper(string.CompareOrdinal(left.ToString(), right.ToString()) == 0, PrimitiveType.Boolean, null, m_parser);
                            break;

                        case PrimitiveType.Number:
                            try
                            {
                                // compare the values
                                // +0 and -0 are treated as "equal" in C#, so we don't need to test them separately.
                                // and NaN is always unequal to everything else, including itself.
                                if (left.IsOkayToCombine && right.IsOkayToCombine)
                                {
                                    newLiteral = new ConstantWrapper(left.ToNumber() == right.ToNumber(), PrimitiveType.Boolean, null, m_parser);
                                }
                            }
                            catch (InvalidCastException)
                            {
                                // some kind of casting in ToNumber caused a situation where we don't want
                                // to perform the combination on these operands
                            }
                            break;
                    }
                }
                else
                {
                    // if they aren't the same type, they ain't equal
                    newLiteral = new ConstantWrapper(false, PrimitiveType.Boolean, null, m_parser);
                }
            }

            return newLiteral;
        }

        private ConstantWrapper StrictNotEqual(ConstantWrapper left, ConstantWrapper right)
        {
            ConstantWrapper newLiteral = null;

            if (m_parser.Settings.IsModificationAllowed(TreeModifications.EvaluateNumericExpressions))
            {
                PrimitiveType leftType = left.PrimitiveType;
                if (leftType == right.PrimitiveType)
                {
                    // the values are the same type
                    switch (leftType)
                    {
                        case PrimitiveType.Null:
                            // null !== null is false
                            newLiteral = new ConstantWrapper(false, PrimitiveType.Boolean, null, m_parser);
                            break;

                        case PrimitiveType.Boolean:
                            // compare boolean values
                            newLiteral = new ConstantWrapper(left.ToBoolean() != right.ToBoolean(), PrimitiveType.Boolean, null, m_parser);
                            break;

                        case PrimitiveType.String:
                            // compare string ordinally
                            newLiteral = new ConstantWrapper(string.CompareOrdinal(left.ToString(), right.ToString()) != 0, PrimitiveType.Boolean, null, m_parser);
                            break;

                        case PrimitiveType.Number:
                            try
                            {
                                // compare the values
                                // +0 and -0 are treated as "equal" in C#, so we don't need to test them separately.
                                // and NaN is always unequal to everything else, including itself.
                                if (left.IsOkayToCombine && right.IsOkayToCombine)
                                {
                                    newLiteral = new ConstantWrapper(left.ToNumber() != right.ToNumber(), PrimitiveType.Boolean, null, m_parser);
                                }
                            }
                            catch (InvalidCastException)
                            {
                                // some kind of casting in ToNumber caused a situation where we don't want
                                // to perform the combination on these operands
                            }
                            break;
                    }
                }
                else
                {
                    // if they aren't the same type, they are not equal
                    newLiteral = new ConstantWrapper(true, PrimitiveType.Boolean, null, m_parser);
                }
            }

            return newLiteral;
        }

        private ConstantWrapper BitwiseAnd(ConstantWrapper left, ConstantWrapper right)
        {
            ConstantWrapper newLiteral = null;

            if (m_parser.Settings.IsModificationAllowed(TreeModifications.EvaluateNumericExpressions))
            {
                try
                {
                    Int32 lValue = left.ToInt32();
                    Int32 rValue = right.ToInt32();
                    newLiteral = new ConstantWrapper(Convert.ToDouble(lValue & rValue), PrimitiveType.Number, null, m_parser);
                }
                catch (InvalidCastException)
                {
                    // some kind of casting in ToNumber caused a situation where we don't want
                    // to perform the combination on these operands
                }
            }

            return newLiteral;
        }

        private ConstantWrapper BitwiseOr(ConstantWrapper left, ConstantWrapper right)
        {
            ConstantWrapper newLiteral = null;

            if (m_parser.Settings.IsModificationAllowed(TreeModifications.EvaluateNumericExpressions))
            {
                try
                {
                    Int32 lValue = left.ToInt32();
                    Int32 rValue = right.ToInt32();
                    newLiteral = new ConstantWrapper(Convert.ToDouble(lValue | rValue), PrimitiveType.Number, null, m_parser);
                }
                catch (InvalidCastException)
                {
                    // some kind of casting in ToNumber caused a situation where we don't want
                    // to perform the combination on these operands
                }
            }

            return newLiteral;
        }

        private ConstantWrapper BitwiseXor(ConstantWrapper left, ConstantWrapper right)
        {
            ConstantWrapper newLiteral = null;

            if (m_parser.Settings.IsModificationAllowed(TreeModifications.EvaluateNumericExpressions))
            {
                try
                {
                    Int32 lValue = left.ToInt32();
                    Int32 rValue = right.ToInt32();
                    newLiteral = new ConstantWrapper(Convert.ToDouble(lValue ^ rValue), PrimitiveType.Number, null, m_parser);
                }
                catch (InvalidCastException)
                {
                    // some kind of casting in ToNumber caused a situation where we don't want
                    // to perform the combination on these operands
                }
            }

            return newLiteral;
        }

        private ConstantWrapper LogicalAnd(ConstantWrapper left, ConstantWrapper right)
        {
            ConstantWrapper newLiteral = null;
            if (m_parser.Settings.IsModificationAllowed(TreeModifications.EvaluateNumericExpressions))
            {
                try
                {
                    // if the left-hand side evaluates to true, return the right-hand side.
                    // if the left-hand side is false, return it.
                    newLiteral = left.ToBoolean() ? right : left;
                }
                catch (InvalidCastException)
                {
                    // if we couldn't cast to bool, ignore
                }
            }

            return newLiteral;
        }

        private ConstantWrapper LogicalOr(ConstantWrapper left, ConstantWrapper right)
        {
            ConstantWrapper newLiteral = null;
            if (m_parser.Settings.IsModificationAllowed(TreeModifications.EvaluateNumericExpressions))
            {
                try
                {
                    // if the left-hand side evaluates to true, return the left-hand side.
                    // if the left-hand side is false, return the right-hand side.
                    newLiteral = left.ToBoolean() ? left : right;
                }
                catch (InvalidCastException)
                {
                    // if we couldn't cast to bool, ignore
                }
            }

            return newLiteral;
        }

        #endregion

        //
        // IVisitor implementations
        //

        public override void Visit(BinaryOperator node)
        {
            if (node != null)
            {
                // depth-first
                base.Visit(node);

                if (m_parser.Settings.EvalLiteralExpressions)
                {
                    // if this is an assign operator, an in, or an instanceof, then we won't
                    // try to evaluate it
                    if (!node.IsAssign && node.OperatorToken != JSToken.In && node.OperatorToken != JSToken.InstanceOf)
                    {
                        if (node.OperatorToken == JSToken.StrictEqual || node.OperatorToken == JSToken.StrictNotEqual)
                        {
                            // the operator is a strict equality (or not-equal).
                            // check the primitive types of the two operands -- if they are known but not the same, we can
                            // shortcut the whole process by just replacing this node with a boolean literal.
                            var leftType = node.Operand1.FindPrimitiveType();
                            if (leftType != PrimitiveType.Other)
                            {
                                var rightType = node.Operand2.FindPrimitiveType();
                                if (rightType != PrimitiveType.Other)
                                {
                                    // both sides are known
                                    if (leftType != rightType)
                                    {
                                        // they are not the same type -- replace with a boolean and bail
                                        node.Parent.ReplaceChild(
                                            node,
                                            new ConstantWrapper(node.OperatorToken == JSToken.StrictEqual ? false : true, PrimitiveType.Boolean, node.Context, m_parser));
                                        return;
                                    }

                                    // they are the same type -- we can change the operator to simple equality/not equality
                                    node.OperatorToken = node.OperatorToken == JSToken.StrictEqual ? JSToken.Equal : JSToken.NotEqual;
                                }
                            }
                        }

                        // see if the left operand is a literal number, boolean, string, or null
                        ConstantWrapper left = node.Operand1 as ConstantWrapper;
                        if (left != null)
                        {
                            if (node.OperatorToken == JSToken.Comma)
                            {
                                // the comma operator evaluates the left, then evaluates the right and returns it.
                                // but if the left is a literal, evaluating it doesn't DO anything, so we can replace the
                                // entire operation with the right-hand operand
                                ConstantWrapper rightConstant = node.Operand2 as ConstantWrapper;
                                if (rightConstant != null)
                                {
                                    // we'll replace the operator with the right-hand operand, BUT it's a constant, too.
                                    // first check to see if replacing this node with a constant will result in creating
                                    // a member-bracket operator that can be turned into a member-dot. If it is, then that
                                    // method will handle the replacement. But if it doesn't, then we should just replace
                                    // the comma with the right-hand operand.
                                    if (!ReplaceMemberBracketWithDot(node, rightConstant))
                                    {
                                        node.Parent.ReplaceChild(node, rightConstant);
                                    }
                                }
                                else
                                {
                                    // replace the comma operator with the right-hand operand
                                    node.Parent.ReplaceChild(node, node.Operand2);
                                }
                            }
                            else
                            {
                                // see if the right operand is a literal number, boolean, string, or null
                                ConstantWrapper right = node.Operand2 as ConstantWrapper;
                                if (right != null)
                                {
                                    // then they are both constants and we can evaluate the operation
                                    EvalThisOperator(node, left, right);
                                }
                                else
                                {
                                    // see if the right is a binary operator that can be combined with ours
                                    BinaryOperator rightBinary = node.Operand2 as BinaryOperator;
                                    if (rightBinary != null)
                                    {
                                        ConstantWrapper rightLeft = rightBinary.Operand1 as ConstantWrapper;
                                        if (rightLeft != null)
                                        {
                                            // eval our left and the right-hand binary's left and put the combined operation as
                                            // the child of the right-hand binary
                                            EvalToTheRight(node, left, rightLeft, rightBinary);
                                        }
                                        else
                                        {
                                            ConstantWrapper rightRight = rightBinary.Operand2 as ConstantWrapper;
                                            if (rightRight != null)
                                            {
                                                EvalFarToTheRight(node, left, rightRight, rightBinary);
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        else
                        {
                            // left is not a constantwrapper. See if the right is
                            ConstantWrapper right = node.Operand2 as ConstantWrapper;
                            if (right != null)
                            {
                                // the right is a constant. See if the the left is a binary operator...
                                BinaryOperator leftBinary = node.Operand1 as BinaryOperator;
                                if (leftBinary != null)
                                {
                                    // ...with a constant on the right, and the operators can be combined
                                    ConstantWrapper leftRight = leftBinary.Operand2 as ConstantWrapper;
                                    if (leftRight != null)
                                    {
                                        EvalToTheLeft(node, right, leftRight, leftBinary);
                                    }
                                    else
                                    {
                                        ConstantWrapper leftLeft = leftBinary.Operand1 as ConstantWrapper;
                                        if (leftLeft != null)
                                        {
                                            EvalFarToTheLeft(node, right, leftLeft, leftBinary);
                                        }
                                    }
                                }
                                else if (m_parser.Settings.IsModificationAllowed(TreeModifications.SimplifyStringToNumericConversion))
                                {
                                    // see if it's a lookup and this is a minus operation and the constant is a zero
                                    Lookup lookup = node.Operand1 as Lookup;
                                    if (lookup != null && node.OperatorToken == JSToken.Minus && right.IsIntegerLiteral && right.ToNumber() == 0)
                                    {
                                        // okay, so we have "lookup - 0"
                                        // this is done frequently to force a value to be numeric. 
                                        // There is an easier way: apply the unary + operator to it. 
                                        NumericUnary unary = new NumericUnary(node.Context, m_parser, lookup, JSToken.Plus);
                                        node.Parent.ReplaceChild(node, unary);
                                    }
                                }
                            }
                            // TODO: shouldn't we check if they BOTH are binary operators? (a*6)*(5*b) ==> a*30*b (for instance)
                        }
                    }
                }
            }
        }

        public override void Visit(Conditional node)
        {
            if (node != null)
            {
                // depth-first
                base.Visit(node);

                if (m_parser.Settings.IsModificationAllowed(TreeModifications.EvaluateNumericExpressions))
                {
                    // if the condition is a literal, evaluating the condition doesn't do anything, AND
                    // we know now whether it's true or not.
                    ConstantWrapper literalCondition = node.Condition as ConstantWrapper;
                    if (literalCondition != null)
                    {
                        try
                        {
                            // if the boolean represenation of the literal is true, we can replace the condition operator
                            // with the true expression; otherwise we can replace it with the false expression
                            node.Parent.ReplaceChild(node, literalCondition.ToBoolean() ? node.TrueExpression : node.FalseExpression);
                        }
                        catch (InvalidCastException)
                        {
                            // ignore any invalid cast errors
                        }
                    }
                }
            }
        }

        public override void Visit(ConditionalCompilationElseIf node)
        {
            if (node != null)
            {
                // depth-first
                base.Visit(node);

                if (m_parser.Settings.IsModificationAllowed(TreeModifications.EvaluateNumericExpressions))
                {
                    // if the if-condition is a constant, we can eliminate one of the two branches
                    ConstantWrapper constantCondition = node.Condition as ConstantWrapper;
                    if (constantCondition != null)
                    {
                        // instead, replace the condition with a 1 if it's always true or a 0 if it's always false
                        if (constantCondition.IsNotOneOrPositiveZero)
                        {
                            try
                            {
                                node.ReplaceChild(node.Condition,
                                    new ConstantWrapper(constantCondition.ToBoolean() ? 1 : 0, PrimitiveType.Number, node.Condition.Context, m_parser));
                            }
                            catch (InvalidCastException)
                            {
                                // ignore any invalid cast exceptions
                            }
                        }
                    }
                }
            }
        }

        public override void Visit(ConditionalCompilationIf node)
        {
            if (node != null)
            {
                // depth-first
                base.Visit(node);

                if (m_parser.Settings.IsModificationAllowed(TreeModifications.EvaluateNumericExpressions))
                {
                    // if the if-condition is a constant, we can eliminate one of the two branches
                    ConstantWrapper constantCondition = node.Condition as ConstantWrapper;
                    if (constantCondition != null)
                    {
                        // instead, replace the condition with a 1 if it's always true or a 0 if it's always false
                        if (constantCondition.IsNotOneOrPositiveZero)
                        {
                            try
                            {
                                node.ReplaceChild(node.Condition,
                                    new ConstantWrapper(constantCondition.ToBoolean() ? 1 : 0, PrimitiveType.Number, node.Condition.Context, m_parser));
                            }
                            catch (InvalidCastException)
                            {
                                // ignore any invalid cast exceptions
                            }
                        }
                    }
                }
            }
        }

        public override void Visit(DoWhile node)
        {
            if (node != null)
            {
                // depth-first
                base.Visit(node);

                if (m_parser.Settings.IsModificationAllowed(TreeModifications.EvaluateNumericExpressions))
                {
                    // if the condition is a constant, we can simplify things
                    ConstantWrapper constantCondition = node.Condition as ConstantWrapper;
                    if (constantCondition != null && constantCondition.IsNotOneOrPositiveZero)
                    {
                        try
                        {
                            // the condition is a constant, so it is always either true or false
                            // we can replace the condition with a one or a zero -- only one byte
                            node.ReplaceChild(node.Condition,
                                new ConstantWrapper(constantCondition.ToBoolean() ? 1 : 0, PrimitiveType.Number, node.Condition.Context, m_parser));
                        }
                        catch (InvalidCastException)
                        {
                            // ignore any invalid cast errors
                        }
                    }
                }
            }
        }

        public override void Visit(ForNode node)
        {
            if (node != null)
            {
                // depth-first
                base.Visit(node);

                if (m_parser.Settings.IsModificationAllowed(TreeModifications.EvaluateNumericExpressions))
                {
                    ConstantWrapper constantCondition = node.Condition as ConstantWrapper;
                    if (constantCondition != null)
                    {
                        try
                        {
                            // if condition is always false, change it to a zero (only one byte)
                            // and if it is always true, remove it (default behavior)
                            if (constantCondition.ToBoolean())
                            {
                                // always true -- don't need a condition at all
                                node.ReplaceChild(node.Condition, null);
                            }
                            else if (constantCondition.IsNotOneOrPositiveZero)
                            {
                                // always false and it's not already a zero. Make it so (only one byte)
                                node.ReplaceChild(node.Condition, new ConstantWrapper(0, PrimitiveType.Number, node.Condition.Context, m_parser));
                            }
                        }
                        catch (InvalidCastException)
                        {
                            // ignore any invalid cast exceptions
                        }
                    }
                }
            }
        }

        public override void Visit(IfNode node)
        {
            if (node != null)
            {
                // depth-first
                base.Visit(node);

                if (m_parser.Settings.IsModificationAllowed(TreeModifications.EvaluateNumericExpressions))
                {
                    // if the if-condition is a constant, we can eliminate one of the two branches
                    ConstantWrapper constantCondition = node.Condition as ConstantWrapper;
                    if (constantCondition != null)
                    {
                        // instead, replace the condition with a 1 if it's always true or a 0 if it's always false
                        if (constantCondition.IsNotOneOrPositiveZero)
                        {
                            try
                            {
                                node.ReplaceChild(node.Condition,
                                    new ConstantWrapper(constantCondition.ToBoolean() ? 1 : 0, PrimitiveType.Number, node.Condition.Context, m_parser));
                            }
                            catch (InvalidCastException)
                            {
                                // ignore any invalid cast exceptions
                            }
                        }
                    }
                }
            }
        }

        public override void Visit(NumericUnary node)
        {
            if (node != null)
            {
                // depth-first
                base.Visit(node);

                if (m_parser.Settings.IsModificationAllowed(TreeModifications.EvaluateNumericExpressions))
                {
                    // see if our operand is a ConstantWrapper
                    ConstantWrapper literalOperand = node.Operand as ConstantWrapper;
                    if (literalOperand != null)
                    {
                        // must be number, boolean, string, or null
                        switch (node.OperatorToken)
                        {
                            case JSToken.Plus:
                                try
                                {
                                    // replace with a constant representing operand.ToNumber,
                                    node.Parent.ReplaceChild(node, new ConstantWrapper(literalOperand.ToNumber(), PrimitiveType.Number, node.Context, m_parser));
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
                                    node.Parent.ReplaceChild(node, new ConstantWrapper(-literalOperand.ToNumber(), PrimitiveType.Number, node.Context, m_parser));
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
                                    node.Parent.ReplaceChild(node, new ConstantWrapper(Convert.ToDouble(~literalOperand.ToInt32()), PrimitiveType.Number, node.Context, m_parser));
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
                                    node.Parent.ReplaceChild(node, new ConstantWrapper(!literalOperand.ToBoolean(), PrimitiveType.Boolean, node.Context, m_parser));
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
        }

        public override void Visit(TypeOfNode node)
        {
            if (node != null)
            {
                // depth-first
                base.Visit(node);

                if (m_parser.Settings.IsModificationAllowed(TreeModifications.EvaluateNumericExpressions))
                {
                    // see if our operand is a ConstantWrapper
                    ConstantWrapper literalOperand = node.Operand as ConstantWrapper;
                    if (literalOperand != null)
                    {
                        // either number, string, boolean, or null.
                        // the operand is a literal. Therefore we already know what the typeof
                        // operator will return. Just short-circuit that behavior now and replace the operator
                        // with a string literal of the proper value
                        string typeName = null;
                        if (literalOperand.IsStringLiteral)
                        {
                            // "string"
                            typeName = "string";
                        }
                        else if (literalOperand.IsNumericLiteral)
                        {
                            // "number"
                            typeName = "number";
                        }
                        else if (literalOperand.IsBooleanLiteral)
                        {
                            // "boolean"
                            typeName = "boolean";
                        }
                        else if (literalOperand.Value == null)
                        {
                            // "object"
                            typeName = "object";
                        }

                        if (!string.IsNullOrEmpty(typeName))
                        {
                            node.Parent.ReplaceChild(node, new ConstantWrapper(typeName, PrimitiveType.String, node.Context, m_parser));
                        }
                    }
                    else if (node.Operand is ObjectLiteral)
                    {
                        node.Parent.ReplaceChild(node, new ConstantWrapper("object", PrimitiveType.String, node.Context, m_parser));
                    }
                }
            }
        }

        public override void Visit(VoidNode node)
        {
            if (node != null)
            {
                // depth-first
                base.Visit(node);

                if (m_parser.Settings.IsModificationAllowed(TreeModifications.EvaluateNumericExpressions))
                {
                    // see if our operand is a ConstantWrapper
                    ConstantWrapper literalOperand = node.Operand as ConstantWrapper;
                    if (literalOperand != null)
                    {
                        // either number, string, boolean, or null.
                        // the void operator evaluates its operand and returns undefined. Since evaluating a literal
                        // does nothing, then it doesn't matter what the heck it is. Replace it with a zero -- a one-
                        // character literal.
                        node.ReplaceChild(literalOperand, new ConstantWrapper(0, PrimitiveType.Number, node.Context, m_parser));
                    }
                }
            }
        }

        public override void Visit(WhileNode node)
        {
            if (node != null)
            {
                // depth-first
                base.Visit(node);

                // see if the condition is a constant
                if (m_parser.Settings.IsModificationAllowed(TreeModifications.EvaluateNumericExpressions))
                {
                    ConstantWrapper constantCondition = node.Condition as ConstantWrapper;
                    if (constantCondition != null)
                    {
                        // TODO: (someday) we'd RATHER eliminate the statement altogether if the condition is always false,
                        // but we'd need to make sure var'd variables and declared functions are properly handled.
                        try
                        {
                            bool isTrue = constantCondition.ToBoolean();
                            if (isTrue)
                            {
                                // the condition is always true; we should change it to a for(;;) statement.
                                // less bytes than while(1)

                                // check to see if we want to combine a preceding var with a for-statement
                                AstNode initializer = null;
                                if (m_parser.Settings.IsModificationAllowed(TreeModifications.MoveVarIntoFor))
                                {
                                    // if the previous statement is a var, we can move it to the initializer
                                    // and save even more bytes. The parent should always be a block. If not, 
                                    // then assume there is no previous.
                                    Block parentBlock = node.Parent as Block;
                                    if (parentBlock != null)
                                    {
                                        int whileIndex = parentBlock.StatementIndex(node);
                                        if (whileIndex > 0)
                                        {
                                            Var previousVar = parentBlock[whileIndex - 1] as Var;
                                            if (previousVar != null)
                                            {
                                                initializer = previousVar;
                                                parentBlock.ReplaceChild(previousVar, null);
                                            }
                                        }
                                    }
                                }

                                // create the for using our body and replace ourselves with it
                                ForNode forNode = new ForNode(node.Context, m_parser, initializer, null, null, node.Body);
                                node.Parent.ReplaceChild(node, forNode);
                            }
                            else if (constantCondition.IsNotOneOrPositiveZero)
                            {
                                // the condition is always false, so we can replace the condition
                                // with a zero -- only one byte
                                node.ReplaceChild(node.Condition, new ConstantWrapper(0, PrimitiveType.Number, null, m_parser));
                            }
                        }
                        catch (InvalidCastException)
                        {
                            // ignore any invalid cast exceptions
                        }
                    }
                }
            }
        }
    }
}
