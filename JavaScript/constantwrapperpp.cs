// constantwrapperpp.cs
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

using System.Text;

namespace Microsoft.Ajax.Utilities
{
    public class ConstantWrapperPP : AstNode
    {
        private string m_varName;
        public string VarName { get { return m_varName; } }

        private bool m_forceComments;

        public ConstantWrapperPP(string varName, bool forceComments, Context context, JSParser parser)
            : base(context, parser)
        {
            m_varName = varName;
            m_forceComments = forceComments;
        }

        public override AstNode Clone()
        {
            return new ConstantWrapperPP(
                m_varName,
                m_forceComments,
                (Context == null ? null : Context.Clone()),
                Parser
                );
        }

        public override string ToCode(ToCodeFormat format)
        {
            StringBuilder sb = new StringBuilder();
            if (m_forceComments)
            {
                sb.Append("/*");
            }
            sb.Append('@');
            sb.Append(m_varName);
            if (m_forceComments)
            {
                sb.Append("@*/");
            }
            return sb.ToString();
        }
    }
}
