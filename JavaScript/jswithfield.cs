// jswithfield.cs
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

using System.Reflection;

namespace Microsoft.Ajax.Utilities
{
    public class JSWithField : JSLocalField
    {
        internal JSWithField(string name, FieldAttributes attributes)
            : base(name, null, attributes)
        {
            // with-fields cannot be crunced because they might
            // need to reference a property on the with object
            CanCrunch = false;
        }

        internal JSWithField(JSVariableField outerField)
            : base(outerField)
        {
            // with-fields cannot be crunced because they might
            // need to reference a property on the with object
            CanCrunch = false;
        }
    }
}
