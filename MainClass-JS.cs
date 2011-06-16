// MainClass-JS.cs
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
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Xml;

namespace Microsoft.Ajax.Utilities
{
    public partial class MainClass
    {
        /// <summary>
        /// Undefined global variables will be added to this list
        /// </summary>
        private List<UndefinedReferenceException> m_undefined;// = null;

        #region JS-only settings

        // whether to analyze the resulting script for common problems
        // (as opposed to just crunching it)
        private bool m_analyze;// = false;

        // collapse certain contructors into literals
        private bool m_collapseToLiteral = true;

        // combine duplicate labels -- on by default with hypercrunch
        private bool m_combineDuplicateLiterals; // = false;

        // set to something other than Ignore if we know our eval statements 
        // will be referencing variables and functions in the immediate or
        // parent scopes
        private EvalTreatment m_evalTreatment = EvalTreatment.Ignore;

        // whether or not to evaluate literal expressions
        private bool m_evalLiteralExpressions = true;

        // whether or not to reorder function and var declarations within scopes
        private bool m_reorderScopeDeclarations = true;

        /// <summary>
        /// List of expected global variables we don't want to assume are undefined
        /// </summary>
        private List<string> m_globals;// = null;

        /// <summary>
        /// List of names we don't want automatically renamed
        /// </summary>
        private List<string> m_noAutoRename; // = null;

        // whether and how to crunch local names
        private LocalRenaming m_localRenaming = LocalRenaming.CrunchAll;

        // set to true to add code to support Mac Safari quirks
        private bool m_macSafariQuirks = true;

        // whether to keep all function names (true), or allow them to be renamed along with local vars (false)
        private bool m_preserveFunctionNames;// = false;

        // whether to keep unreferenced function expression names
        private bool m_removeFunctionExpressionNames = true;

        // whether to remove unneeded code
        private bool m_removeUnneededCode = true;

        // set to true to make sure the resulting code is safe to be
        // inserted inline into an HTML page
        private bool m_safeForInline = true;

        // whether to strip debug statements from output
        private bool m_stripDebugStatements = true;

        // set of names (variables or functions) that we want to always RENAME to something else
        private Dictionary<string, string> m_renameMap;

        // when using the manual-rename map, rename properties when this value us true 
        private bool m_renameProperties = true;

        // whether to ignore or parse conditional-compilation comments
        private bool m_ignoreConditionalCompilation; // = false;

        // whether to only preprocess (true), or to completely parse and analyze code (false)
        private bool m_preprocessOnly; // = false;

        // whether to preserve important comments or remove them
        private bool m_preserveImportantComments = true;

        // list of identifier names to consider "debug" lookups
        private List<string> m_debugLookups; // = null;

        #endregion

        #region file processing

