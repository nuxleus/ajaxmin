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

        internal override void AnalyzeNode()
        {
            // first we want to weed out duplicates that don't have initializers
            // var a=1, a=2 is okay, but var a, a=2 and var a=2, a should both be just var a=2, 
            // and var a, a should just be var a
            if (Parser.Settings.IsModificationAllowed(TreeModifications.RemoveDuplicateVar))
            {
                int ndx = 0;
                while (ndx < m_list.Count)
                {
                    string thisName = m_list[ndx].Identifier;

                    // handle differently if we have an initializer or not
                    if (m_list[ndx].Initializer != null)
                    {
                        // the current vardecl has an initializer, so we want to delete any other
                        // vardecls of the same name in the rest of the list with no initializer
                        // and move on to the next item afterwards
                        DeleteNoInits(++ndx, thisName);
                    }
                    else
                    {
                        // this vardecl has no initializer, so we can delete it if there is ANY
                        // other vardecl with the same name (whether or not it has an initializer)
                        if (VarDeclExists(ndx + 1, thisName))
                        {
                            m_list.RemoveAt(ndx);

                            // don't increment the index; we just deleted the current item,
                            // so the next item just slid into this position
                        }
                        else
                        {
                            // nope -- it's the only one. Move on to the next
                            ++ndx;
                        }
                    }
                }
            }

            // recurse the analyze
            base.AnalyzeNode();
        }

        private bool VarDeclExists(int ndx, string name)
        {
            // only need to look forward from the index passed
            for (; ndx < m_list.Count; ++ndx)
            {
                // string must be exact match
                if (string.CompareOrdinal(m_list[ndx].Identifier, name) == 0)
                {
                    // there is at least one -- we can bail
                    return true;
                }
            }
            // if we got here, we didn't find any matches
            return false;
        }

        private void DeleteNoInits(int min, string name)
        {
            // walk backwards from the end of the list down to (and including) the minimum index
            for (int ndx = m_list.Count - 1; ndx >= min; --ndx)
            {
                // if the name matches and there is no initializer...
                if (string.CompareOrdinal(m_list[ndx].Identifier, name) == 0
                    && m_list[ndx].Initializer == null)
                {
                    // ...remove it from the list
                    m_list.RemoveAt(ndx);
                }
            }
        }

        public override AstNode Clone()
        {
            // creates a new EMPTY statement
            Var newVar = new Var((Context == null ? null : Context.Clone()), Parser);
            // now go through and clone all the actual declarations
            for (int ndx = 0; ndx < m_list.Count; ++ndx)
            {
                if (m_list[ndx] != null)
                {
                    // cloning the declaration will add a field to the current scope
                    // (better already be in the proper scope chain)
                    newVar.Append(m_list[ndx].Clone());
                }
            }
            return newVar;
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
                decl.Parent = this;
                m_list.Add(decl);
            }
            else
            {
                Var otherVar = elem as Var;
                if (otherVar != null)
                {
                    for (int ndx = 0; ndx < otherVar.m_list.Count; ++ndx)
                    {
                        Append((AstNode)otherVar.m_list[ndx]);
                    }
                }
            }
        }

        internal void InsertAt(int index, AstNode elem)
        {
            VariableDeclaration decl = elem as VariableDeclaration;
            if (decl != null)
            {
                decl.Parent = this;
                m_list.Insert(index, decl);
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

        public bool Contains(string name)
        {
            // look at each vardecl in our list
            for (int ndx = 0; ndx < m_list.Count; ++ndx)
            {
                // if it matches the target name exactly...
                if (string.CompareOrdinal(m_list[ndx].Identifier, name) == 0)
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
    }
}
