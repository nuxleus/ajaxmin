// cccomment.cs
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
    public class ConditionalCompilationComment : AstNode
    {
        private Block m_statements;
        public Block Statements { get { return m_statements; } }

        public ConditionalCompilationComment(Context context, JSParser parser)
            : base(context, parser)
        {
            m_statements = new Block(null, parser);
            m_statements.Parent = this;
        }


        internal override bool RequiresSeparator
        {
            get
            {
                return m_statements.Count > 0 ? m_statements[m_statements.Count - 1].RequiresSeparator : true;
            }
        }
        public override AstNode Clone()
        {
            throw new NotImplementedException();
        }

        public void Append(AstNode statement)
        {
            if (statement != null)
            {
                Context.UpdateWith(statement.Context);
                m_statements.Append(statement);
            }
        }

        public override IEnumerable<AstNode> Children
        {
            get
            {
                if (m_statements != null)
                {
                    yield return m_statements;
                }
            }
        }

        public override bool ReplaceChild(AstNode oldNode, AstNode newNode)
        {
            if (m_statements == oldNode)
            {
                m_statements = ForceToBlock(newNode);
                if (m_statements != null) { m_statements.Parent = this; }
                return true;
            }
            return false;
        }

        public override string ToCode(ToCodeFormat format)
        {
            StringBuilder sb = new StringBuilder();

            // if there aren't any statements, we don't need this comment
            if (m_statements.Count > 0)
            {
                sb.Append("/*");

                // a first conditional compilation statement will take care of the opening @-sign,
                // so if the first statement is NOT a conditional compilation statement, then we
                // need to take care of it outselves
                if (!(m_statements[0] is ConditionalCompilationStatement))
                {
                    sb.Append("@ ");
                }

                sb.Append(m_statements.ToCode(ToCodeFormat.NoBraces));
                sb.Append("@*/");
            }
            return sb.ToString();
        }
    }
}
