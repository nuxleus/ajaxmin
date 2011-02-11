// ast.cs
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
    public enum ToCodeFormat
    {
        Normal,
        AlwaysBraces,
        Commas,
        NoBraces,
        NoFunction,
        Parentheses,
        Semicolons,
        NestedTry,
        Preprocessor,
        ElseIf
    }

    internal enum EncloseBlockType
    {
        IfWithoutElse,
        SingleDoWhile
    }

    public abstract class AstNode
    {
        public AstNode Parent { get; set; }
        public Context Context { get; set; }
        public JSParser Parser { get; private set; }

        protected AstNode(Context context, JSParser parser)
        {
            Parser = parser;
            if (context != null)
            {
                Context = context;
            }
            else
            {
                // generate a bogus context
                Context = new Context(parser);
            }
        }

        internal Stack<ActivationObject> ScopeStack { get { return Parser.ScopeStack; } }

        public abstract string ToCode(ToCodeFormat format);
        public virtual string ToCode() { return ToCode(ToCodeFormat.Normal); }

        /// <summary>
        /// Only call Clone when you know you are in the proper scope chain
        /// </summary>
        /// <returns></returns>
        public abstract AstNode Clone();

        internal virtual void AnalyzeNode()
        {
            // most objects just recurse to their children
            foreach (AstNode child in Children)
            {
                child.AnalyzeNode();
            }
        }

        public virtual void CleanupNodes()
        {
            // most objects just recurse to their children
            foreach (AstNode child in Children)
            {
                child.CleanupNodes();
            }
        }

        protected Block ForceToBlock(AstNode astNode)
        {
            // if the node is null or already a block, then we're 
            // good to go -- just return it.
            Block block = astNode as Block;
            if (astNode == null || block != null)
            {
                return block;
            }

            // it's not a block, so create a new block, append the astnode
            // and return the block
            block = new Block(null, Parser);
            block.Append(astNode);
            return block;
        }

        // call this function to return a logical-not version of itself
        internal virtual AstNode LogicalNot()
        {
            // by default we return null, meaning that the logical not of this
            // node isn't smaller or the same size as the current node
            return null;
        }

        internal virtual string GetFunctionGuess(AstNode target)
        {
            // most objects serived from AST return an empty string
            return string.Empty;
        }

        internal virtual bool EncloseBlock(EncloseBlockType type)
        {
            // almost all statements return false
            return false;
        }

        internal virtual bool RequiresSeparator
        {
            get { return true; }
        }

        internal virtual bool EndsWithEmptyBlock
        {
            get { return false; }
        }

        internal virtual bool IsDebuggerStatement
        {
            get { return false; }
        }

        public virtual IEnumerable<AstNode> Children
        {
            get
            {
                yield break;
            }
        }

        public bool IsWindowLookup
        {
            get
            {
                Lookup lookup = this as Lookup;
                return (lookup != null
                        && string.CompareOrdinal(lookup.Name, "window") == 0
                        && lookup.LocalField == null);
            }
        }

        public virtual bool ReplaceChild(AstNode oldNode, AstNode newNode)
        {
            return false;
        }

        public virtual AstNode LeftHandSide
        {
            get
            {
                // default is just to return ourselves
                return this;
            }
        }

        public virtual ActivationObject EnclosingScope
        {
            get
            {
                // if we don't have a parent, then we are in the global scope.
                // otherwise, just ask our parent. Nodes with scope will override this property.
                return Parent != null ? Parent.EnclosingScope : Parser.GlobalScope;
            }
        }
    }
}
