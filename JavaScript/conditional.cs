// conditional.cs
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
using System.Text;

namespace Microsoft.Ajax.Utilities
{

    public sealed class Conditional : AstNode
    {
        private AstNode m_condition;
        public AstNode Condition { get { return m_condition; } }

        private AstNode m_trueExpression;
        public AstNode TrueExpression { get { return m_trueExpression; } }

        private AstNode m_falseExpression;
        public AstNode FalseExpression { get { return m_falseExpression; } }

        public Conditional(Context context, JSParser parser, AstNode condition, AstNode trueExpression, AstNode falseExpression)
            : base(context, parser)
        {
            m_condition = condition;
            m_trueExpression = trueExpression;
            m_falseExpression = falseExpression;
            if (condition != null) condition.Parent = this;
            if (trueExpression != null) trueExpression.Parent = this;
            if (falseExpression != null) falseExpression.Parent = this;
        }

        public override AstNode Clone()
        {
            return new Conditional(
                (Context == null ? null : Context.Clone()),
                Parser,
                (m_condition == null ? null : m_condition.Clone()),
                (m_trueExpression == null ? null : m_trueExpression.Clone()),
                (m_falseExpression == null ? null : m_falseExpression.Clone())
                );
        }

        public override IEnumerable<AstNode> Children
        {
            get
            {
                if (m_condition != null)
                {
                    yield return m_condition;
                }
                if (m_trueExpression != null)
                {
                    yield return m_trueExpression;
                }
                if (m_falseExpression != null)
                {
                    yield return m_falseExpression;
                }
            }
        }

        public override bool ReplaceChild(AstNode oldNode, AstNode newNode)
        {
            if (m_condition == oldNode)
            {
                m_condition = newNode;
                if (newNode != null) { newNode.Parent = this; }
                return true;
            }
            if (m_trueExpression == oldNode)
            {
                m_trueExpression = newNode;
                if (newNode != null) { newNode.Parent = this; }
                return true;
            }
            if (m_falseExpression == oldNode)
            {
                m_falseExpression = newNode;
                if (newNode != null) { newNode.Parent = this; }
                return true;
            }
            return false;
        }

        public override AstNode LeftHandSide
        {
            get
            {
                // the condition is on the left
                return m_condition.LeftHandSide;
            }
        }

        private static bool NeedsParens(AstNode node, JSToken refToken)
        {
            bool needsParens = false;

            // assignments and commas are the only operators that need parens
            // around them. Conditional is pretty low down the list
            BinaryOperator binaryOp = node as BinaryOperator;
            if (binaryOp != null)
            {
                OpPrec thisPrecedence = JSScanner.GetOperatorPrecedence(refToken);
                OpPrec nodePrecedence = JSScanner.GetOperatorPrecedence(binaryOp.OperatorToken);
                needsParens = (nodePrecedence < thisPrecedence);
            }

            return needsParens;
        }

        public override string ToCode(ToCodeFormat format)
        {
            StringBuilder sb = new StringBuilder();
            bool parens = NeedsParens(m_condition, JSToken.ConditionalIf);
            if (parens)
            {
                sb.Append('(');
            }

            sb.Append(m_condition.ToCode());
            if (parens)
            {
                sb.Append(')');
            }

            CodeSettings codeSettings = Parser.Settings;
            if (codeSettings.OutputMode == OutputMode.MultipleLines && codeSettings.IndentSize > 0)
            {
                sb.Append(" ? ");
            }
            else
            {
                sb.Append('?');
            }

            // the true and false operands are parsed as assignment operators, so use that token as the
            // reference token to compare against for operator precedence to determine if we need parens
            parens = NeedsParens(m_trueExpression, JSToken.Assign);
            if (parens)
            {
                sb.Append('(');
            }

            sb.Append(m_trueExpression.ToCode());
            if (parens)
            {
                sb.Append(')');
            }

            if (codeSettings.OutputMode == OutputMode.MultipleLines && codeSettings.IndentSize > 0)
            {
                sb.Append(" : ");
            }
            else
            {
                sb.Append(':');
            }

            parens = NeedsParens(m_falseExpression, JSToken.Assign);
            if (parens)
            {
                sb.Append('(');
            }

            sb.Append(m_falseExpression.ToCode());
            if (parens)
            {
                sb.Append(')');
            }
            return sb.ToString();
        }

        public override void CleanupNodes()
        {
            base.CleanupNodes();

            if (Parser.Settings.EvalLiteralExpressions
                && Parser.Settings.IsModificationAllowed(TreeModifications.EvaluateNumericExpressions))
            {
                // if the condition is a literal, evaluating the condition doesn't do anything, AND
                // we know now whether it's true or not.
                ConstantWrapper literalCondition = m_condition as ConstantWrapper;
                if (literalCondition != null)
                {
                    try
                    {
                        // if the boolean represenation of the literal is true, we can replace the condition operator
                        // with the true expression; otherwise we can replace it with the false expression
                        Parent.ReplaceChild(this, literalCondition.ToBoolean() ? m_trueExpression : m_falseExpression);
                    }
                    catch (InvalidCastException)
                    {
                        // ignore any invalid cast errors
                    }
                }
            }
        }
    }
}