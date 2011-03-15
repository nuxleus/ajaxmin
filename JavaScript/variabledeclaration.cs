// variabledeclaration.cs
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
using System.Reflection;
using System.Text;

namespace Microsoft.Ajax.Utilities
{
    public sealed class VariableDeclaration : AstNode
    {
        private string m_identifier;
        public string Identifier { get { return m_identifier; } }

        private AstNode m_initializer;
        public AstNode Initializer { get { return m_initializer; } }

        private JSVariableField m_field;
        public JSVariableField Field { get { return m_field; } }

        private bool m_ccSpecialCase;
        internal bool IsCCSpecialCase
        {
            set { m_ccSpecialCase = value; }
        }

        public bool UseCCOn { get; set; }

        private bool m_isGenerated;
        internal bool IsGenerated
        {
            get { return m_isGenerated; }
            set
            {
                m_isGenerated = value;
                JSLocalField localField = m_field as JSLocalField;
                if (localField != null)
                {
                    localField.IsGenerated = m_isGenerated;
                }
            }
        }

        public VariableDeclaration(Context context, JSParser parser, string identifier, Context idContext, AstNode initializer, FieldAttributes fieldAttributes)
            : this(context, parser, identifier, idContext, initializer, fieldAttributes, false)
        {
        }

        public VariableDeclaration(Context context, JSParser parser, string identifier, Context idContext, AstNode initializer, FieldAttributes fieldAttributes, bool ignoreDuplicates)
            : base(context, parser)
        {
            // identifier cannot be null
            m_identifier = identifier;

            // initializer may be null
            m_initializer = initializer;
            if (m_initializer != null) { m_initializer.Parent = this; }

            // we'll need to do special stuff if the initializer if a function expression,
            // so try the conversion now
            FunctionObject functionValue = m_initializer as FunctionObject;
            string name = m_identifier.ToString();

            ActivationObject currentScope = ScopeStack.Peek();
            ActivationObject definingScope = currentScope;
            if (definingScope is BlockScope)
            {
                // block scope -- the variable is ACTUALLY defined in the containing function/global scope,
                // so we need to check THERE for duplicate defines.
                do
                {
                    definingScope = definingScope.Parent;
                } while (definingScope is BlockScope);
            }

            JSVariableField field = definingScope[name];
            if (field != null
                && (functionValue == null || functionValue != field.FieldValue))
            {
                // this is a declaration that already has a field declared. 
                // if the field is a named function expression, we want to fire an
                // ambiguous named function expression error -- and we know it's an NFE
                // if the FieldValue is a function object OR if the field
                // has already been marked ambiguous
                if (field.IsAmbiguous || field.FieldValue is FunctionObject)
                {
                    if (idContext != null)
                    {
                        idContext.HandleError(
                            JSError.AmbiguousNamedFunctionExpression,
                            true
                            );
                    }
                    else if (context != null)
                    {
                        // not identifier context???? Try the whole statment context.
                        // if neither context is set, then we don't get an error!
                        context.HandleError(
                            JSError.AmbiguousNamedFunctionExpression,
                            true
                            );
                    }

                    // if we are preserving function names, then we need to mark this field
                    // as not crunchable
                    if (Parser.Settings.PreserveFunctionNames)
                    {
                        field.CanCrunch = false;
                    }
                }
                else if (!ignoreDuplicates)
                {
                    if (idContext != null)
                    {
                        // otherwise just a normal duplicate error
                        idContext.HandleError(
                          JSError.DuplicateName,
                          field.IsLiteral
                          );
                    }
                    else if (context != null)
                    {
                        // otherwise just a normal duplicate error
                        context.HandleError(
                          JSError.DuplicateName,
                          field.IsLiteral
                          );
                    }
                }
            }

            bool isLiteral = ((fieldAttributes & FieldAttributes.Literal) != 0);

            // normally the value will be null.
            // but if there is no initializer, we'll use Missing so we can tell the difference.
            // and if this is a literal, we'll set it to the actual literal astnode
            object val = null;
            if (m_initializer == null)
            {
                val = Missing.Value;
            }
            else if (isLiteral || (functionValue != null))
            {
                val = m_initializer;
            }

            m_field = currentScope.DeclareField(
              m_identifier,
              val,
              fieldAttributes
              );
            m_field.OriginalContext = idContext;

            // we are now declared by a var statement
            m_field.IsDeclared = true;

            // if we are declaring a variable inside a with statement, then we will be declaring
            // a local variable in the enclosing scope if the with object doesn't have a property
            // of that name. But if it does, we won't actually be creating a variable field -- we'll
            // just use the property. So if we use an initializer in this declaration, then we will
            // actually be referencing the value.
            // SO, if this is a with-scope and this variable declaration has an initializer, we're going
            // to go ahead and bump up the reference.
            if (currentScope is WithScope && m_initializer != null)
            {
                m_field.AddReference(currentScope);
            }

            // special case the ambiguous function expression test. If we are var-ing a variable
            // with the same name as the function expression, then it's okay. We won't have an ambiguous
            // reference and it will be okay to use the name to reference the function expression
            if (functionValue != null && string.CompareOrdinal(m_identifier, functionValue.Name) == 0)
            {
                // null out the link to the named function expression
                // and make the function object point to the PROPER variable: the local within its own scope
                // and the inner is not pointing to the outer.
                functionValue.DetachFromOuterField(false);
                m_field.IsFunction = false;
            }
        }

