// break.cs
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
    public sealed class Break : AstNode
    {
        private int m_nestLevel;

        private string m_label;
        internal string Label { get { return m_label; } }

        public Break(Context context, JSParser parser, int count, string label)
            : base(context, parser)
        {
            m_label = (label == null || label.Length == 0) ? null : label;
            m_nestLevel = count;
        }

        public override AstNode Clone()
        {
            return new Break(
              (Context == null ? null : Context.Clone()),
              Parser,
              m_nestLevel,
              m_label);
        }

        internal override void AnalyzeNode()
        {
            if (m_label != null)
            {
                // if the nest level is zero, then we might be able to remove the label altogether
                // IF local renaming is not KeepAll AND the kill switch for removing them isn't set.
                // the nest level will be zero if the label is undefined.
                if (m_nestLevel == 0
                    && Parser.Settings.LocalRenaming != LocalRenaming.KeepAll
                    && Parser.Settings.IsModificationAllowed(TreeModifications.RemoveUnnecessaryLabels))
                {
                    m_label = null;
                }
            }

            // don't need to call the base; this statement has no children to recurse
            //base.AnalyzeNode();
        }

        public override string ToCode(ToCodeFormat format)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("break");
            if (m_label != null)
            {
                sb.Append(' ');
                if (Parser.Settings.LocalRenaming != LocalRenaming.KeepAll
                    && Parser.Settings.IsModificationAllowed(TreeModifications.LocalRenaming))
                {
                    // hypercrunched -- only depends on nesting level
                    sb.Append(CrunchEnumerator.CrunchedLabel(m_nestLevel));
                }
                else
                {
                    // not hypercrunched -- just output label
                    sb.Append(m_label);
                }
            }

            return sb.ToString();
        }
    }
}