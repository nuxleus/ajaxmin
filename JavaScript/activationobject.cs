// activationobject.cs
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
using System.Globalization;
using System.Reflection;

namespace Microsoft.Ajax.Utilities
{
    public abstract class ActivationObject
    {
        private bool m_isKnownAtCompileTime;
        public bool IsKnownAtCompileTime
        {
            get { return m_isKnownAtCompileTime; }
            set { m_isKnownAtCompileTime = value; }
        }

        private Dictionary<string, JSVariableField> m_nameTable;
        internal Dictionary<string, JSVariableField> NameTable { get { return m_nameTable; } }

        private List<JSVariableField> m_fieldTable;
        internal List<JSVariableField> FieldTable { get { return m_fieldTable; } }

        private List<ActivationObject> m_childScopes;
        internal List<ActivationObject> ChildScopes { get { return m_childScopes; } }

        private SimpleHashtable m_verboten;
        internal SimpleHashtable Verboten { get { return m_verboten; } }

        private ActivationObject m_parent;
        public ActivationObject Parent
        {
            get { return m_parent; }
        }

        // for literal-combining
        private Dictionary<string, LiteralReference> m_literalMap;
        private static uint s_literalCounter; // = 0;

        private JSParser m_parser;
        protected JSParser Parser
        {
            get { return m_parser; }
        }

        protected ActivationObject(ActivationObject parent, JSParser parser)
        {
            m_parent = parent;
            m_nameTable = new Dictionary<string, JSVariableField>();
            m_fieldTable = new List<JSVariableField>();
            m_childScopes = new List<ActivationObject>();
            m_verboten = new SimpleHashtable(32);
            m_isKnownAtCompileTime = true;
            m_parser = parser;

            // if our parent is a scope....
            if (parent != null)
            {
                // add us to the parent's list of child scopes
                parent.m_childScopes.Add(this);
            }
        }

