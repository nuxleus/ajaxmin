// codesettings.cs
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
using System.Collections.ObjectModel;
using System.Text;

namespace Microsoft.Ajax.Utilities
{
    public enum OutputMode
    {
        SingleLine,
        MultipleLines
    }

    public enum LocalRenaming
    {
        KeepAll,
        KeepLocalizationVars,
        CrunchAll
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "Eval")]
    public enum EvalTreatment
    {
        Ignore = 0,
        MakeImmediateSafe,
        MakeAllSafe
    }

    public class CodeSettings
    {
        public CodeSettings()
        {
            this.CollapseToLiteral = true;
            this.CombineDuplicateLiterals = false;
            this.EvalTreatment = EvalTreatment.Ignore;
            this.IndentSize = 4;
            this.InlineSafeStrings = true;
            this.LocalRenaming = LocalRenaming.CrunchAll;
            this.MacSafariQuirks = true;
            this.MinifyCode = true;
            this.OutputMode = OutputMode.SingleLine;
            this.PreserveFunctionNames = false;
            this.PreserveImportantComments = true;
            this.ReorderScopeDeclarations = true;
            this.RemoveFunctionExpressionNames = true;
            this.RemoveUnneededCode = true;
            this.StripDebugStatements = true;
            this.AllowEmbeddedAspNetBlocks = false;
            this.EvalLiteralExpressions = true;
            this.ManualRenamesProperties = true;

            // by default there are five names in the debug lookup collection
            var initialList = new string[] { "Debug", "$Debug", "WAssert", "Msn.Debug", "Web.Debug" };
            this.DebugLookups = new ReadOnlyCollection<string>(initialList);
        }

        #region Manually rename

        /// <summary>
        /// dictionary of identifiers we want to manually rename
        /// </summary>
        private Dictionary<string, string> m_identifierReplacementMap;

        /// <summary>
        /// Add a rename pair to the identifier rename map
        /// </summary>
        /// <param name="sourceName">name of the identifier in the source code</param>
        /// <param name="newName">new name with which to replace the source name</param>
        /// <returns>true if added; false if either name is not a valid JavaScript identifier</returns>
        public bool AddRenamePair(string sourceName, string newName)
        {
            bool successfullyAdded = false;

            // both names MUST be valid JavaScript identifiers
            if (JSScanner.IsValidIdentifier(sourceName) && JSScanner.IsValidIdentifier(newName))
            {
                // if there isn't a rename map, create it now
                if (m_identifierReplacementMap == null)
                {
                    m_identifierReplacementMap = new Dictionary<string, string>();
                }

                if (m_identifierReplacementMap.ContainsKey(sourceName))
                {
                    // just replace the value
                    m_identifierReplacementMap[sourceName] = newName;
                }
                else
                {
                    // add the new pair
                    m_identifierReplacementMap.Add(sourceName, newName);
                }

                // if we get here, we added it (or updated it if it's a dupe)
                successfullyAdded = true;
            }

            return successfullyAdded;
        }

        /// <summary>
        /// Clear any rename pairs from the identifier rename map
        /// </summary>
        public void ClearRenamePairs()
        {
            m_identifierReplacementMap.Clear();
        }

        /// <summary>
        /// returns whether or not there are any rename pairs in this settings object
        /// </summary>
        public bool HasRenamePairs
        {
            get
            {
                return m_identifierReplacementMap != null && m_identifierReplacementMap.Count > 0;
            }
        }

        /// <summary>
        /// Given a source identifier, return a new name for it, if one has already been added
        /// </summary>
        /// <param name="sourceName">source name to check</param>
        /// <returns>new name if it exists, null otherwise</returns>
        public string GetNewName(string sourceName)
        {
            // default is null
            string newName = null;

            // if there is no map, then there is no new name
            if (m_identifierReplacementMap != null)
            {
                m_identifierReplacementMap.TryGetValue(sourceName, out newName);
            }

            return newName;
        }

        /// <summary>
        /// Gets or sets a string representation of all the indentifier replacements as a comma-separated
        /// list of "source=target" identifiers.
        /// </summary>
        public string RenamePairs
        {
            get
            {
                StringBuilder sb = new StringBuilder();
                foreach (string sourceName in m_identifierReplacementMap.Keys)
                {
                    if (sb.Length > 0)
                    {
                        sb.Append(',');
                    }
                    sb.Append(sourceName);
                    sb.Append('=');
                    sb.Append(m_identifierReplacementMap[sourceName]);
                }
                return sb.ToString();
            }

            set
            {
                if (!string.IsNullOrEmpty(value))
                {
                    // pairs are comma-separated
                    foreach (var pair in value.Split(','))
                    {
                        // source name and new name are separated by an equal sign
                        var parts = pair.Split('=');

                        // must be exactly one equal sign
                        if (parts.Length == 2)
                        {
                            // try adding the trimmed pair to the collection
                            AddRenamePair(parts[0].Trim(), parts[1].Trim());
                        }
                    }
                }
            }
        }

        #endregion

        #region No automatic rename

        /// <summary>
        /// read-only collection of identifiers we do not want renamed
        /// </summary>
        public ReadOnlyCollection<string> NoAutoRenameIdentifiers { get; private set; }

        /// <summary>
        /// sets the collection of known global names to the array of string passed to this method
        /// </summary>
        /// <param name="globalArray">array of known global names</param>
        public int SetNoAutoRename(params string[] noRenameNames)
        {
            int numAdded = 0;
            if (noRenameNames == null)
            {
                NoAutoRenameIdentifiers = null;
            }
            else
            {
                // create a list with a capacity equal to the number of items in the array
                var checkedNames = new List<string>(noRenameNames.Length);

                // validate that each name in the array is a valid JS identifier
                foreach (var name in noRenameNames)
                {
                    // must be a valid JS identifier
                    string trimmedName = name.Trim();
                    if (JSScanner.IsValidIdentifier(trimmedName))
                    {
                        checkedNames.Add(trimmedName);
                    }
                }
                NoAutoRenameIdentifiers = new ReadOnlyCollection<string>(checkedNames);
                numAdded = checkedNames.Count;
            }

            return numAdded;
        }

        /// <summary>
        /// Get or sets the no-automatic-renaming list as a single string of comma-separated identifiers
        /// </summary>
        public string NoAutoRenameList
        {
            get
            {
                StringBuilder sb = new StringBuilder();
                foreach (var noRename in NoAutoRenameIdentifiers)
                {
                    if (sb.Length > 0)
                    {
                        sb.Append(',');
                    }
                    sb.Append(noRename);
                }
                return sb.ToString();
            }

            set
            {
                if (!string.IsNullOrEmpty(value))
                {
                    SetNoAutoRename(value.Split(','));
                }
            }
        }

        #endregion

        #region known globals
        
        /// <summary>
        /// read-only collection of known global names
        /// </summary>
        public ReadOnlyCollection<string> KnownGlobalNames { get; private set; }

        /// <summary>
        /// sets the collection of known global names to the array of string passed to this method
        /// </summary>
        /// <param name="globalArray">array of known global names</param>
        public int SetKnownGlobalNames(params string[] globalArray)
        {
            int numAdded = 0;
            if (globalArray == null)
            {
                KnownGlobalNames = null;
            }
            else
            {
                // create a list with a capacity equal to the number of items in the array
                var checkedNames = new List<string>(globalArray.Length);

                // validate that each name in the array is a valid JS identifier
                foreach (var name in globalArray)
                {
                    // must be a valid JS identifier
                    string trimmedName = name.Trim();
                    if (JSScanner.IsValidIdentifier(trimmedName))
                    {
                        checkedNames.Add(trimmedName);
                    }
                }
                KnownGlobalNames = new ReadOnlyCollection<string>(checkedNames);
                numAdded = checkedNames.Count;
            }

            return numAdded;
        }

        /// <summary>
        /// Gets or sets the known global names list as a single comma-separated string
        /// </summary>
        public string KnownGlobalNamesList
        {
            get
            {
                StringBuilder sb = new StringBuilder();
                foreach (var knownGlobal in KnownGlobalNames)
                {
                    if (sb.Length > 0)
                    {
                        sb.Append(',');
                    }
                    sb.Append(knownGlobal);
                }
                return sb.ToString();
            }

            set
            {
                if (!string.IsNullOrEmpty(value))
                {
                    SetKnownGlobalNames(value.Split(','));
                }
            }
        }

        #endregion

        #region Preprocessor defines

        /// <summary>
        /// Collection of names to define for the preprocessor
        /// </summary>
        public ReadOnlyCollection<string> PreprocessorDefines { get; private set; }

        /// <summary>
        /// Set the collection of defined names for the preprocessor
        /// </summary>
        /// <param name="definedNames">array of defined name strings</param>
        /// <returns>number of names successfully added to the collection</returns>
        public int SetPreprocessorDefines(params string[] definedNames)
        {
            int numAdded = 0;
            if (definedNames == null)
            {
                PreprocessorDefines = null;
            }
            else
            {
                // create a list with a capacity equal to the number of items in the array
                var checkedNames = new List<string>(definedNames.Length);

                // validate that each name in the array is a valid JS identifier
                foreach (var name in definedNames)
                {
                    // must be a valid JS identifier
                    string trimmedName = name.Trim();
                    if (JSScanner.IsValidIdentifier(trimmedName))
                    {
                        checkedNames.Add(trimmedName);
                    }
                }
                PreprocessorDefines = new ReadOnlyCollection<string>(checkedNames);
                numAdded = checkedNames.Count;
            }

            return numAdded;
        }

        /// <summary>
        /// string representation of the list of names defined for the preprocessor, comma-separated
        /// </summary>
        public string PreprocessorDefineList
        {
            get
            {
                // createa string builder and add each of the defined names to it
                // one-by-one, separating them with a comma
                var sb = new StringBuilder();
                foreach (var definedName in PreprocessorDefines)
                {
                    if (sb.Length > 0)
                    {
                        sb.Append(',');
                    }
                    sb.Append(definedName);
                }
                return sb.ToString();
            }
            set
            {
                if (!string.IsNullOrEmpty(value))
                {
                    SetPreprocessorDefines(value.Split(','));
                }
            }
        }

        #endregion

        #region Debug lookups

        /// <summary>
        /// Collection of "debug" lookup identifiers
        /// </summary>
        public ReadOnlyCollection<string> DebugLookups { get; private set; }

        /// <summary>
        /// Set the collection of debug "lookup" identifiers
        /// </summary>
        /// <param name="definedNames">array of debug lookup identifier strings</param>
        /// <returns>number of names successfully added to the collection</returns>
        public int SetDebugLookups(params string[] debugLookups)
        {
            int numAdded = 0;
            if (debugLookups == null)
            {
                DebugLookups = null;
            }
            else
            {
                // create a list with a capacity equal to the number of items in the array
                var checkedNames = new List<string>(debugLookups.Length);

                // validate that each name in the array is a valid JS identifier
                foreach (var lookup in debugLookups)
                {
                    string trimmedName = lookup.Trim();

                    // see if there is a period AFTER the first character. The string must START
                    // with a valid JS identifier, so if it starts with a period, it's invalid anyway.
                    if (trimmedName.IndexOf('.') > 0)
                    {
                        // there's a period -- this must be a member chain of valid JS identifiers
                        var memberChain = trimmedName.Split('.');

                        // assume it's good unless we find a bad identifier in the chain
                        var isValid = true;
                        foreach (var name in memberChain)
                        {
                            if (!JSScanner.IsValidIdentifier(name))
                            {
                                isValid = false;
                                break;
                            }
                        }

                        // if it's a valid chain, then we can add it to the list
                        if (isValid && !checkedNames.Contains(trimmedName))
                        {
                            checkedNames.Add(trimmedName);
                        }
                    }
                    else
                    {
                        // no period. must be a regular valid identifier.
                        if (JSScanner.IsValidIdentifier(trimmedName) && !checkedNames.Contains(trimmedName))
                        {
                            checkedNames.Add(trimmedName);
                        }
                    }
                }
                DebugLookups = new ReadOnlyCollection<string>(checkedNames);
                numAdded = checkedNames.Count;
            }

            return numAdded;
        }

        /// <summary>
        /// string representation of the list of debug lookups, comma-separated
        /// </summary>
        public string DebugLookupList
        {
            get
            {
                // createa string builder and add each of the debug lookups to it
                // one-by-one, separating them with a comma
                var sb = new StringBuilder();
                foreach (var debugLookup in DebugLookups)
                {
                    if (sb.Length > 0)
                    {
                        sb.Append(',');
                    }
                    sb.Append(debugLookup);
                }
                return sb.ToString();
            }
            set
            {
                if (!string.IsNullOrEmpty(value))
                {
                    SetDebugLookups(value.Split(','));
                }
            }
        }

        #endregion

        /// <summary>
        /// Whether to allow embedded asp.net blocks.
        /// </summary>
        public bool AllowEmbeddedAspNetBlocks
        {
            get;
            set;
        }

        /// <summary>
        /// deprecated setting
        /// </summary>
        [Obsolete("This property is obsolete and no longer used")]
        public bool CatchAsLocal
        {
            get; set;
        }

        /// <summary>
        /// collapse new Array() to [] and new Object() to {} [true]
        /// or leave ais [false]
        /// </summary>
        public bool CollapseToLiteral
        {
            get; set;
        }

        /// <summary>
        /// Combine duplicate literals within function scopes to local variables [true]
        /// or leave them as-is [false]
        /// </summary>
        public bool CombineDuplicateLiterals
        {
            get; set;
        }

        /// <summary>
        /// Gets or sets a value indicating whether EvalsAreSafe.
        /// Deprecated in favor of EvalTreatment, which is an enumeration
        /// allowing for more options than just true or false.
        /// True for this property is the equivalent of EvalTreament.Ignore;
        /// False is the equivalent to EvalTreament.MakeAllSafe
        /// </summary>
        [Obsolete("This property is deprecated; use EvalTreatment instead")]
        public bool EvalsAreSafe
        {
            get
            {
                return EvalTreatment == EvalTreatment.Ignore;
            }
            set
            {
                EvalTreatment = (value ? EvalTreatment.Ignore : EvalTreatment.MakeAllSafe);
            }
        }

        /// <summary>
        /// Evaluate expressions containing only literal bool, string, numeric, or null values [true]
        /// Leave literal expressions alone and do not evaluate them [false]
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "Eval")]
        public bool EvalLiteralExpressions
        {
            get;
            set;
        }

        /// <summary>
        /// Eval statements are safe and do not access local variables or functions [true]
        /// Code run by Eval statements may attempt to access local variables and functions [false]
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "Eval")]
        public EvalTreatment EvalTreatment
        {
            get; set;
        }

        /// <summary>
        /// whether or not to ignore conditional-compilation comment syntax (true) or
        /// to try to retain the comments in the output (false; default)
        /// </summary>
        public bool IgnoreConditionalCompilation { get; set; }

        /// <summary>
        /// Number of spaces per indent level when in MultipleLines output mode
        /// </summary>
        public int IndentSize
        {
            get; set;
        }

        /// <summary>
        /// Break up string literals containing &lt;/script&gt; so inline code won't break [true]
        /// Leave string literals as-is [false]
        /// </summary>
        public bool InlineSafeStrings
        {
            get; set;
        }

        /// <summary>
        /// How to rename local variables and functions:
        /// KeepAll - do not rename local variables and functions
        /// CrunchAll - rename all local variables and functions to shorter names
        /// KeepLocalizationVars - rename all local variables and functions that do NOT start with L_
        /// </summary>
        public LocalRenaming LocalRenaming
        {
            get; set;
        }

        /// <summary>
        /// Add characters to the output to make sure Mac Safari bugs are not generated [true]
        /// Disregard potential Mac Safari bugs [false]
        /// </summary>
        public bool MacSafariQuirks
        {
            get; set;
        }

        /// <summary>
        /// Modify the source code's syntax tree to provide the smallest equivalent output [true]
        /// Do not modify the syntax tree [false]
        /// </summary>
        public bool MinifyCode
        {
            get; set;
        }

        /// <summary>
        /// When using the manual-rename feature, properties with the "from" name will
        /// get renamed to the "to" name if this property is true (default), and left alone
        /// if this property is false.
        /// </summary>
        public bool ManualRenamesProperties
        {
            get; set;
        }

        /// <summary>
        /// Kill switch flags for each individual mod to the parsed code tree. Allows for
        /// callers to turn off specific modifications if desired.
        /// </summary>
        public TreeModifications KillSwitch
        {
            get; set;
        }

        /// <summary>
        /// Output mode:
        /// SingleLine - output all code on a single line
        /// MultipleLines - break the output into multiple lines to be more human-readable
        /// </summary>
        public OutputMode OutputMode
        {
            get; set;
        }

        /// <summary>
        /// Gets or sets a value indicating whether all function names must be preserved
        /// and remain as-named.
        /// </summary>
        public bool PreserveFunctionNames
        {
            get; set;
        }

        /// <summary>
        /// Gets or sets a value indicating whether to preserve important comments in the output.
        /// Default is true.
        /// </summary>
        public bool PreserveImportantComments
        {
            get; set;
        }

        /// <summary>
        /// Gets or sets a value indicating whether or not to reorder function and variable
        /// declarations within scopes
        /// </summary>
        public bool ReorderScopeDeclarations
        {
            get; set;
        }

        /// <summary>
        /// Gets or sets a value indicating whether or not to remove unreferenced function expression names
        /// </summary>
        public bool RemoveFunctionExpressionNames
        {
            get; set;
        }

        /// <summary>
        /// Remove unneeded code, like uncalled local functions [true]
        /// Keep all code [false]
        /// </summary>
        public bool RemoveUnneededCode
        {
            get; set;
        }

        /// <summary>
        /// Strip debug statements [true]
        /// Leave debug statements [false]
        /// </summary>
        public bool StripDebugStatements
        {
            get; set;
        }

        [Obsolete("This property is obsolete and no longer used")]
        public bool W3Strict
        {
            get; set;
        }

        /// <summary>
        /// Determine whether a particular AST tree modification is allowed, or has
        /// been squelched (regardless of any other settings)
        /// </summary>
        /// <param name="modification">one or more tree modification settings</param>
        /// <returns>true only if NONE of the passed modifications have their kill bits set</returns>
        public bool IsModificationAllowed(TreeModifications modification)
        {
            return (this.KillSwitch & modification) == TreeModifications.None;
        }

        #region Indent methods

        // this is the indent level and size for the pretty-print
        private int m_indentLevel;// = 0;

        internal void Indent()
        {
            ++m_indentLevel;
        }

        internal void Unindent()
        {
            --m_indentLevel;
        }

        internal string IndentSpaces
        {
            get
            {
                int numSpaces = m_indentLevel * IndentSize;
                return (numSpaces > 0 ? new string(' ', numSpaces) : string.Empty);
            }
        }

        // put indent level and size together for a new-line
        internal bool NewLine(StringBuilder sb)
        {
            bool addNewLine = (OutputMode == OutputMode.MultipleLines);
            if (addNewLine)
            {
                sb.AppendLine();
                sb.Append(IndentSpaces);
            }

            return addNewLine;
        }

        #endregion
    }

    [Flags]
    public enum TreeModifications : long
    {
        /// <summary>
        /// Default. No specific tree modification
        /// </summary>
        None                                        = 0x0000000000000000,

        /// <summary>
        /// Preserve "important" comments in output: /*! ... */
        /// </summary>
        PreserveImportantComments                   = 0x0000000000000001,

        /// <summary>
        /// Replace a member-bracket call with a member-dot construct if the member
        /// name is a string literal that can be an identifier.
        /// A["B"] ==&gt; A.B
        /// </summary>
        BracketMemberToDotMember                    = 0x0000000000000002,

        /// <summary>
        /// Replace a new Object constructor call with an object literal
        /// new Object() ==&gt; {}
        /// </summary>
        NewObjectToObjectLiteral                    = 0x0000000000000004,

        /// <summary>
        /// Change Array constructor calls with array literals.
        /// Does not replace constructors called with a single numeric parameter
        /// (could be a capacity contructor call).
        /// new Array() ==&gt; []
        /// new Array(A,B,C) ==&gt; [A,B,C]
        /// </summary>
        NewArrayToArrayLiteral                      = 0x0000000000000008,

        /// <summary>
        /// Remove the default case in a switch statement if the block contains
        /// only a break statement.
        /// remove default:break;
        /// </summary>
        RemoveEmptyDefaultCase                      = 0x0000000000000010,

        /// <summary>
        /// If there is no default case, remove any case statements that contain
        /// only a single break statement.
        /// remove case A:break;
        /// </summary>
        RemoveEmptyCaseWhenNoDefault                = 0x0000000000000020,

        /// <summary>
        /// Remove the break statement from the last case block of a switch statement.
        /// switch(A){case B: C;break;} ==&gt; switch(A){case B:C;}
        /// </summary>
        RemoveBreakFromLastCaseBlock                = 0x0000000000000040,

        /// <summary>
        /// Remove an empty finally statement if there is a non-empty catch block.
        /// try{...}catch(E){...}finally{} ==&gt; try{...}catch(E){...}
        /// </summary>
        RemoveEmptyFinally                          = 0x0000000000000080,

        /// <summary>
        /// Remove duplicate var declarations in a var statement that have no initializers.
        /// var A,A=B  ==&gt;  var A=B
        /// var A=B,A  ==&gt;  var A=B
        /// </summary>
        RemoveDuplicateVar                          = 0x0000000000000100,

        /// <summary>
        /// Combine adjacent var statements.
        /// var A;var B  ==&gt;  var A,B
        /// </summary>
        CombineVarStatements                        = 0x0000000000000200,

        /// <summary>
        /// Move preceeding var statement into the initializer of the for statement.
        /// var A;for(var B;;);  ==&gt;  for(var A,B;;);
        /// var A;for(;;)  ==&gt; for(var A;;)
        /// </summary>
        MoveVarIntoFor                              = 0x0000000000000400,

        /// <summary>
        /// Combine adjacent var statement and return statement to a single return statement
        /// var A=B;return A  ==&gt; return B
        /// </summary>
        VarInitializeReturnToReturnInitializer      = 0x0000000000000800,

        /// <summary>
        /// Replace an if-statement that has empty true and false branches with just the 
        /// condition expression.
        /// if(A);else;  ==&gt; A;
        /// </summary>
        IfEmptyToExpression                         = 0x0000000000001000,

        /// <summary>
        /// replace if-statement that only has a single call statement in the true branch
        /// with a logical-and statement
        /// if(A)B() ==&gt; A&amp;&amp;B()
        /// </summary>
        IfConditionCallToConditionAndCall           = 0x0000000000002000,

        /// <summary>
        /// Replace an if-else-statement where both branches are only a single return
        /// statement with a single return statement and a conditional operator.
        /// if(A)return B;else return C  ==&gt;  return A?B:C 
        /// </summary>
        IfElseReturnToReturnConditional             = 0x0000000000004000,

        /// <summary>
        /// If a function ends in an if-statement that only has a true-branch containing
        /// a single return statement with no operand, replace the if-statement with just
        /// the condition expression.
        /// function A(...){...;if(B)return}  ==&gt; function A(...){...;B}
        /// </summary>
        IfConditionReturnToCondition                = 0x0000000000008000,

        /// <summary>
        /// If the true-block of an if-statment is empty and the else-block is not,
        /// negate the condition and move the else-block to the true-block.
        /// if(A);else B  ==&gt;  if(!A)B
        /// </summary>
        IfConditionFalseToIfNotConditionTrue        = 0x0000000000010000,

        /// <summary>
        /// Combine adjacent string literals.
        /// "A"+"B"  ==&gt; "AB"
        /// </summary>
        CombineAdjacentStringLiterals               = 0x0000000000020000,

        /// <summary>
        /// Remove unary-plus operators when the operand is a numeric literal
        /// +123  ==&gt;  123
        /// </summary>
        RemoveUnaryPlusOnNumericLiteral             = 0x0000000000040000,

        /// <summary>
        /// Apply (and cascade) unary-minus operators to the value of a numeric literal
        /// -(4)  ==&gt;  -4   (unary minus applied to a numeric 4 ==&gt; numeric -4)
        /// -(-4)  ==&gt;  4   (same as above, but cascading)
        /// </summary>
        ApplyUnaryMinusToNumericLiteral             = 0x0000000000080000,

        /// <summary>
        /// Apply minification technics to string literals
        /// </summary>
        MinifyStringLiterals                        = 0x0000000000100000,

        /// <summary>
        /// Apply minification techniques to numeric literals
        /// </summary>
        MinifyNumericLiterals                       = 0x0000000000200000,

        /// <summary>
        /// Remove unused function parameters
        /// </summary>
        RemoveUnusedParameters                      = 0x0000000000400000,

        /// <summary>
        /// remove "debug" statements
        /// </summary>
        StripDebugStatements                        = 0x0000000000800000,

        /// <summary>
        /// Rename local variables and functions
        /// </summary>
        LocalRenaming                               = 0x0000000001000000,

        /// <summary>
        /// Remove unused function expression names
        /// </summary>
        RemoveFunctionExpressionNames               = 0x0000000002000000,

        /// <summary>
        /// Remove unnecessary labels from break or continue statements
        /// </summary>
        RemoveUnnecessaryLabels                     = 0x0000000004000000,

        /// <summary>
        /// Remove unnecessary @cc_on statements
        /// </summary>
        RemoveUnnecessaryCCOnStatements             = 0x0000000008000000,

        /// <summary>
        /// Convert (new Date()).getTime() to +new Date
        /// </summary>
        DateGetTimeToUnaryPlus                      = 0x0000000010000000,

        /// <summary>
        /// Evaluate numeric literal expressions.
        /// 1 + 2  ==&gt; 3
        /// </summary>
        EvaluateNumericExpressions                  = 0x0000000020000000,

        /// <summary>
        /// Simplify a common method on converting string to numeric: 
        /// lookup - 0  ==&gt; +lookup
        /// (Subtracting zero converts lookup to number, then doesn't modify
        /// it; unary plus also converts operand to numeric)
        /// </summary>
        SimplifyStringToNumericConversion           = 0x0000000040000000,

        /// <summary>
        /// Rename properties in object literals, member-dot, and member-bracket operations
        /// </summary>
        PropertyRenaming                            = 0x0000000080000000,

        /// <summary>
        /// Use preprocessor defines and the ///#IFDEF directive
        /// </summary>
        PreprocessorDefines                         = 0x0000000100000000,

        /// <summary>
        /// Remove the quotes arounf objectl literal property names when
        /// the names are valid identifiers.
        /// </summary>
        RemoveQuotesFromObjectLiteralNames          = 0x0000000200000000,

        /// <summary>
        /// Change boolean literals to not operators.
        /// true  -> !0
        /// false -> !1
        /// </summary>
        BooleanLiteralsToNotOperators               = 0x0000000400000000,

        /// <summary>
        /// Change if-statements with expression statements as their branches to expressions
        /// </summary>
        IfExpressionsToExpression                   = 0x0000000800000000,

        /// <summary>
        /// Combine adjacent expression statements into a single expression statement
        /// using the comma operator
        /// </summary>
        CombineAdjacentExpressionStatements         = 0x0000001000000000,

        /// <summary>
        /// If the types of both sides of a strict operator (=== or !==) are known
        /// to be the same, we can reduce the operators to == or !=
        /// </summary>
        ReduceStrictOperatorIfTypesAreSame          = 0x0000002000000000,

        /// <summary>
        /// If the types of both sides of a strict operator (=== or !==) are known
        /// to be different, than we can reduct the binary operator to false or true (respectively)
        /// </summary>
        ReduceStrictOperatorIfTypesAreDifferent     = 0x0000004000000000,

        /// <summary>
        /// Move function declarations to the top of the containing scope
        /// </summary>
        MoveFunctionToTopOfScope                    = 0x0000008000000000,

        /// <summary>
        /// Combine var statements at the top of the containing scope
        /// </summary>
        CombineVarStatementsToTopOfScope            = 0x0000010000000000,

        /// <summary>
        /// If the condition of an if-statement or conditional starts with a not-operator,
        /// get rid of the not-operator and swap the true/false branches.
        /// </summary>
        IfNotTrueFalseToIfFalseTrue                 = 0x0000020000000000,
    }
}
