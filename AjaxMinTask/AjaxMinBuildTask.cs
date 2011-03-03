// AjaxMinBuildTask.cs
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
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.Ajax.Utilities;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System.Security;

namespace Microsoft.Ajax.Minifier.Tasks
{
    /// <summary>
    /// Provides the MS Build task for Microsoft Ajax Minifier. Please see the list of supported properties below.
    /// </summary>
    [SecurityCritical]
    public class AjaxMin : Task
    {
        /// <summary>
        /// Internal js code settings class. Used to store build task parameter values for JS.
        /// </summary>
        private CodeSettings _jsCodeSettings = new CodeSettings();

        /// <summary>
        /// Internal css code settings class. Used to store build task parameter values for CSS.
        /// </summary>
        private CssSettings _cssCodeSettings = new CssSettings();

        /// <summary>
        /// AjaxMin Minifier
        /// </summary>
        private readonly Utilities.Minifier _minifier = new Utilities.Minifier();

        /// <summary>
        /// Warning level threshold for reporting errors. Defalut valus is 0 (syntax/run-time errors)
        /// </summary>
        public int WarningLevel { get; set; }

        #region JavaScript parameters

        /// <summary>
        /// JavaScript source files to minify.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1709:IdentifiersShouldBeCasedCorrectly", MessageId = "Js")]
        public ITaskItem[] JsSourceFiles { get; set; }

        /// <summary>
        /// Target extension for minified JS files
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1709:IdentifiersShouldBeCasedCorrectly", MessageId = "Js")]
        public string JsTargetExtension { get; set; }

        /// <summary>
        /// Source extension pattern for JS files.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1709:IdentifiersShouldBeCasedCorrectly", MessageId = "Js")]
        public string JsSourceExtensionPattern { get; set; }

        /// <summary>
        /// Ensures the final semicolon in minified JS file.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1709:IdentifiersShouldBeCasedCorrectly", MessageId = "Js")]
        public bool JsEnsureFinalSemicolon { get; set;}

        /// <summary>
        /// <see cref="CodeSettings.CollapseToLiteral"/> for more information.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1709:IdentifiersShouldBeCasedCorrectly", MessageId = "Js")]
        public bool JsCollapseToLiteral
        {
            get { return this._jsCodeSettings.CollapseToLiteral;  }
            set { this._jsCodeSettings.CollapseToLiteral = value; }
        }
        
        /// <summary>
        /// <see cref="CodeSettings.CombineDuplicateLiterals"/> for more information.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1709:IdentifiersShouldBeCasedCorrectly", MessageId = "Js")]
        public bool JsCombineDuplicateLiterals
        {
            get { return this._jsCodeSettings.CombineDuplicateLiterals; }
            set { this._jsCodeSettings.CombineDuplicateLiterals = value; }
        }
        
        /// <summary>
        /// <see cref="CodeSettings.EvalTreatment"/> for more information.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "Eval"), System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1709:IdentifiersShouldBeCasedCorrectly", MessageId = "Js")]
        public string JsEvalTreatment
        {
            get { return this._jsCodeSettings.EvalTreatment.ToString(); }
            set { this._jsCodeSettings.EvalTreatment = ParseEnumValue<EvalTreatment>(value); }
        }
        
        /// <summary>
        /// <see cref="CodeSettings.IndentSize"/> for more information.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1709:IdentifiersShouldBeCasedCorrectly", MessageId = "Js")]
        public int JsIndentSize
        {
            get { return this._jsCodeSettings.IndentSize; }
            set { this._jsCodeSettings.IndentSize = value; }
        }
        
        /// <summary>
        /// <see cref="CodeSettings.InlineSafeStrings"/> for more information.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1709:IdentifiersShouldBeCasedCorrectly", MessageId = "Js")]
        public bool JsInlineSafeStrings
        {
            get { return this._jsCodeSettings.InlineSafeStrings; }
            set { this._jsCodeSettings.InlineSafeStrings = value; }
        }
        
        /// <summary>
        /// <see cref="CodeSettings.LocalRenaming"/> for more information.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1709:IdentifiersShouldBeCasedCorrectly", MessageId = "Js")]
        public string JsLocalRenaming
        {
            get { return this._jsCodeSettings.LocalRenaming.ToString(); }
            set { this._jsCodeSettings.LocalRenaming = ParseEnumValue<LocalRenaming>(value); }
        }

        /// <summary>
        /// <see cref="CodeSettings.AddRenamePairs"/> for more information.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1709:IdentifiersShouldBeCasedCorrectly", MessageId = "Js")]
        public string JsManualRenamePairs
        {
            get { return this._jsCodeSettings.RenamePairs; }
            set { this._jsCodeSettings.RenamePairs = value; }
        }

