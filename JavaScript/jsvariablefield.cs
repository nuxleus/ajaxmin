// jsvariablefield.cs
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
using System.Reflection;

namespace Microsoft.Ajax.Utilities
{
    public class JSVariableField
    {
        private String m_name;
        private Object m_value;
        private FieldAttributes m_attributeFlags;
        private JSNamedFunctionExpressionField m_namedFuncExpr;
        private bool m_isAmbiguous; //= false;
        private bool m_isDeclared; //= false;
        private bool m_isPlaceholder; //= false;
        private bool m_canCrunch;// = false;

        private int m_refCount;// = 0;
        public int RefCount { get { return m_refCount; } }

        private Context m_originalContext; // never update this context object. It is shared
        internal Context OriginalContext
        {
            get { return m_originalContext; }
            set { m_originalContext = value; }
        }

        private JSVariableField m_outerField;
        public JSVariableField OuterField 
        { 
            get { return m_outerField; }
            set { m_outerField = value; }
        }

        public Object FieldValue
        {
            get { return m_value; }
            set { m_value = value; }
        }

        public bool CanCrunch
        {
            get { return m_canCrunch; }
            set 
            { 
                m_canCrunch = value;
                if (m_outerField != null)
                {
                    m_outerField.CanCrunch = value;
                }
            }
        }

        public bool IsDeclared
        {
            get { return m_isDeclared; }
            set 
            { 
                m_isDeclared = value;
                if (m_outerField != null)
                {
                    m_outerField.IsDeclared = value;
                }
            }
        }

        public JSNamedFunctionExpressionField NamedFunctionExpression
        {
            get { return m_namedFuncExpr; }
            set { m_namedFuncExpr = value; }
        }

        public bool IsAmbiguous
        {
            get { return m_isAmbiguous; }
            set { m_isAmbiguous = value; }
        }

        public bool IsPlaceholder
        {
            get { return m_isPlaceholder; }
            set { m_isPlaceholder = value; }
        }

        public bool IsFunction
        {
            get; internal set;
        }

        public JSVariableField(string name, FieldAttributes fieldAttributes, object value)
        {
            m_name = name;
            m_attributeFlags = fieldAttributes;
            m_value = value;
        }

        public JSVariableField(JSVariableField outerField)
            : this(outerField.Name, outerField.Attributes, outerField.FieldValue)
        {
            m_outerField = outerField;
        }

        public virtual void AddReference(ActivationObject scope)
        {
            // if we have an outer field, add the reference to it
            if (m_outerField != null)
            {
                m_outerField.AddReference(scope);
            }

            ++m_refCount;
            if (m_value is FunctionObject)
            {
                // add the reference to the scope
                ((FunctionObject)FieldValue).FunctionScope.AddReference(scope);
            }

            // no longer a placeholder if we are referenced
            if (m_isPlaceholder)
            {
                m_isPlaceholder = false;
            }
        }

        // we'll set this to true if the variable is referenced in a lookup
        public bool IsReferenced
        {
            get
            {
                // if the refcount is zero, we know we're not referenced.
                // if the count is greater than zero and we're a function definition,
                // then we need to do a little more work
                FunctionObject funcObj = FieldValue as FunctionObject;
                if (funcObj != null)
                {
                    // ask the function object if it's referenced. 
                    // Pass the field refcount because it would be useful for func declarations
                    return funcObj.IsReferenced(m_refCount);
                }
                return m_refCount > 0;
            }
        }

        public override string ToString()
        {
            // just output the name
            return m_name;
        }

        public virtual String Name
        {
            get
            {
                return m_name;
            }
        }

        public FieldAttributes Attributes
        {
            get
            {
                return m_attributeFlags;
            }
            set
            {
                m_attributeFlags = value;
            }
        }

        public bool IsLiteral
        {
            get
            {
                return ((m_attributeFlags & FieldAttributes.Literal) != 0);
            }
        }
    }
}