        private int ProcessJSFile(string sourceFileName, string encodingName, ResourceStrings resourceStrings, StringBuilder outputBuilder, ref bool lastEndedSemicolon, ref long sourceLength)
        {
            int retVal = 0;

            // blank line before
            WriteProgress();

            // read our chunk of code
            var source = ReadInputFile(sourceFileName, encodingName, ref sourceLength);

            // create the a parser object for our chunk of code
            JSParser parser = new JSParser(source);

            // set up the file context for the parser
            parser.FileContext = string.IsNullOrEmpty(sourceFileName) ? "stdin" : sourceFileName;

            // hook the engine events
            parser.CompilerError += OnCompilerError;
            parser.UndefinedReference += OnUndefinedReference;

            // put the resource strings object into the parser
            parser.ResourceStrings = resourceStrings;

            // set our flags
            CodeSettings settings = new CodeSettings();
            settings.ManualRenamesProperties = m_renameProperties;
            settings.CollapseToLiteral = m_collapseToLiteral;
            settings.CombineDuplicateLiterals = m_combineDuplicateLiterals;
            settings.EvalLiteralExpressions = m_evalLiteralExpressions;
            settings.EvalTreatment = m_evalTreatment;
            settings.IndentSize = m_indentSize;
            settings.InlineSafeStrings = m_safeForInline;
            settings.LocalRenaming = m_localRenaming;
            settings.MacSafariQuirks = m_macSafariQuirks;
            settings.OutputMode = (m_prettyPrint ? OutputMode.MultipleLines : OutputMode.SingleLine);
            settings.PreserveFunctionNames = m_preserveFunctionNames;
            settings.ReorderScopeDeclarations = m_reorderScopeDeclarations;
            settings.RemoveFunctionExpressionNames = m_removeFunctionExpressionNames;
            settings.RemoveUnneededCode = m_removeUnneededCode;
            settings.StripDebugStatements = m_stripDebugStatements;
			settings.AllowEmbeddedAspNetBlocks = m_allowAspNet;
            settings.SetKnownGlobalNames(m_globals == null ? null : m_globals.ToArray());
            settings.SetNoAutoRename(m_noAutoRename == null ? null : m_noAutoRename.ToArray());
            settings.IgnoreConditionalCompilation = m_ignoreConditionalCompilation;
            settings.PreserveImportantComments = m_preserveImportantComments;

            // if there are defined preprocessor names
            if (m_defines != null && m_defines.Count > 0)
            {
                // set the list of defined names to our array of names
                settings.SetPreprocessorDefines(m_defines.ToArray());
            }

            // if there are rename entries...
            if (m_renameMap != null && m_renameMap.Count > 0)
            {
                // add each of them to the parser
                foreach (var sourceName in m_renameMap.Keys)
                {
                    settings.AddRenamePair(sourceName, m_renameMap[sourceName]);
                }
            }

            // if the lookups collection is not null, replace any current lookups with
            // whatever the collection is (which might be empty)
            if (m_debugLookups != null)
            {
                settings.SetDebugLookups(m_debugLookups.ToArray());
            }

            // cast the kill switch numeric value to the appropriate TreeModifications enumeration
            settings.KillSwitch = (TreeModifications)m_killSwitch;

            string resultingCode = null;
            if (m_preprocessOnly)
            {
                // we only want to preprocess the code. Call that api on the parser
                resultingCode = parser.PreprocessOnly(settings);
            }
            else
            {
                Block scriptBlock = parser.Parse(settings);
                if (scriptBlock != null)
                {
                    if (m_analyze)
                    {
                        // blank line before
                        WriteProgress();

                        // output our report
                        CreateReport(parser.GlobalScope);
                    }

                    // crunch the output and write it to debug stream
                    resultingCode = scriptBlock.ToCode();
                }
                else
                {
                    // no code?
                    WriteProgress(StringMgr.GetString("NoParsedCode"));
                }
            }

            if (!string.IsNullOrEmpty(resultingCode))
            {
                // always output the crunched code to debug stream
                System.Diagnostics.Debug.WriteLine(resultingCode);

                // if the last block of code didn't end in a semi-colon,
                // then we need to add one now
                if (!lastEndedSemicolon)
                {
                    outputBuilder.Append(';');
                }

                // we'll output either the crunched code (normal) or
                // the raw source if we're just echoing the input
                string outputCode = (m_echoInput ? source : resultingCode);

                // send the output code to the output stream
                outputBuilder.Append(outputCode);

                // check if this string ended in a semi-colon so we'll know if
                // we need to add one between this code and the next block (if any)
                lastEndedSemicolon = (outputCode[outputCode.Length - 1] == ';');
            }
            else
            {
                // resulting code is null or empty
                Debug.WriteLine(StringMgr.GetString("OutputEmpty"));
            }

            return retVal;
        }

        #endregion

        #region CreateJSFromResourceStrings method

        private static string CreateJSFromResourceStrings(ResourceStrings resourceStrings)
        {
            IDictionaryEnumerator enumerator = resourceStrings.GetEnumerator();

            StringBuilder sb = new StringBuilder();
            // start the var statement using the requested name and open the initializer object literal
            sb.Append("var ");
            sb.Append(resourceStrings.Name);
            sb.Append("={");

            // we're going to need to insert commas between each pair, so we'll use a boolean
            // flag to indicate that we're on the first pair. When we output the first pair, we'll
            // set the flag to false. When the flag is false, we're about to insert another pair, so
            // we'll add the comma just before.
            bool firstItem = true;

            // loop through all items in the collection
            while (enumerator.MoveNext())
            {
                // if this isn't the first item, we need to add a comma separator
                if (!firstItem)
                {
                    sb.Append(',');
                }
                else
                {
                    // next loop is no longer the first item
                    firstItem = false;
                }

                // append the key as the name, a colon to separate the name and value,
                // and then the value
                // must quote if not valid JS identifier format, or if it is, but it's a keyword
                string propertyName = enumerator.Key.ToString();
                if (!JSScanner.IsValidIdentifier(propertyName) || JSScanner.IsKeyword(propertyName))
                {
                    sb.Append("\"");
                    // because we are using quotes for the delimiters, replace any instances
                    // of a quote character (") with an escaped quote character (\")
                    sb.Append(propertyName.Replace("\"", "\\\""));
                    sb.Append("\"");
                }
                else
                {
                    sb.Append(propertyName);
                }
                sb.Append(':');

                // make sure the Value is properly escaped, quoted, and whatever we
                // need to do to make sure it's a proper JS string.
                // pass false for whether this string is an argument to a RegExp constructor.
                // pass false for whether to use W3Strict formatting for character escapes (use maximum browser compatibility)
                string stringValue = ConstantWrapper.EscapeString(
                    enumerator.Value.ToString(),
                    false,
                    false
                    );
                sb.Append(stringValue);
            }

            // close the object literal and return the string
            sb.AppendLine("};");
            return sb.ToString();
        }

