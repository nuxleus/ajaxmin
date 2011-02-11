// functionobject.cs
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
using System.Globalization;
using System.Reflection;
using System.Text;

namespace Microsoft.Ajax.Utilities
{
    public sealed class FunctionObject : AstNode
    {
        public Block Body { get; private set; }
        public FunctionType FunctionType { get; private set; }

        private ParameterDeclaration[] m_parameterDeclarations;

        private bool m_leftHandFunction;// = false;
        public bool LeftHandFunctionExpression
        {
            get
            {
                return (FunctionType == FunctionType.Expression && m_leftHandFunction);
            }
            set
            {
                m_leftHandFunction = value;
            }
        }

        // we'll want to roll over if for some reason we ever hit the max
        private static int s_uniqueNumber;// = 0;
        private int UniqueNumber
        {
            get
            {
                lock (this)
                {
                    if (s_uniqueNumber == int.MaxValue)
                    {
                        s_uniqueNumber = 0;
                    }
                    return s_uniqueNumber++;
                }
            }
        }

        private Lookup m_identifier;
        private string m_name;
        public string Name
        {
            get
            {
                return (m_identifier != null ? m_identifier.Name : m_name);
            }
            set
            {
                if (m_identifier != null)
                {
                    m_identifier.Name = value;
                }
                else
                {
                    m_name = value;
                }
            }
        }
        public Context IdContext { get { return (m_identifier == null ? null : m_identifier.Context); } }


        private JSVariableField m_variableField;
        public JSLocalField LocalField { get { return m_variableField as JSLocalField; } }
        public int RefCount { get { return (m_variableField == null ? 0 : m_variableField.RefCount); } }

        private FunctionScope m_functionScope;
        public FunctionScope FunctionScope { get { return m_functionScope; } }

        public override ActivationObject EnclosingScope
        {
            get
            {
                return m_functionScope;
            }
        }