        internal virtual void AnalyzeScope()
        {
            // check for unused local fields or arguments
            foreach (JSVariableField variableField in m_nameTable.Values)
            {
                JSLocalField locField = variableField as JSLocalField;
                if (locField != null && !locField.IsReferenced && locField.OriginalContext != null)
                {
                    if (locField.FieldValue is FunctionObject)
                    {
                        Context ctx = ((FunctionObject)locField.FieldValue).IdContext;
                        if (ctx == null) { ctx = locField.OriginalContext; }
                        ctx.HandleError(JSError.FunctionNotReferenced, false);
                    }
                    else if (!locField.IsGenerated)
                    {
                        JSArgumentField argumentField = locField as JSArgumentField;
                        if (argumentField != null)
                        {
                            // we only want to throw this error if it's possible to remove it
                            // from the argument list. And that will only happen if there are
                            // no REFERENCED arguments after this one in the formal parameter list.
                            // Assertion: because this is a JSArgumentField, this should be a function scope,
                            // let's walk up to the first function scope we find, just in case.
                            FunctionScope functionScope = this as FunctionScope;
                            if (functionScope == null)
                            {
                                ActivationObject scope = this.Parent;
                                while (scope != null)
                                {
                                    functionScope = scope as FunctionScope;
                                    if (scope != null)
                                    {
                                        break;
                                    }
                                }
                            }
                            if (functionScope == null || functionScope.IsArgumentTrimmable(argumentField))
                            {
                                locField.OriginalContext.HandleError(
                                  JSError.ArgumentNotReferenced,
                                  false
                                  );
                            }
                        }
                        else if (locField.OuterField == null || !locField.OuterField.IsReferenced)
                        {
                            locField.OriginalContext.HandleError(
                              JSError.VariableDefinedNotReferenced,
                              false
                              );
                        }
                    }
                }
            }

            // recurse 
            foreach (ActivationObject activationObject in m_childScopes)
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

        internal virtual void AnalyzeLiterals()
        {
            // create our shortcuts first
            CreateLiteralShortcuts();

            // then recurse
            foreach (ActivationObject activationObject in m_childScopes)
            {
                try
                {
                    Parser.ScopeStack.Push(activationObject);
                    activationObject.AnalyzeLiterals();
                }
                finally
                {
                    Parser.ScopeStack.Pop();
                }
            }
        }

        #region crunching methods

        internal virtual void ReserveFields()
        {
            // traverse through our children first to get depth-first
            foreach (ActivationObject scope in m_childScopes)
            {
                scope.ReserveFields();
            }

            // then reserve all our fields that need reserving
            // check for unused local fields or arguments
            foreach (JSVariableField variableField in m_nameTable.Values)
            {
                string name = variableField.Name;
                JSLocalField localField = variableField as JSLocalField;
                if (localField != null)
                {
                    // if this is a named-function-expression name, then we want to use the name of the 
                    // outer field so we don't collide in IE
                    JSNamedFunctionExpressionField namedExprField = localField as JSNamedFunctionExpressionField;
                    if (namedExprField != null)
                    {
                        // make sure the field is in this scope's verboten list so we don't accidentally reuse
                        // an outer scope variable name
                        if (m_verboten[localField] == null)
                        {
                            m_verboten[localField] = localField;
                        }

                        // we don't need to reserve up the scope because the named function expression's
                        // "outer" field is always in the very next scope
                    }
                    else if (localField.OuterField != null)
                    {
                        // if the outer field is not null, then this field (not the name) needs to be 
                        // reserved up the scope chain until the scope where it's defined.
                        // make sure the field is in this scope's verboten list so we don't accidentally reuse
                        // the outer scope's variable name
                        if (m_verboten[localField] == null)
                        {
                            m_verboten[localField] = localField;
                        }

                        for (ActivationObject scope = this; scope != null; scope = scope.Parent)
                        {
                            // get the local field by this name (if any)
                            JSLocalField scopeField = scope.GetLocalField(name);
                            if (scopeField == null)
                            {
                                // it's not referenced in this scope -- if the field isn't in the verboten
                                // list, add it now
                                if (scope.m_verboten[localField] == null)
                                {
                                    scope.m_verboten[localField] = localField;
                                }
                            }
                            else if (scopeField.OuterField == null)
                            {
                                // found the original field -- stop looking
                                break;
                            }
                        }
                    }
                    else if (m_parser.Settings.LocalRenaming == LocalRenaming.KeepLocalizationVars
                      && localField.Name.StartsWith("L_", StringComparison.Ordinal))
                    {
                        // localization variable. don't crunch it.
                        // add it to this scope's verboten list in the extremely off-hand chance
                        // that a crunched variable might be the same pattern
                        if (m_verboten[localField] == null)
                        {
                            m_verboten[localField] = localField;
                        }
                    }
                }
                else
                {
                    // must be a global of some sort
                    // reserve the name in this scope and all the way up the chain
                    for (ActivationObject scope = this; scope != null; scope = scope.Parent)
                    {
                        if (scope.m_verboten[name] == null)
                        {
                            scope.m_verboten[name] = variableField;
                        }
                    }
                }
            }

            // finally, if this scope is not known at compile time, 
            // AND we know we want to make all affected scopes safe
            // for the eval statement
            // AND we are actually referenced by the enclosing scope, 
            // then our parent scope is also not known at compile time
            if (!m_isKnownAtCompileTime
                && Parser.Settings.EvalTreatment == EvalTreatment.MakeAllSafe)
            {
                ActivationObject parentScope = (ActivationObject)Parent;
                FunctionScope funcScope = this as FunctionScope;
                if (funcScope == null)
                {
                    // we're not a function -- parent is unknown too
                    parentScope.IsKnownAtCompileTime = false;
                }
                else
                {
                    JSLocalField localField = parentScope.GetLocalField(funcScope.FunctionObject.Name);
                    if (localField == null || localField.IsReferenced)
                    {
                        parentScope.IsKnownAtCompileTime = false;
                    }
                }
            }
        }

        internal virtual void ValidateGeneratedNames()
        {
            // check all the variables defined within this scope.
            // we're looking for uncrunched generated fields.
            foreach (JSVariableField variableField in m_fieldTable)
            {
                JSLocalField localField = variableField as JSLocalField;
                if (localField != null && localField.IsGenerated
                  && localField.CrunchedName == null)
                {
                    // we need to rename this field.
                    // first we need to walk all the child scopes depth-first
                    // looking for references to this field. Once we find a reference,
                    // we then need to add all the other variables referenced in those
                    // scopes and all above them (from here) so we know what names we
                    // can't use.
                    Dictionary<string, string> avoidTable = new Dictionary<string, string>();
                    GenerateAvoidList(avoidTable, localField.Name);

                    // now that we have our avoid list, create a crunch enumerator from it
                    CrunchEnumerator crunchEnum = new CrunchEnumerator(avoidTable);

                    // and use it to generate a new name
                    localField.CrunchedName = crunchEnum.NextName();
                }
            }

            // recursively traverse through our children
            foreach (ActivationObject scope in m_childScopes)
            {
                scope.ValidateGeneratedNames();
            }
        }

        private bool GenerateAvoidList(Dictionary<string, string> table, string name)
        {
            // our reference flag is based on what was passed to us
            bool isReferenced = false;

            // depth first, so walk all the children
            foreach (ActivationObject childScope in m_childScopes)
            {
                // if any child returns true, then it or one of its descendents
                // reference this variable. So we reference it, too
                if (childScope.GenerateAvoidList(table, name))
                {
                    // we'll return true because we reference it
                    isReferenced = true;
                }
            }
            if (!isReferenced)
            {
                // none of our children reference the scope, so see if we do
                if (m_nameTable.ContainsKey(name))
                {
                    isReferenced = true;
                }
            }

            if (isReferenced)
            {
                // if we reference the name or are in line to reference the name,
                // we need to add all the variables we reference to the list
                foreach (JSVariableField variableField in m_fieldTable)
                {
                    string fieldName = variableField.ToString();
                    if (!table.ContainsKey(fieldName))
                    {
                        table[fieldName] = fieldName;
                    }
                }
            }
            // return whether or not we are in the reference chain
            return isReferenced;
        }

        internal virtual void HyperCrunch()
        {
            // if we're not known at compile time, then we can't crunch
            // the local variables in this scope, because we can't know if
            // something will reference any of it at runtime.
            // eval is something that will make the scope unknown because we
            // don't know what eval will evaluate to until runtime
            if (m_isKnownAtCompileTime)
            {
                // get an array of all the uncrunched local variables defined in this scope
                JSLocalField[] localFields = GetUncrunchedLocals();
                if (localFields.Length > 0)
                {
                    // create a crunch-name enumerator, taking into account our verboten set
                    CrunchEnumerator crunchEnum = new CrunchEnumerator(m_verboten);
                    for (int ndx = 0; ndx < localFields.Length; ++ndx)
                    {
                        JSLocalField localField = localFields[ndx];

                        // if we are an unambiguous reference to a named function expression and we are not
                        // referenced by anyone else, then we can just skip this variable because the
                        // name will be stripped from the output anyway.
                        // we also always want to crunch "placeholder" fields.
                        if (localField.CanCrunch
                            && (localField.RefCount > 0 || localField.IsDeclared || localField.IsPlaceholder
                            || !(Parser.Settings.RemoveFunctionExpressionNames && Parser.Settings.IsModificationAllowed(TreeModifications.RemoveFunctionExpressionNames))))
                        {
                            localFields[ndx].CrunchedName = crunchEnum.NextName();
                        }
                    }
                }
            }

            // then traverse through our children
            foreach (ActivationObject scope in m_childScopes)
            {
                scope.HyperCrunch();
            }
        }

        internal JSLocalField[] GetUncrunchedLocals()
        {
            // there can't be more uncrunched fields than total fields
            List<JSLocalField> list = new List<JSLocalField>(m_nameTable.Count);
            foreach (JSVariableField variableField in m_nameTable.Values)
            {
                // we're only interested in local fields
                JSLocalField localField = variableField as JSLocalField;

                // if the local field is defined in this scope and hasn't been crunched
                // AND can still be crunched
                if (localField != null && localField.OuterField == null && localField.CrunchedName == null
                    && localField.CanCrunch)
                {
                    // if local renaming is not crunch all, then it must be crunch all but localization
                    // (we don't get called if we aren't crunching anything). 
                    // SO for the first clause:
                    // IF we are crunch all, we're good; but if we aren't crunch all, then we're only good if
                    //    the name doesn't start with "L_".
                    // The second clause is only computed IF we already think we're good to go.
                    // IF we aren't preserving function names, then we're good. BUT if we are, we're
                    // only good to go if this field doesn't represent a function object.
                    if ((m_parser.Settings.LocalRenaming == LocalRenaming.CrunchAll 
                        || !localField.Name.StartsWith("L_", StringComparison.Ordinal))
                        && !(m_parser.Settings.PreserveFunctionNames && localField.IsFunction))
                    {
                        // add to our list
                        list.Add(localField);
                    }
                }
            }
            // sort the array by reference count, descending
            list.Sort(ReferenceComparer.Instance);
            // return as an array
            return list.ToArray();
        }

        #endregion

        #region field-management methods

        public virtual JSVariableField this[string name]
        {
            get
            {
                JSVariableField variableField;
                // check to see if this name is already defined in this scope
                if (!m_nameTable.TryGetValue(name, out variableField))
                {
                    // not in this scope
                    variableField = null;
                }
                return variableField;
            }
        }

        public virtual JSVariableField FindReference(string name)
        {
            // see if we have it
            JSVariableField variableField = this[name];
            // if we didn't find anything and this scope has a parent
            if (variableField == null && Parent != null)
            {
                // recursively go up the scope chain
                variableField = Parent.FindReference(name);
            }
            return variableField;
        }

        public virtual JSVariableField DeclareField(string name, object value, FieldAttributes attributes)
        {
            JSVariableField variableField;
            if (!m_nameTable.TryGetValue(name, out variableField))
            {
                variableField = CreateField(name, value, attributes);
                AddField(variableField);
            }
            return variableField;
        }

        public abstract JSVariableField CreateField(JSVariableField outerField);
        public abstract JSVariableField CreateField(string name, object value, FieldAttributes attributes);

        public virtual JSVariableField CreateInnerField(JSVariableField outerField)
        {
            JSVariableField innerField;
            if (outerField is JSGlobalField || outerField is JSPredefinedField)
            {
                // if this is a global or predefined field, then just add the field itself
                // to the local scope. We don't want to create a local reference.
                innerField = outerField;
            }
            else
            {
                // create a new inner field to be added to our scope
                innerField = CreateField(outerField);
            }

            // add the field to our scope and return it
            AddField(innerField);
            return innerField;
        }

        internal JSVariableField AddField(JSVariableField variableField)
        {
            m_nameTable[variableField.Name] = variableField;
            m_fieldTable.Add(variableField);
            return variableField;
        }

        public virtual JSLocalField GetLocalField(string name)
        {
            return m_nameTable.ContainsKey(name) ? m_nameTable[name] as JSLocalField : null;
        }

        #endregion

        /// <summary>
        /// this class is used to sort the crunchable local fields in a scope so that the fields
        /// most in need of crunching get crunched first, therefore having the smallest-length
        /// crunched variable name.
        /// Highest priority are the fields most-often referenced.
        /// Among fields with the same reference count, the longest fields get priority.
        /// Lastly, alphabetize.
        /// </summary>
        private class ReferenceComparer : IComparer<JSLocalField>
        {
            // singleton instance
            public static readonly IComparer<JSLocalField> Instance = new ReferenceComparer();
            // never instantiate outside this class
            private ReferenceComparer() { }

            #region IComparer<JSLocalField> Members

            public int Compare(JSLocalField left, JSLocalField right)
            {
                int comparison = 0;
                if (left != null && right != null)
                {
                    comparison = right.RefCount - left.RefCount;
                    if (comparison == 0)
                    {
                        comparison = right.Name.Length - left.Name.Length;
                        if (comparison == 0)
                        {
                            comparison = string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase);
                        }
                    }
                }
                return comparison;
            }

            #endregion
        }

