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

        private bool m_isGenerated;
        internal bool IsGenerated
        {
            get 
            {
                // if we are pointing to an outer field, return ITS flag, not ours
                JSLocalField outerLocal = OuterField as JSLocalField;
                return outerLocal != null ? outerLocal.IsGenerated : m_isGenerated;
            }
            set 
            {
                // always set our flag, just in case
                m_isGenerated = value;

                // if we are pointing to an outer field, set it's flag as well
                JSLocalField outerLocal = OuterField as JSLocalField;
                if (outerLocal != null)
                {
                    outerLocal.IsGenerated = value;
                }
            }
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
    }
}