        /// <summary>
        /// <see cref="CodeSettings.SetNoAutoRename"/> for more information.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1709:IdentifiersShouldBeCasedCorrectly", MessageId = "Js")]
        public string JsNoAutoRename
        {
            get { return this._jsCodeSettings.NoAutoRenameList; }
            set { this._jsCodeSettings.NoAutoRenameList = value; }
        }

        /// <summary>
        /// <see cref="CodeSettings.SetKnownGlobalNames"/> for more information.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1709:IdentifiersShouldBeCasedCorrectly", MessageId = "Js")]
        public string JsKnownGlobalNames
        {
            get { return this._jsCodeSettings.KnownGlobalNamesList; }
            set { this._jsCodeSettings.KnownGlobalNamesList = value; }
        }
        
        /// <summary>
        /// <see cref="CodeSettings.MacSafariQuirks"/> for more information.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1709:IdentifiersShouldBeCasedCorrectly", MessageId = "Js")]
        public bool JsMacSafariQuirks
        {
            get { return this._jsCodeSettings.MacSafariQuirks; }
            set { this._jsCodeSettings.MacSafariQuirks = value; }
        }

        /// <summary>
        /// <see cref="CodeSettings.IgnoreConditionalCompilation"/> for more information.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1709:IdentifiersShouldBeCasedCorrectly", MessageId = "Js")]
        public bool JsIgnoreConditionalCompilation
        {
            get { return this._jsCodeSettings.IgnoreConditionalCompilation; }
            set { this._jsCodeSettings.IgnoreConditionalCompilation = value; }
        }

        /// <summary>
        /// <see cref="CodeSettings.MinifyCode"/> for more information.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1709:IdentifiersShouldBeCasedCorrectly", MessageId = "Js")]
        public bool JsMinifyCode
        {
            get { return this._jsCodeSettings.MinifyCode; }
            set { this._jsCodeSettings.MinifyCode = value; }
        }

        /// <summary>
        /// <see cref="CodeSettings.OutputMode"/> for more information.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1709:IdentifiersShouldBeCasedCorrectly", MessageId = "Js")]
        public string JsOutputMode
        {
            get { return this._jsCodeSettings.OutputMode.ToString(); }
            set { this._jsCodeSettings.OutputMode = ParseEnumValue<OutputMode>(value); }
        }

        /// <summary>
        /// <see cref="CodeSettings.PreserveFunctionNames"/> for more information.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1709:IdentifiersShouldBeCasedCorrectly", MessageId = "Js")]
        public bool JsPreserveFunctionNames
        {
            get { return this._jsCodeSettings.PreserveFunctionNames; }
            set { this._jsCodeSettings.PreserveFunctionNames = value; }
        }

        /// <summary>
        /// <see cref="CodeSettings.RemoveFunctionExpressionNames"/> for more information.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1709:IdentifiersShouldBeCasedCorrectly", MessageId = "Js")]
        public bool JsRemoveFunctionExpressionNames
        {
            get { return this._jsCodeSettings.RemoveFunctionExpressionNames; }
            set { this._jsCodeSettings.RemoveFunctionExpressionNames = value; }
        }
        
        /// <summary>
        /// <see cref="CodeSettings.RemoveUnneededCode"/> for more information.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1709:IdentifiersShouldBeCasedCorrectly", MessageId = "Js")]
        public bool JsRemoveUnneededCode
        {
            get { return this._jsCodeSettings.RemoveUnneededCode; }
            set { this._jsCodeSettings.RemoveUnneededCode = value; }
        }
        
        /// <summary>
        /// <see cref="CodeSettings.StripDebugStatements"/> for more information.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1709:IdentifiersShouldBeCasedCorrectly", MessageId = "Js")]
        public bool JsStripDebugStatements
        {
            get { return this._jsCodeSettings.StripDebugStatements; }
            set { this._jsCodeSettings.StripDebugStatements = value; }
        }
        
        /// <summary>
        /// <see cref="CodeSettings.AllowEmbeddedAspNetBlocks"/> for more information.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1709:IdentifiersShouldBeCasedCorrectly", MessageId = "Js")]
        public bool JsAllowEmbeddedAspNetBlocks
        {
            get { return this._jsCodeSettings.AllowEmbeddedAspNetBlocks; }
            set { this._jsCodeSettings.AllowEmbeddedAspNetBlocks = value; }
        }

        #endregion

        #region CSS parameters

        /// <summary>
        /// CSS source files to minify.
        /// </summary>
        public ITaskItem[] CssSourceFiles { get; set; }

        /// <summary>
        /// Target extension for minified CSS files
        /// </summary>
        public string CssTargetExtension { get; set; }

