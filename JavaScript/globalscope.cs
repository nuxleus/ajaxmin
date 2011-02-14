// globalscope.cs
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
using System.Reflection;

namespace Microsoft.Ajax.Utilities
{
    public sealed class GlobalScope : ActivationObject
    {
        private GlobalObject m_globalObject;
        private GlobalObject m_windowObject;
        private List<string> m_assumedGlobals;

        internal GlobalScope(JSParser parser)
            : base(null, parser)
        {
            // define the Global object's properties, and methods
            m_globalObject = new GlobalObject(
              GlobalObjectInstance.GlobalObject,
                //new string[] { "Infinity", "NaN", "undefined", "window" },
              new string[] { "Infinity", "NaN", "undefined", "window", "Image", "Math", "XMLHttpRequest" },
              new string[] { "decodeURI", "decodeURIComponent", "encodeURI", "encodeURIComponent", "escape", "isNaN", "isFinite", "parseFloat", "parseInt", "unescape", "ActiveXObject", "Array", "Boolean", "Date", "Error", "Function", "Number", "Object", "RegExp", "String", "HTMLElement" }
              );

            // define the Window object's properties, and methods
            m_windowObject = new GlobalObject(
              GlobalObjectInstance.WindowObject,
                //null, 
              new string[] { "frames", "clientInformation", "clipboardData", "document", "event", "external", "history", "location", "navigator", "screen", "closed", "name", "opener", "parent", "self", "status", "top" },
              new string[] { "alert", "blur", "clearInterval", "clearTimeout", "close", "confirm", "createPopup", "execScript", "focus", "moveTo", "moveBy", "navigate", "open", "prompt", "realizeBy", "realizeTo", "scroll", "scrollBy", "scrollTo", "setActive", "setInterval", "setTimeout", "showModalDialog", "showModelessDialog" }
              );
        }

        internal void SetAssumedGlobals(string[] globals)
        {
            // if we are passed anything....
            if (globals != null && globals.Length > 0)
            {
                // initialize the list with a copy of the array
                m_assumedGlobals = new List<string>(globals);
            }
        }

        internal override void AnalyzeScope()
        {
            // rename fields if we need to
            RenameFields();

            // it's okay for the global scope to have unused vars, so don't bother checking
            // the fields, but recurse the function scopes anyway
            foreach (ActivationObject activationObject in ChildScopes)
            {
                try
                {
                    Parser.ScopeStack.Push(activationObject);
                    activationObject.AnalyzeScope();
                }
                finally
                {
                    Parser.ScopeStack.Pop();
                }
            }
        }

        internal override void ReserveFields()
        {
            // don't do anything but traverse through our children
            foreach (ActivationObject scope in ChildScopes)
            {
                scope.ReserveFields();
            }
        }

        internal override void HyperCrunch()
        {
            // don't crunch global values -- they might be referenced in other scripts
            // within the page but outside this module.

            // traverse through our children scopes
            foreach (ActivationObject scope in ChildScopes)
            {
                scope.HyperCrunch();
            }
        }

        public override JSVariableField this[string name]
        {
            get
            {
                // check the name table
                JSVariableField variableField = base[name];
                if (variableField == null)
                {
                    // not found so far, check the window object
                    variableField = m_windowObject.GetField(name);
                }
                if (variableField == null)
                {
                    // not found so far, check the global object
                    variableField = m_globalObject.GetField(name);
                }
                if (variableField == null)
                {
                    // see if this value is provided in our "assumed" global list specified on the command line
                    if (m_assumedGlobals != null && m_assumedGlobals.Count > 0)
                    {
                        foreach (string globalName in m_assumedGlobals)
                        {
                            if (name == globalName)
                            {
                                variableField = CreateField(name, null, 0);
                                break;
                            }
                        }
                    }
                }
                return variableField;
            }
        }

        public override JSVariableField CreateField(string name, object value, FieldAttributes attributes)
        {
            return new JSGlobalField(name, value, attributes);
        }

        public override JSVariableField CreateField(JSVariableField outerField)
        {
            // should NEVER try to create an inner field in a global scope
            throw new NotImplementedException();
        }

        public override JSLocalField GetLocalField(String name)
        {
            // there are no local fields in the global scope
            return null;
        }

        // the global scope does nothing when told to add literals -- just returns null
        internal override List<ConstantWrapper> AddLiteral(ConstantWrapper constantWrapper, ActivationObject refScope)
        {
            return null;
        }

        protected override void CreateLiteralShortcuts()
        {
            // do nothing -- we don't create shortcuts in the global scope
        }
    }
}