        #endregion

        #region Variable Renaming method

        private void ProcessRenamingFile(string filePath)
        {
            try
            {
                XmlDocument xmlDoc = new XmlDocument();
                xmlDoc.Load(filePath);

                // get all the <rename> nodes in the document
                var renameNodes = xmlDoc.SelectNodes("//rename");

                // not an error if there are no variables to rename; but if there are no nodes, then
                // there's nothing to process
                if (renameNodes.Count > 0)
                {
                    // process each <rename> node
                    for (var ndx = 0; ndx < renameNodes.Count; ++ndx)
                    {
                        var renameNode = renameNodes[ndx];

                        // get the from and to attributes
                        var fromAttribute = renameNode.Attributes["from"];
                        var toAttribute = renameNode.Attributes["to"];

                        // need to have both, and their values both need to be non-null and non-empty
                        if (fromAttribute != null && !string.IsNullOrEmpty(fromAttribute.Value)
                            && toAttribute != null && !string.IsNullOrEmpty(toAttribute.Value))
                        {
                            // create the map if it doesn't yet exist
                            if (m_renameMap == null)
                            {
                                m_renameMap = new Dictionary<string, string>();
                            }

                            // if one or the other name is invalid, the pair will be ignored
                            m_renameMap.Add(fromAttribute.Value, toAttribute.Value);
                        }
                    }
                }

                // get all the <norename> nodes in the document
                var norenameNodes = xmlDoc.SelectNodes("//norename");

                // not an error if there aren't any
                if (norenameNodes.Count > 0)
                {
                    for (var ndx = 0; ndx < norenameNodes.Count; ++ndx)
                    {
                        var node = norenameNodes[ndx];
                        var idAttribute = node.Attributes["id"];
                        if (idAttribute != null && !string.IsNullOrEmpty(idAttribute.Value))
                        {
                            // if we haven't created it yet, do it now
                            if (m_noAutoRename == null)
                            {
                                m_noAutoRename = new List<string>();
                            }

                            m_noAutoRename.Add(idAttribute.Value);
                        }
                    }
                }
            }
            catch (XmlException e)
            {
                // throw an error indicating the XML error
                System.Diagnostics.Debug.WriteLine(e.ToString());
                throw new UsageException(ConsoleOutputMode.Console, "InputXmlError", e.Message);
            }
        }

        #endregion
        
        #region reporting methods

        private void CreateReport(GlobalScope globalScope)
        {
            // output global scope report
            WriteScopeReport(null, globalScope);

            // generate a flat array of function scopes ordered by context line start
            ActivationObject[] scopes = GetAllFunctionScopes(globalScope);

            // for each function scope, output a scope report
            foreach (ActivationObject scope in scopes)
            {
                FunctionScope funcScope = scope as FunctionScope;
                WriteScopeReport(
                  (funcScope != null ? funcScope.FunctionObject : null),
                  scope
                  );
            }
            // write the unreferenced global report
            WriteUnrefedReport();
        }

        private ActivationObject[] GetAllFunctionScopes(GlobalScope globalScope)
        {
            // create a list to hold all the scopes
            List<ActivationObject> scopes = new List<ActivationObject>();

            // recursively add all the function scopes to the list
            AddScopes(scopes, globalScope);

            // sort the scopes by starting line (from the context)
            scopes.Sort(ScopeComparer.Instance);

            // return as an array
            return scopes.ToArray();
        }

