// call.cs
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
    public sealed class CallNode : AstNode
    {
        private AstNode m_func;
        public AstNode Function
        {
            get { return m_func; }
        }

        private bool m_isConstructor;
        public bool IsConstructor
        {
            get { return m_isConstructor; }
            set { m_isConstructor = value; }
        }

        private bool m_inBrackets;
        public bool InBrackets
        {
            get { return m_inBrackets; }
        }

        private AstNodeList m_args;
        public AstNodeList Arguments
        {
            get { return m_args; }
        }

        public CallNode(Context context, JSParser parser, AstNode function, AstNodeList args, bool inBrackets)
            : base(context, parser)
        {
            m_func = function;
            m_args = args;
            m_inBrackets = inBrackets;

            if (m_func != null)
            {
                m_func.Parent = this;
            }
            if (m_args != null)
            {
                m_args.Parent = this;
            }
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
                return EnumerateNonNullNodes(m_func, m_args);
            }
        }

        public override bool ReplaceChild(AstNode oldNode, AstNode newNode)
        {
            if (m_func == oldNode)
            {
                m_func = newNode;
                if (newNode != null) { newNode.Parent = this; }
                return true;
            }
            if (m_args == oldNode)
            {
                if (newNode == null)
                {
                    // remove it
                    m_args = null;
                    return true;
                }
                else
                {
                    // if the new node isn't an AstNodeList, ignore it
                    AstNodeList newList = newNode as AstNodeList;
                    if (newList != null)
                    {
                        m_args = newList;
                        newNode.Parent = this;
                        return true;
                    }
                }
            }
            return false;
        }

        public override AstNode LeftHandSide
        {
            get
            {
                // the function is on the left
                return m_func.LeftHandSide;
            }
        }

        internal override bool IsDebuggerStatement
        {
            get
            {
                // see if this is a member, lookup, or call node
                // if it is, then we will pop positive if the recursive call does
                return ((m_func is Member || m_func is CallNode || m_func is Lookup) && m_func.IsDebuggerStatement);
            }
        }

        public override string ToCode(ToCodeFormat format)
        {
            StringBuilder sb = new StringBuilder();

            // we will only add a space if we KNOW we might need one
            bool needsSpace = false;

            // normal processing...
            if (m_isConstructor)
            {
                sb.Append("new");
                // the new operator might need to be followed by a space, depending
                // on what will actually follow
                needsSpace = true;
            }

            // list of items that DON'T need parens.
            // lookup, call or member don't need parens. All other items
            // are lower-precedence and therefore need to be wrapped in
            // parentheses to keep the right order.
            // function objects take care of their own parentheses.
            CallNode funcCall = m_func as CallNode;
            bool encloseInParens = !(
                 (m_func is Lookup)
              || (m_func is Member)
              || (funcCall != null)
              || (m_func is ThisLiteral)
              || (m_func is FunctionObject)
              );

            // because if the new-operator associates to the right and the ()-operator associates
            // to the left, we need to be careful that we don't change the precedence order when the 
            // function of a new operator is itself a call. In that case, the call will have it's own
            // parameters (and therefore parentheses) that will need to be associated with the call
            // and NOT the new -- the call will need to be surrounded with parens to keep that association.
            if (m_isConstructor && funcCall != null && !funcCall.InBrackets)
            {
                encloseInParens = true;
            }


            // if the root is a constructor with no arguments, we'll need to wrap it in parens so the 
            // member-dot comes out with the right precedence.
            // (don't bother checking if we already are already going to use parens)
            if (!encloseInParens && funcCall != null && funcCall.IsConstructor
                && (funcCall.Arguments == null || funcCall.Arguments.Count == 0))
            {
                encloseInParens = true;
            }

            if (encloseInParens)
            {
                // we're adding a parenthesis, so no -- we won't need to
                // add a space
                needsSpace = false;
                sb.Append('(');
            }

            string functionString = m_func.ToCode();
            // if we still think we might need a space, check the function we just
            // formatted. If it starts with an identifier part, then we need the space.
            if (needsSpace && JSScanner.StartsWithIdentifierPart(functionString))
            {
                sb.Append(' ');
            }
            sb.Append(functionString);

            if (encloseInParens)
            {
                sb.Append(')');
            }
            // if this isn't a constructor, or if it is and there are parameters,
            // then we want to output the parameters. But if this is a constructor with
            // no parameters, we can skip the whole empty-argument-parens thing altogether.
            if (!m_isConstructor || (m_args != null && m_args.Count > 0))
            {
                sb.Append((m_inBrackets ? '[' : '('));
                if (m_args != null)
                {
                    sb.Append(m_args.ToCode(ToCodeFormat.Commas));
                }
                sb.Append((m_inBrackets ? ']' : ')'));
            }

            return sb.ToString();
        }

        internal override string GetFunctionGuess(AstNode target)
        {
            // get our guess from the function call
            string funcName = m_func.GetFunctionGuess(target);

            // MSN VOODOO: if this is the addMethod method, then the
            // name of the function is the first parameter. 
            // The syntax of the add method call is: obj.addMethod("name",function(){...})
            // so there should be two parameters....
            if (funcName == "addMethod" && m_args.Count == 2)
            {
                // the first one should be a string constant....
                ConstantWrapper firstParam = m_args[0] as ConstantWrapper;
                // and the second one should be the function expression we're looking for
                if ((firstParam != null) && (firstParam.Value is string) && (m_args[1] == target))
                {
                    // use that first parameter as the guess
                    funcName = firstParam.ToString();
                }
            }
            return funcName;
        }
    }
}