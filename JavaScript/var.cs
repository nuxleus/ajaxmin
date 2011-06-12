// var.cs
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
    /// <summary>
    /// Summary description for variablestatement.
    /// </summary>
    public sealed class Var : AstNode
    {
        private List<VariableDeclaration> m_list;

        public int Count
        {
            get { return m_list.Count; }
        }

        public VariableDeclaration this[int index]
        {
            get { return m_list[index]; }
        }

        public Var(Context context, JSParser parser)
            : base(context, parser)
        {
            m_list = new List<VariableDeclaration>();
        }

        public override void Accept(IVisitor visitor)
        {
            if (visitor != null)
            {
                visitor.Visit(this);
            }
        }

        public override IEnumerable<AstNode> Children
        {
            get
            {
                return EnumerateNonNullNodes(m_list);
            }
        }

        public override bool ReplaceChild(AstNode oldNode, AstNode newNode)
        {
            for (int ndx = 0; ndx < m_list.Count; ++ndx)
            {
                if (m_list[ndx] == oldNode)
                {
                    if (newNode == null)
                    {
                        // remove it
                        m_list.RemoveAt(ndx);
                        return true;
                    }
                    else
                    {
                        // if the new node isn't a variabledeclaration, ignore the call
                        VariableDeclaration newDecl = newNode as VariableDeclaration;
                        if (newDecl != null)
                        {
                            m_list[ndx] = newDecl;
                            newNode.Parent = this;
                            return true;
                        }
                        break;
                    }
                }
            }
            return false;
        }

        internal void Append(AstNode elem)
        {
            VariableDeclaration decl = elem as VariableDeclaration;
            if (decl != null)
            {
                // first check the list for existing instances of this name.
                // if there are no duplicates (indicated by returning true), add it to the list.
                // if there is a dup (indicated by returning false) then that dup
                // has an initializer, and we DON'T want to add this new one if it doesn't
                // have it's own initializer.
                if (HandleDuplicates(decl.Identifier)
                    || decl.Initializer != null)
                {
                    // set the parent and add it to the list
                    decl.Parent = this;
                    m_list.Add(decl);
                }
            }
            else
            {
                Var otherVar = elem as Var;
                if (otherVar != null)
                {
                    for (int ndx = 0; ndx < otherVar.m_list.Count; ++ndx)
                    {
                        Append(otherVar.m_list[ndx]);
                    }
                }
            }
        }

        internal void InsertAt(int index, AstNode elem)
        {
            VariableDeclaration decl = elem as VariableDeclaration;
            if (decl != null)
            {
                // first check the list for existing instances of this name.
                // if there are no duplicates (indicated by returning true), add it to the list.
                // if there is a dup (indicated by returning false) then that dup
                // has an initializer, and we DON'T want to add this new one if it doesn't
                // have it's own initializer.
                if (HandleDuplicates(decl.Identifier)
                    || decl.Initializer != null)
                {
                    // set the parent and add it to the list
                    decl.Parent = this;
                    m_list.Insert(index, decl);
                }
            }
            else
            {
                Var otherVar = elem as Var;
                if (otherVar != null)
                {
                    // walk the source backwards so they end up in the right order
                    for (int ndx = otherVar.m_list.Count - 1; ndx >= 0; --ndx)
                    {
                        InsertAt(index, otherVar.m_list[ndx]);
                    }
                }
            }
        }

        private bool HandleDuplicates(string name)
        {
            var hasInitializer = true;
            // walk backwards because we'll be removing items from the list
            for (var ndx = m_list.Count - 1; ndx >= 0 ; --ndx)
            {
                VariableDeclaration varDecl = m_list[ndx];

                // if the name is a match...
                if (string.CompareOrdinal(varDecl.Identifier, name) == 0)
                {
                    // check the initializer. If there is no initializer, then
                    // we want to remove it because we'll be adding a new one.
                    // but if there is an initializer, keep it but return false
                    // to indicate that there is still a duplicate in the list, 
                    // and that dup has an initializer.
                    if (varDecl.Initializer != null)
                    {
                        hasInitializer = false;
                    }
                    else
                    {
                        m_list.RemoveAt(ndx);
                    }
                }
            }

            return hasInitializer;
        }

        public void RemoveAt(int index)
        {
            if (0 <= index & index < m_list.Count)
            {
                m_list.RemoveAt(index);
            }
        }

        public bool Contains(string name)
        {
            // look at each vardecl in our list
            foreach(var varDecl in m_list)
            {
                // if it matches the target name exactly...
                if (string.CompareOrdinal(varDecl.Identifier, name) == 0)
                {
                    // ...we found a match
                    return true;
                }
            }
            // if we get here, we didn't find any matches
            return false;
        }

        public override string ToCode(ToCodeFormat format)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("var ");
            Parser.Settings.Indent();

            bool first = true;
            for (int ndx = 0; ndx < m_list.Count; ++ndx)
            {
                VariableDeclaration vdecl = m_list[ndx];
                if (vdecl != null)
                {
                    if (!first)
                    {
                        sb.Append(',');
                        Parser.Settings.NewLine(sb);
                    }
                    sb.Append(vdecl.ToCode());
                    first = false;
                }
            }
            Parser.Settings.Unindent();
            return sb.ToString();
        }

        /*
        public void RemoveUnreferencedGenerated(FunctionScope scope)
        {
            // walk backwards -- we're going to delete any generated variables that
            // aren't actually referenced
            for (int ndx = m_list.Count - 1; ndx >= 0; --ndx)
            {
                VariableDeclaration varDecl = m_list[ndx];
                // if it's not generated, leave it
                if (varDecl.IsGenerated)
                {
                    // get the local field
                    JSLocalField localField = varDecl.Field as JSLocalField;
                    // if it isn't referenced...
                    if (localField != null && !localField.IsReferenced)
                    {
                        // remove it from the var statement
                        m_list.RemoveAt(ndx);
                        // and remove the field from the scope
                        if (scope != null)
                        {
                            scope.Remove(localField);
                        }
                    }
                }
            }
            // remove the entire statement altogether if there's nothing left
            if (m_list.Count == 0)
            {
                Parent.ReplaceChild(this, null);
            }
        }
        */
    }
}