        private void AddScopes(List<ActivationObject> list, ActivationObject parentScope)
        {
            // for each child scope...
            foreach (ActivationObject scope in parentScope.ChildScopes)
            {
                // add the scope to the list if it's not a globalscopes
                // which leaves function scopes and block scopes (from catch blocks)
                if (!(scope is GlobalScope))
                {
                    list.Add(scope);
                }
                // recurse...
                AddScopes(list, scope);
            }
        }

        private void WriteScopeReport(FunctionObject funcObj, ActivationObject scope)
        {
            // output the function header
            if (scope is GlobalScope)
            {
                WriteProgress(StringMgr.GetString("GlobalObjectsHeader"));
            }
            else
            {
                FunctionScope functionScope = scope as FunctionScope;
                if (functionScope != null && funcObj != null)
                {
                    WriteFunctionHeader(funcObj, scope.IsKnownAtCompileTime);
                }
                else
                {
                    BlockScope blockScope = scope as BlockScope;
                    if (blockScope is CatchScope)
                    {
                        WriteBlockHeader(blockScope, StringMgr.GetString("BlockTypeCatch"));
                    }
                    else if (blockScope is WithScope)
                    {
                        WriteBlockHeader(blockScope, StringMgr.GetString("BlockTypeWith"));
                    }
                    else
                    {
                        WriteProgress();
                        WriteProgress(StringMgr.GetString("UnknownScopeType", scope.GetType().ToString()));
                    }
                }
            }

            // get all the fields in the scope
            JSVariableField[] scopeFields = scope.GetFields();
            // sort the fields
            Array.Sort(scopeFields, FieldComparer.Instance);

            // iterate over all the fields
            foreach (JSVariableField variableField in scopeFields)
            {
                // don't report placeholder fields
                if (!variableField.IsPlaceholder)
                {
                    WriteMemberReport(variableField, scope);
                }
            }
        }

        private void WriteBlockHeader(BlockScope blockScope, string blockType)
        {
            string knownMarker = string.Empty;
            if (!blockScope.IsKnownAtCompileTime)
            {
                StringBuilder sb = new StringBuilder();
                sb.Append('[');
                sb.Append(StringMgr.GetString("NotKnown"));
                sb.Append(']');
                knownMarker = sb.ToString();
            }

            WriteProgress();
            WriteProgress(StringMgr.GetString(
              "BlockScopeHeader",
              blockType,
              blockScope.Context.StartLineNumber,
              blockScope.Context.StartColumn,
              knownMarker
              ));
        }

        //TYPE "NAME" - Starts at line LINE, col COLUMN STATUS [crunched to CRUNCH]
        //
        //TYPE: Function, Function getter, Function setter
        //STATUS: '', Unknown, Unreachable
        private void WriteFunctionHeader(FunctionObject funcObj, bool isKnown)
        {
            // get the crunched value (if any)
            string crunched = string.Empty;
            JSLocalField localField = funcObj.LocalField as JSLocalField;
            if (localField != null && localField.CrunchedName != null)
            {
                crunched = StringMgr.GetString("CrunchedTo", localField.CrunchedName, localField.RefCount);
            }

            // get the status if the function
            StringBuilder statusBuilder = new StringBuilder();
            if (!isKnown)
            {
                statusBuilder.Append('[');
                statusBuilder.Append(StringMgr.GetString("NotKnown"));
            }
            if (funcObj.FunctionScope.Parent is GlobalScope)
            {
                // global function.
                // if this is a named function expression, we still want to know if it's
                // referenced by anyone
                if (funcObj.FunctionType == FunctionType.Expression
                    && !string.IsNullOrEmpty(funcObj.Name))
                {
                    // output a comma separator if not the first item, otherwise 
                    // open the square bracket
                    if (statusBuilder.Length > 0)
                    {
                        statusBuilder.Append(", ");
                    }
                    else
                    {
                        statusBuilder.Append('[');
                    }
                    statusBuilder.Append(StringMgr.GetString(
                        "FunctionInfoReferences",
                        funcObj.RefCount
                        ));
                }
            }
            else if (!funcObj.FunctionScope.IsReferenced(null))
            {
                // local function that isn't referenced -- unreachable!
                // output a comma separator if not the first item, otherwise 
                // open the square bracket
                if (statusBuilder.Length > 0)
                {
                    statusBuilder.Append(", ");
                }
                else
                {
                    statusBuilder.Append('[');
                }
                statusBuilder.Append(StringMgr.GetString("Unreachable"));
            }
            if (statusBuilder.Length > 0)
            {
                statusBuilder.Append(']');
            }
            string status = statusBuilder.ToString();

            string functionType;
            switch (funcObj.FunctionType)
            {
                case FunctionType.Getter:
                    functionType = "FunctionTypePropGet";
                    break;

                case FunctionType.Setter:
                    functionType = "FunctionTypePropSet";
                    break;

                case FunctionType.Expression:
                    functionType = "FunctionTypeExpression";
                    break;

                default:
                    functionType = "FunctionTypeFunction";
                    break;
            }

            // output
            WriteProgress();
            WriteProgress(StringMgr.GetString(
              "FunctionHeader",
              StringMgr.GetString(functionType),
              funcObj.Name,
              funcObj.Context.StartLineNumber,
              funcObj.Context.StartColumn,
              status,
              crunched
              ));
        }

