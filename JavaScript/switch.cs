// switch.cs
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
    public sealed class Switch : AstNode
    {
        private AstNode m_expression;
        private AstNodeList m_cases;

        public Switch(Context context, JSParser parser, AstNode expression, AstNodeList cases)
            : base(context, parser)
        {
            m_expression = expression;
            m_cases = cases;
            if (m_expression != null) m_expression.Parent = this;
            if (m_cases != null) m_cases.Parent = this;
        }

        public override AstNode Clone()
        {
            return new Switch(
              (Context == null ? null : Context.Clone()),
              Parser,
              (m_expression == null ? null : m_expression.Clone()),
              (m_cases == null ? null : (AstNodeList)m_cases.Clone())
              );
        }

        internal override bool RequiresSeparator
        {
            get
            {
                // switch always has curly-braces, so we don't
                // require the separator
                return false;
            }
        }

        internal override void AnalyzeNode()
        {
            // recurse
            base.AnalyzeNode();

            // we only want to remove stuff if we are hypercrunching
            if (Parser.Settings.RemoveUnneededCode)
            {
                // because we are looking at breaks, we need to know if this
                // switch statement is labeled
                string thisLabel = string.Empty;
                LabeledStatement label = Parent as LabeledStatement;
                if (label != null)
                {
                    thisLabel = label.Label;
                }

                // loop through all the cases, looking for the default.
                // then, if it's empty (or just doesn't do anything), we can
                // get rid of it altogether
                int defaultCase = -1;
                bool eliminateDefault = false;
                for (int ndx = 0; ndx < m_cases.Count; ++ndx)
                {
                    // it should always be a switch case, but just in case...
                    SwitchCase switchCase = m_cases[ndx] as SwitchCase;
                    if (switchCase != null)
                    {
                        if (switchCase.IsDefault)
                        {
                            // save the index for later
                            defaultCase = ndx;

                            // set the flag to true unless we can prove that we need it.
                            // we'll prove we need it by finding the statement block executed by
                            // this case and showing that it's neither empty nor containing
                            // just a single break statement.
                            eliminateDefault = true;
                        }

                        // if the default case is empty, then we need to keep going
                        // until we find the very next non-empty case
                        if (eliminateDefault && switchCase.Statements.Count > 0)
                        {
                            // this is the set of statements executed during default processing.
                            // if it does nothing -- one break statement -- then we can get rid
                            // of the default case. Otherwise we need to leave it in.
                            if (switchCase.Statements.Count == 1)
                            {
                                // see if it's a break
                                Break lastBreak = switchCase.Statements[0] as Break;

                                // if the last statement is not a break,
                                // OR it has a label and it's not this switch statement...
                                if (lastBreak == null
                                  || (lastBreak.Label != null && lastBreak.Label != thisLabel))
                                {
                                    // set the flag back to false to indicate that we need to keep it.
                                    eliminateDefault = false;
                                }
                            }
                            else
                            {
                                // set the flag back to false to indicate that we need to keep it.
                                eliminateDefault = false;
                            }

                            // break out of the loop
                            break;
                        }
                    }
                }

                // if we get here and the flag is still true, then either the default case is
                // empty, or it contains only a single break statement. Either way, we can get 
                // rid of it.
                if (eliminateDefault && defaultCase >= 0
                    && Parser.Settings.IsModificationAllowed(TreeModifications.RemoveEmptyDefaultCase))
                {
                    // remove it and reset the position index
                    m_cases.RemoveAt(defaultCase);
                    defaultCase = -1;
                }

                // if we have no default handling, then we know we can get rid
                // of any cases that don't do anything either.
                if (defaultCase == -1
                    && Parser.Settings.IsModificationAllowed(TreeModifications.RemoveEmptyCaseWhenNoDefault))
                {
                    // when we delete a case statement, we set this flag to true.
                    // when we hit a non-empty case statement, we set the flag to false.
                    // if we hit an empty case statement when this flag is true, we can delete this case, too.
                    bool emptyStatements = true;
                    Break deletedBreak = null;

                    // walk the tree backwards because we don't know how many we will
                    // be deleting, and if we go backwards, we won't have to adjust the 
                    // index as we go.
                    for (int ndx = m_cases.Count - 1; ndx >= 0; --ndx)
                    {
                        // should always be a switch case
                        SwitchCase switchCase = m_cases[ndx] as SwitchCase;
                        if (switchCase != null)
                        {
                            // if the block is empty and the last block was empty, we can delete this case.
                            // OR if there is only one statement and it's a break, we can delete it, too.
                            if (switchCase.Statements.Count == 0 && emptyStatements)
                            {
                                // remove this case statement because it falls through to a deleted case
                                m_cases.RemoveAt(ndx);
                            }
                            else
                            {
                                // onlyBreak will be set to null if this block is not a single-statement break block
                                Break onlyBreak = (switchCase.Statements.Count == 1 ? switchCase.Statements[0] as Break : null);
                                if (onlyBreak != null)
                                {
                                    // we'll only delete this case if the break either doesn't have a label
                                    // OR the label matches the switch statement
                                    if (onlyBreak.Label == null || onlyBreak.Label == thisLabel)
                                    {
                                        // if this is a block with only a break, then we need to keep a hold of the break
                                        // statement in case we need it later
                                        deletedBreak = onlyBreak;

                                        // remove this case statement
                                        m_cases.RemoveAt(ndx);
                                        // make sure the flag is set so we delete any other empty
                                        // cases that fell through to this empty case block
                                        emptyStatements = true;
                                    }
                                    else
                                    {
                                        // the break statement has a label and it's not the switch statement.
                                        // we're going to keep this block
                                        emptyStatements = false;
                                        deletedBreak = null;
                                    }
                                }
                                else
                                {
                                    // either this is a non-empty block, or it's an empty case that falls through
                                    // to a non-empty block. if we have been deleting case statements and this
                                    // is not an empty block....
                                    if (emptyStatements && switchCase.Statements.Count > 0 && deletedBreak != null)
                                    {
                                        // we'll need to append the deleted break statement if it doesn't already have
                                        // a flow-changing statement: break, continue, return, or throw
                                        AstNode lastStatement = switchCase.Statements[switchCase.Statements.Count - 1];
                                        if (!(lastStatement is Break) && !(lastStatement is ContinueNode)
                                          && !(lastStatement is ReturnNode) && !(lastStatement is ThrowNode))
                                        {
                                            switchCase.Statements.Append(deletedBreak);
                                        }
                                    }

                                    // make sure the deletedBreak flag is reset
                                    deletedBreak = null;

                                    // reset the flag
                                    emptyStatements = false;
                                }
                            }
                        }
                    }
                }

                // if the last case's statement list ends in a break, 
                // we can get rid of the break statement
                if (m_cases.Count > 0
                    && Parser.Settings.IsModificationAllowed(TreeModifications.RemoveBreakFromLastCaseBlock))
                {
                    SwitchCase lastCase = m_cases[m_cases.Count - 1] as SwitchCase;
                    if (lastCase != null)
                    {
                        // get the block of statements making up the last case block
                        Block lastBlock = lastCase.Statements;
                        // if the last statement is not a break, then lastBreak will be null
                        Break lastBreak = (lastBlock.Count > 0 ? lastBlock[lastBlock.Count - 1] as Break : null);
                        // if lastBreak is not null and it either has no label, or the label matches this switch statement...
                        if (lastBreak != null
                          && (lastBreak.Label == null || lastBreak.Label == thisLabel))
                        {
                            // remove the break statement
                            lastBlock.RemoveLast();
                        }
                    }
                }
            }
        }

        public override IEnumerable<AstNode> Children
        {
            get
            {
                return EnumerateNonNullNodes(m_expression, m_cases);
            }
        }

        public override bool ReplaceChild(AstNode oldNode, AstNode newNode)
        {
            if (m_expression == oldNode)
            {
                m_expression = newNode;
                if (newNode != null) { newNode.Parent = this; }
                return true;
            }
            if (m_cases == oldNode)
            {
                if (newNode == null)
                {
                    // remove it
                    m_cases = null;
                    return true;
                }
                else
                {
                    // if the new node isn't an AstNodeList, ignore the call
                    AstNodeList newList = newNode as AstNodeList;
                    if (newList != null)
                    {
                        m_cases = newList;
                        newNode.Parent = this;
                        return true;
                    }
                }
            }
            return false;
        }

        public override string ToCode(ToCodeFormat format)
        {
            StringBuilder sb = new StringBuilder();
            // switch and value
            sb.Append("switch(");
            sb.Append(m_expression.ToCode());
            sb.Append(')');

            // opening brace
            Parser.Settings.NewLine(sb);
            sb.Append('{');

            // cases
            sb.Append(m_cases.ToCode(ToCodeFormat.Semicolons));

            // closing brace
            Parser.Settings.NewLine(sb);
            sb.Append('}');
            return sb.ToString();
        }
    }
}