        public override AstNode Clone()
        {
            VariableDeclaration varDecl = new VariableDeclaration(
                (Context == null ? null : Context.Clone()),
                Parser,
                m_identifier,
                m_field.OriginalContext,
                (m_initializer == null ? null : m_initializer.Clone()),
                m_field.Attributes
                );
            varDecl.m_isGenerated = m_isGenerated;
            return varDecl;
        }

        internal override string GetFunctionGuess(AstNode target)
        {
            return m_identifier;
        }

        public override IEnumerable<AstNode> Children
        {
            get
            {
                if (m_initializer != null)
                {
                    yield return m_initializer;
                }
            }
        }

        public override bool ReplaceChild(AstNode oldNode, AstNode newNode)
        {
            if (m_initializer == oldNode)
            {
                m_initializer = newNode;
                if (newNode != null) { newNode.Parent = this; }
                return true;
            }
            return false;
        }

        public override string ToCode(ToCodeFormat format)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(m_field.ToString());
            if (m_initializer != null)
            {
                if (m_ccSpecialCase)
                {
                    sb.Append(UseCCOn ? "/*@cc_on=" : "/*@=");
                }
                else
                {

                    if (Parser.Settings.OutputMode == OutputMode.MultipleLines && Parser.Settings.IndentSize > 0)
                    {
                        sb.Append(" = ");
                    }
                    else
                    {
                        sb.Append('=');
                    }
                }

                bool useParen = false;
                // a comma operator is the only thing with a lesser precedence than an assignment
                BinaryOperator binOp = m_initializer as BinaryOperator;
                if (binOp != null && binOp.OperatorToken == JSToken.Comma)
                {
                    useParen = true;
                }
                if (useParen)
                {
                    sb.Append('(');
                }
                sb.Append(m_initializer.ToCode(m_ccSpecialCase ? ToCodeFormat.Preprocessor : ToCodeFormat.Normal));
                if (useParen)
                {
                    sb.Append(')');
                }

                if (m_ccSpecialCase)
                {
                    sb.Append("@*/");
                }
            }
            return sb.ToString();
        }

        internal override void AnalyzeNode()
        {
            base.AnalyzeNode();

            if (m_ccSpecialCase && Parser.Settings.IsModificationAllowed(TreeModifications.RemoveUnnecessaryCCOnStatements))
            {
                UseCCOn = !Parser.EncounteredCCOn;
                Parser.EncounteredCCOn = true;
            }
        }
    }
}
