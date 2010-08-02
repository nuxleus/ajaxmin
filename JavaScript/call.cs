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

        public override AstNode Clone()
        {
            CallNode newCallNode = new CallNode(
                (Context == null ? null : Context.Clone()),
                Parser,
                (m_func == null ? null : m_func.Clone()),
                (m_args == null ? null : (AstNodeList) m_args.Clone()),
                m_inBrackets
                );
            newCallNode.m_isConstructor = m_isConstructor;
            return newCallNode;
        }

        internal override void AnalyzeNode()
        {
            // see if this is a member (we'll need it for a couple checks)
            Member member = m_func as Member;

            if (Parser.Settings.StripDebugStatements && Parser.Settings.IsModificationAllowed(TreeModifications.StripDebugStatements))
            {  
                // if this is a member, and it's a debugger object, and it's a constructor....
                if (member != null && member.IsDebuggerStatement && m_isConstructor)
                {
                    // we need to replace our debugger object with a generic Object
                    m_func = new Lookup("Object", m_func.Context, Parser);
                    // and make sure the node list is empty
                    if (m_args != null && m_args.Count > 0)
                    {
                        m_args = new AstNodeList(m_args.Context, Parser);
                    }
                }
            }

            // if this is a constructor and we want to collapse
            // some of them to literals...
            if (m_isConstructor && Parser.Settings.CollapseToLiteral)
            {
                // see if this is a lookup, and if so, if it's pointing to one
                // of the two constructors we want to collapse
                Lookup lookup = m_func as Lookup;
                if (lookup != null)
                {
                    if (lookup.Name == "Object" && (m_args == null || m_args.Count == 0)
                        && Parser.Settings.IsModificationAllowed(TreeModifications.NewObjectToObjectLiteral))
                    {
                        // replace our node with an object literal
                        ObjectLiteral objLiteral = new ObjectLiteral(Context, Parser, null, null);
                        if (Parent.ReplaceChild(this, objLiteral))
                        {
                            // and bail now. No need to recurse -- it's an empty literal
                            return;
                        }
                    }
                    else if (lookup.Name == "Array"
                        && Parser.Settings.IsModificationAllowed(TreeModifications.NewArrayToArrayLiteral))
                    {
                        // Array is trickier. 
                        // If there are no arguments, then just use [].
                        // if there are multiple arguments, then use [arg0,arg1...argN].
                        // but if there is one argument and it's numeric, we can't crunch it.
                        // also can't crunch if it's a function call or a member or something, since we won't
                        // KNOW whether or not it's numeric.
                        //
                        // so first see if it even is a single-argument constant wrapper. 
                        ConstantWrapper constWrapper = (m_args != null && m_args.Count == 1 ? m_args[0] as ConstantWrapper : null);

                        // if the argument count is not one, then we crunch.
                        // if the argument count IS one, we only crunch if we have a constant wrapper, 
                        // AND it's not numeric.
                        if (m_args == null
                          || m_args.Count != 1
                          || (constWrapper != null && !constWrapper.IsNumericLiteral))
                        {
                            // create the new array literal object
                            ArrayLiteral arrayLiteral = new ArrayLiteral(Context, Parser, m_args);
                            // replace ourself within our parent
                            if (Parent.ReplaceChild(this, arrayLiteral))
                            {
                                // recurse
                                arrayLiteral.AnalyzeNode();
                                // and bail -- we don't want to recurse this node any more
                                return;
                            }
                        }
                    }
                }
            }

            // if we are replacing resource references with strings generated from resource files
            // and this is a brackets call: lookup[args]
            ResourceStrings resourceStrings = Parser.ResourceStrings;
            if (m_inBrackets && resourceStrings != null && resourceStrings.Count > 0)
            {
                // see if the root object is a lookup that corresponds to the 
                // global value (not a local field) for our resource object
                // (same name)
                Lookup rootLookup = m_func as Lookup;
                if (rootLookup != null
                    && rootLookup.LocalField == null
                    && string.CompareOrdinal(rootLookup.Name, resourceStrings.Name) == 0)
                {
                    // we're going to replace this node with a string constant wrapper
                    // but first we need to make sure that this is a valid lookup.
                    // if the parameter contains anything that would vary at run-time, 
                    // then we need to throw an error.
                    // the parser will always have either one or zero nodes in the arguments
                    // arg list. We're not interested in zero args, so just make sure there is one
                    if (m_args.Count == 1)
                    {
                        // must be a constant wrapper
                        ConstantWrapper argConstant = m_args[0] as ConstantWrapper;
                        if (argConstant != null)
                        {
                            string resourceName = argConstant.Value.ToString();

                            // get the localized string from the resources object
                            ConstantWrapper resourceLiteral = new ConstantWrapper(
                                resourceStrings[resourceName],
                                false,
                                Context,
                                Parser);

                            // replace this node with localized string, analyze it, and bail
                            // so we don't anaylze the tree we just replaced
                            Parent.ReplaceChild(this, resourceLiteral);
                            resourceLiteral.AnalyzeNode();
                            return;
                        }
                        else
                        {
                            // error! must be a constant
                            Context.HandleError(
                                JSError.ResourceReferenceMustBeConstant,
                                true);
                        }
                    }
                    else
                    {
                        // error! can only be a single constant argument to the string resource object.
                        // the parser will only have zero or one arguments, so this must be zero
                        // (since the parser won't pass multiple args to a [] operator)
                        Context.HandleError(
                            JSError.ResourceReferenceMustBeConstant,
                            true);
                    }
                }
            }

            // and finally, if this is a backets call and the argument is a constantwrapper that can
            // be an identifier, just change us to a member node:  obj["prop"] to obj.prop
            if (m_inBrackets && m_args != null
                && Parser.Settings.IsModificationAllowed(TreeModifications.BracketMemberToDotMember))
            {
                string argText = m_args.SingleConstantArgument;
                if (argText != null
                    && JSScanner.IsValidIdentifier(argText)
                    && !JSScanner.IsKeyword(argText))
                {
                    Member replacementMember = new Member(Context, Parser, m_func, argText);
                    Parent.ReplaceChild(this, replacementMember);
                    replacementMember.AnalyzeNode();
                    return;
                }
            }

            // call the base class to recurse
            base.AnalyzeNode();

            // if this is a window.eval call, then we need to mark this scope as unknown just as
            // we would if this was a regular eval call.
            // (unless, of course, the parser settings say evals are safe)
            // call AFTER recursing so we know the left-hand side properties have had a chance to
            // lookup their fields to see if they are local or global
            if (Parser.Settings.EvalTreatment != EvalTreatment.Ignore)
            {
                if (member != null && string.CompareOrdinal(member.Name, "eval") == 0)
                {
                    if (member.LeftHandSide.IsWindowLookup)
                    {
                        // this is a call to window.eval()
                        // mark this scope as unknown so we don't crunch out locals 
                        // we might reference in the eval at runtime
                        ScopeStack.Peek().IsKnownAtCompileTime = false;
                    }
                }
                else
                {
                    CallNode callNode = m_func as CallNode;
                    if (callNode != null 
                        && callNode.InBrackets
                        && callNode.LeftHandSide.IsWindowLookup
                        && callNode.Arguments.IsSingleConstantArgument("eval"))
                    {
                        // this is a call to window["eval"]
                        // mark this scope as unknown so we don't crunch out locals 
                        // we might reference in the eval at runtime
                        ScopeStack.Peek().IsKnownAtCompileTime = false;
                    }
                }
            }

            /* REVIEW: may be too late. lookups may alread have been analyzed and
             * found undefined
            // check to see if this is an assignment to a window["prop"] structure
            BinaryOperator binaryOp = Parent as BinaryOperator;
            if (binaryOp != null && binaryOp.IsAssign
                && m_inBrackets
                && m_func.IsWindowLookup
                && m_args != null)
            {
                // and IF the property is a non-empty constant that isn't currently
                // a global field...
                string propertyName = m_args.SingleConstantArgument;
                if (!string.IsNullOrEmpty(propertyName)
                    && Parser.GlobalScope[propertyName] == null)
                {
                    // we want to also add it to the global fields so it's not undefined
                    Parser.GlobalScope.DeclareField(propertyName, null, 0);
                }
            }
            */
        }

        public override IEnumerable<AstNode> Children
        {
            get
            {
                if (m_func != null)
                {
                    yield return m_func;
                }
                if (m_args != null)
                {
                    yield return m_args;
                }
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