        public FunctionObject(Lookup identifier, JSParser parser, FunctionType functionType, ParameterDeclaration[] parameterDeclarations, Block bodyBlock, Context functionContext, FunctionScope functionScope)
            : base(functionContext, parser)
        {
            FunctionType = functionType;
            m_functionScope = functionScope;
            if (functionScope != null)
            {
                functionScope.FunctionObject = this;
            }

            m_name = string.Empty;
            m_identifier = identifier;
            if (m_identifier != null) { m_identifier.Parent = this; }

            // make sure the parameter array is never null so we don't have to keep checking it
            m_parameterDeclarations = parameterDeclarations ?? new ParameterDeclaration[0];

            Body = bodyBlock;
            if (bodyBlock != null) { bodyBlock.Parent = this; }

            // now we need to make sure that the enclosing scope has the name of this function defined
            // so that any references get properly resolved once we start analyzing the parent scope
            // see if this is not anonymnous AND not a getter/setter
            bool isGetterSetter = (FunctionType == FunctionType.Getter || FunctionType == FunctionType.Setter);
            if (m_identifier != null && !isGetterSetter)
            {
                // yes -- add the function name to the current enclosing
                // check whether the function name is in use already
                // shouldn't be any duplicate names
                ActivationObject enclosingScope = m_functionScope.Parent;
                // functions aren't owned by block scopes
                while (enclosingScope is BlockScope)
                {
                    enclosingScope = enclosingScope.Parent;
                }

                // if the enclosing scope already contains this name, then we know we have a dup
                string functionName = m_identifier.Name;
                m_variableField = enclosingScope[functionName];
                if (m_variableField != null)
                {
                    // it's pointing to a function
                    m_variableField.IsFunction = true;

                    if (FunctionType == FunctionType.Expression)
                    {
                        // if the containing scope is itself a named function expression, then just
                        // continue on as if everything is fine. It will chain and be good.
                        if (!(m_variableField is JSNamedFunctionExpressionField))
                        {
                            if (m_variableField.NamedFunctionExpression != null)
                            {
                                // we have a second named function expression in the same scope
                                // with the same name. Not an error unless someone actually references
                                // it.

                                // we are now ambiguous.
                                m_variableField.IsAmbiguous = true;

                                // BUT because this field now points to multiple function object, we
                                // need to break the connection. We'll leave the inner NFEs pointing
                                // to this field as the outer field so the names all align, however.
                                DetachFromOuterField(true);

                                // create a new NFE pointing to the existing field as the outer so
                                // the names stay in sync, and with a value of our function object.
                                JSNamedFunctionExpressionField namedExpressionField = 
                                    new JSNamedFunctionExpressionField(m_variableField);
                                namedExpressionField.FieldValue = this;
                                m_functionScope.AddField(namedExpressionField);

                                // hook our function object up to the named field
                                m_variableField = namedExpressionField;
                                m_identifier.LocalField = namedExpressionField;

                                // we're done; quit.
                                return;
                            }
                            else if (m_variableField.IsAmbiguous)
                            {
                                // we're pointing to a field that is already marked as ambiguous.
                                // just create our own NFE pointing to this one, and hook us up.
                                JSNamedFunctionExpressionField namedExpressionField = 
                                    new JSNamedFunctionExpressionField(m_variableField);
                                namedExpressionField.FieldValue = this;
                                m_functionScope.AddField(namedExpressionField);

                                // hook our function object up to the named field
                                m_variableField = namedExpressionField;
                                m_identifier.LocalField = namedExpressionField;

                                // we're done; quit.
                                return;
                            }
                            else
                            {
                                // we are a named function expression in a scope that has already
                                // defined a local variable of the same name. Not good. Throw the 
                                // error but keep them attached because the names have to be synced
                                // to keep the same meaning in all browsers.
                                m_identifier.Context.HandleError(JSError.AmbiguousNamedFunctionExpression, true);

                                // if we are preserving function names, then we need to mark this field
                                // as not crunchable
                                if (Parser.Settings.PreserveFunctionNames)
                                {
                                    m_variableField.CanCrunch = false;
                                }
                            }
                        }
                        /*else
                        {
                            // it's okay; just chain the NFEs as normal and everything will work out
                            // and the names will be properly synced.
                        }*/
                    }
                    else
                    {
                        // function declaration -- duplicate name
                        m_identifier.Context.HandleError(JSError.DuplicateName, false);
                    }
                }
                else
                {
                    // doesn't exist -- create it now
                    m_variableField = enclosingScope.DeclareField(functionName, this, 0);

                    // and it's a pointing to a function object
                    m_variableField.IsFunction = true;
                }

                // the identifier only cares about the local field if it is a local field
                m_identifier.LocalField = m_variableField as JSLocalField;

                // if we're here, we have a name. if this is a function expression, then we have
                // a named function expression and we need to do a little more work to prepare for
                // the ambiguities of named function expressions in various browsers.
                if (FunctionType == FunctionType.Expression)
                {
                    // now add a field within the function scope that indicates that it's okay to reference
                    // this named function expression from WITHIN the function itself.
                    // the inner field points to the outer field since we're going to want to catch ambiguous
                    // references in the future
                    JSNamedFunctionExpressionField namedExpressionField = new JSNamedFunctionExpressionField(m_variableField);
                    m_functionScope.AddField(namedExpressionField);
                    m_variableField.NamedFunctionExpression = namedExpressionField;
                }
                else
                {
                    // function declarations are declared by definition
                    m_variableField.IsDeclared = true;
                }
            }
        }

        public ParameterDeclaration[] GetParameterDeclarations()
        {
            return m_parameterDeclarations;
        }

        public override AstNode Clone()
        {
            /*
            // create the function scope and push it on the stack
            FunctionScope newScope = new FunctionScope(ScopeStack.Peek(), (m_functionType != FunctionType.Declaration),
                                                       Parser);
            ScopeStack.Push(newScope);
            try
            {
                // create the parameter list
                ParameterDeclaration[] newParameterList = new ParameterDeclaration[m_parameterDeclarations.Length];
                for (int ndx = 0; ndx < m_parameterDeclarations.Length; ++ndx)
                {
                    if (m_parameterDeclarations[ndx] != null)
                    {
                        // creating the parameter declaration will add a field to the new function scope
                        // use the uncrunched name so we can carry-forward the original
                        newParameterList[ndx] = new ParameterDeclaration(
                            m_parameterDeclarations[ndx].Context.Clone(),
                            Parser,
                            m_parameterDeclarations[ndx].OriginalName
                            );
                    }
                }
                // create the function object
                return new FunctionObject(
                    (m_identifier == null ? null : (Lookup) m_identifier.Clone()),
                    Parser,
                    m_functionType,
                    newParameterList,
                    (m_bodyBlock == null ? null : (Block) m_bodyBlock.Clone()),
                    (Context == null ? null : Context.Clone()),
                    newScope
                    );
            }
            finally
            {
                ScopeStack.Pop();
            }
            */
            throw new NotImplementedException();
        }

