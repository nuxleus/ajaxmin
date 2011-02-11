// while.cs
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
    public sealed class WhileNode : AstNode
    {
        public AstNode Condition { get; private set; }
        public Block Body { get; private set; }

        public WhileNode(Context context, JSParser parser, AstNode condition, AstNode body)
            : base(context, parser)
        {
            Condition = condition;
            Body = ForceToBlock(body);
            if (Condition != null) Condition.Parent = this;
            if (Body != null) Body.Parent = this;
        }

        public override AstNode Clone()
        {
            return new WhileNode(
              (Context == null ? null : Context.Clone()),
              Parser,
              (Condition == null ? null : Condition.Clone()),
              (Body == null ? null : Body.Clone())
              );
        }

        internal override void AnalyzeNode()
        {
            if (Parser.Settings.StripDebugStatements
                 && Parser.Settings.IsModificationAllowed(TreeModifications.StripDebugStatements) 
                 && Body != null 
                 && Body.IsDebuggerStatement)
            {
                Body = null;
            }

            // recurse
            base.AnalyzeNode();

            // if the body is now empty, make it null
            if (Body != null && Body.Count == 0)
            {
                Body = null;
            }
        }

        public override IEnumerable<AstNode> Children
        {
            get
            {
                if (Condition != null)
                {
                    yield return Condition;
                }
                if (Body != null)
                {
                    yield return Body;
                }
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
            if (Body == oldNode)
            {
                Body = ForceToBlock(newNode);
                if (Body != null) { Body.Parent = this; }
                return true;
            }
            return false;
        }

        internal override bool RequiresSeparator
        {
            get
            {
                // requires a separator if the body does
                return Body == null ? true : Body.RequiresSeparator;
            }
        }

        internal override bool EndsWithEmptyBlock
        {
            get
            {
                return Body == null ? true : Body.EndsWithEmptyBlock;
            }
        }

        internal override bool EncloseBlock(EncloseBlockType type)
        {
            // pass the query on to the body
            return Body == null ? false : Body.EncloseBlock(type);
        }

        public override string ToCode(ToCodeFormat format)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("while(");
            sb.Append(Condition.ToCode());
            sb.Append(')');

            string bodyString = (
              Body == null
              ? string.Empty
              : Body.ToCode()
              );
            sb.Append(bodyString);
            return sb.ToString();
        }

        public override void CleanupNodes()
        {
            base.CleanupNodes();

            // see if the condition is a constant
            if (Parser.Settings.EvalLiteralExpressions
                && Parser.Settings.IsModificationAllowed(TreeModifications.EvaluateNumericExpressions))
            {
                ConstantWrapper constantCondition = Condition as ConstantWrapper;
                if (constantCondition != null)
                {
                    // TODO: we'd RATHER eliminate the statement altogether if the condition is always false,
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
                            if (Parser.Settings.IsModificationAllowed(TreeModifications.MoveVarIntoFor))
                            {
                                // if the previous statement is a var, we can move it to the initializer
                                // and save even more bytes. The parent should always be a block. If not, 
                                // then assume there is no previous.
                                Block parentBlock = Parent as Block;
                                if (parentBlock != null)
                                {
                                    int whileIndex = parentBlock.StatementIndex(this);
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
                            ForNode forNode = new ForNode(Context, Parser, initializer, null, null, Body);
                            Parent.ReplaceChild(this, forNode);
                        }
                        else if (constantCondition.IsNotOneOrPositiveZero)
                        {
                            // the condition is always false, so we can replace the condition
                            // with a zero -- only one byte
                            Condition = new ConstantWrapper(0, PrimitiveType.Number, null, Parser);
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