// TreeVisitor.cs
//
// Copyright 2011 Microsoft Corporation
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
    public class TreeVisitor : IVisitor
    {
        public TreeVisitor() { }

        private void AcceptChildren(AstNode node)
        {
            foreach (var childNode in node.Children)
            {
                childNode.Accept(this);
            }
        }

        #region IVisitor Members

        public virtual void Visit(ArrayLiteral node)
        {
            if (node != null)
            {
                AcceptChildren(node);
            }
        }

        public virtual void Visit(AspNetBlockNode node)
        {
            // no children
        }

        public virtual void Visit(AstNodeList node)
        {
            if (node != null)
            {
                 AcceptChildren(node);
            }
        }

        public virtual void Visit(BinaryOperator node)
        {
            if (node != null)
            {
                 AcceptChildren(node);
            }
        }

        public virtual void Visit(Block node)
        {
            if (node != null)
            {
                 AcceptChildren(node);
            }
        }

        public virtual void Visit(Break node)
        {
            if (node != null)
            {
                 AcceptChildren(node);
            }
        }

        public virtual void Visit(CallNode node)
        {
            if (node != null)
            {
                 AcceptChildren(node);
            }
        }

        public virtual void Visit(ConditionalCompilationComment node)
        {
            if (node != null)
            {
                 AcceptChildren(node);
            }
        }

        public virtual void Visit(ConditionalCompilationElse node)
        {
            // no children
        }

        public virtual void Visit(ConditionalCompilationElseIf node)
        {
            if (node != null)
            {
                 AcceptChildren(node);
            }
        }

        public virtual void Visit(ConditionalCompilationEnd node)
        {
            // no children
        }

        public virtual void Visit(ConditionalCompilationIf node)
        {
            if (node != null)
            {
                 AcceptChildren(node);
            }
        }

        public virtual void Visit(ConditionalCompilationOn node)
        {
            // no children
        }

        public virtual void Visit(ConditionalCompilationSet node)
        {
            if (node != null)
            {
                 AcceptChildren(node);
            }
        }

        public virtual void Visit(Conditional node)
        {
            if (node != null)
            {
                 AcceptChildren(node);
            }
        }

        public virtual void Visit(ConstantWrapper node)
        {
            // no children
        }

        public virtual void Visit(ConstantWrapperPP node)
        {
            // no children
        }

        public virtual void Visit(ContinueNode node)
        {
            if (node != null)
            {
                 AcceptChildren(node);
            }
        }

        public virtual void Visit(DebuggerNode node)
        {
            // no children
        }

        public virtual void Visit(Delete node)
        {
            if (node != null)
            {
                 AcceptChildren(node);
            }
        }

        public virtual void Visit(DoWhile node)
        {
            if (node != null)
            {
                 AcceptChildren(node);
            }
        }

        public virtual void Visit(EvaluateNode node)
        {
            if (node != null)
            {
                 AcceptChildren(node);
            }
        }

        public virtual void Visit(ForIn node)
        {
            if (node != null)
            {
                 AcceptChildren(node);
            }
        }

        public virtual void Visit(ForNode node)
        {
            if (node != null)
            {
                 AcceptChildren(node);
            }
        }

        public virtual void Visit(FunctionObject node)
        {
            if (node != null)
            {
                 AcceptChildren(node);
            }
        }

        public virtual void Visit(GetterSetter node)
        {
            if (node != null)
            {
                 AcceptChildren(node);
            }
        }

        public virtual void Visit(IfNode node)
        {
            if (node != null)
            {
                 AcceptChildren(node);
            }
        }

        public virtual void Visit(ImportantComment node)
        {
            // no children
        }

        public virtual void Visit(LabeledStatement node)
        {
            if (node != null)
            {
                 AcceptChildren(node);
            }
        }

        public virtual void Visit(Lookup node)
        {
            // no children
        }

        public virtual void Visit(Member node)
        {
            if (node != null)
            {
                 AcceptChildren(node);
            }
        }

        public virtual void Visit(NumericUnary node)
        {
            if (node != null)
            {
                 AcceptChildren(node);
            }
        }

        public virtual void Visit(ObjectLiteral node)
        {
            if (node != null)
            {
                 AcceptChildren(node);
            }
        }

        public virtual void Visit(ObjectLiteralField node)
        {
            // no children
        }

        public virtual void Visit(PostOrPrefixOperator node)
        {
            if (node != null)
            {
                 AcceptChildren(node);
            }
        }

        public virtual void Visit(RegExpLiteral node)
        {
            // no children
        }

        public virtual void Visit(ReturnNode node)
        {
            if (node != null)
            {
                 AcceptChildren(node);
            }
        }

        public virtual void Visit(Switch node)
        {
            if (node != null)
            {
                 AcceptChildren(node);
            }
        }

        public virtual void Visit(SwitchCase node)
        {
            if (node != null)
            {
                 AcceptChildren(node);
            }
        }

        public virtual void Visit(ThisLiteral node)
        {
            // no children
        }

        public virtual void Visit(ThrowNode node)
        {
            if (node != null)
            {
                 AcceptChildren(node);
            }
        }

        public virtual void Visit(TryNode node)
        {
            if (node != null)
            {
                 AcceptChildren(node);
            }
        }

        public virtual void Visit(TypeOfNode node)
        {
            if (node != null)
            {
                 AcceptChildren(node);
            }
        }

        public virtual void Visit(Var node)
        {
            if (node != null)
            {
                 AcceptChildren(node);
            }
        }

        public virtual void Visit(VariableDeclaration node)
        {
            if (node != null)
            {
                 AcceptChildren(node);
            }
        }

        public virtual void Visit(VoidNode node)
        {
            if (node != null)
            {
                 AcceptChildren(node);
            }
        }

        public virtual void Visit(WhileNode node)
        {
            if (node != null)
            {
                 AcceptChildren(node);
            }
        }

        public virtual void Visit(WithNode node)
        {
            if (node != null)
            {
                 AcceptChildren(node);
            }
        }

        #endregion
    }
}
