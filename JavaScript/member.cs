// member.cs
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

    public sealed class Member : Expression
    {
        public AstNode Root { get; private set; }
        public string Name { get; set; }

        public Member(Context context, JSParser parser, AstNode rootObject, string memberName)
            : base(context, parser)
        {
            Name = memberName;

            Root = rootObject;
            if (Root != null) Root.Parent = this;
        }

        public override void Accept(IVisitor visitor)
        {
            if (visitor != null)
            {
                visitor.Visit(this);
            }
        }

        public override bool IsEquivalentTo(AstNode otherNode)
        {
            var otherMember = otherNode as Member;
            return otherMember != null
                && string.CompareOrdinal(this.Name, otherMember.Name) == 0
                && this.Root.IsEquivalentTo(otherMember.Root);
        }

        internal override string GetFunctionGuess(AstNode target)
        {
            // MSN VOODOO: treat the as and ns methods as special if the expression is the root,
            // the parent is the call, and there is one string parameter -- use the string parameter
            if (Root == target && (Name == "as" || Name == "ns"))
            {
                CallNode call = Parent as CallNode;
                if (call != null && call.Arguments.Count == 1)
                {
                    ConstantWrapper firstParam = call.Arguments[0] as ConstantWrapper;
                    if (firstParam != null)
                    {
                        return firstParam.ToString();
                    }
                }
            }
            return Name;
        }

        internal override bool IsDebuggerStatement
        {
            get
            {
                // depends on whether the root is
                return Root.IsDebuggerStatement;
            }
        }

        public override IEnumerable<AstNode> Children
        {
            get
            {
                return EnumerateNonNullNodes(Root);
            }
        }

        public override bool ReplaceChild(AstNode oldNode, AstNode newNode)
        {
            if (Root == oldNode)
            {
                Root = newNode;
                if (newNode != null) { newNode.Parent = this; }
                return true;
            }
            return false;
        }

        public override AstNode LeftHandSide
        {
            get
            {
                // the root object is on the left
                return Root.LeftHandSide;
            }
        }

        //code in parser relies on the member string (x.y.z...) being returned from here 
        public override string ToCode(ToCodeFormat format)
        {
            // pass P to the root object so it knows we might want parentheses
            string rootCrunched = Root.ToCode();

            // these tests are for items that DON'T need parens, and then we NOT the results.
            // non-numeric constant wrappers don't need parens (boolean, string, null).
            // numeric constant wrappers need parens IF there is no decimal point.
            // function expressions will take care of their own parens.
            bool needParen = !(
              (Root is Lookup)
              || (Root is Member)
              || (Root is CallNode)
              || (Root is ThisLiteral)
              || (Root is ArrayLiteral)
              || (Root is ObjectLiteral)
              || (Root is RegExpLiteral)
              || (Root is FunctionObject)
              || (Root is ConstantWrapper && !((ConstantWrapper)Root).IsNumericLiteral)
              || (Root is ConstantWrapper && ((ConstantWrapper)Root).IsNumericLiteral && rootCrunched.Contains("."))
              );

            // if the root is a constructor with no arguments, we'll need to wrap it in parens so the 
            // member-dot comes out with the right precedence.
            // (don't bother checking if we already are already going to use parens)
            if (!needParen)
            {
                CallNode callNode = Root as CallNode;
                if (callNode != null && callNode.IsConstructor && (callNode.Arguments == null || callNode.Arguments.Count == 0))
                {
                    needParen = true;
                }
            }

            StringBuilder sb = new StringBuilder();
            if (needParen)
            {
                sb.Append('(');
                sb.Append(rootCrunched);
                sb.Append(')');
            }
            else
            {
                sb.Append(rootCrunched);
            }
            sb.Append('.');
            sb.Append(Name);
            return sb.ToString();
        }
    }
}
