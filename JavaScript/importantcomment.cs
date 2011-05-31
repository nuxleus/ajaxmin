// importantcomment.cs
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

namespace Microsoft.Ajax.Utilities
{
    public class ImportantComment : AstNode
    {
        private string m_comment;

        public ImportantComment(Context context, JSParser parser)
            : base(context, parser)
        {
            // replace all internal CR-LF pairs with just a single LF
            m_comment = Context.Code.Replace("\r\n", "\n");
        }

        public override void Accept(IVisitor visitor)
        {
            if (visitor != null)
            {
                visitor.Visit(this);
            }
        }

        public override string ToCode(ToCodeFormat format)
        {
            // make sure important comments start a new line afterwards
            return m_comment + '\n';
        }

        internal override bool RequiresSeparator
        {
            get
            {
                // never requires a separator because we always line-break after the comment
                return false;
            }
        }
    }
}