        /// <summary>
        /// Source extension pattern for CSS files.
        /// </summary>
        public string CssSourceExtensionPattern { get; set; }

        /// <summary>
        /// <see cref="CssSettings.ColorNames"/> for more information.
        /// </summary>
        public string CssColorNames
        {
            get { return this._cssCodeSettings.ColorNames.ToString(); }
            set { this._cssCodeSettings.ColorNames = ParseEnumValue<CssColor>(value); }
        }
        
        /// <summary>
        /// <see cref="CssSettings.CommentMode"/> for more information.
        /// </summary>
        public string CssCommentMode
        {
            get { return this._cssCodeSettings.CommentMode.ToString(); }
            set { this._cssCodeSettings.CommentMode = ParseEnumValue<CssComment>(value); }
        }
        
        /// <summary>
        /// <see cref="CssSettings.ExpandOutput"/> for more information.
        /// </summary>
        public bool CssExpandOutput
        {
            get { return this._cssCodeSettings.ExpandOutput; }
            set { this._cssCodeSettings.ExpandOutput = value; }
        }

        /// <summary>
        /// <see cref="CssSettings.IndentSpaces"/> for more information.
        /// </summary>
        public int CssIndentSpaces
        {
            get { return this._cssCodeSettings.IndentSpaces; }
            set { this._cssCodeSettings.IndentSpaces = value; }
        }

        /// <summary>
        /// <see cref="CssSettings.Severity"/> for more information.
        /// </summary>
        public int CssSeverity
        {
            get { return this._cssCodeSettings.Severity; }
            set { this._cssCodeSettings.Severity = value; }
        }
        
        /// <summary>
        /// <see cref="CssSettings.TermSemicolons"/> for more information.
        /// </summary>
        public bool CssTermSemicolons
        {
            get { return this._cssCodeSettings.TermSemicolons; }
            set { this._cssCodeSettings.TermSemicolons = value; }
        }

        /// <summary>
        /// <see cref="CssSettings.MinifyExpressions"/> for more information.
        /// </summary>
        public bool CssMinifyExpressions
        {
            get { return this._cssCodeSettings.MinifyExpressions; }
            set { this._cssCodeSettings.MinifyExpressions = value; }
        }

        /// <summary>
        /// <see cref="CssSettings.AllowEmbeddedAspNetBlocks"/> for more information.
        /// </summary>
        public bool CssAllowEmbeddedAspNetBlocks
        {
            get { return this._cssCodeSettings.AllowEmbeddedAspNetBlocks; }
            set { this._cssCodeSettings.AllowEmbeddedAspNetBlocks = value; }
        }

        #endregion

        /// <summary>
        /// Constructor for <see cref="AjaxMin"/> class. Initializes the default
        /// values for all parameters.
        /// </summary>
        public AjaxMin()
        {
            this.JsEnsureFinalSemicolon = true;
        }

        /// <summary>
        /// Executes the Ajax Minifier build task
        /// </summary>
        /// <returns>True if the build task successfully succeded; otherwise, false.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA2204:Literals should be spelled correctly", MessageId = "Minifier"), System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA2204:Literals should be spelled correctly", MessageId = "JsTargetExtension"), System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA2204:Literals should be spelled correctly", MessageId = "JsSourceExtensionPattern"), System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA2204:Literals should be spelled correctly", MessageId = "CssTargetExtension"), System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA2204:Literals should be spelled correctly", MessageId = "CssSourceExtensionPattern"), System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Globalization", "CA1303:Do not pass literals as localized parameters", MessageId = "Microsoft.Build.Utilities.TaskLoggingHelper.LogError(System.String,System.Object[])")]
        [SecurityCritical]
        public override bool Execute()
        {
            if (this.Log.HasLoggedErrors)
            {
                return false;
            }

            _minifier.WarningLevel = this.WarningLevel;

            // Deal with JS minification
            if (this.JsSourceFiles != null && this.JsSourceFiles.Length > 0)
            {
                if (string.IsNullOrWhiteSpace(this.JsSourceExtensionPattern))
                {
                    Log.LogError("Microsoft Ajax Minifier: You must supply a value for JsSourceExtensionPattern.",
                                 new object[0]);
                    return false;
                }

                if (string.IsNullOrWhiteSpace(this.JsTargetExtension))
                {
                    Log.LogError("Microsoft Ajax Minifier: You must supply a value for JsTargetExtension.",
                                 new object[0]);
                    return false;
                }

                MinifyJavaScript();
            }

            // Deal with CSS minification
            if (this.CssSourceFiles != null && this.CssSourceFiles.Length > 0)
            {
                if (string.IsNullOrWhiteSpace(this.CssSourceExtensionPattern))
                {
                    Log.LogError("Microsoft Ajax Minifier: You must supply a value for CssSourceExtensionPattern.",
                                 new object[0]);
                    return false;
                }

                if (string.IsNullOrWhiteSpace(this.CssTargetExtension))
                {
                    Log.LogError("Microsoft Ajax Minifier: You must supply a value for CssTargetExtension.",
                                 new object[0]);
                    return false;
                }

                MinifyStyleSheets();
            }

            return true;
        }