        #region Literal-combining code

        protected virtual void CreateLiteralShortcuts()
        {
            if (m_literalMap != null)
            {
                // get a reference to the first function scope in the chain
                // might be this, might be a parent
                FunctionScope functionScope = null;
                ActivationObject scope = this;
                while (scope != null && (functionScope = scope as FunctionScope) == null)
                {
                    scope = scope.Parent;
                }

                // if we didn't find a parent function scope, then don't do any combining
                // because the literals are globals
                if (functionScope != null)
                {
                    // for each value in our literal map
                    foreach (string constantString in m_literalMap.Keys)
                    {
                        LiteralReference literalReference = m_literalMap[constantString];

                        // if the child scope isn't null, then we don't reference the literal
                        // and only one of our child scopes does, so we don't want to add the
                        // shortcut here.
                        // OR if there are no constant wrappers left in the list, then we've already
                        // replaced them all and there's nothing left to do.
                        // BUT if the child scope is null, either we reference it, or more than
                        // one child references it. So if there are any constant wrappers in the list,
                        // then we want to add the shortcut and replace all the constants
                        if (literalReference.ChildScope == null && literalReference.ConstantWrapperList.Count > 0)
                        {
                            // AND we only want to do it if it will be worthwhile.
                            // (and a constant of length 1 is never worthwhile)
                            int constantLength = constantString.Length;
                            if (constantLength > 1)
                            {
                                int minCount = (constantLength + 7) / (constantLength - 1);
                                if (literalReference.Count > minCount)
                                {
                                    // create a special name that won't collide with any other variable names
                                    string specialName = string.Format(CultureInfo.InvariantCulture, "[literal:{0}]", ++s_literalCounter);

                                    // add a generated var statement at the top of the function block that
                                    // is equal to the literal value (just use the first constant wrapper as a model)
                                    ConstantWrapper modelConstant = literalReference.ConstantWrapperList[0];
                                    
                                    // by default we will use the value of the first instance as the generated variable's value
                                    object generatedValue = modelConstant.Value;

                                    // BUT....
                                    // if this is a numeric value, then we need to determine whether we should use a
                                    // positive or negative version of this value to minimize the number of minus operators in the results
                                    if (modelConstant.IsNumericLiteral)
                                    {
                                        // first we need to go through the existing references and count how many negative values there are
                                        var numberOfNegatives = 0;
                                        foreach (ConstantWrapper constantWrapper in literalReference.ConstantWrapperList)
                                        {
                                            // since the model us numeric, we shouldn't have any problems calling the
                                            // ToNumber method on the others (which should all also be numeric)
                                            if (constantWrapper.ToNumber() < 0)
                                            {
                                                ++numberOfNegatives;
                                            }
                                        }

                                        // now if more than half of the references are negative, we will want the generated value
                                        // to also be negative! Otherwise we want to force it to Positive.
                                        var absoluteValue = Math.Abs((double)generatedValue);
                                        if (numberOfNegatives > literalReference.ConstantWrapperList.Count / 2)
                                        {
                                            // force it to negative
                                            generatedValue = -absoluteValue;
                                        }
                                        else
                                        {
                                            // force it to positive
                                            generatedValue = absoluteValue;
                                        }
                                    }

                                    // add the generated variable to the function scope
                                    functionScope.FunctionObject.AddGeneratedVar(
                                        specialName,
                                        new ConstantWrapper(
                                            generatedValue,
                                            modelConstant.PrimitiveType,
                                            modelConstant.Context,
                                            Parser),
                                        true);

                                    // walk the list of constant wrappers backwards (because we'll be removing them
                                    // as we go along) and replace each one with a lookup for the generated variable.
                                    // Don't forget to analyze the lookup.
                                    for (int ndx = literalReference.ConstantWrapperList.Count - 1; ndx >= 0; --ndx)
                                    {
                                        ConstantWrapper constantWrapper = literalReference.ConstantWrapperList[ndx];

                                        // create the lookup based on the thisliteral context
                                        Lookup lookup = new Lookup(specialName, constantWrapper.Context, Parser);
                                        // indicate this is generated by our code, not the user
                                        lookup.IsGenerated = true;

                                        // by default, we're just going to replace the constant with the lookup
                                        AstNode replacement = lookup;

                                        // if the constant wrapper is a numeric value that is the NEGATIVE of the
                                        // combined numeric value (which would happen if the literal was subsequently
                                        // combined with a unary minus operator), then we need to change this to a unary-minus
                                        // operator on the lookup, not just the lookup.
                                        if (constantWrapper.IsNumericLiteral)
                                        {
                                            // since the constant wrapper is numeric, we shouldn't have any problems
                                            // calling ToNumber
                                            if ((double)generatedValue == -constantWrapper.ToNumber())
                                            {
                                                // it has been negated! Change the replacement to a unary minus operator
                                                // with the lookup as its operand
                                                replacement = new NumericUnary(
                                                    constantWrapper.Context,
                                                    Parser,
                                                    lookup,
                                                    JSToken.Minus);
                                            }
                                        }

                                        // replace the this literal with the appropriate node
                                        constantWrapper.Parent.ReplaceChild(constantWrapper, replacement);

                                        // set up the lookup's outer local field using the scope of the
                                        // original constant wrapper
                                        lookup.SetOuterLocalField(constantWrapper.EnclosingScope);

                                        // and remove it from the list. This is so child scopes don't also try to
                                        // add a shortcut -- the list will be empty.
                                        literalReference.ConstantWrapperList.RemoveAt(ndx);
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        // called during ConstantWrapper.AnalyzeNode
        internal virtual List<ConstantWrapper> AddLiteral(ConstantWrapper constantWrapper, ActivationObject refScope)
        {
            List<ConstantWrapper> nodeList = null;

            // numeric constants that are NaN or Infinity need not apply
            if (!constantWrapper.IsSpecialNumeric)
            {
                // if the constant is only one character long, it's never a good idea to
                // try and replace it
                string constantValue = constantWrapper.ToCode();
                if (constantValue.Length > 1)
                {
                    // go up the chain recursively. return the highest shared
                    // constant node list so we can share it if we need to
                    nodeList = ((ActivationObject)Parent).AddLiteral(constantWrapper, this);

                    // if we haven't created our literal map yet, do so now
                    if (m_literalMap == null)
                    {
                        m_literalMap = new Dictionary<string, LiteralReference>();
                    }

                    // now handle our scope 
                    LiteralReference literalReference;
                    // see if this constant is in our map
                    if (m_literalMap.ContainsKey(constantValue))
                    {
                        literalReference = m_literalMap[constantValue];
                        // increment the counter
                        literalReference.Increment();
                        // add this constant wrapper to the list
                        literalReference.ConstantWrapperList.Add(constantWrapper);

                        // if this is the ref scope, or if the ref scope is not the child scope, 
                        // set the child scope to null
                        if (literalReference.ChildScope != null
                          && (refScope == this || refScope != literalReference.ChildScope))
                        {
                            literalReference.ChildScope = null;
                        }
                    }
                    else
                    {
                        // add to the table with count = 1 and our given constant wrapper
                        // if this is the ref scope, child scope is null; otherwise use refScope
                        // if nodelist is null, create a new list; otherwise use the shared list
                        literalReference = new LiteralReference(
                          constantWrapper,
                          (refScope != this ? refScope : null),
                          nodeList
                          );
                        m_literalMap.Add(constantValue, literalReference);
                    }

                    // return whatever list it is we used for our node.
                    // it might be shared, or it might be new if we didn't find a shared list
                    nodeList = literalReference.ConstantWrapperList;
                }
            }

            return nodeList;
        }

        private class LiteralReference
        {
            private int m_count;
            public int Count
            {
                get { return m_count; }
            }

            private ActivationObject m_childScope;
            public ActivationObject ChildScope
            {
                get { return m_childScope; }
                set { m_childScope = value; }
            }

            private List<ConstantWrapper> m_constantWrappers;
            public List<ConstantWrapper> ConstantWrapperList
            {
                get { return m_constantWrappers; }
            }

            public LiteralReference(ConstantWrapper constantWrapper, ActivationObject childScope, List<ConstantWrapper> sharedList)
            {
                m_count = 1;
                m_childScope = childScope;

                m_constantWrappers = sharedList != null ? sharedList : new List<ConstantWrapper>();
                m_constantWrappers.Add(constantWrapper);
            }

            public void Increment()
            {
                m_count++;
            }
        }

        #endregion

        public JSVariableField[] GetFields()
        {
            return m_fieldTable.ToArray();
        }
    }
}