        internal bool IsReferenced(int fieldRefCount)
        {
            // function expressions, getters, and setters are always referenced.
            // for function declarations, if the field refcount is zero, then we know we're not referenced.
            // otherwise, let's check with the scopes to see what's up.
            bool isReferenced = false;
            if (FunctionType != FunctionType.Declaration)
            {
                isReferenced = true;
            }
            else if (fieldRefCount > 0)
            {
                // we are going to visit each referenced scope and ask if any are
                // referenced. If any one is, then we are too. Since this will be a graph,
                // keep a hashtable of visited scopes so we don't get into endless loops.
                isReferenced = m_functionScope.IsReferenced(null);
            }
            return isReferenced;
        }

        internal override void AnalyzeNode()
        {
            // get the name of this function, calculate something if it's anonymous
            m_name = (m_identifier == null ? GuessAtName() : m_identifier.Name);

            // don't analyze the identifier or we'll add an extra reference to it.
            // and we don't need to analyze the parameters because they were fielded-up
            // back when the function object was created, too

            // push the stack and analyze the body
            ScopeStack.Push(m_functionScope);
            try
            {
                // recurse
                base.AnalyzeNode();
            }
            finally
            {
                ScopeStack.Pop();
            }
        }

        public override IEnumerable<AstNode> Children
        {
            get
            {
                if (Body != null)
                {
                    yield return Body;
                }
            }
        }

        public override bool ReplaceChild(AstNode oldNode, AstNode newNode)
        {
            if (Body == oldNode)
            {
                if (newNode == null)
                {
                    // just remove it
                    Body = null;
                    return true;
                }
                else
                {
                    // if the new node isn't a block, ignore it
                    Block newBlock = newNode as Block;
                    if (newBlock != null)
                    {
                        Body = newBlock;
                        newNode.Parent = this;
                        return true;
                    }
                }
            }
            return false;
        }

        private bool HideFromOutput
        {
            get
            {
                // don't remove function expressions or getter/setters;
                // don't remove global functions; and if the scope is
                // unknown, then we can't remove it either, because we don't know
                // what the unknown code might call.
                return (FunctionType == FunctionType.Declaration 
                    && LocalField != null 
                    && !LocalField.IsReferenced 
                    && m_functionScope.Parent.IsKnownAtCompileTime 
                    && Parser.Settings.RemoveUnneededCode);
            }
        }

        public override string ToCode(ToCodeFormat format)
        {
            StringBuilder sb = new StringBuilder();
            if (!Parser.Settings.MinifyCode || !HideFromOutput)
            {
                if (LeftHandFunctionExpression)
                {
                    sb.Append('(');
                }
                if (format != ToCodeFormat.NoFunction)
                {
                    sb.Append("function");
                    if (m_identifier != null)
                    {
                        // we don't want to show the name for named function expressions where the
                        // name is never referenced. Don't use IsReferenced because that will always
                        // return true for function expressions. Since we really only want to know if
                        // the field name is referenced, check the refcount directly on the field.
                        // also output the function expression name if this is debug mode
                        if (FunctionType != FunctionType.Expression
                            || !(Parser.Settings.RemoveFunctionExpressionNames && Parser.Settings.IsModificationAllowed(TreeModifications.RemoveFunctionExpressionNames))
                            || m_variableField.RefCount > 0)
                        {
                            sb.Append(' ');
                            sb.Append(m_identifier.ToCode());
                        }
                    }
                }

                sb.Append('(');
                if (m_parameterDeclarations.Length > 0)
                {
                    // figure out the last referenced argument so we can skip
                    // any that aren't actually referenced
                    int lastRef = m_parameterDeclarations.Length - 1;

                    // if we're not known at compile time, then we can't leave off unreferenced parameters
                    // (also don't leave things off if we're not hypercrunching)
                    // (also check the kill flag for removing unused parameters)
                    if (Parser.Settings.RemoveUnneededCode 
                        && m_functionScope.IsKnownAtCompileTime 
                        && Parser.Settings.MinifyCode
                        && Parser.Settings.IsModificationAllowed(TreeModifications.RemoveUnusedParameters))
                    {
                        while (lastRef >= 0)
                        {
                            // we want to loop backwards until we either find a parameter that is referenced.
                            // at that point, lastRef will be the index of the last referenced parameter so
                            // we can output from 0 to lastRef
                            JSArgumentField argumentField = m_parameterDeclarations[lastRef].Field;
                            if (argumentField != null && !argumentField.IsReferenced)
                            {
                                --lastRef;
                            }
                            else
                            {
                                // found a referenced parameter, or something weird -- stop looking
                                break;
                            }
                        }
                    }

                    for (int ndx = 0; ndx <= lastRef; ++ndx)
                    {
                        if (ndx > 0)
                        {
                            sb.Append(',');
                        }
                        sb.Append(m_parameterDeclarations[ndx].Name);
                    }
                }

                sb.Append(')');
                if (Body != null)
                {
                    if (Body.Count == 0)
                    {
                        sb.Append("{}");
                    }
                    else
                    {
                        sb.Append(Body.ToCode(ToCodeFormat.AlwaysBraces));
                    }
                }

                if (LeftHandFunctionExpression)
                {
                    sb.Append(')');
                }
            }
            return sb.ToString();
        }

