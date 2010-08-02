// ccif.cs
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

namespace Microsoft.Ajax.Utilities
{
    public class ConditionalCompilationIf : ConditionalCompilationStatement
    {
        private AstNode m_condition;

        public ConditionalCompilationIf(Context context, JSParser parser, AstNode condition)
            : base(context, parser)
        {
            m_condition = condition;
            m_condition.Parent = this;
        }

        public override AstNode Clone()
        {
            return new ConditionalCompilationIf(Context, Parser, m_condition);
        }

        public override IEnumerable<AstNode> Children
        {
            get
            {
                if (m_condition != null)
                {
                    yield return m_condition;
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
            return false;
        }

        public override string ToCode(ToCodeFormat format)
        {
            return "@if(" + m_condition.ToCode() + ")";
        }
    }
}