        /// <summary>
        /// Minifies JS files provided by the caller of the build task.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA2204:Literals should be spelled correctly", MessageId = "Minifier"), System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Globalization", "CA1303:Do not pass literals as localized parameters", MessageId = "Microsoft.Build.Utilities.TaskLoggingHelper.LogError(System.String,System.Object[])")]
        private void MinifyJavaScript()
        {
            foreach (ITaskItem item in this.JsSourceFiles)
            {
                string path = Regex.Replace(item.ItemSpec, this.JsSourceExtensionPattern, this.JsTargetExtension,
                                            RegexOptions.IgnoreCase);
                try
                {
                    string source = File.ReadAllText(item.ItemSpec);
                    string minifiedJs = this._minifier.MinifyJavaScript(source, this._jsCodeSettings);
                    if (this._minifier.Errors.Count > 0)
                    {
                        base.Log.LogError("Microsoft Ajax Minifier: Could not minify {0}", new object[] { item.ItemSpec });
                        foreach (string error in this._minifier.Errors)
                        {
                            base.Log.LogError("Error Message: {0}", new object[] { error });
                        }
                    }
                    else
                    {
                        if (this.JsEnsureFinalSemicolon && !string.IsNullOrEmpty(minifiedJs))
                        {
                            minifiedJs = minifiedJs + ";";
                        }
                        try
                        {
                            File.WriteAllText(path, minifiedJs);
                        }
                        catch (UnauthorizedAccessException)
                        {
                            base.Log.LogError("The Microsoft Ajax Minifier does not have permission to write to {0}",
                                              new object[] { path });
                        }
                    }
                }
                catch (Exception)
                {
                    base.Log.LogError("The Microsoft Ajax Minifier was not able to minify {0}",
                                      new object[] { path });
                    throw;
                }
            }
        }

        /// <summary>
        /// Minifies CSS files provided by the caller of the build task.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA2204:Literals should be spelled correctly", MessageId = "Minifier"), System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Globalization", "CA1303:Do not pass literals as localized parameters", MessageId = "Microsoft.Build.Utilities.TaskLoggingHelper.LogError(System.String,System.Object[])")]
        private void MinifyStyleSheets()
        {
            foreach (ITaskItem item in this.CssSourceFiles)
            {
                string path = Regex.Replace(item.ItemSpec, this.CssSourceExtensionPattern, this.CssTargetExtension, RegexOptions.IgnoreCase);

                try
                {
                    string source = File.ReadAllText(item.ItemSpec);
                    string contents = this._minifier.MinifyStyleSheet(source, this._cssCodeSettings);
                    if (this._minifier.Errors.Count > 0)
                    {
                        base.Log.LogError("Microsoft Ajax Minifier: Could not minify {0}", new object[] {item.ItemSpec});
                        foreach (string error in this._minifier.Errors)
                        {
                            base.Log.LogError("Error Message: {0}", new object[] {error});
                        }
                    }
                    else
                    {
                        try
                        {
                            File.WriteAllText(path, contents);
                        }
                        catch (UnauthorizedAccessException)
                        {
                            base.Log.LogError("The Microsoft Ajax Minifier does not have permission to write to {0}",
                                              new object[] {path});
                        }
                    }
                }
                catch (Exception)
                {
                    base.Log.LogError("The Microsoft Ajax Minifier was not able to minify {0}", new object[] { path });
                    throw;
                }
            }
        }

        /// <summary>
        /// Parses the enum value of the given enum type from the string.
        /// </summary>
        /// <typeparam name="T">Type of the enum.</typeparam>
        /// <param name="strValue">Value of the parameter in the string form.</param>
        /// <returns>Parsed enum value</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Globalization", "CA1303:Do not pass literals as localized parameters", MessageId = "Microsoft.Build.Utilities.TaskLoggingHelper.LogError(System.String,System.Object[])")]
        private T ParseEnumValue<T>(string strValue) where T: struct
        {
            if (!string.IsNullOrWhiteSpace(strValue))
            {
                T result;
                if (Enum.TryParse(strValue, true, out result))
                {
                    return result;
                }

            }

            // if we cannot parse it for any reason, post the error and stop the task.
            base.Log.LogError("Invalid input parameter {0}", strValue);
            return default(T);
        }
    }
}