        // NAME [SCOPE TYPE] [crunched to CRUNCH]
        //
        // SCOPE: global, local, outer, ''
        // TYPE: var, function, argument, arguments array, possibly undefined
        private void WriteMemberReport(JSVariableField variableField, ActivationObject immediateScope)
        {
            // skip any *unreferenced* named-function-expression fields
            JSNamedFunctionExpressionField namedFuncExpr = variableField as JSNamedFunctionExpressionField;
            if (namedFuncExpr == null || namedFuncExpr.RefCount > 0 || !m_removeFunctionExpressionNames)
            {
                string scope = string.Empty;
                string type = string.Empty;
                string crunched = string.Empty;
                string name = variableField.Name;
                if (variableField.IsLiteral)
                {
                    name = variableField.FieldValue.ToString();
                }

                // calculate the crunched label
                JSLocalField localField = variableField as JSLocalField;
                if (localField != null)
                {
                    if (localField.CrunchedName != null)
                    {
                        crunched = StringMgr.GetString("CrunchedTo", localField.CrunchedName, localField.RefCount);
                    }
                }

                // get the field's default scope and type
                GetFieldScopeType(variableField, immediateScope, out scope, out type);
                if (variableField is JSWithField)
                {
                    // if the field is a with field, we won't be using the crunched field (since
                    // those fields can't be crunched), so let's overload it with what the field
                    // could POSSIBLY be if the with object doesn't have a property of that name
                    string outerScope;
                    string outerType;
                    GetFieldScopeType(variableField.OuterField, immediateScope, out outerScope, out outerType);
                    crunched = StringMgr.GetString("MemberInfoWithPossibly", outerScope, outerType);
                }

                // format the entire string
                WriteProgress(StringMgr.GetString(
                 "MemberInfoFormat",
                 name,
                 scope,
                 type,
                 crunched
                 ));
            }
        }

