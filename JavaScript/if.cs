// if.cs
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

    public sealed class IfNode : AstNode
    {
        private AstNode m_condition;
        public AstNode Condition { get { return m_condition; } }

        private Block m_trueBranch;
        public Block TrueBlock { get { return m_trueBranch; } }

        private Block m_falseBranch;
        public Block FalseBlock { get { return m_falseBranch; } }

        public IfNode(Context context, JSParser parser, AstNode condition, AstNode trueBranch, AstNode falseBranch)
            : base(context, parser)
        {
            m_condition = condition;
            m_trueBranch = ForceToBlock(trueBranch);
            m_falseBranch = ForceToBlock(falseBranch);

            // make sure the parent element is set
            if (m_condition != null) m_condition.Parent = this;
            if (m_trueBranch != null) m_trueBranch.Parent = this;
            if (m_falseBranch != null) m_falseBranch.Parent = this;
        }

        public override AstNode Clone()
        {
            return new IfNode(
                (Context == null ? null : Context.Clone()),
                Parser,
                (m_condition == null ? null : m_condition.Clone()),
                (m_trueBranch == null ? null : m_trueBranch.Clone()),
                (m_falseBranch == null ? null : m_falseBranch.Clone())
                );
        }

        internal override void AnalyzeNode()
        {
            if (Parser.Settings.StripDebugStatements
                 && Parser.Settings.IsModificationAllowed(TreeModifications.StripDebugStatements))
            {
                if (m_trueBranch != null && m_trueBranch.IsDebuggerStatement)
                {
                    m_trueBranch = null;
                }

                if (m_falseBranch != null && m_falseBranch.IsDebuggerStatement)
                {
                    m_falseBranch = null;
                }
            }

            // recurse....
            base.AnalyzeNode();

            // now check to see if the two branches are now empty.
            // if they are, null them out.
            if (m_trueBranch != null && m_trueBranch.Count == 0)
            {
                m_trueBranch = null;
            }
            if (m_falseBranch != null && m_falseBranch.Count == 0)
            {
                m_falseBranch = null;
            }

            // if there is no true branch but a false branch, then
            // put a not on the condition and move the false branch to the true branch.
            if (m_trueBranch == null && m_falseBranch != null
                && Parser.Settings.IsModificationAllowed(TreeModifications.IfConditionFalseToIfNotConditionTrue))
            {
                // check to see if not-ing the condition produces a quick and easy
                // version first
                AstNode nottedCondition = m_condition.LogicalNot();
                if (nottedCondition != null)
                {
                    // it does -- use it
                    m_condition = nottedCondition;
                }
                else
                {
                    // it doesn't. Just wrap it.
                    m_condition = new NumericUnary(
                      null,
                      Parser,
                      m_condition,
                      JSToken.LogicalNot
                      );
                }
                // don't forget to set the parent
                m_condition.Parent = this;

                // and swap the branches
                m_trueBranch = m_falseBranch;
                m_falseBranch = null;
            }
            else if (m_trueBranch == null && m_falseBranch == null
                && Parser.Settings.IsModificationAllowed(TreeModifications.IfEmptyToExpression))
            {
                // NEITHER branches have anything now!

                // something we can do in the future: as long as the condition doesn't
                // contain calls or assignments, we should be able to completely delete
                // the statement altogether rather than changing it to an expression
                // statement on the condition.
                
                // I'm just not doing it yet because I don't
                // know what the effect will be on the iteration of block statements.
                // if we're on item, 5, for instance, and we delete it, will the next
                // item be item 6, or will it return the NEW item 5 (since the old item
                // 5 was deleted and everything shifted up)?

                // We don't know what it is and what the side-effects may be, so
                // just change this statement into an expression statement by replacing us with 
                // the expression
                Parent.ReplaceChild(this, m_condition);
                // no need to analyze -- we already recursed
            }

            // if this statement is now of the pattern "if (condtion) callNode" then
            // we can further reduce it by changing it to "condition && callNode".
            if (m_trueBranch != null && m_falseBranch == null
                && m_trueBranch.Count == 1
                && Parser.Settings.IsModificationAllowed(TreeModifications.IfConditionCallToConditionAndCall))
            {
                // BUT we don't want to replace the statement if the true branch is a
                // call to an onXXXX method of an object. This is because of an IE bug
                // that throws an error if you pass any parameter to onclick or onfocus or
                // any of those event handlers directly from within an and-expression -- 
                // although expression-statements seem to work just fine.
                CallNode callNode = m_trueBranch[0] as CallNode;
                if (callNode != null)
                {
                    Member callMember = callNode.Function as Member;
                    if (callMember == null
                        || !callMember.Name.StartsWith("on", StringComparison.Ordinal)
                        || callNode.Arguments.Count == 0)
                    {
                        // we're good -- go ahead and replace it
                        BinaryOperator binaryOp = new BinaryOperator(
                            Context,
                            Parser,
                            m_condition,
                            m_trueBranch,
                            JSToken.LogicalAnd
                            );

                        // we don't need to analyse this new node because we've already analyzed
                        // the pieces parts as part of the if. And the AnalyzeNode for the BinaryOperator
                        // doesn't really do anything else. Just replace our current node with this
                        // new node
                        Parent.ReplaceChild(this, binaryOp);
                    }
                }
            }
        }

        public override IEnumerable<AstNode> Children
        {
            get
            {
                if (m_condition != null)
                {
                    yield return m_condition;
                }
                if (m_trueBranch != null)
                {
                    yield return m_trueBranch;
                }
                if (m_falseBranch != null)
                {
                    yield return m_falseBranch;
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
            if (m_trueBranch == oldNode)
            {
                m_trueBranch = ForceToBlock(newNode);
                if (m_trueBranch != null) { m_trueBranch.Parent = this; }
                return true;
            }
            if (m_falseBranch == oldNode)
            {
                m_falseBranch = ForceToBlock(newNode);
                if (m_falseBranch != null) { m_falseBranch.Parent = this; }
                return true;
            }
            return false;
        }

        internal Conditional CanBeReturnOperand(AstNode ultimateOperand, bool isFunctionLevel)
        {
            Conditional conditional = null;
            try
            {
                if (m_trueBranch != null && m_trueBranch.Count == 1)
                {
                    ReturnNode returnNode = m_trueBranch[0] as ReturnNode;
                    if (returnNode != null)
                    {
                        AstNode expr1 = returnNode.Operand;
                        if (m_falseBranch == null || m_falseBranch.Count == 0)
                        {
                            // no false branch to speak of. Convert to conditional.
                            // if there is an ultimate expression, use it.
                            // if we are not at the function body level, we can't
                            // combine these. But if we are we can
                            // use a false expression of "void 0" (undefined)
                            if (ultimateOperand != null || isFunctionLevel)
                            {
                                conditional = new Conditional(
                                    (Context == null ? null : Context.Clone()),
                                    Parser,
                                    m_condition,
                                    expr1 ?? CreateVoidNode(),
                                    ultimateOperand ?? CreateVoidNode());
                            }
                        }
                        else if (m_falseBranch.Count == 1)
                        {
                            // there is a false branch with only a single statement
                            // see if it is a return statement
                            returnNode = m_falseBranch[0] as ReturnNode;
                            if (returnNode != null)
                            {
                                // it is. so we have if(cond)return expr1;else return expr2
                                // return cond?expr1:expr2
                                AstNode expr2 = returnNode.Operand;
                                conditional = new Conditional(
                                    (Context == null ? null : Context.Clone()),
                                    Parser,
                                    m_condition,
                                    expr1 ?? CreateVoidNode(),
                                    expr2 ?? CreateVoidNode());
                            }
                            else
                            {
                                // see if it's another if-statement
                                IfNode elseIf = m_falseBranch[0] as IfNode;
                                if (elseIf != null)
                                {
                                    // it's a nested if-statement. See if IT can be a return argument.
                                    Conditional expr2 = elseIf.CanBeReturnOperand(ultimateOperand, isFunctionLevel);
                                    if (expr2 != null)
                                    {
                                        // it can, so we can just nest the conditionals
                                        conditional = new Conditional(
                                            (Context == null ? null : Context.Clone()),
                                            Parser,
                                            m_condition,
                                            expr1,
                                            expr2);
                                    }
                                }
                                // else neither return- nor if-statement
                            }
                        }
                        // else false branch has more than one statement                           
                    }
                    // else the single statement is not a return-statement
                }
                // else no true branch, or not a single statement in the branch
            }
            catch (NotImplementedException)
            {
                // one of the clone calls probably failed.
                // don't say this can be a return argument.
            }
            return conditional;
        }

        private VoidNode CreateVoidNode()
        {
            return new VoidNode(null, Parser, new ConstantWrapper(0.0, true, null, Parser));
        }

        internal override bool RequiresSeparator
        {
            get
            {
                // if we have an else block, then the if statement
                // requires a separator if the else block does. 
                // otherwise only if the true case requires one.
                if (m_falseBranch != null)
                {
                    return m_falseBranch.RequiresSeparator;
                }
                if (m_trueBranch != null)
                {
                    return m_trueBranch.RequiresSeparator;
                }
                return true;
            }
        }

        internal override bool EndsWithEmptyBlock
        {
            get
            {
                if (m_falseBranch != null)
                {
                    return m_falseBranch.EndsWithEmptyBlock;
                }
                if (m_trueBranch != null)
                {
                    return m_trueBranch.EndsWithEmptyBlock;
                }
                return true;
            }
        }

        internal override bool EncloseBlock(EncloseBlockType type)
        {
            // if there's an else block, recurse down that branch
            if (m_falseBranch != null)
            {
                return m_falseBranch.EncloseBlock(type);
            }
            else if (type == EncloseBlockType.IfWithoutElse)
            {
                // there is no else branch -- we might have to enclose the outer block
                return true;
            }
            else if (m_trueBranch != null)
            {
                return m_trueBranch.EncloseBlock(type);
            }
            return false;
        }

        public override string ToCode(ToCodeFormat format)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("if(");
            sb.Append(m_condition.ToCode());
            sb.Append(')');

            // if we're in Safari-quirks mode, we will need to wrap the if block
            // in curly braces if it only includes a function declaration. Safari
            // throws parsing errors in those situations
            ToCodeFormat elseFormat = ToCodeFormat.Normal;
            if (Parser.Settings.MacSafariQuirks
                && m_falseBranch != null
                && m_falseBranch.Count == 1
                && m_falseBranch[0] is FunctionObject)
            {
                elseFormat = ToCodeFormat.AlwaysBraces;
            }

            // get the else block -- we need to know if there is anything in order
            // to fully determine if the true-branch needs curly-braces
            string elseBlock = (
                m_falseBranch == null
                ? string.Empty
                : m_falseBranch.ToCode(elseFormat));

            // we'll need to force the true block to be enclosed in curly braces if
            // there is an else block and the true block contains a single statement
            // that ends in an if that doesn't have an else block
            ToCodeFormat trueFormat = (m_falseBranch != null
                && m_trueBranch != null
                && m_trueBranch.EncloseBlock(EncloseBlockType.IfWithoutElse)
                ? ToCodeFormat.AlwaysBraces
                : ToCodeFormat.Normal);

            if (elseBlock.Length > 0
              && m_trueBranch != null
              && m_trueBranch.EncloseBlock(EncloseBlockType.SingleDoWhile))
            {
                trueFormat = ToCodeFormat.AlwaysBraces;
            }

            // if we're in Safari-quirks mode, we will need to wrap the if block
            // in curly braces if it only includes a function declaration. Safari
            // throws parsing errors in those situations
            if (Parser.Settings.MacSafariQuirks
                && m_trueBranch != null
                && m_trueBranch.Count == 1
                && m_trueBranch[0] is FunctionObject)
            {
                trueFormat = ToCodeFormat.AlwaysBraces;
            }

            // add the true block
            string trueBlock = (
                m_trueBranch == null
                ? string.Empty
                : m_trueBranch.ToCode(trueFormat));
            sb.Append(trueBlock);

            if (elseBlock.Length > 0)
            {
                if (trueFormat != ToCodeFormat.AlwaysBraces
                    && !trueBlock.EndsWith(";", StringComparison.Ordinal)
                    && (m_trueBranch == null || m_trueBranch.RequiresSeparator))
                {
                    sb.Append(';');
                }

                // if we are in pretty-print mode, drop the else onto a new line
                Parser.Settings.NewLine(sb);
                sb.Append("else");
                // if the first character could be interpreted as a continuation
                // of the "else" statement, then we need to add a space
                if (JSScanner.StartsWithIdentifierPart(elseBlock))
                {
                    sb.Append(' ');
                }

                sb.Append(elseBlock);
            }
            return sb.ToString();
        }
    }
}