// functionscope.cs
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
    public sealed class FunctionScope : ActivationObject
    {
        private FunctionObject m_owningFunctionObject;
        public FunctionObject FunctionObject
        {
            get { return m_owningFunctionObject; }
            set { m_owningFunctionObject = value; }
        }

        private Dictionary<ActivationObject, ActivationObject> m_refScopes;

        private List<ThisLiteral> m_thisLiterals;

        internal FunctionScope(ActivationObject parent, bool isExpression, JSParser parser)
            : base(parent, parser)
        {
            if (isExpression)
            {
                // parent scopes automatically reference enclosed function expressions
                AddReference(Parent);
            }
        }

        internal void AddThisLiteral(ThisLiteral thisLiteral)
        {
            if (m_thisLiterals == null)
            {
                m_thisLiterals = new List<ThisLiteral>();
            }
            m_thisLiterals.Add(thisLiteral);
        }

        internal override void AnalyzeScope()
        {
            if (Parser.Settings.CombineDuplicateLiterals)
            {
                // combine this literals
                CreateThisShortcuts();
            }
            // default processing
            base.AnalyzeScope();
        }

        /*
        internal void Remove(JSLocalField localField)
        {
            NameTable.Remove(localField.Name);
            FieldTable.Remove(localField);
        }
        */

        internal JSArgumentField AddNewArgumentField(String name)
        {
            JSArgumentField result = new JSArgumentField(name, Missing.Value);
            AddField(result);
            return result;
        }

        internal JSArgumentsField AddArgumentsField()
        {
            JSArgumentsField arguments = new JSArgumentsField();
            AddField(arguments);
            return arguments;
        }

        internal bool IsArgumentTrimmable(JSArgumentField argumentField)
        {
            return m_owningFunctionObject.IsArgumentTrimmable(argumentField);
        }

        public override JSVariableField FindReference(string name)
        {
            JSVariableField variableField = this[name];
            if (variableField == null)
            {
                // didn't find a field in this scope.
                // special to function scopes: check to see if this is the arguments object
                if (string.Compare(name, "arguments", StringComparison.Ordinal) == 0)
                {
                    // this is a reference to the arguments object, so add the 
                    // arguments field to the scope and return it
                    variableField = AddArgumentsField();
                }
                else
                {
                    // recurse up the parent chain
                    variableField = Parent.FindReference(name);
                }
            }
            return variableField;
        }

        public override JSVariableField CreateField(string name, object value, FieldAttributes attributes)
        {
            return new JSLocalField(name, value, attributes);
        }

        public override JSVariableField CreateField(JSVariableField outerField)
        {
            return new JSLocalField(outerField);
        }

        internal void AddReference(ActivationObject scope)
        {
            // make sure the hash is created
            if (m_refScopes == null)
            {
                m_refScopes = new Dictionary<ActivationObject, ActivationObject>();
            }
            // we don't want to include block scopes or with scopes -- they are really
            // contained within their parents
            while (scope != null && scope is BlockScope)
            {
                scope = scope.Parent;
            }
            if (scope != null && !m_refScopes.ContainsKey(scope))
            {
                // add the scope to the hash
                m_refScopes.Add(scope, scope);
            }
        }

        internal bool IsReferenced(Dictionary<ActivationObject, ActivationObject> visited)
        {
            // first off, if the parent scope of this scope is a global scope, 
            // then we're a global function and referenced by default.
            if (Parent is GlobalScope)
            {
                return true;
            }

            // if we were passed null, then create a new hash table for us to pass on
            if (visited == null)
            {
                visited = new Dictionary<ActivationObject, ActivationObject>();
            }

            // add our scope to the visited hash
            if (!visited.ContainsKey(this))
            {
                visited.Add(this, this);
            }

            // now we walk the hash of referencing scopes and try to find one that that is
            if (m_refScopes != null)
            {
                foreach (ActivationObject referencingScope in m_refScopes.Keys)
                {
                    // skip any that we've already been to
                    if (!visited.ContainsKey(referencingScope))
                    {
                        // if we are referenced by the global scope, then we are referenced
                        if (referencingScope is GlobalScope)
                        {
                            return true;
                        }

                        // if this is a function scope, traverse through it
                        FunctionScope functionScope = referencingScope as FunctionScope;
                        if (functionScope != null && functionScope.IsReferenced(visited))
                        {
                            return true;
                        }
                    }
                }
            }

            // if we get here, then we didn't find any referencing scopes
            // that were referenced
            return false;
        }

        #region This-combining code

        private void CreateThisShortcuts()
        {
            // if we are renaming variables, AND
            // if there are four or more thisliterals for this scope...
            if (m_thisLiterals != null
              && m_thisLiterals.Count >= 4)
            {
                const string thisPlaceholderName = "[this]";
                // then we want to consolidate them.
                // add a var to the top of the function - the constructor of the vardecl will
                // cause the field to be created in the current scope
                m_owningFunctionObject.AddGeneratedVar(thisPlaceholderName, new ThisLiteral(null, Parser), true);

                // replace all thisliterals with a lookup to the [this] variable
                foreach (ThisLiteral thisLiteral in m_thisLiterals)
                {
                    // create the lookup based on the thisliteral context
                    Lookup lookup = new Lookup(thisPlaceholderName, thisLiteral.Context, Parser);
                    // indicate this is generated by our code, not the user
                    lookup.IsGenerated = true;

                    // replace the this literal with the lookup
                    thisLiteral.Parent.ReplaceChild(thisLiteral, lookup);

                    // set the variable field for the lookup, since this would normally be done in
                    // the analyze-node pass, and we're beyond that
                    lookup.VariableField = FindReference(thisPlaceholderName);
                    lookup.VariableField.AddReference(this);
                }
            }
        }

        #endregion
    }
}