        private static void GetFieldScopeType(JSVariableField variableField, ActivationObject immediateScope, out string scope, out string type)
        {
            JSLocalField localField = variableField as JSLocalField;
            JSPredefinedField predefinedField = variableField as JSPredefinedField;
            JSNamedFunctionExpressionField namedFuncExpr = variableField as JSNamedFunctionExpressionField;

            // default scope is blank
            scope = string.Empty;

            if (variableField is JSArgumentField)
            {
                type = StringMgr.GetString("MemberInfoTypeArgument");
            }
            else if (variableField is JSArgumentsField)
            {
                type = StringMgr.GetString("MemberInfoTypeArguments");
            }
            else if (predefinedField != null)
            {
                switch (predefinedField.GlobalObject)
                {
                    case GlobalObjectInstance.GlobalObject:
                        scope = StringMgr.GetString("MemberInfoScopeGlobalObject");
                        break;

                    case GlobalObjectInstance.WindowObject:
                        scope = StringMgr.GetString("MemberInfoScopeWindowObject");
                        break;

                    case GlobalObjectInstance.Other:
                        scope = StringMgr.GetString("MemberInfoScopeOtherObject");
                        break;
                }
                switch (predefinedField.MemberType)
                {
                    case MemberTypes.Method:
                        type = StringMgr.GetString("MemberInfoBuiltInMethod");
                        break;

                    case MemberTypes.Property:
                        type = StringMgr.GetString("MemberInfoBuiltInProperty");
                        break;

                    default:
                        type = StringMgr.GetString("MemberInfoBuiltInObject");
                        break;
                }
            }
            else if (variableField is JSGlobalField)
            {
                if ((variableField.Attributes & FieldAttributes.RTSpecialName) == FieldAttributes.RTSpecialName)
                {
                    // this is a special "global." It might not be a global, but something referenced
                    // in a with scope somewhere down the line.
                    type = StringMgr.GetString("MemberInfoPossiblyUndefined");
                }
                else if (variableField.FieldValue is FunctionObject)
                {
                    if (variableField.NamedFunctionExpression == null)
                    {
                        type = StringMgr.GetString("MemberInfoGlobalFunction");
                    }
                    else
                    {
                        type = StringMgr.GetString("MemberInfoFunctionExpression");
                    }
                }
                else
                {
                    type = StringMgr.GetString("MemberInfoGlobalVar");
                }
            }
            else if (variableField is JSWithField)
            {
                type = StringMgr.GetString("MemberInfoWithField");
            }
            else if (namedFuncExpr != null)
            {
                type = StringMgr.GetString("MemberInfoSelfFuncExpr");
            }
            else if (localField != null)
            {
                // type string
                if (localField.FieldValue is FunctionObject)
                {
                    if (localField.NamedFunctionExpression == null)
                    {
                        type = StringMgr.GetString("MemberInfoLocalFunction");
                    }
                    else
                    {
                        type = StringMgr.GetString("MemberInfoFunctionExpression");
                    }
                }
                else if (localField.IsLiteral)
                {
                    type = StringMgr.GetString("MemberInfoLocalLiteral");
                }
                else
                {
                    type = StringMgr.GetString("MemberInfoLocalVar");
                }

                // scope string
                // this is a local variable, so there MUST be a non-null function scope passed
                // to us. That function scope will be the scope we are expecting local variables
                // to be defined in. If the field is defined in that scope, it's local -- otherwise
                // it must be an outer variable.
                JSVariableField scopeField = immediateScope[variableField.Name];
                if (scopeField == null || scopeField.OuterField != null)
                {
                    scope = StringMgr.GetString("MemberInfoScopeOuter");
                }
                else
                {
                    scope = StringMgr.GetString("MemberInfoScopeLocal");
                }
            }
            else
            {
                type = StringMgr.GetString("MemberInfoBuiltInObject");
            }
        }

        private void WriteUnrefedReport()
        {
            if (m_undefined != null && m_undefined.Count > 0)
            {
                // sort the undefined reference exceptions
                m_undefined.Sort(UndefinedComparer.Instance);

                // write the report
                WriteProgress();
                WriteProgress(StringMgr.GetString("UndefinedGlobalHeader"));
                foreach (UndefinedReferenceException ex in m_undefined)
                {
                    WriteProgress(StringMgr.GetString(
                      "UndefinedInfo",
                      ex.Name,
                      ex.Line,
                      ex.Column,
                      ex.ReferenceType.ToString()
                      ));
                }
            }
        }

        #endregion

        #region Error-handling Members

        private void OnCompilerError(object sender, JScriptExceptionEventArgs e)
        {
            ContextError error = e.Error;
            // ignore severity values greater than our severity level
            if (error.Severity <= m_warningLevel)
            {
                // we found an error
                m_errorsFound = true;

                // write the error out
                WriteError(error.ToString());
            }
        }

        private void OnUndefinedReference(object sender, UndefinedReferenceEventArgs e)
        {
            if (m_undefined == null)
            {
                m_undefined = new List<UndefinedReferenceException>();
            }
            m_undefined.Add(e.Exception);
        }

        #endregion

        #region Comparer classes

        private class ScopeComparer : IComparer<ActivationObject>
        {
            // singleton instance
            public static readonly IComparer<ActivationObject> Instance = new ScopeComparer();

            // private constructor -- use singleton
            private ScopeComparer() { }

            #region IComparer<ActivationObject> Members

            public int Compare(ActivationObject left, ActivationObject right)
            {
                int comparison = 0;
                Context leftContext = GetContext(left);
                Context rightContext = GetContext(right);
                if (leftContext == null)
                {
                    // if they're both null, return 0 (equal)
                    // otherwise just the left is null, so we want it at the end, so
                    // return 1 to indicate that it goes after the right context
                    return (rightContext == null ? 0 : 1);
                }
                else if (rightContext == null)
                {
                    // return -1 to indicate that the right context (null) goes after the left
                    return -1;
                }

                // compare their start lines
                comparison = leftContext.StartLineNumber - rightContext.StartLineNumber;
                if (comparison == 0)
                {
                    comparison = leftContext.StartColumn - rightContext.StartColumn;
                }
                return comparison;
            }

