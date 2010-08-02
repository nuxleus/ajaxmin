// labeledstatement.cs
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
    public sealed class LabeledStatement : AstNode
    {
        private string m_label;
        private AstNode m_statement;
        private int m_nestCount;

        public string Label
        {
            get { return m_label; }
            set { m_label = value; }
        }

        public LabeledStatement(Context context, JSParser parser, string label, int nestCount, AstNode statement)
            : base(context, parser)
        {
            m_label = label;
            m_statement = statement;
            m_nestCount = nestCount;

            if (m_statement != null)
            {
                m_statement.Parent = this;
            }
        }

        public override AstNode Clone()
        {
            return new LabeledStatement(
              (Context == null ? null : Context.Clone()),
              Parser,
              m_label,
              m_nestCount,
              (m_statement == null ? null : m_statement.Clone())
              );
        }

        internal override bool RequiresSeparator
        {
            get
            {
                // requires a separator if the statement does
                return (m_statement != null ? m_statement.RequiresSeparator : false);
            }
        }

        internal override bool EndsWithEmptyBlock
        {
            get
            {
                return (m_statement != null ? m_statement.EndsWithEmptyBlock : false);
            }
        }

        public override AstNode LeftHandSide
        {
            get
            {
                // the label is on the left, but it's sorta ignored
                return (m_statement != null ? m_statement.LeftHandSide : null);
            }
        }

        internal override bool EncloseBlock(EncloseBlockType type)
        {
            // pass the query on to the statement
            return (m_statement != null ? m_statement.EncloseBlock(type) : false);
        }

        public override IEnumerable<AstNode> Children
        {
            get
            {
                if (m_statement != null)
                {
                    yield return m_statement;
                }
            }
        }

        public override bool ReplaceChild(AstNode oldNode, AstNode newNode)
        {
            if (m_statement == oldNode)
            {
                m_statement = newNode;
                if (newNode != null) { newNode.Parent = this; }
                return true;
            }
            return false;
        }

        public override string ToCode(ToCodeFormat format)
        {
            StringBuilder sb = new StringBuilder();
            if (Parser.Settings.LocalRenaming != LocalRenaming.KeepAll
				&& Parser.Settings.IsModificationAllowed(TreeModifications.LocalRenaming))
            {
                // we're hyper-crunching.
                // we want to output our label as per our nested level.
                // top-level is "a", next level is "b", etc.
                // we don't need to worry about collisions with variables.
                sb.Append(CrunchEnumerator.CrunchedLabel(m_nestCount));
            }
            else
            {
                // not hypercrunching -- just output our label
                sb.Append(m_label);
            }
            sb.Append(':');
            if (m_statement != null)
            {
                // don't sent the AlwaysBraces down the chain -- we're handling it here.
                // but send any other formats down -- we don't know why they were sent.
                sb.Append(m_statement.ToCode(format));
            }
            return sb.ToString();
        }

    }
}
