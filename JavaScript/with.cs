// with.cs
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
    public sealed class WithNode : AstNode
    {
        private AstNode m_withObject;
        public Block Body { get; private set; }

        public WithNode(Context context, JSParser parser, AstNode obj, AstNode body)
            : base(context, parser)
        {
            m_withObject = obj;
            Body = ForceToBlock(body);

            if (m_withObject != null) { m_withObject.Parent = this; }
            if (Body != null) { Body.Parent = this; }
        }

        public override void Accept(IVisitor visitor)
        {
            if (visitor != null)
            {
                visitor.Visit(this);
            }
        }

        internal override string GetFunctionGuess(AstNode target)
        {
            return "with";
        }

        public override IEnumerable<AstNode> Children
        {
            get
            {
                return EnumerateNonNullNodes(m_withObject, Body);
            }
        }

        public override bool ReplaceChild(AstNode oldNode, AstNode newNode)
        {
            if (m_withObject == oldNode)
            {
                m_withObject = newNode;
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
            sb.Append("with(");
            sb.Append(m_withObject.ToCode());
            sb.Append(")");

            string bodyString = (
              Body == null
              ? ";"
              : Body.ToCode()
              );
            sb.Append(bodyString);
            return sb.ToString();
        }
    }
}