            private static Context GetContext(ActivationObject obj)
            {
                FunctionScope funcScope = obj as FunctionScope;
                if (funcScope != null && funcScope.FunctionObject != null)
                {
                    return funcScope.FunctionObject.Context;
                }
                else
                {
                    BlockScope blockScope = obj as BlockScope;
                    if (blockScope != null)
                    {
                        return blockScope.Context;
                    }
                }
                return null;
            }

            #endregion
        }

        private class FieldComparer : IComparer<JSVariableField>
        {
            // singleton instance
            public static readonly IComparer<JSVariableField> Instance = new FieldComparer();

            // private constructor -- use singleton
            private FieldComparer() { }

            #region IComparer<JSVariableField> Members

            /// <summary>
            /// Argument fields first
            /// Fields defined
            /// Functions defined
            /// Globals referenced
            /// Outer fields referenced
            /// Functions referenced
            /// </summary>
            /// <param name="x">left-hand object</param>
            /// <param name="y">right-hand object</param>
            /// <returns>&gt;0 left before right, &lt;0 right before left</returns>
            public int Compare(JSVariableField left, JSVariableField right)
            {
                int comparison = 0;
                if (left != null && right != null)
                {
                    // compare type class
                    comparison = GetOrderIndex(left) - GetOrderIndex(right);
                    if (comparison == 0)
                    {
                        // sort alphabetically
                        comparison = string.Compare(
                          left.Name,
                          right.Name,
                          StringComparison.OrdinalIgnoreCase
                          );
                    }
                }
                return comparison;
            }

            #endregion

            private static FieldOrder GetOrderIndex(JSVariableField obj)
            {
                if (obj is JSArgumentField)
                {
                    return FieldOrder.Argument;
                }
                if (obj is JSArgumentsField)
                {
                    return FieldOrder.ArgumentsArray;
                }

                JSGlobalField globalField = obj as JSGlobalField;
                if (globalField != null)
                {
                    return (
                      globalField.FieldValue is FunctionObject
                      ? FieldOrder.GlobalFunctionReferenced
                      : FieldOrder.GlobalFieldReferenced
                      );
                }

                JSLocalField localField = obj as JSLocalField;
                if (localField != null)
                {
                    if (localField.OuterField != null)
                    {
                        return (
                         localField.FieldValue is FunctionObject
                         ? FieldOrder.OuterFunctionReferenced
                         : FieldOrder.OuterFieldReferenced
                         );
                    }
                    else
                    {
                        return (
                        localField.FieldValue is FunctionObject
                        ? FieldOrder.FunctionDefined
                        : FieldOrder.FieldDefined
                        );
                    }
                }
                return FieldOrder.Other;
            }

            private enum FieldOrder : int
            {
                Argument = 0,
                ArgumentsArray,
                FieldDefined,
                FunctionDefined,
                OuterFieldReferenced,
                OuterFunctionReferenced,
                GlobalFieldReferenced,
                GlobalFunctionReferenced,
                Other
            }
        }

        private class UndefinedComparer : IComparer<UndefinedReferenceException>
        {
            // singleton instance
            public static readonly IComparer<UndefinedReferenceException> Instance = new UndefinedComparer();

            // private constructor -- use singleton
            private UndefinedComparer() { }

            #region IComparer<UndefinedReferenceException> Members

            public int Compare(UndefinedReferenceException left, UndefinedReferenceException right)
            {
                // first do the right thing if one or both are null
                if (left == null && right == null)
                {
                    // both null -- equal
                    return 0;
                }

                if (left == null)
                {
                    // left is null, right is not -- left is less
                    return -1;
                }

                if (right == null)
                {
                    // left is not null, right is -- left is more
                    return 1;
                }

                // neither are null
                int comparison = string.Compare(left.ToString(), right.ToString(), StringComparison.OrdinalIgnoreCase);
                if (comparison == 0)
                {
                    comparison = left.Line - right.Line;
                    if (comparison == 0)
                    {
                        comparison = left.Column - right.Column;
                    }
                }

                return comparison;
            }

            #endregion
        }

        #endregion
    }
}
