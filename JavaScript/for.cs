// for.cs
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

    public sealed class ForNode : AstNode
    {
        public AstNode Initializer { get; private set; }
        public AstNode Condition { get; private set; }
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "Incrementer")]
        public AstNode Incrementer { get; private set; }
        public Block Body { get; private set; }

        public ForNode(Context context, JSParser parser, AstNode initializer, AstNode condition, AstNode increment, AstNode body)
            : base(context, parser)
        {
            Initializer = initializer;
            Condition = condition;
            Incrementer = increment;
            Body = ForceToBlock(body);
            if (Body != null) Body.Parent = this;
            if (Incrementer != null) Incrementer.Parent = this;
            if (Condition != null) Condition.Parent = this;
            if (Initializer != null) Initializer.Parent = this;
        }

        public void SetInitializer(AstNode newNode)
        {
            Initializer = newNode;
            if (newNode != null)
            {
                newNode.Parent = this;
            }
        }

        public override void Accept(IVisitor visitor)
        {
            if (visitor != null)
            {
                visitor.Visit(this);
            }
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

        public override IEnumerable<AstNode> Children
        {
            get
            {
                return EnumerateNonNullNodes(Initializer, Condition, Incrementer, Body);
            }
        }

        public override bool ReplaceChild(AstNode oldNode, AstNode newNode)
        {
            if (Initializer == oldNode)
            {
                Initializer = newNode;
                if (newNode != null) { newNode.Parent = this; }
                return true;
            }
            if (Condition == oldNode)
            {
                Condition = newNode;
                if (newNode != null) { newNode.Parent = this; }
                return true;
            }
            if (Incrementer == oldNode)
            {
                Incrementer = newNode;
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

        public override string ToCode(ToCodeFormat format)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("for(");
            if (Initializer != null)
            {
                sb.Append(Initializer.ToCode());
            }
            sb.Append(';');
            CodeSettings codeSettings = Parser.Settings;
            if (codeSettings.OutputMode == OutputMode.MultipleLines && codeSettings.IndentSize > 0)
            {
                sb.Append(' ');
            }
            if (Condition != null)
            {
                sb.Append(Condition.ToCode());
            }
            sb.Append(';');
            if (codeSettings.OutputMode == OutputMode.MultipleLines && codeSettings.IndentSize > 0)
            {
                sb.Append(' ');
            }
            if (Incrementer != null)
            {
                sb.Append(Incrementer.ToCode());
            }
            sb.Append(')');
            string bodyString = (
              Body == null
              ? string.Empty
              : Body.ToCode()
              );
            sb.Append(bodyString);
            return sb.ToString();
        }
    }
}
