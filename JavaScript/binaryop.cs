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

    public sealed class BinaryOperator : AstNode
    {
        public AstNode Operand1 { get; private set; }
        public AstNode Operand2 { get; private set; }
        public JSToken OperatorToken { get; private set; }

        public BinaryOperator(Context context, JSParser parser, AstNode operand1, AstNode operand2, JSToken operatorToken)
            : base(context, parser)
        {
            if (operand1 != null) operand1.Parent = this;
            if (operand2 != null) operand2.Parent = this;
            Operand1 = operand1;
            Operand2 = operand2;
            OperatorToken = operatorToken;
        }

        public override AstNode Clone()
        {
            return new BinaryOperator(
                (Context == null ? null : Context.Clone()),
                Parser,
                (Operand1 == null ? null : Operand1.Clone()),
                (Operand2 == null ? null : Operand2.Clone()),
                OperatorToken
                );
        }

        public override IEnumerable<AstNode> Children
        {
            get
            {
                if (Operand1 != null)
                {
                    yield return Operand1;
                }
                if (Operand2 != null)
                {
                    yield return Operand2;
                }
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

        internal override void AnalyzeNode()
        {
            base.AnalyzeNode();

            // see if this operation is subtracting zero from a lookup -- that is typically done to
            // coerce a value to numeric. There's a simpler way: unary plus operator.
            if (OperatorToken == JSToken.Minus
                && Parser.Settings.IsModificationAllowed(TreeModifications.SimplifyStringToNumericConversion))
            {
                Lookup lookup = Operand1 as Lookup;
                if (lookup != null)
                {
                    ConstantWrapper right = Operand2 as ConstantWrapper;
                    if (right != null && right.IsIntegerLiteral && right.ToNumber() == 0)
                    {
                        // okay, so we have "lookup - 0"
                        // this is done frequently to force a value to be numeric. 
                        // There is an easier way: apply the unary + operator to it. 
                        NumericUnary unary = new NumericUnary(Context, Parser, lookup, JSToken.Plus);
                        Parent.ReplaceChild(this, unary);

                        // because we recursed at the top of this function, we don't need to Analyze
                        // the new Unary node. The AnalyzeNode method of NumericUnary only does something
                        // if the operand is a constant -- and this one is a Lookup. And we already analyzed
                        // the lookup.
                    }
                }
            }
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

        #region Literal-expression evaluation code

        /// <summary>
        /// This is where we evaluate operations on constant values
        /// </summary>
        public override void CleanupNodes()
        {
            // recurse, then see if we can evaluate our expression
            base.CleanupNodes();

            if (Parser.Settings.EvalLiteralExpressions)
            {
                // if this is an assign operator, an in, or an instanceof, then we won't
                // try to evaluate it
                if (!IsAssign && OperatorToken != JSToken.In && OperatorToken != JSToken.InstanceOf)
                {
                    // see if the left operand is a literal number, boolean, string, or null
                    ConstantWrapper left = Operand1 as ConstantWrapper;
                    if (left != null)
                    {
                        if (OperatorToken == JSToken.Comma)
                        {
                            // the comma operator evaluates the left, then evaluates the right and returns it.
                            // but if the left is a literal, evaluating it doesn't DO anything, so we can replace the
                            // entire operation with the right-hand operand
                            ConstantWrapper rightConstant = Operand2 as ConstantWrapper;
                            if (rightConstant != null)
                            {
                                if (!ReplaceMemberBracketWithDot(rightConstant))
                                {
                                    Parent.ReplaceChild(this, rightConstant);
                                }
                            }
                            else
                            {
                                Parent.ReplaceChild(this, Operand2);
                            }
                        }
                        else
                        {
                            // see if the right operand is a literal number, boolean, string, or null
                            ConstantWrapper right = Operand2 as ConstantWrapper;
                            if (right != null)
                            {
                                // then they are both constants and we can evaluate the operation
                                EvalThisOperator(left, right);
                            }
                            else
                            {
                                // see if the right is a binary operator that can be combined with ours
                                BinaryOperator rightBinary = Operand2 as BinaryOperator;
                                if (rightBinary != null)
                                {
                                    ConstantWrapper rightLeft = rightBinary.Operand1 as ConstantWrapper;
                                    if (rightLeft != null)
                                    {
                                        // eval our left and the right-hand binary's left and put the combined operation as
                                        // the child of the right-hand binary
                                        EvalToTheRight(left, rightLeft, rightBinary);
                                    }
                                    else
                                    {
                                        ConstantWrapper rightRight = rightBinary.Operand2 as ConstantWrapper;
                                        if (rightRight != null)
                                        {
                                            EvalFarToTheRight(left, rightRight, rightBinary);
                                        }
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        // left is not a constantwrapper. See if the right is
                        ConstantWrapper right = Operand2 as ConstantWrapper;
                        if (right != null)
                        {
                            // the right is a constant. See if the the left is a binary operator...
                            BinaryOperator leftBinary = Operand1 as BinaryOperator;
                            if (leftBinary != null)
                            {
                                // ...with a constant on the right, and the operators can be combined
                                ConstantWrapper leftRight = leftBinary.Operand2 as ConstantWrapper;
                                if (leftRight != null)
                                {
                                    EvalToTheLeft(right, leftRight, leftBinary);
                                }
                                else
                                {
                                    ConstantWrapper leftLeft = leftBinary.Operand1 as ConstantWrapper;
                                    if (leftLeft != null)
                                    {
                                        EvalFarToTheLeft(right, leftLeft, leftBinary);
                                    }
                                }
                            }
                            else if (Parser.Settings.IsModificationAllowed(TreeModifications.SimplifyStringToNumericConversion))
                            {
                                // see if it's a lookup and this is a minus operation and the constant is a zero
                                Lookup lookup = Operand1 as Lookup;
                                if (lookup != null && OperatorToken == JSToken.Minus && right.IsIntegerLiteral && right.ToNumber() == 0)
                                {
                                    // okay, so we have "lookup - 0"
                                    // this is done frequently to force a value to be numeric. 
                                    // There is an easier way: apply the unary + operator to it. 
                                    NumericUnary unary = new NumericUnary(Context, Parser, lookup, JSToken.Plus);
                                    Parent.ReplaceChild(this, unary);
                                }
                            }
                        }
                        // TODO: shouldn't we check if they BOTH are binary operators? (a*6)*(5*b) ==> a*30*b (for instance)
                    }
                }
            }
        }

        /// <summary>
        /// If the new literal is a string literal, then we need to check to see if our
        /// parent is a CallNode. If it is, and if the string literal can be an identifier,
        /// we'll replace it with a Member-Dot operation.
        /// </summary>
        /// <param name="newLiteral">newLiteral we intend to replace this binaryop node with</param>
        /// <returns>true if we replaced the parent callnode with a member-dot operation</returns>
        private bool ReplaceMemberBracketWithDot(ConstantWrapper newLiteral)
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
                CallNode parentCall = (Parent is AstNodeList ? Parent.Parent as CallNode : null);
                if (parentCall != null && parentCall.InBrackets)
                {
                    // get the newly-combined string
                    string combinedString = newLiteral.ToString();

                    // see if this new string is the target of a replacement operation
                    string newName;
                    if (Parser.Settings.HasRenamePairs && Parser.Settings.ManualRenamesProperties
                        && Parser.Settings.IsModificationAllowed(TreeModifications.PropertyRenaming)
                        && !string.IsNullOrEmpty(newName = Parser.Settings.GetNewName(combinedString)))
                    {
                        // yes, it is. Now see if the new name is safe to be converted to a dot-operation.
                        if (Parser.Settings.IsModificationAllowed(TreeModifications.BracketMemberToDotMember)
                            && JSScanner.IsSafeIdentifier(newName)
                            && !JSScanner.IsKeyword(newName))
                        {
                            // we want to replace the call with operator with a new member dot operation, and
                            // since we won't be analyzing it (we're past the analyze phase, we're going to need
                            // to use the new string value
                            Member replacementMember = new Member(parentCall.Context, Parser, parentCall.Function, newName);
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
                    else if (Parser.Settings.IsModificationAllowed(TreeModifications.BracketMemberToDotMember))
                    {
                        // our parent is a call-bracket -- now we just need to see if the newly-combined
                        // string can be an identifier
                        if (JSScanner.IsSafeIdentifier(combinedString) && !JSScanner.IsKeyword(combinedString))
                        {
                            // yes -- replace the parent call with a new member node using the newly-combined string
                            Member replacementMember = new Member(parentCall.Context, Parser, parentCall.Function, combinedString);
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
        private void EvalThisOperator(ConstantWrapper left, ConstantWrapper right)
        {
            // we can evaluate these operators if we know both operands are literal
            // number, boolean, string or null
            ConstantWrapper newLiteral = null;
            switch (OperatorToken)
            {
                case JSToken.Multiply:
                    newLiteral = Multiply(left, right);
                    break;

                case JSToken.Divide:
                    newLiteral = Divide(left, right);
                    if (newLiteral != null && newLiteral.ToCode().Length > ToCode().Length)
                    {
                        // the result is bigger than the expression.
                        // eg: 1/3 is smaller than .333333333333333
                        // never mind.
                        newLiteral = null;
                    }
                    break;

                case JSToken.Modulo:
                    newLiteral = Modulo(left, right);
                    if (newLiteral != null && newLiteral.ToCode().Length > ToCode().Length)
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
                if (!ReplaceMemberBracketWithDot(newLiteral))
                {
                    Parent.ReplaceChild(this, newLiteral);
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
        private void RotateFromLeft(BinaryOperator binaryOp, ConstantWrapper newLiteral)
        {
            // replace our node with the binary operator
            binaryOp.ReplaceChild(binaryOp.Operand2, newLiteral);
            Parent.ReplaceChild(this, binaryOp);

            // and just for good measure.. revisit the node that's taking our place, since
            // we just changed a constant value. Assuming the other operand is a constant, too.
            ConstantWrapper otherConstant = binaryOp.Operand1 as ConstantWrapper;
            if (otherConstant != null)
            {
                binaryOp.EvalThisOperator(otherConstant, newLiteral);
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
        private void RotateFromRight(BinaryOperator binaryOp, ConstantWrapper newLiteral)
        {
            // replace our node with the binary operator
            binaryOp.ReplaceChild(binaryOp.Operand1, newLiteral);
            Parent.ReplaceChild(this, binaryOp);

            // and just for good measure.. revisit the node that's taking our place, since
            // we just changed a constant value. Assuming the other operand is a constant, too.
            ConstantWrapper otherConstant = binaryOp.Operand2 as ConstantWrapper;
            if (otherConstant != null)
            {
                binaryOp.EvalThisOperator(newLiteral, otherConstant);
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
        private void EvalToTheLeft(ConstantWrapper thisConstant, ConstantWrapper otherConstant, BinaryOperator leftOperator)
        {
            if (leftOperator.OperatorToken == JSToken.Plus && OperatorToken == JSToken.Plus)
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
                        RotateFromLeft(leftOperator, newLiteral);
                    }
                }
            }
            else if (leftOperator.OperatorToken == JSToken.Minus)
            {
                if (OperatorToken == JSToken.Plus)
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

                            RotateFromLeft(leftOperator, newLiteral);
                        }
                        else
                        {
                            // if the left-left is a constant, then we can try combining with it
                            ConstantWrapper leftLeft = leftOperator.Operand1 as ConstantWrapper;
                            if (leftLeft != null)
                            {
                                EvalFarToTheLeft(thisConstant, leftLeft, leftOperator);
                            }
                        }
                    }
                }
                else if (OperatorToken == JSToken.Minus)
                {
                    // minus-minus. Both operations are numeric.
                    // a-n1-n2 => a-(n1+n2), so we can add the two constants and subtract from 
                    // the left-hand non-constant. 
                    ConstantWrapper newLiteral = NumericAddition(otherConstant, thisConstant);
                    if (newLiteral != null && NoOverflow(newLiteral))
                    {
                        // make it the new right-hand literal for the left-hand operator
                        // and make the left-hand operator replace our operator
                        RotateFromLeft(leftOperator, newLiteral);
                    }
                    else
                    {
                        // if the left-left is a constant, then we can try combining with it
                        ConstantWrapper leftLeft = leftOperator.Operand1 as ConstantWrapper;
                        if (leftLeft != null)
                        {
                            EvalFarToTheLeft(thisConstant, leftLeft, leftOperator);
                        }
                    }
                }
            }
            else if (leftOperator.OperatorToken == OperatorToken
                && (OperatorToken == JSToken.Multiply || OperatorToken == JSToken.Divide))
            {
                // either multiply-multiply or divide-divide
                // either way, we use the other operand and the product of the two constants.
                // if the product blows up to an infinte value, then don't combine them because that
                // could change the way the program goes at runtime, depending on the unknown value.
                ConstantWrapper newLiteral = Multiply(otherConstant, thisConstant);
                if (newLiteral != null && NoMultiplicativeOverOrUnderFlow(otherConstant, thisConstant, newLiteral))
                {
                    RotateFromLeft(leftOperator, newLiteral);
                }
            }
            else if ((leftOperator.OperatorToken == JSToken.Multiply && OperatorToken == JSToken.Divide)
                || (leftOperator.OperatorToken == JSToken.Divide && OperatorToken == JSToken.Multiply))
            {
                if (Parser.Settings.IsModificationAllowed(TreeModifications.EvaluateNumericExpressions))
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
                            RotateFromLeft(leftOperator, otherOverThis);
                        }
                    }
                    else if (thisOverOther != null && NoMultiplicativeOverOrUnderFlow(thisConstant, otherConstant, thisOverOther))
                    {
                        // but only if it's smaller than the original expression
                        if (thisOverOtherLength <= otherConstant.ToCode().Length + thisConstant.ToCode().Length + 1)
                        {
                            // opposite operator
                            leftOperator.OperatorToken = leftOperator.OperatorToken == JSToken.Multiply ? JSToken.Divide : JSToken.Multiply;
                            RotateFromLeft(leftOperator, thisOverOther);
                        }
                    }
                }
            }
            else if (OperatorToken == leftOperator.OperatorToken
                && (OperatorToken == JSToken.BitwiseAnd || OperatorToken == JSToken.BitwiseOr || OperatorToken == JSToken.BitwiseXor))
            {
                // identical bitwise operators can be combined
                ConstantWrapper newLiteral = null;
                switch(OperatorToken)
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
                    RotateFromLeft(leftOperator, newLiteral);
                }
            }
        }

        /// <summary>
        /// Evaluate: (CONST [op] OTHER) [op] CONST
        /// </summary>
        /// <param name="thisConstant">second constant</param>
        /// <param name="otherConstant">first constant</param>
        /// <param name="leftOperator">first operator</param>
        private void EvalFarToTheLeft(ConstantWrapper thisConstant, ConstantWrapper otherConstant, BinaryOperator leftOperator)
        {
            if (leftOperator.OperatorToken == JSToken.Minus)
            {
                if (OperatorToken == JSToken.Plus)
                {
                    // minus-plus
                    ConstantWrapper newLiteral = NumericAddition(otherConstant, thisConstant);
                    if (newLiteral != null && NoOverflow(newLiteral))
                    {
                        RotateFromRight(leftOperator, newLiteral);
                    }
                }
                else if (OperatorToken == JSToken.Minus)
                {
                    // minus-minus
                    ConstantWrapper newLiteral = Minus(otherConstant, thisConstant);
                    if (newLiteral != null && NoOverflow(newLiteral))
                    {
                        RotateFromRight(leftOperator, newLiteral);
                    }
                }
            }
            else if (OperatorToken == JSToken.Multiply)
            {
                if (leftOperator.OperatorToken == JSToken.Multiply || leftOperator.OperatorToken == JSToken.Divide)
                {
                    ConstantWrapper newLiteral = Multiply(otherConstant, thisConstant);
                    if (newLiteral != null && NoMultiplicativeOverOrUnderFlow(otherConstant, thisConstant, newLiteral))
                    {
                        RotateFromRight(leftOperator, newLiteral);
                    }
                }
            }
            else if (OperatorToken == JSToken.Divide)
            {
                if (leftOperator.OperatorToken == JSToken.Divide)
                {
                    // divide-divide
                    ConstantWrapper newLiteral = Divide(otherConstant, thisConstant);
                    if (newLiteral != null && NoMultiplicativeOverOrUnderFlow(otherConstant, thisConstant, newLiteral)
                        && newLiteral.ToCode().Length <= thisConstant.ToCode().Length + otherConstant.ToCode().Length + 1)
                    {
                        RotateFromRight(leftOperator, newLiteral);
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
                            RotateFromRight(leftOperator, otherOverThis);
                        }
                    }
                    else if (thisOverOther != null && NoMultiplicativeOverOrUnderFlow(thisConstant, otherConstant, thisOverOther))
                    {
                        if (thisOverOtherLength <= thisConstant.ToCode().Length + otherConstant.ToCode().Length + 1)
                        {
                            // swap the operands
                            AstNode temp = leftOperator.Operand1;
                            leftOperator.Operand1 = leftOperator.Operand2;
                            leftOperator.Operand2 = temp;

                            // operator is the opposite
                            leftOperator.OperatorToken = JSToken.Divide;
                            RotateFromLeft(leftOperator, thisOverOther);
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
        private void EvalToTheRight(ConstantWrapper thisConstant, ConstantWrapper otherConstant, BinaryOperator rightOperator)
        {
            if (OperatorToken == JSToken.Plus)
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
                        RotateFromRight(rightOperator, newLiteral);
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
                        RotateFromRight(rightOperator, newLiteral);
                    }
                    else
                    {
                        ConstantWrapper rightRight = rightOperator.Operand2 as ConstantWrapper;
                        if (rightRight != null)
                        {
                            EvalFarToTheRight(thisConstant, rightRight, rightOperator);
                        }
                    }
                }
            }
            else if (OperatorToken == JSToken.Minus && rightOperator.OperatorToken == JSToken.Minus)
            {
                // minus-minus
                // both operations are numeric, so we can combine the constant operands. However, we 
                // can't combine them into a plus, so make sure we do the minus in the opposite direction
                ConstantWrapper newLiteral = Minus(otherConstant, thisConstant);
                if (newLiteral != null && NoOverflow(newLiteral))
                {
                    AstNode temp = rightOperator.Operand1;
                    rightOperator.Operand1 = rightOperator.Operand2;
                    rightOperator.Operand2 = temp;

                    RotateFromLeft(rightOperator, newLiteral);
                }
                else
                {
                    ConstantWrapper rightRight = rightOperator.Operand2 as ConstantWrapper;
                    if (rightRight != null)
                    {
                        EvalFarToTheRight(thisConstant, rightRight, rightOperator);
                    }
                }
            }
            else if (OperatorToken == JSToken.Multiply
                && (rightOperator.OperatorToken == JSToken.Multiply || rightOperator.OperatorToken == JSToken.Divide))
            {
                // multiply-divide or multiply-multiply
                // multiply the operands and use the right-hand operator
                ConstantWrapper newLiteral = Multiply(thisConstant, otherConstant);
                if (newLiteral != null && NoMultiplicativeOverOrUnderFlow(thisConstant, otherConstant, newLiteral))
                {
                    RotateFromRight(rightOperator, newLiteral);
                }
            }
            else if (OperatorToken == JSToken.Divide)
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

                        RotateFromRight(rightOperator, newLiteral);
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
                            RotateFromRight(rightOperator, leftOverRight);
                        }
                    }
                    else if (rightOverLeft != null && NoMultiplicativeOverOrUnderFlow(otherConstant, thisConstant, rightOverLeft))
                    {
                        // but only if the resulting value is smaller than the original expression
                        if (rightOverLeftLength <= thisConstant.ToCode().Length + otherConstant.ToCode().Length + 1)
                        {
                            // use right-over-left. Keep the operator, but swap the operands
                            AstNode temp = rightOperator.Operand1;
                            rightOperator.Operand1 = rightOperator.Operand2;
                            rightOperator.Operand2 = temp;
                            RotateFromLeft(rightOperator, rightOverLeft);
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
        private void EvalFarToTheRight(ConstantWrapper thisConstant, ConstantWrapper otherConstant, BinaryOperator rightOperator)
        {
            if (rightOperator.OperatorToken == JSToken.Minus)
            {
                if (OperatorToken == JSToken.Plus)
                {
                    // plus-minus
                    // our constant cannot be a string, though
                    if (!thisConstant.IsStringLiteral)
                    {
                        ConstantWrapper newLiteral = Minus(otherConstant, thisConstant);
                        if (newLiteral != null && NoOverflow(newLiteral))
                        {
                            RotateFromLeft(rightOperator, newLiteral);
                        }
                    }
                }
                else if (OperatorToken == JSToken.Minus)
                {
                    // minus-minus
                    ConstantWrapper newLiteral = NumericAddition(thisConstant, otherConstant);
                    if (newLiteral != null && NoOverflow(newLiteral))
                    {
                        // but we need to swap the left and right operands first
                        AstNode temp = rightOperator.Operand1;
                        rightOperator.Operand1 = rightOperator.Operand2;
                        rightOperator.Operand2 = temp;

                        // then rotate the node up after replacing old with new
                        RotateFromRight(rightOperator, newLiteral);
                    }
                }
            }
            else if (OperatorToken == JSToken.Multiply)
            {
                if (rightOperator.OperatorToken == JSToken.Multiply)
                {
                    // mult-mult
                    ConstantWrapper newLiteral = Multiply(thisConstant, otherConstant);
                    if (newLiteral != null && NoMultiplicativeOverOrUnderFlow(thisConstant, otherConstant, newLiteral))
                    {
                        RotateFromLeft(rightOperator, newLiteral);
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
                            RotateFromLeft(rightOperator, otherOverThis);
                        }
                    }
                    else if (thisOverOther != null && NoMultiplicativeOverOrUnderFlow(thisConstant, otherConstant, thisOverOther))
                    {
                        if (thisOverOtherLength <= thisConstant.ToCode().Length + otherConstant.ToCode().Length + 1)
                        {
                            // keep the order, but opposite operator
                            rightOperator.OperatorToken = JSToken.Multiply;
                            RotateFromRight(rightOperator, thisOverOther);
                        }
                    }
                }
            }
            else if (OperatorToken == JSToken.Divide)
            {
                if (rightOperator.OperatorToken == JSToken.Multiply)
                {
                    // divide-mult
                    ConstantWrapper newLiteral = Divide(thisConstant, otherConstant);
                    if (newLiteral != null && NoMultiplicativeOverOrUnderFlow(thisConstant, otherConstant, newLiteral)
                        && newLiteral.ToCode().Length <= thisConstant.ToCode().Length + otherConstant.ToCode().Length + 1)
                    {
                        // swap the operands
                        AstNode temp = rightOperator.Operand1;
                        rightOperator.Operand1 = rightOperator.Operand2;
                        rightOperator.Operand2 = temp;
                        // change the operator
                        rightOperator.OperatorToken = JSToken.Divide;
                        RotateFromRight(rightOperator, newLiteral);
                    }
                }
                else if (rightOperator.OperatorToken == JSToken.Divide)
                {
                    // divide-divide
                    ConstantWrapper newLiteral = Multiply(thisConstant, otherConstant);
                    if (newLiteral != null && NoMultiplicativeOverOrUnderFlow(thisConstant, otherConstant, newLiteral))
                    {
                        // but we need to swap the left and right operands first
                        AstNode temp = rightOperator.Operand1;
                        rightOperator.Operand1 = rightOperator.Operand2;
                        rightOperator.Operand2 = temp;

                        // then rotate the node up after replacing old with new
                        RotateFromRight(rightOperator, newLiteral);
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
                && Parser.Settings.IsModificationAllowed(TreeModifications.EvaluateNumericExpressions))
            {
                try
                {
                    double leftValue = left.ToNumber();
                    double rightValue = right.ToNumber();
                    double result = leftValue * rightValue;

                    if (ConstantWrapper.NumberIsOkayToCombine(result))
                    {
                        newLiteral = new ConstantWrapper(result, PrimitiveType.Number, null, left.Parser);
                    }
                    else
                    {
                        if (!left.IsNumericLiteral && ConstantWrapper.NumberIsOkayToCombine(leftValue))
                        {
                            left.Parent.ReplaceChild(left, new ConstantWrapper(leftValue, PrimitiveType.Number, left.Context, Parser));
                        }
                        if (!right.IsNumericLiteral && ConstantWrapper.NumberIsOkayToCombine(rightValue))
                        {
                            right.Parent.ReplaceChild(right, new ConstantWrapper(rightValue, PrimitiveType.Number, right.Context, Parser));
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
                && Parser.Settings.IsModificationAllowed(TreeModifications.EvaluateNumericExpressions))
            {
                try
                {
                    double leftValue = left.ToNumber();
                    double rightValue = right.ToNumber();
                    double result = leftValue / rightValue;

                    if (ConstantWrapper.NumberIsOkayToCombine(result))
                    {
                        newLiteral = new ConstantWrapper(result, PrimitiveType.Number, null, left.Parser);
                    }
                    else
                    {
                        if (!left.IsNumericLiteral && ConstantWrapper.NumberIsOkayToCombine(leftValue))
                        {
                            left.Parent.ReplaceChild(left, new ConstantWrapper(leftValue, PrimitiveType.Number, left.Context, Parser));
                        }
                        if (!right.IsNumericLiteral && ConstantWrapper.NumberIsOkayToCombine(rightValue))
                        {
                            right.Parent.ReplaceChild(right, new ConstantWrapper(rightValue, PrimitiveType.Number, right.Context, Parser));
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
                && Parser.Settings.IsModificationAllowed(TreeModifications.EvaluateNumericExpressions))
            {
                try
                {
                    double leftValue = left.ToNumber();
                    double rightValue = right.ToNumber();
                    double result = leftValue % rightValue;

                    if (ConstantWrapper.NumberIsOkayToCombine(result))
                    {
                        newLiteral = new ConstantWrapper(result, PrimitiveType.Number, null, left.Parser);
                    }
                    else
                    {
                        if (!left.IsNumericLiteral && ConstantWrapper.NumberIsOkayToCombine(leftValue))
                        {
                            left.Parent.ReplaceChild(left, new ConstantWrapper(leftValue, PrimitiveType.Number, left.Context, Parser));
                        }
                        if (!right.IsNumericLiteral && ConstantWrapper.NumberIsOkayToCombine(rightValue))
                        {
                            right.Parent.ReplaceChild(right, new ConstantWrapper(rightValue, PrimitiveType.Number, right.Context, Parser));
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
                && Parser.Settings.IsModificationAllowed(TreeModifications.EvaluateNumericExpressions))
            {
                try
                {
                    double leftValue = left.ToNumber();
                    double rightValue = right.ToNumber();
                    double result = leftValue + rightValue;

                    if (ConstantWrapper.NumberIsOkayToCombine(result))
                    {
                        newLiteral = new ConstantWrapper(result, PrimitiveType.Number, null, left.Parser);
                    }
                    else
                    {
                        if (!left.IsNumericLiteral && ConstantWrapper.NumberIsOkayToCombine(leftValue))
                        {
                            left.Parent.ReplaceChild(left, new ConstantWrapper(leftValue, PrimitiveType.Number, left.Context, Parser));
                        }
                        if (!right.IsNumericLiteral && ConstantWrapper.NumberIsOkayToCombine(rightValue))
                        {
                            right.Parent.ReplaceChild(right, new ConstantWrapper(rightValue, PrimitiveType.Number, right.Context, Parser));
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
            if (Parser.Settings.IsModificationAllowed(TreeModifications.CombineAdjacentStringLiterals))
            {
                // if either one of the operands is not a string literal, then check to see if we allow
                // evaluation of numeric expression; if not, then no-go. IF they are both string literals,
                // then it doesn't matter what the numeric flag says.
                if ((left.IsStringLiteral && right.IsStringLiteral)
                    || Parser.Settings.IsModificationAllowed(TreeModifications.EvaluateNumericExpressions))
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
                        newLiteral = new ConstantWrapper(left.ToString() + right.ToString(), PrimitiveType.String, null, left.Parser);
                    }
                }
            }

            return newLiteral;
        }

        private ConstantWrapper Minus(ConstantWrapper left, ConstantWrapper right)
        {
            ConstantWrapper newLiteral = null;

            if (left.IsOkayToCombine && right.IsOkayToCombine
                && Parser.Settings.IsModificationAllowed(TreeModifications.EvaluateNumericExpressions))
            {
                try
                {
                    double leftValue = left.ToNumber();
                    double rightValue = right.ToNumber();
                    double result = leftValue - rightValue;

                    if (ConstantWrapper.NumberIsOkayToCombine(result))
                    {
                        newLiteral = new ConstantWrapper(result, PrimitiveType.Number, null, left.Parser);
                    }
                    else
                    {
                        if (!left.IsNumericLiteral && ConstantWrapper.NumberIsOkayToCombine(leftValue))
                        {
                            left.Parent.ReplaceChild(left, new ConstantWrapper(leftValue, PrimitiveType.Number, left.Context, Parser));
                        }
                        if (!right.IsNumericLiteral && ConstantWrapper.NumberIsOkayToCombine(rightValue))
                        {
                            right.Parent.ReplaceChild(right, new ConstantWrapper(rightValue, PrimitiveType.Number, right.Context, Parser));
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

            if (Parser.Settings.IsModificationAllowed(TreeModifications.EvaluateNumericExpressions))
            {
                try
                {
                    // left-hand value is a 32-bit signed integer
                    Int32 lvalue = left.ToInt32();

                    // mask only the bottom 5 bits of the right-hand value
                    int rvalue = (int)(right.ToUInt32() & 0x1F);

                    // convert the result to a double
                    double result = Convert.ToDouble(lvalue << rvalue);
                    newLiteral = new ConstantWrapper(result, PrimitiveType.Number, null, left.Parser);
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

            if (Parser.Settings.IsModificationAllowed(TreeModifications.EvaluateNumericExpressions))
            {
                try
                {
                    // left-hand value is a 32-bit signed integer
                    Int32 lvalue = left.ToInt32();

                    // mask only the bottom 5 bits of the right-hand value
                    int rvalue = (int)(right.ToUInt32() & 0x1F);

                    // convert the result to a double
                    double result = Convert.ToDouble(lvalue >> rvalue);
                    newLiteral = new ConstantWrapper(result, PrimitiveType.Number, null, left.Parser);
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

            if (Parser.Settings.IsModificationAllowed(TreeModifications.EvaluateNumericExpressions))
            {
                try
                {
                    // left-hand value is a 32-bit signed integer
                    UInt32 lvalue = left.ToUInt32();

                    // mask only the bottom 5 bits of the right-hand value
                    int rvalue = (int)(right.ToUInt32() & 0x1F);

                    // convert the result to a double
                    double result = Convert.ToDouble(lvalue >> rvalue);
                    newLiteral = new ConstantWrapper(result, PrimitiveType.Number, null, left.Parser);
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

            if (Parser.Settings.IsModificationAllowed(TreeModifications.EvaluateNumericExpressions))
            {
                if (left.IsStringLiteral && right.IsStringLiteral)
                {
                    // do a straight ordinal comparison of the strings
                    newLiteral = new ConstantWrapper(string.CompareOrdinal(left.ToString(), right.ToString()) < 0, PrimitiveType.Boolean, null, left.Parser);
                }
                else
                {
                    try
                    {
                        // either one or both are NOT a string -- numeric comparison
                        if (left.IsOkayToCombine && right.IsOkayToCombine)
                        {
                            newLiteral = new ConstantWrapper(left.ToNumber() < right.ToNumber(), PrimitiveType.Boolean, null, left.Parser);
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

            if (Parser.Settings.IsModificationAllowed(TreeModifications.EvaluateNumericExpressions))
            {
                if (left.IsStringLiteral && right.IsStringLiteral)
                {
                    // do a straight ordinal comparison of the strings
                    newLiteral = new ConstantWrapper(string.CompareOrdinal(left.ToString(), right.ToString()) <= 0, PrimitiveType.Boolean, null, left.Parser);
                }
                else
                {
                    try
                    {
                        // either one or both are NOT a string -- numeric comparison
                        if (left.IsOkayToCombine && right.IsOkayToCombine)
                        {
                            newLiteral = new ConstantWrapper(left.ToNumber() <= right.ToNumber(), PrimitiveType.Boolean, null, left.Parser);
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

            if (Parser.Settings.IsModificationAllowed(TreeModifications.EvaluateNumericExpressions))
            {
                if (left.IsStringLiteral && right.IsStringLiteral)
                {
                    // do a straight ordinal comparison of the strings
                    newLiteral = new ConstantWrapper(string.CompareOrdinal(left.ToString(), right.ToString()) > 0, PrimitiveType.Boolean, null, left.Parser);
                }
                else
                {
                    try
                    {
                        // either one or both are NOT a string -- numeric comparison
                        if (left.IsOkayToCombine && right.IsOkayToCombine)
                        {
                            newLiteral = new ConstantWrapper(left.ToNumber() > right.ToNumber(), PrimitiveType.Boolean, null, left.Parser);
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

            if (Parser.Settings.IsModificationAllowed(TreeModifications.EvaluateNumericExpressions))
            {
                if (left.IsStringLiteral && right.IsStringLiteral)
                {
                    // do a straight ordinal comparison of the strings
                    newLiteral = new ConstantWrapper(string.CompareOrdinal(left.ToString(), right.ToString()) >= 0, PrimitiveType.Boolean, null, left.Parser);
                }
                else
                {
                    try
                    {
                        // either one or both are NOT a string -- numeric comparison
                        if (left.IsOkayToCombine && right.IsOkayToCombine)
                        {
                            newLiteral = new ConstantWrapper(left.ToNumber() >= right.ToNumber(), PrimitiveType.Boolean, null, left.Parser);
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

            if (Parser.Settings.IsModificationAllowed(TreeModifications.EvaluateNumericExpressions))
            {
                PrimitiveType leftType = left.PrimitiveType;
                if (leftType == right.PrimitiveType)
                {
                    // the values are the same type
                    switch (leftType)
                    {
                        case PrimitiveType.Null:
                            // null == null is true
                            newLiteral = new ConstantWrapper(true, PrimitiveType.Boolean, null, left.Parser);
                            break;

                        case PrimitiveType.Boolean:
                            // compare boolean values
                            newLiteral = new ConstantWrapper(left.ToBoolean() == right.ToBoolean(), PrimitiveType.Boolean, null, left.Parser);
                            break;

                        case PrimitiveType.String:
                            // compare string ordinally
                            newLiteral = new ConstantWrapper(string.CompareOrdinal(left.ToString(), right.ToString()) == 0, PrimitiveType.Boolean, null, left.Parser);
                            break;

                        case PrimitiveType.Number:
                            try
                            {
                                // compare the values
                                // +0 and -0 are treated as "equal" in C#, so we don't need to test them separately.
                                // and NaN is always unequal to everything else, including itself.
                                if (left.IsOkayToCombine && right.IsOkayToCombine)
                                {
                                    newLiteral = new ConstantWrapper(left.ToNumber() == right.ToNumber(), PrimitiveType.Boolean, null, left.Parser);
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
                        newLiteral = new ConstantWrapper(left.ToNumber() == right.ToNumber(), PrimitiveType.Boolean, null, left.Parser);
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

            if (Parser.Settings.IsModificationAllowed(TreeModifications.EvaluateNumericExpressions))
            {
                PrimitiveType leftType = left.PrimitiveType;
                if (leftType == right.PrimitiveType)
                {
                    // the values are the same type
                    switch (leftType)
                    {
                        case PrimitiveType.Null:
                            // null != null is false
                            newLiteral = new ConstantWrapper(false, PrimitiveType.Boolean, null, left.Parser);
                            break;

                        case PrimitiveType.Boolean:
                            // compare boolean values
                            newLiteral = new ConstantWrapper(left.ToBoolean() != right.ToBoolean(), PrimitiveType.Boolean, null, left.Parser);
                            break;

                        case PrimitiveType.String:
                            // compare string ordinally
                            newLiteral = new ConstantWrapper(string.CompareOrdinal(left.ToString(), right.ToString()) != 0, PrimitiveType.Boolean, null, left.Parser);
                            break;

                        case PrimitiveType.Number:
                            try
                            {
                                // compare the values
                                // +0 and -0 are treated as "equal" in C#, so we don't need to test them separately.
                                // and NaN is always unequal to everything else, including itself.
                                if (left.IsOkayToCombine && right.IsOkayToCombine)
                                {
                                    newLiteral = new ConstantWrapper(left.ToNumber() != right.ToNumber(), PrimitiveType.Boolean, null, left.Parser);
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
                        newLiteral = new ConstantWrapper(left.ToNumber() != right.ToNumber(), PrimitiveType.Boolean, null, left.Parser);
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

            if (Parser.Settings.IsModificationAllowed(TreeModifications.EvaluateNumericExpressions))
            {
                PrimitiveType leftType = left.PrimitiveType;
                if (leftType == right.PrimitiveType)
                {
                    // the values are the same type
                    switch (leftType)
                    {
                        case PrimitiveType.Null:
                            // null === null is true
                            newLiteral = new ConstantWrapper(true, PrimitiveType.Boolean, null, left.Parser);
                            break;

                        case PrimitiveType.Boolean:
                            // compare boolean values
                            newLiteral = new ConstantWrapper(left.ToBoolean() == right.ToBoolean(), PrimitiveType.Boolean, null, left.Parser);
                            break;

                        case PrimitiveType.String:
                            // compare string ordinally
                            newLiteral = new ConstantWrapper(string.CompareOrdinal(left.ToString(), right.ToString()) == 0, PrimitiveType.Boolean, null, left.Parser);
                            break;

                        case PrimitiveType.Number:
                            try
                            {
                                // compare the values
                                // +0 and -0 are treated as "equal" in C#, so we don't need to test them separately.
                                // and NaN is always unequal to everything else, including itself.
                                if (left.IsOkayToCombine && right.IsOkayToCombine)
                                {
                                    newLiteral = new ConstantWrapper(left.ToNumber() == right.ToNumber(), PrimitiveType.Boolean, null, left.Parser);
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
                    newLiteral = new ConstantWrapper(false, PrimitiveType.Boolean, null, left.Parser);
                }
            }

            return newLiteral;
        }

        private ConstantWrapper StrictNotEqual(ConstantWrapper left, ConstantWrapper right)
        {
            ConstantWrapper newLiteral = null;

            if (Parser.Settings.IsModificationAllowed(TreeModifications.EvaluateNumericExpressions))
            {
                PrimitiveType leftType = left.PrimitiveType;
                if (leftType == right.PrimitiveType)
                {
                    // the values are the same type
                    switch (leftType)
                    {
                        case PrimitiveType.Null:
                            // null !== null is false
                            newLiteral = new ConstantWrapper(false, PrimitiveType.Boolean, null, left.Parser);
                            break;

                        case PrimitiveType.Boolean:
                            // compare boolean values
                            newLiteral = new ConstantWrapper(left.ToBoolean() != right.ToBoolean(), PrimitiveType.Boolean, null, left.Parser);
                            break;

                        case PrimitiveType.String:
                            // compare string ordinally
                            newLiteral = new ConstantWrapper(string.CompareOrdinal(left.ToString(), right.ToString()) != 0, PrimitiveType.Boolean, null, left.Parser);
                            break;

                        case PrimitiveType.Number:
                            try
                            {
                                // compare the values
                                // +0 and -0 are treated as "equal" in C#, so we don't need to test them separately.
                                // and NaN is always unequal to everything else, including itself.
                                if (left.IsOkayToCombine && right.IsOkayToCombine)
                                {
                                    newLiteral = new ConstantWrapper(left.ToNumber() != right.ToNumber(), PrimitiveType.Boolean, null, left.Parser);
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
                    newLiteral = new ConstantWrapper(true, PrimitiveType.Boolean, null, left.Parser);
                }
            }

            return newLiteral;
        }

        private ConstantWrapper BitwiseAnd(ConstantWrapper left, ConstantWrapper right)
        {
            ConstantWrapper newLiteral = null;

            if (Parser.Settings.IsModificationAllowed(TreeModifications.EvaluateNumericExpressions))
            {
                try
                {
                    Int32 lValue = left.ToInt32();
                    Int32 rValue = right.ToInt32();
                    newLiteral = new ConstantWrapper(Convert.ToDouble(lValue & rValue), PrimitiveType.Number, null, left.Parser);
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

            if (Parser.Settings.IsModificationAllowed(TreeModifications.EvaluateNumericExpressions))
            {
                try
                {
                    Int32 lValue = left.ToInt32();
                    Int32 rValue = right.ToInt32();
                    newLiteral = new ConstantWrapper(Convert.ToDouble(lValue | rValue), PrimitiveType.Number, null, left.Parser);
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

            if (Parser.Settings.IsModificationAllowed(TreeModifications.EvaluateNumericExpressions))
            {
                try
                {
                    Int32 lValue = left.ToInt32();
                    Int32 rValue = right.ToInt32();
                    newLiteral = new ConstantWrapper(Convert.ToDouble(lValue ^ rValue), PrimitiveType.Number, null, left.Parser);
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
            if (Parser.Settings.IsModificationAllowed(TreeModifications.EvaluateNumericExpressions))
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
            if (Parser.Settings.IsModificationAllowed(TreeModifications.EvaluateNumericExpressions))
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
    }
}
