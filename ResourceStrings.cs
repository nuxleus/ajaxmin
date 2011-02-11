// ResourceStrings.cs
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

using System.Collections;
using System.Collections.Generic;

namespace Microsoft.Ajax.Utilities
{
    public class ResourceStrings
    {
        private string m_name;
        public string Name
        {
            get { return m_name; }
            set { m_name = value; }
        }

        private Dictionary<string, object> m_properties;
        public object this[string name]
        {
            get
            {
                object propertyValue;
                if (!m_properties.TryGetValue(name, out propertyValue))
                {
                    // couldn't find the property -- make sure we return null
                    propertyValue = null;
                }
                return propertyValue;
            }
            set
            {
                m_properties[name] = value;
            }
        }

        public int Count
        {
            get { return m_properties.Count; }
        }

        public ResourceStrings(IDictionaryEnumerator enumerator)
        {
            m_properties = new Dictionary<string, object>();

            if (enumerator != null)
            {
                // use the IDictionaryEnumerator to add properties to the collection
                while (enumerator.MoveNext())
                {
                    // get the property name
                    string propertyName = enumerator.Key.ToString();

                    // set the name/value in the resource object
                    m_properties[propertyName] = enumerator.Value;
                }
            }
        }

        public IDictionaryEnumerator GetEnumerator()
        {
            return m_properties.GetEnumerator();
        }
    }
}