        internal override bool RequiresSeparator
        {
            get { return false; }
        }

        private string GuessAtName()
        {
            AstNode parent = Parent;
            if (parent != null)
            {
                if (parent is AstNodeList)
                {
                    // if the parent is an ASTList, then we're really interested
                    // in our parent's parent (probably a call)
                    parent = parent.Parent;
                }
                CallNode call = parent as CallNode;
                if (call != null && call.IsConstructor)
                {
                    // if this function expression is the object of a new, then we want the parent
                    parent = parent.Parent;
                }

                string guess = parent.GetFunctionGuess(this);
                if (guess != null && guess.Length > 0)
                {
                    if (guess.StartsWith("\"", StringComparison.Ordinal)
                      && guess.EndsWith("\"", StringComparison.Ordinal))
                    {
                        // don't need to wrap it in quotes -- it already is
                        return guess;
                    }
                    // wrap the guessed name in quotes
                    return string.Format(CultureInfo.InvariantCulture, "\"{0}\"", guess);
                }
                else
                {
                    return string.Format(CultureInfo.InvariantCulture, "anonymous_{0}", UniqueNumber);
                }
            }
            return string.Empty;
        }

        internal void AddGeneratedVar(string name, AstNode initializer, bool isLiteral)
        {
            // if the body is empty, create one now
            if (Body == null)
            {
                Body = new Block(null, Parser);
            }

            // see if the first statement in the body (if any) is a var already
            Var var = null;
            if (Body.Count > 0)
            {
                var = Body[0] as Var;
            }

            VariableDeclaration varDecl = new VariableDeclaration(
                null,
                Parser,
                name,
                new Context(Parser),
                initializer,
                (isLiteral ? FieldAttributes.Literal : 0)
                );
            varDecl.IsGenerated = true;
            if (var != null)
            {
                // the first statement is a var; just add a new declaration to the front
                var.InsertAt(0, varDecl);
            }
            else
            {
                // not a var; create a new one
                var = new Var(null, Parser);
                var.Append(varDecl);
                Body.Insert(0, var);
            }
        }

        internal bool IsArgumentTrimmable(JSArgumentField targetArgumentField)
        {
            // walk backward until we either find the given argument field or the
            // first parameter that is referenced. 
            // If we find the argument field, then we can trim it because there are no
            // referenced parameters after it.
            // if we find a referenced argument, then the parameter is not trimmable.
            JSArgumentField argumentField = null;
            for (int index = m_parameterDeclarations.Length - 1; index >= 0; --index)
            {
                argumentField = m_parameterDeclarations[index].Field;
                if (argumentField != null
                    && (argumentField == targetArgumentField || argumentField.IsReferenced))
                {
                    break;
                }
            }
            // if the argument field we landed on is the same as the target argument field,
            // then we found the target argument BEFORE we found a referenced parameter. Therefore
            // the argument can be trimmed.
            return (argumentField == targetArgumentField);
        }

        public void DetachFromOuterField(bool leaveInnerPointingToOuter)
        {
            // we're going to change the reference from the outer variable to the inner variable
            // save the inner variable field
            JSNamedFunctionExpressionField nfeField = m_variableField.NamedFunctionExpression;
            // break the connection from the outer to the inner
            m_variableField.NamedFunctionExpression = null;

            if (!leaveInnerPointingToOuter)
            {
                // detach the inner from the outer
                nfeField.Detach();
            }

            // the outer field no longer points to the function object
            m_variableField.FieldValue = null;
            // but the inner field should
            nfeField.FieldValue = this;

            // our variable field is now the inner field
            m_variableField = nfeField;

            // and so is out identifier
            m_identifier.LocalField = nfeField;
        }
    }
}