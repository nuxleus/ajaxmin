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
        public AstNode Condition { get; private set; }
        public Block TrueBlock { get; private set; }
        public Block FalseBlock { get; private set; }

        public IfNode(Context context, JSParser parser, AstNode condition, AstNode trueBranch, AstNode falseBranch)
            : base(context, parser)
        {
            Condition = condition;
            TrueBlock = ForceToBlock(trueBranch);
            FalseBlock = ForceToBlock(falseBranch);

            // make sure the parent element is set
            if (Condition != null) Condition.Parent = this;
            if (TrueBlock != null) TrueBlock.Parent = this;
            if (FalseBlock != null) FalseBlock.Parent = this;
        }

        public override void Accept(IVisitor visitor)
        {
            if (visitor != null)
            {
                visitor.Visit(this);
            }
        }

        public void SwapBranches()
        {
            Block temp = TrueBlock;
            TrueBlock = FalseBlock;
            FalseBlock = temp;
        }

        public override IEnumerable<AstNode> Children
        {
            get
            {
                return EnumerateNonNullNodes(Condition, TrueBlock, FalseBlock);
            }
        }

        public override bool ReplaceChild(AstNode oldNode, AstNode newNode)
        {
            if (Condition == oldNode)
            {
                Condition = newNode;
                if (newNode != null) { newNode.Parent = this; }
                return true;
            }
            if (TrueBlock == oldNode)
            {
                TrueBlock = ForceToBlock(newNode);
                if (TrueBlock != null) { TrueBlock.Parent = this; }
                return true;
            }
            if (FalseBlock == oldNode)
            {
                FalseBlock = ForceToBlock(newNode);
                if (FalseBlock != null) { FalseBlock.Parent = this; }
                return true;
            }
            return false;
        }

        internal Conditional CanBeReturnOperand(AstNode ultimateOperand, bool isFunctionLevel)
        {
            Conditional conditional = null;
            try
            {
                if (TrueBlock != null && TrueBlock.Count == 1)
                {
                    ReturnNode returnNode = TrueBlock[0] as ReturnNode;
                    if (returnNode != null)
                    {
                        AstNode expr1 = returnNode.Operand;
                        if (FalseBlock == null || FalseBlock.Count == 0)
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
                                    Condition,
                                    expr1 ?? CreateVoidNode(),
                                    ultimateOperand ?? CreateVoidNode());
                            }
                        }
                        else if (FalseBlock.Count == 1)
                        {
                            // there is a false branch with only a single statement
                            // see if it is a return statement
                            returnNode = FalseBlock[0] as ReturnNode;
                            if (returnNode != null)
                            {
                                // it is. so we have if(cond)return expr1;else return expr2
                                // return cond?expr1:expr2
                                AstNode expr2 = returnNode.Operand;
                                conditional = new Conditional(
                                    (Context == null ? null : Context.Clone()),
                                    Parser,
                                    Condition,
                                    expr1 ?? CreateVoidNode(),
                                    expr2 ?? CreateVoidNode());
                            }
                            else
                            {
                                // see if it's another if-statement
                                IfNode elseIf = FalseBlock[0] as IfNode;
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
                                            Condition,
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
            return new VoidNode(null, Parser, new ConstantWrapper(0.0, PrimitiveType.Number, null, Parser));
        }

        internal override bool RequiresSeparator
        {
            get
            {
                // if we have an else block, then the if statement
                // requires a separator if the else block does. 
                // otherwise only if the true case requires one.
                if (FalseBlock != null)
                {
                    return FalseBlock.RequiresSeparator;
                }
                if (TrueBlock != null)
                {
                    return TrueBlock.RequiresSeparator;
                }
                return true;
            }
        }

        internal override bool EndsWithEmptyBlock
        {
            get
            {
                if (FalseBlock != null)
                {
                    return FalseBlock.EndsWithEmptyBlock;
                }
                if (TrueBlock != null)
                {
                    return TrueBlock.EndsWithEmptyBlock;
                }
                return true;
            }
        }

        internal override bool EncloseBlock(EncloseBlockType type)
        {
            // if there's an else block, recurse down that branch
            if (FalseBlock != null)
            {
                return FalseBlock.EncloseBlock(type);
            }
            else if (type == EncloseBlockType.IfWithoutElse)
            {
                // there is no else branch -- we might have to enclose the outer block
                return true;
            }
            else if (TrueBlock != null)
            {
                return TrueBlock.EncloseBlock(type);
            }
            return false;
        }

        public override string ToCode(ToCodeFormat format)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("if(");
            sb.Append(Condition.ToCode());
            sb.Append(')');

            // if we're in Safari-quirks mode, we will need to wrap the if block
            // in curly braces if it only includes a function declaration. Safari
            // throws parsing errors in those situations
            ToCodeFormat elseFormat = ToCodeFormat.Normal;
            if (FalseBlock != null && FalseBlock.Count == 1)
            {
                if (Parser.Settings.MacSafariQuirks
                    && FalseBlock[0] is FunctionObject)
                {
                    elseFormat = ToCodeFormat.AlwaysBraces;
                }
                else if (FalseBlock[0] is IfNode)
                {
                    elseFormat = ToCodeFormat.ElseIf;
                }
            }

            // get the else block -- we need to know if there is anything in order
            // to fully determine if the true-branch needs curly-braces
            string elseBlock = (
                FalseBlock == null
                ? string.Empty
                : FalseBlock.ToCode(elseFormat));

            // we'll need to force the true block to be enclosed in curly braces if
            // there is an else block and the true block contains a single statement
            // that ends in an if that doesn't have an else block
            ToCodeFormat trueFormat = (FalseBlock != null
                && TrueBlock != null
                && TrueBlock.EncloseBlock(EncloseBlockType.IfWithoutElse)
                ? ToCodeFormat.AlwaysBraces
                : ToCodeFormat.Normal);

            if (elseBlock.Length > 0
              && TrueBlock != null
              && TrueBlock.EncloseBlock(EncloseBlockType.SingleDoWhile))
            {
                trueFormat = ToCodeFormat.AlwaysBraces;
            }

            // if we're in Safari-quirks mode, we will need to wrap the if block
            // in curly braces if it only includes a function declaration. Safari
            // throws parsing errors in those situations
            if (Parser.Settings.MacSafariQuirks
                && TrueBlock != null
                && TrueBlock.Count == 1
                && TrueBlock[0] is FunctionObject)
            {
                trueFormat = ToCodeFormat.AlwaysBraces;
            }

            // add the true block
            string trueBlock = (
                TrueBlock == null
                ? string.Empty
                : TrueBlock.ToCode(trueFormat));
            sb.Append(trueBlock);

            if (elseBlock.Length > 0)
            {
                if (trueFormat != ToCodeFormat.AlwaysBraces
                    && !trueBlock.EndsWith(";", StringComparison.Ordinal)
                    && (TrueBlock == null || TrueBlock.RequiresSeparator))
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