// try.cs
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

using System.Collections.Generic;
using System.Text;

namespace Microsoft.Ajax.Utilities
{
    public sealed class TryNode : AstNode
    {
		public Block TryBlock { get; private set; }
		public Block CatchBlock { get; private set; }
		public Block FinallyBlock { get; private set; }

        public string CatchVarName { get; private set; }
        public Context CatchVarContext { get; private set; }

        private JSVariableField m_catchVariable;

        public TryNode(Context context, JSParser parser, AstNode tryBlock, string catchVarName, Context catchVarContext, AstNode catchBlock, AstNode finallyBlock)
            : base(context, parser)
        {
            CatchVarName = catchVarName;
            TryBlock = ForceToBlock(tryBlock);
            CatchBlock = ForceToBlock(catchBlock);
            FinallyBlock = ForceToBlock(finallyBlock);
            if (TryBlock != null) { TryBlock.Parent = this; }
            if (CatchBlock != null) { CatchBlock.Parent = this; }
            if (FinallyBlock != null) { FinallyBlock.Parent = this; }

            CatchVarContext = catchVarContext;
        }

        public void SetCatchVariable(JSVariableField field)
        {
            m_catchVariable = field;
        }

        public override void Accept(IVisitor visitor)
        {
            if (visitor != null)
            {
                visitor.Visit(this);
            }
        }

        public override IEnumerable<AstNode> Children
        {
            get
            {
                return EnumerateNonNullNodes(TryBlock, CatchBlock, FinallyBlock);
            }
        }

        public override bool ReplaceChild(AstNode oldNode, AstNode newNode)
        {
            if (TryBlock == oldNode)
            {
                TryBlock = ForceToBlock(newNode);
                if (TryBlock != null) { TryBlock.Parent = this; }
                return true;
            }
            if (CatchBlock == oldNode)
            {
                CatchBlock = ForceToBlock(newNode);
                if (CatchBlock != null) { CatchBlock.Parent = this; }
                return true;
            }
            if (FinallyBlock == oldNode)
            {
                FinallyBlock = ForceToBlock(newNode);
                if (FinallyBlock != null) { FinallyBlock.Parent = this; }
                return true;
            }
            return false;
        }

        internal override bool RequiresSeparator
        {
            get
            {
                // try requires no separator
                return false;
            }
        }

        public override string ToCode(ToCodeFormat format)
        {
            StringBuilder sb = new StringBuilder();

            // passing a "T" format means nested try's don't actually nest -- they 
            // just add the catch clauses to the end
            if (format != ToCodeFormat.NestedTry)
            {
                sb.Append("try");
                if (TryBlock == null)
                {
                    // empty body
                    sb.Append("{}");
                }
                else
                {
                    sb.Append(TryBlock.ToCode(ToCodeFormat.NestedTry));
                }
            }
            else
            {
                sb.Append(TryBlock.ToCode(ToCodeFormat.NestedTry));
            }

            // handle the catch clause (if any)
            // catch should always have braces around it
            string catchString = (
                CatchBlock == null
                ? string.Empty
                : CatchBlock.Count == 0
                    ? "{}"
                    : CatchBlock.ToCode(ToCodeFormat.AlwaysBraces)
                );
            if (catchString.Length > 0)
            {
                Parser.Settings.NewLine(sb);
                sb.Append("catch(");
                if (m_catchVariable != null)
                {
                    sb.Append(m_catchVariable.ToString());
                }
                else if (CatchVarName != null)
                {
                    sb.Append(CatchVarName);
                }
                sb.Append(')');
                sb.Append(catchString);
            }

            // handle the finally, if any
            // finally should always have braces around it
            string finallyString = (
              FinallyBlock == null
              ? string.Empty
              : FinallyBlock.ToCode(ToCodeFormat.AlwaysBraces)
              );
            if (finallyString.Length > 0)
            {
                Parser.Settings.NewLine(sb);
                sb.Append("finally");
                sb.Append(finallyString);
            }
            return sb.ToString();
        }
    }
}
