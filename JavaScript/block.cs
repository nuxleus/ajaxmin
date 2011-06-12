// block.cs
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

    public sealed class Block : AstNode
    {
        private List<AstNode> m_list;

        public AstNode this[int index]
        {
            get { return m_list[index]; }
            set
            {
                m_list[index] = value;
                if (value != null)
                { 
                    value.Parent = this; 
                }
            }
        }

        private BlockScope m_blockScope;
        internal BlockScope BlockScope
        {
            get { return m_blockScope; }
            set { m_blockScope = value; }
        }

        public override ActivationObject EnclosingScope
        {
            get
            {
                return m_blockScope != null ? m_blockScope : base.EnclosingScope;
            }
        }

        public Block(Context context, JSParser parser)
            : base(context, parser)
        {
            m_list = new List<AstNode>();
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
                // 0 statements, true (lone semicolon)
                // 1 = ask list[0]
                // > 1, false (enclosed in braces
                // if there are 2 or more statements in the block, then
                // we'll wrap them in braces and they won't need a separator
                return (
                  m_list.Count == 0
                  ? true
                  : (m_list.Count == 1 ? m_list[0].RequiresSeparator : false)
                  );
            }
        }

        // if the block has no statements, it ends in an empty block
        internal override bool EndsWithEmptyBlock
        {
            get
            {
                return (m_list.Count == 0);
            }
        }

        internal override bool EncloseBlock(EncloseBlockType type)
        {
            // if there's more than one item, then return false.
            // otherwise recurse the call
            return (m_list.Count == 1 && m_list[0].EncloseBlock(type));
        }

        internal override bool IsDebuggerStatement
        {
            get
            {
                // a block will pop-positive for being a debugger statement
                // if all the statements within it are debugger statements. 
                // So loop through our list, and if any isn't, return false.
                // otherwise return true.
                // empty blocks do not pop positive for "debugger" statements
                if (m_list.Count == 0)
                {
                    return false;
                }

                foreach (AstNode statement in m_list)
                {
                    if (!statement.IsDebuggerStatement)
                    {
                        return false;
                    }
                }
                return true;
            }
        }

        public override bool IsExpression
        {
            get
            {
                // if this block contains a single statement, then recurse.
                // otherwise it isn't.
                //
                // TODO: if there are no statements -- empty block -- then is is an expression?
                // I mean, we can make an empty block be an expression by just making it a zero. 
                return m_list.Count == 1 && m_list[0].IsExpression;
            }
        }

        public int Count
        {
            get { return m_list.Count; }
        }

        public override IEnumerable<AstNode> Children
        {
            get
            {
                return EnumerateNonNullNodes(m_list);
            }
        }

        public int StatementIndex(AstNode childNode)
        {
            // find childNode in our collection of statements
            for (var ndx = 0; ndx < m_list.Count; ++ndx)
            {
                if (m_list[ndx] == childNode)
                {
                    return ndx;
                }
            }
            // if we got here, then childNode is not a statement in our collection!
            return -1;
        }

        public override bool ReplaceChild(AstNode oldNode, AstNode newNode)
        {
            for (int ndx = m_list.Count - 1; ndx >= 0; --ndx)
            {
                if (m_list[ndx] == oldNode)
                {
                    if (newNode == null)
                    {
                        // just remove it
                        m_list.RemoveAt(ndx);
                    }
                    else
                    {
                        Block newBlock = newNode as Block;
                        if (newBlock != null)
                        {
                            // the new "statement" is a block. That means we need to insert all
                            // the statements from the new block at the location of the old item.
                            m_list.RemoveAt(ndx);
                            m_list.InsertRange(ndx, newBlock.m_list);
                        }
                        else
                        {
                            // not a block -- slap it in there
                            m_list[ndx] = newNode;
                            newNode.Parent = this;
                        }
                    }
                    return true;
                }
            }
            return false;
        }

        public override string ToCode(ToCodeFormat format)
        {
            if (format == ToCodeFormat.NoBraces && m_list.Count == 0)
            {
                return string.Empty;
            }

            StringBuilder sb = new StringBuilder();
            bool closeBrace = false;
            bool unindent = false;

            // if the format is N, then we never enclose in braces.
            // if the format is B or T, then we always enclose in braces.
            // anything else, then we enclose in braces if there's more than
            // one enclosing line
            if (format != ToCodeFormat.NoBraces && Parent != null)
            {
                if (format == ToCodeFormat.AlwaysBraces
                  || format == ToCodeFormat.NestedTry
                  || m_list.Count > 1)
                {
                    // opening brace on a new line
                    Parser.Settings.NewLine(sb);

                    // up the indent level for the content within
                    Parser.Settings.Indent();

                    sb.Append("{");
                    closeBrace = true;
                }
                else if (m_list.Count == 1 && format != ToCodeFormat.ElseIf)
                {
                    // we're pretty-printing a single-line block.
                    // we still won't enclose in brackets, but we need to indent it
                    Parser.Settings.Indent();
                    unindent = true;
                }
            }

            bool requireSeparator = false;
            bool endsWithEmptyBlock = false;
            bool mightNeedSpace = false;
            for (int ndx = 0; ndx < m_list.Count; ++ndx)
            {
                AstNode item = m_list[ndx];
                if (item != null)
                {
                    string itemText = item.ToCode();
                    if (itemText.Length > 0)
                    {
                        // see if we need to add a semi-colon
                        if (ndx > 0 && requireSeparator)
                        {
                            sb.Append(';');
                            if (Parser.Settings.OutputMode == OutputMode.SingleLine && item is ImportantComment)
                            {
                                // if this item is an important comment and we're in single-line mode, 
                                // we'll start on a new line. Since this is single-line mode, don't call AppendLine
                                // because it will send CRLF -- we just want the LF
                                sb.Append('\n');
                            }
                            // we no longer require a separator next time around
                            requireSeparator = false;
                        }

                        // if this is an else-if construct, we don't want to break to a new line.
                        // but all other formats put the next statement on a newline
                        if (format != ToCodeFormat.ElseIf)
                        {
                            Parser.Settings.NewLine(sb);
                        }

                        if (mightNeedSpace && JSScanner.IsValidIdentifierPart(itemText[0]))
                        {
                            sb.Append(' ');
                        }

                        sb.Append(itemText);
                        requireSeparator = (item.RequiresSeparator && !itemText.EndsWith(";", StringComparison.Ordinal));
                        endsWithEmptyBlock = item.EndsWithEmptyBlock;

                        mightNeedSpace =
                            (item is ConditionalCompilationStatement)
                            && JSScanner.IsValidIdentifierPart(itemText[itemText.Length - 1]);
                    }
                }
            }
            if (endsWithEmptyBlock)
            {
                sb.Append(';');
            }

            if (closeBrace)
            {
                // unindent now that the block is done
                Parser.Settings.Unindent();
                Parser.Settings.NewLine(sb);
                sb.Append("}");
            }
            else if (unindent)
            {
                Parser.Settings.Unindent();
            }
            return sb.ToString();
        }

        public void Append(AstNode element)
        {
            Block block = element as Block;
            if (block != null)
            {
                // adding a block to the block -- just append the elements
                // from the block to ourselves
                InsertRange(m_list.Count, block.Children);
            }
            else if (element != null)
            {
                // not a block....
                element.Parent = this;
                m_list.Add(element);
            }
        }

        public int IndexOf(AstNode child)
        {
            return m_list.IndexOf(child);
        }

        public void InsertAfter(AstNode after, AstNode item)
        {
            if (item != null)
            {
                int index = m_list.IndexOf(after);
                if (index >= 0)
                {
                    var block = item as Block;
                    if (block != null)
                    {
                        // don't insert a block into a block -- insert the new block's
                        // children instead (don't want nested blocks)
                        InsertRange(index + 1, block.Children);
                    }
                    else
                    {
                        item.Parent = this;
                        m_list.Insert(index + 1, item);
                    }
                }
            }
        }

        public void Insert(int position, AstNode item)
        {
            if (item != null)
            {
                var block = item as Block;
                if (block != null)
                {
                    InsertRange(position, block.Children);
                }
                else
                {
                    item.Parent = this;
                    m_list.Insert(position, item);
                }
            }
        }

        public void RemoveLast()
        {
            m_list.RemoveAt(m_list.Count - 1);
        }

        public void RemoveAt(int index)
        {
            if (0 <= index && index < m_list.Count)
            {
                m_list.RemoveAt(index);
            }
        }

        public void InsertRange(int index, IEnumerable<AstNode> newItems)
        {
            if (newItems != null)
            {
                m_list.InsertRange(index, newItems);
                foreach (AstNode newItem in newItems)
                {
                    newItem.Parent = this;
                }
            }
        }

        public void Clear()
        {
            m_list.Clear();
        }
    }
}