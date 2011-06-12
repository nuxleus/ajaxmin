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
        private string m_crunchedName;// = null;

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

        // we'll set this after analyzing all the variables in the
        // script in order to shrink it down even further
        public string CrunchedName
        {
            get
            {
                // return the outer field's crunched name if there is one,
                // otherwise return ours
                return (m_outerField != null
                    ? m_outerField.CrunchedName
                    : m_crunchedName);
            }
            set
            {
                // only set this if we CAN
                if (m_canCrunch)
                {
                    // if this is an outer reference, pass this on to the outer field
                    if (m_outerField != null)
                    {
                        m_outerField.CrunchedName = value;
                    }
                    else
                    {
                        m_crunchedName = value;
                    }
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

        internal JSVariableField(JSVariableField outerField)
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
            string crunch = CrunchedName;
            return string.IsNullOrEmpty(crunch) ? m_name : crunch;
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

        public override int GetHashCode()
        {
            return m_name.GetHashCode();
        }

        /// <summary>
        /// returns true if the fields point to the same ultimate reference object.
        /// Needs to walk up the outer-reference chain for each field in order to
        /// find the ultimate reference
        /// </summary>
        /// <param name="otherField"></param>
        /// <returns></returns>
        public bool IsSameField(JSVariableField otherField)
        {
            // shortcuts -- if they are already the same object, we're done;
            // and if the other field is null, then we are NOT the same object.
            if (this == otherField)
            {
                return true;
            }
            else if (otherField == null)
            {
                return false;
            }

            // get the ultimate field for this field
            var thisOuter = OuterField != null ? OuterField : this;
            while (thisOuter.OuterField != null)
            {
                thisOuter = thisOuter.OuterField;
            }

            // get the ultimate field for the other field
            var otherOuter = otherField.OuterField != null ? otherField.OuterField : otherField;
            while (otherOuter.OuterField != null)
            {
                otherOuter = otherOuter.OuterField;
            }

            // now that we have the same outer fields, check to see if they are the same
            return thisOuter == otherOuter;
        }
    }
}