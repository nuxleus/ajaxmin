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

    public sealed class Member : AstNode
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

        public override AstNode Clone()
        {
            return new Member(
                (Context == null ? null : Context.Clone()),
                Parser,
                (Root == null ? null : Root.Clone()),
                Name
                );
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

        internal override void AnalyzeNode()
        {
            // if we don't even have any resource strings, then there's nothing
            // we need to do and we can just perform the base operation
            ResourceStrings resourceStrings = Parser.ResourceStrings;
            if (resourceStrings != null && resourceStrings.Count > 0)
            {
                // see if the root object is a lookup that corresponds to the 
                // global value (not a local field) for our resource object
                // (same name)
                Lookup rootLookup = Root as Lookup;
                if (rootLookup != null
                    && rootLookup.LocalField == null
                    && string.CompareOrdinal(rootLookup.Name, resourceStrings.Name) == 0)
                {
                    // it is -- we're going to replace this with a string value.
                    // if this member name is a string on the object, we'll replacve it with
                    // the literal. Otherwise we'll replace it with an empty string.
                    // see if the string resource contains this value
                    ConstantWrapper stringLiteral = new ConstantWrapper(
                        resourceStrings[Name],
                        PrimitiveType.String,
                        Context,
                        Parser
                        );

                    Parent.ReplaceChild(this, stringLiteral);
                    // analyze the literal
                    stringLiteral.AnalyzeNode();
                    return;
                }
            }

            // if we are replacing property names and we have something to replace
            if (Parser.Settings.HasRenamePairs && Parser.Settings.ManualRenamesProperties
                && Parser.Settings.IsModificationAllowed(TreeModifications.PropertyRenaming))
            {
                // see if this name is a target for replacement
                string newName = Parser.Settings.GetNewName(Name);
                if (!string.IsNullOrEmpty(newName))
                {
                    // it is -- set the name to the new name
                    Name = newName;
                }
            }

            // recurse
            base.AnalyzeNode();

            /* REVIEW: might be too late in the parsing -- references to the variable may have
             * already been analyzed and found undefined
            // see if we are assigning to a member on the global window object
            BinaryOperator binaryOp = Parent as BinaryOperator;
            if (binaryOp != null && binaryOp.IsAssign && m_rootObject.IsWindowLookup)
            {
                // make sure the name of this property is a valid global variable, since we
                // are now assigning to it.
                if (Parser.GlobalScope[Name] == null)
                {
                    // it's not -- we need to add it to our list of known globals
                    // by defining a global field for it
                    Parser.GlobalScope.DeclareField(Name, null, 0);
                }
            }
            */
        }

        public override IEnumerable<AstNode> Children
        {
            get
            {
                if (Root != null)
                {
                    yield return Root;
                }
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
