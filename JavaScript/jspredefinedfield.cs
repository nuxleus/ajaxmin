// jspredefinedfield.cs
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
    public sealed class JSPredefinedField : JSVariableField
    {
        private MemberTypes m_memberType;
        public MemberTypes MemberType
        {
            get { return m_memberType; }
        }

        private GlobalObjectInstance m_globalObject;
        public GlobalObjectInstance GlobalObject
        {
            get { return m_globalObject; }
        }

        internal JSPredefinedField(string name, MemberTypes memberType, GlobalObjectInstance globalObject)
            : base(name, 0, null)
        {
            m_memberType = memberType;
            m_globalObject = globalObject;
            // predefined fields cannot be crunched
            CanCrunch = false;
        }
    }

    public enum GlobalObjectInstance
    {
        Other,
        GlobalObject,
        WindowObject
    }
}
