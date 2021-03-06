﻿using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Ajax.Utilities
{
    public class LogicalNot : TreeVisitor
    {
        private AstNode m_expression;
        private bool m_measure;
        private JSParser m_parser;
        private int m_delta;

        public LogicalNot(AstNode node, JSParser parser)
        {
            m_expression = node;
            m_parser = parser;
        }

        public int Measure()
        {
            // we just want to measure the potential delta
            m_measure = true;
            m_delta = 0;

            // do it and return the result
            m_expression.Accept(this);
            return m_delta;
        }

        public void Apply()
        {
            // not measuring; doing
            m_measure = false;
            m_expression.Accept(this);
        }

        private void WrapWithLogicalNot(AstNode operand)
        {
            operand.Parent.ReplaceChild(
                operand, 
                new NumericUnary(
                    null,
                    m_parser,
                    operand,
                    JSToken.LogicalNot));
        }

        private void TypicalHandler(AstNode node)
        {
            if (node != null)
            {
                // don't need to recurse -- to logical-not this, we just need to apply
                // the logical-not operator to it, which will add a character
                if (m_measure)
                {
                    // measure
                    ++m_delta;
                }
                else
                {
                    // simple convert
                    WrapWithLogicalNot(node);
                }
            }
        }

        public override void Visit(ArrayLiteral node)
        {
            // same logic for most nodes
            TypicalHandler(node);
        }

        public override void Visit(BinaryOperator node)
        {
            if (node != null)
            {
                if (m_measure)
                {
                    // measure
                    // depending on the operator, calculate the potential difference in length
                    switch (node.OperatorToken)
                    {
                        case JSToken.Equal:
                        case JSToken.NotEqual:
                        case JSToken.StrictEqual:
                        case JSToken.StrictNotEqual:
                            // these operators can be turned into a logical not without any
                            // delta in code size. == becomes !=, etc.
                            break;

                        case JSToken.LessThan:
                        case JSToken.GreaterThan:
                            // these operators would add another character when turnbed into a not.
                            // for example, < becomes >=, etc
                            ++m_delta;
                            break;

                        case JSToken.LessThanEqual:
                        case JSToken.GreaterThanEqual:
                            // these operators would subtract another character when turnbed into a not.
                            // for example, <= becomes >, etc
                            --m_delta;
                            break;

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
                        case JSToken.BitwiseAnd:
                        case JSToken.BitwiseOr:
                        case JSToken.BitwiseXor:
                        case JSToken.Divide:
                        case JSToken.Multiply:
                        case JSToken.Modulo:
                        case JSToken.Minus:
                        case JSToken.Plus:
                        case JSToken.LeftShift:
                        case JSToken.RightShift:
                        case JSToken.UnsignedRightShift:
                        case JSToken.In:
                        case JSToken.InstanceOf:
                            // these operators have no logical not, which means we need to wrap them in
                            // a unary logical-not operator. And since they have a lower precedence than
                            // the unary logical-not, they'll have to be wrapped in parens. So that means
                            // logical-not'ing these guys adds three characters
                            m_delta += 3;
                            break;

                        case JSToken.Comma:
                            // to logical-not a comma-operator, we just need to logical-not the
                            // right-hand side
                            node.Operand2.Accept(this);
                            break;

                        case JSToken.LogicalAnd:
                        case JSToken.LogicalOr:
                            // the logical-not of a logical-and or logical-or operation is the 
                            // other operation against the not of each operand. Since the opposite
                            // operator is the same length as this operator, then we just need
                            // to recurse both operands to find the true delta.
                            if (node.Operand1 != null)
                            {
                                node.Operand1.Accept(this);
                            }
                            if (node.Operand2 != null)
                            {
                                node.Operand2.Accept(this);
                            }
                            break;
                    }
                }
                else
                {
                    // convert
                    // depending on the operator, perform whatever we need to do to apply a logical
                    // not to the operation
                    switch (node.OperatorToken)
                    {
                        case JSToken.Equal:
                            node.OperatorToken = JSToken.NotEqual;
                            break;

                        case JSToken.NotEqual:
                            node.OperatorToken = JSToken.Equal;
                            break;

                        case JSToken.StrictEqual:
                            node.OperatorToken = JSToken.StrictNotEqual;
                            break;

                        case JSToken.StrictNotEqual:
                            node.OperatorToken = JSToken.StrictEqual;
                            break;

                        case JSToken.LessThan:
                            node.OperatorToken = JSToken.GreaterThanEqual;
                            break;

                        case JSToken.GreaterThan:
                            node.OperatorToken = JSToken.LessThanEqual;
                            break;

                        case JSToken.LessThanEqual:
                            node.OperatorToken = JSToken.GreaterThan;
                            break;

                        case JSToken.GreaterThanEqual:
                            node.OperatorToken = JSToken.LessThan;
                            break;

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
                        case JSToken.BitwiseAnd:
                        case JSToken.BitwiseOr:
                        case JSToken.BitwiseXor:
                        case JSToken.Divide:
                        case JSToken.Multiply:
                        case JSToken.Modulo:
                        case JSToken.Minus:
                        case JSToken.Plus:
                        case JSToken.LeftShift:
                        case JSToken.RightShift:
                        case JSToken.UnsignedRightShift:
                        case JSToken.In:
                        case JSToken.InstanceOf:
                            WrapWithLogicalNot(node);
                            break;

                        case JSToken.Comma:
                            // to logical-not a comma-operator, we just need to logical-not the
                            // right-hand side
                            node.Operand2.Accept(this);
                            break;

                        case JSToken.LogicalAnd:
                        case JSToken.LogicalOr:
                            // the logical-not of a logical-and or logical-or operation is the 
                            // other operation against the not of each operand. Since the opposite
                            // operator is the same length as this operator, then we just need
                            // to recurse both operands and swap the operator token
                            if (node.Operand1 != null)
                            {
                                node.Operand1.Accept(this);
                            }
                            if (node.Operand2 != null)
                            {
                                node.Operand2.Accept(this);
                            }
                            node.OperatorToken = node.OperatorToken == JSToken.LogicalAnd ? JSToken.LogicalOr : JSToken.LogicalAnd;
                            break;
                    }
                }
            }
        }

        public override void Visit(CallNode node)
        {
            // same logic for most nodes
            TypicalHandler(node);
        }

        public override void Visit(Conditional node)
        {
            if (node != null)
            {
                // we have two choices for the conditional. Either:
                //  1. we wrap the whole thing in a logical-not operator, which means we also need to
                //     add parentheses, since conditional is lower-precedence than the logicial not, or
                //  2. apply the logical-not to both the true- and false-branches.
                // The first is guaranteed 3 additional characters. We have to check the delta for
                // each branch and add them together to know how much the second would cost. If it's 
                // greater than 3, then we just want to not the whole thing.
                var notTrue = new LogicalNot(node.TrueExpression, m_parser);
                var notFalse = new LogicalNot(node.FalseExpression, m_parser);
                var costNottingBoth = notTrue.Measure() + notFalse.Measure();

                if (m_measure)
                {
                    // we're just measuring -- adjust the delta accordingly
                    // (the lesser of the two options)
                    m_delta += (costNottingBoth > 3) ? 3 : costNottingBoth;
                }
                else if (costNottingBoth > 3)
                {
                    // just wrap the whole thing
                    WrapWithLogicalNot(node);
                }
                else
                {
                    // less bytes to wrap each branch separately
                    node.TrueExpression.Accept(this);
                    node.FalseExpression.Accept(this);
                }
            }
        }

        public override void Visit(ConstantWrapper node)
        {
            if (node != null)
            {
                // measure
                if (node.PrimitiveType == PrimitiveType.Boolean)
                {
                    if (m_measure)
                    {
                        // if we are converting true/false literals to !0/!1, then
                        // a logical-not doesn't add or subtract anything. But if we aren't,
                        // we need to add/subtract the difference in the length between the
                        // "true" and "false" strings
                        if (!m_parser.Settings.MinifyCode
                            || !m_parser.Settings.IsModificationAllowed(TreeModifications.BooleanLiteralsToNotOperators))
                        {
                            // converting true to false adds a character, false to true subtracts
                            m_delta += node.ToBoolean() ? 1 : -1;
                        }
                    }
                    else
                    {
                        // convert - just flip the boolean value
                        node.Value = !node.ToBoolean();
                    }
                }
                else
                {
                    // just the same typical operation as most other nodes for other types
                    TypicalHandler(node);
                }
            }
        }

        public override void Visit(Delete node)
        {
            // same logic for most nodes
            TypicalHandler(node);
        }

        public override void Visit(Lookup node)
        {
            // same logic for most nodes
            TypicalHandler(node);
        }

        public override void Visit(Member node)
        {
            // same logic for most nodes
            TypicalHandler(node);
        }

        public override void Visit(ObjectLiteral node)
        {
            // same logic for most nodes
            TypicalHandler(node);
        }

        public override void Visit(PostOrPrefixOperator node)
        {
            // same logic for most nodes
            TypicalHandler(node);
        }

        public override void Visit(RegExpLiteral node)
        {
            // same logic for most nodes
            TypicalHandler(node);
        }

        public override void Visit(ThisLiteral node)
        {
            // same logic for most nodes
            TypicalHandler(node);
        }

        public override void Visit(TypeOfNode node)
        {
            // same logic for most nodes
            TypicalHandler(node);
        }

        public override void Visit(VoidNode node)
        {
            // same logic for most nodes
            TypicalHandler(node);
        }

        public override void Visit(NumericUnary node)
        {
            if (node != null)
            {
                // if this is a unary logical-not operator, then we will just remove the
                // logical-not operation
                if (node.OperatorToken == JSToken.LogicalNot)
                {
                    if (m_measure)
                    {
                        // measure
                        // removes the not operator character, but also might remove parens that we would
                        // no longer need.
                        --m_delta;
                        if (node.Operand is BinaryOperator || node.Operand is Conditional)
                        {
                            // those operators are lesser-precedence than the logical-not coperator and would've
                            // added parens that we now don't need
                            m_delta -= 2;
                        }
                    }
                    else
                    {
                        // convert
                        // just replace the not with its own operand
                        node.Parent.ReplaceChild(node, node.Operand);
                    }
                }
                else
                {
                    // same logic as most nodes for the other operators
                    TypicalHandler(node);
                }
            }
        }
    }
}
