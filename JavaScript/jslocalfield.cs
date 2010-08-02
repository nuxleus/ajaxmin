// jslocalfield.cs
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

    public class JSLocalField : JSVariableField
    {
        private bool m_isDefined; //Indicates whether an assignment to the field has been encountered.

        private string m_crunchedName;// = null;

        private bool m_isGenerated;
        internal bool IsGenerated
        {
            get { return m_isGenerated; }
            set { m_isGenerated = value; }
        }

        internal JSLocalField(String name, Object value, FieldAttributes attributes)
            : base(name, attributes, value)
        {
            FieldValue = value;
            CanCrunch = true;
        }

        internal JSLocalField(JSVariableField outerField)
            : base(outerField)
        {
            JSLocalField outerLocalField = outerField as JSLocalField;
            if (outerLocalField != null)
            {
                // copy some properties
                m_isDefined = outerLocalField.m_isDefined;
                m_isGenerated = outerLocalField.m_isGenerated;
            }
        }

        // we'll set this after analyzing all the variables in the
        // script in order to shrink it down even further
        public string CrunchedName
        {
            get
            {
                // use the outer crunched name only if it is a local field
                JSLocalField outerLocalField = OuterField as JSLocalField;
                return (
                  outerLocalField != null
                  ? outerLocalField.CrunchedName
                  : m_crunchedName
                  );
            }
            set
            {
                if (OuterField != null)
                {
                    JSLocalField outerLocalField = OuterField as JSLocalField;
                    if (outerLocalField != null)
                    {
                        outerLocalField.CrunchedName = value;
                    }
                    // TODO: do we need to raise an error if we try to set a crunched name
                    // on a field that isn't a local field?
                    /*else
                    {
                    }*/
                }
                else
                {
                    m_crunchedName = value;
                }
            }
        }

        public override string ToString()
        {
            string crunch = CrunchedName;
            return crunch != null ? crunch : base.ToString();
        }
    }
}
