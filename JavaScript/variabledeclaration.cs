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
        public string Identifier { get; private set; }
        public AstNode Initializer { get; private set; }
        public JSVariableField Field { get; private set; }
        public bool IsCCSpecialCase { get; set; }
        public bool UseCCOn { get; set; }

        private bool m_isGenerated;
        public bool IsGenerated
        {
            get { return m_isGenerated; }
            set
            {
                m_isGenerated = value;
                JSLocalField localField = Field as JSLocalField;
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
            Identifier = identifier;

            // initializer may be null
            Initializer = initializer;
            if (Initializer != null) { Initializer.Parent = this; }

            // we'll need to do special stuff if the initializer if a function expression,
            // so try the conversion now
            FunctionObject functionValue = Initializer as FunctionObject;
            string name = Identifier.ToString();

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
            if (Initializer == null)
            {
                val = Missing.Value;
            }
            else if (isLiteral || (functionValue != null))
            {
                val = Initializer;
            }

            Field = currentScope.DeclareField(
              Identifier,
              val,
              fieldAttributes
              );
            Field.OriginalContext = idContext;

            // we are now declared by a var statement
            Field.IsDeclared = true;

            // if we are declaring a variable inside a with statement, then we will be declaring
            // a local variable in the enclosing scope if the with object doesn't have a property
            // of that name. But if it does, we won't actually be creating a variable field -- we'll
            // just use the property. So if we use an initializer in this declaration, then we will
            // actually be referencing the value.
            // SO, if this is a with-scope and this variable declaration has an initializer, we're going
            // to go ahead and bump up the reference.
            if (currentScope is WithScope && Initializer != null)
            {
                Field.AddReference(currentScope);
            }

            // special case the ambiguous function expression test. If we are var-ing a variable
            // with the same name as the function expression, then it's okay. We won't have an ambiguous
            // reference and it will be okay to use the name to reference the function expression
            if (functionValue != null && string.CompareOrdinal(Identifier, functionValue.Name) == 0)
            {
                // null out the link to the named function expression
                // and make the function object point to the PROPER variable: the local within its own scope
                // and the inner is not pointing to the outer.
                functionValue.DetachFromOuterField(false);
                Field.IsFunction = false;
            }
        }

        public override void Accept(IVisitor visitor)
        {
            if (visitor != null)
            {
                visitor.Visit(this);
            }
        }

        public override bool IsExpression
        {
            get
            {
                // sure. treat a vardecl like an expression. normally this wouldn't be anywhere but
                // in a var statement, but sometimes the special-cc case might be moved into an expression
                // statement
                return true;
            }
        }

        internal override string GetFunctionGuess(AstNode target)
        {
            return Identifier;
        }

        public override IEnumerable<AstNode> Children
        {
            get
            {
                return EnumerateNonNullNodes(Initializer);
            }
        }

        public override bool ReplaceChild(AstNode oldNode, AstNode newNode)
        {
            if (Initializer == oldNode)
            {
                Initializer = newNode;
                if (newNode != null) { newNode.Parent = this; }
                return true;
            }
            return false;
        }

        public override bool IsEquivalentTo(AstNode otherNode)
        {
            JSVariableField otherField = null;
            Lookup otherLookup;
            var otherVarDecl = otherNode as VariableDeclaration;
            if (otherVarDecl != null)
            {
                otherField = otherVarDecl.Field;
            }
            else if ((otherLookup = otherNode as Lookup) != null)
            {
                otherField = otherLookup.VariableField;
            }

            // if we get here, we're not equivalent
            return this.Field != null && this.Field.IsSameField(otherField);
        }

        public override string ToCode(ToCodeFormat format)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(Field.ToString());
            if (Initializer != null)
            {
                if (IsCCSpecialCase)
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
                BinaryOperator binOp = Initializer as BinaryOperator;
                if (binOp != null && binOp.OperatorToken == JSToken.Comma)
                {
                    useParen = true;
                }
                if (useParen)
                {
                    sb.Append('(');
                }
                sb.Append(Initializer.ToCode(IsCCSpecialCase ? ToCodeFormat.Preprocessor : ToCodeFormat.Normal));
                if (useParen)
                {
                    sb.Append(')');
                }

                if (IsCCSpecialCase)
                {
                    sb.Append("@*/");
                }
            }
            return sb.ToString();
        }
    }
}
