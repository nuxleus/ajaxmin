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
        private Block m_block;

        public WithNode(Context context, JSParser parser, AstNode obj, AstNode body)
            : base(context, parser)
        {
            m_withObject = obj;
            m_block = ForceToBlock(body);

            if (m_withObject != null) { m_withObject.Parent = this; }
            if (m_block != null) { m_block.Parent = this; }
        }

        public override AstNode Clone()
        {
            /*return new WithNode(
              (Context == null ? null : Context.Clone()),
              Parser,
              (m_withObject == null ? null : m_withObject.Clone()),
              (m_block == null ? null : m_block.Clone())
              );*/
            throw new NotImplementedException();
        }

        internal override string GetFunctionGuess(AstNode target)
        {
            return "with";
        }

        internal override void AnalyzeNode()
        {
            // throw a warning discouraging the use of this statement
            Context.HandleError(JSError.WithNotRecommended, false);

            // hold onto the with-scope in case we need to do something with it
            BlockScope withScope = (m_block == null ? null : m_block.BlockScope);

            if (Parser.Settings.StripDebugStatements
                 && Parser.Settings.IsModificationAllowed(TreeModifications.StripDebugStatements) 
                 && m_block != null 
                 && m_block.IsDebuggerStatement)
            {
                m_block = null;
            }

            // recurse
            base.AnalyzeNode();

            // we'd have to know what the object (obj) evaluates to before we
            // can figure out what to add to the scope -- not possible without actually
            // running the code. This could throw a whole bunch of 'undefined' errors.
            if (m_block != null && m_block.Count == 0)
            {
                m_block = null;
            }

            // we got rid of the block -- tidy up the no-longer-needed scope
            if (m_block == null && withScope != null)
            {
                // because the scope is empty, we now know it (it does nothing)
                withScope.IsKnownAtCompileTime = true;
            }
        }

        public override IEnumerable<AstNode> Children
        {
            get
            {
                if (m_withObject != null)
                {
                    yield return m_withObject;
                }
                if (m_block != null)
                {
                    yield return m_block;
                }
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
            if (m_block == oldNode)
            {
                m_block = ForceToBlock(newNode);
                if (m_block != null) { m_block.Parent = this; }
                return true;
            }
            return false;
        }

        internal override bool RequiresSeparator
        {
            get
            {
                // requires a separator if the body does
                return m_block == null ? true : m_block.RequiresSeparator;
            }
        }

        internal override bool EndsWithEmptyBlock
        {
            get
            {
                return m_block == null ? true : m_block.EndsWithEmptyBlock;
            }
        }

        internal override bool EncloseBlock(EncloseBlockType type)
        {
            // pass the query on to the body
            return m_block == null ? false : m_block.EncloseBlock(type);
        }

        public override string ToCode(ToCodeFormat format)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("with(");
            sb.Append(m_withObject.ToCode());
            sb.Append(")");

            string bodyString = (
              m_block == null
              ? ";"
              : m_block.ToCode()
              );
            sb.Append(bodyString);
            return sb.ToString();
        }
    }
}