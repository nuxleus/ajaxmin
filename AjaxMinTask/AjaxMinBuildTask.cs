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
using System.Security;
using System.Text.RegularExpressions;
using Microsoft.Ajax.Utilities;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.Ajax.Minifier.Tasks
{
    public static class StringExtension
    {
        public static bool IsNullOrWhiteSpace(this string value)
        {
            return string.IsNullOrEmpty(value) || value.Trim().Length == 0;
        }
    }

    /// <summary>
    /// Provides the MS Build task for Microsoft Ajax Minifier. Please see the list of supported properties below.
    /// </summary>
    [SecurityCritical]
    public class AjaxMin : Task
    {
        #region private fields

        /// <summary>
        /// Internal js code settings class. Used to store build task parameter values for JS.
        /// </summary>
        private CodeSettings m_jsCodeSettings = new CodeSettings();

        /// <summary>
        /// Internal css code settings class. Used to store build task parameter values for CSS.
        /// </summary>
        private CssSettings m_cssCodeSettings = new CssSettings();

        /// <summary>
        /// AjaxMin Minifier
        /// </summary>
        private readonly Utilities.Minifier m_minifier = new Utilities.Minifier();

        #endregion

        #region Common properties

        /// <summary>
        /// Warning level threshold for reporting errors. Defalut valus is 0 (syntax/run-time errors)
        /// </summary>
        public int WarningLevel { get; set; }

        /// <summary>
        /// Whether to treat AjaxMin warnings as build errors (true) or not (false). Default value is false.
        /// </summary>
        public bool TreatWarningsAsErrors { get; set; }

        #endregion

        #region JavaScript parameters

        /// <summary>
        /// JavaScript source files to minify.
        /// </summary>
        public ITaskItem[] JsSourceFiles { get; set; }

        /// <summary>
        /// Target extension for minified JS files
        /// </summary>
        public string JsTargetExtension { get; set; }

        /// <summary>
        /// Source extension pattern for JS files.
        /// </summary>
        public string JsSourceExtensionPattern { get; set; }

        /// <summary>
        /// Ensures the final semicolon in minified JS file.
        /// </summary>
        public bool JsEnsureFinalSemicolon { get; set;}

        /// <summary>
        /// <see cref="CodeSettings.CollapseToLiteral"/> for more information.
        /// </summary>
        public bool JsCollapseToLiteral
        {
            get { return this.m_jsCodeSettings.CollapseToLiteral;  }
            set { this.m_jsCodeSettings.CollapseToLiteral = value; }
        }
        
        /// <summary>
        /// <see cref="CodeSettings.CombineDuplicateLiterals"/> for more information.
        /// </summary>
        public bool JsCombineDuplicateLiterals
        {
            get { return this.m_jsCodeSettings.CombineDuplicateLiterals; }
            set { this.m_jsCodeSettings.CombineDuplicateLiterals = value; }
        }
        
        /// <summary>
        /// <see cref="CodeSettings.EvalTreatment"/> for more information.
        /// </summary>
        public string JsEvalTreatment
        {
            get { return this.m_jsCodeSettings.EvalTreatment.ToString(); }
            set { this.m_jsCodeSettings.EvalTreatment = ParseEnumValue<EvalTreatment>(value); }
        }
        
        /// <summary>
        /// <see cref="CodeSettings.IndentSize"/> for more information.
        /// </summary>
        public int JsIndentSize
        {
            get { return this.m_jsCodeSettings.IndentSize; }
            set { this.m_jsCodeSettings.IndentSize = value; }
        }
        
        /// <summary>
        /// <see cref="CodeSettings.InlineSafeStrings"/> for more information.
        /// </summary>
        public bool JsInlineSafeStrings
        {
            get { return this.m_jsCodeSettings.InlineSafeStrings; }
            set { this.m_jsCodeSettings.InlineSafeStrings = value; }
        }
        
        /// <summary>
        /// <see cref="CodeSettings.LocalRenaming"/> for more information.
        /// </summary>
        public string JsLocalRenaming
        {
            get { return this.m_jsCodeSettings.LocalRenaming.ToString(); }
            set { this.m_jsCodeSettings.LocalRenaming = ParseEnumValue<LocalRenaming>(value); }
        }

        /// <summary>
        /// <see cref="CodeSettings.AddRenamePairs"/> for more information.
        /// </summary>
        public string JsManualRenamePairs
        {
            get { return this.m_jsCodeSettings.RenamePairs; }
            set { this.m_jsCodeSettings.RenamePairs = value; }
        }

        /// <summary>
        /// <see cref="CodeSettings.SetNoAutoRename"/> for more information.
        /// </summary>
        public string JsNoAutoRename
        {
            get { return this.m_jsCodeSettings.NoAutoRenameList; }
            set { this.m_jsCodeSettings.NoAutoRenameList = value; }
        }

        /// <summary>
        /// <see cref="CodeSettings.SetKnownGlobalNames"/> for more information.
        /// </summary>
        public string JsKnownGlobalNames
        {
            get { return this.m_jsCodeSettings.KnownGlobalNamesList; }
            set { this.m_jsCodeSettings.KnownGlobalNamesList = value; }
        }

        /// <summary>
        /// <see cref="CodeSettings.SetKnownGlobalNames"/> for more information.
        /// </summary>
        public string JsDebugLookups
        {
            get { return this.m_jsCodeSettings.DebugLookupList; }
            set { this.m_jsCodeSettings.DebugLookupList = value; }
        }
        
        /// <summary>
        /// <see cref="CodeSettings.MacSafariQuirks"/> for more information.
        /// </summary>
        public bool JsMacSafariQuirks
        {
            get { return this.m_jsCodeSettings.MacSafariQuirks; }
            set { this.m_jsCodeSettings.MacSafariQuirks = value; }
        }

        /// <summary>
        /// <see cref="CodeSettings.IgnoreConditionalCompilation"/> for more information.
        /// </summary>
        public bool JsIgnoreConditionalCompilation
        {
            get { return this.m_jsCodeSettings.IgnoreConditionalCompilation; }
            set { this.m_jsCodeSettings.IgnoreConditionalCompilation = value; }
        }

        /// <summary>
        /// <see cref="CodeSettings.MinifyCode"/> for more information.
        /// </summary>
        public bool JsMinifyCode
        {
            get { return this.m_jsCodeSettings.MinifyCode; }
            set { this.m_jsCodeSettings.MinifyCode = value; }
        }

        /// <summary>
        /// <see cref="CodeSettings.OutputMode"/> for more information.
        /// </summary>
        public string JsOutputMode
        {
            get { return this.m_jsCodeSettings.OutputMode.ToString(); }
            set { this.m_jsCodeSettings.OutputMode = ParseEnumValue<OutputMode>(value); }
        }

        /// <summary>
        /// <see cref="CodeSettings.PreserveFunctionNames"/> for more information.
        /// </summary>
        public bool JsPreserveFunctionNames
        {
            get { return this.m_jsCodeSettings.PreserveFunctionNames; }
            set { this.m_jsCodeSettings.PreserveFunctionNames = value; }
        }

        /// <summary>
        /// <see cref="CodeSettings.RemoveFunctionExpressionNames"/> for more information.
        /// </summary>
        public bool JsRemoveFunctionExpressionNames
        {
            get { return this.m_jsCodeSettings.RemoveFunctionExpressionNames; }
            set { this.m_jsCodeSettings.RemoveFunctionExpressionNames = value; }
        }
        
        /// <summary>
        /// <see cref="CodeSettings.RemoveUnneededCode"/> for more information.
        /// </summary>
        public bool JsRemoveUnneededCode
        {
            get { return this.m_jsCodeSettings.RemoveUnneededCode; }
            set { this.m_jsCodeSettings.RemoveUnneededCode = value; }
        }
        
        /// <summary>
        /// <see cref="CodeSettings.StripDebugStatements"/> for more information.
        /// </summary>
        public bool JsStripDebugStatements
        {
            get { return this.m_jsCodeSettings.StripDebugStatements; }
            set { this.m_jsCodeSettings.StripDebugStatements = value; }
        }

        /// <summary>
        /// <see cref="CodeSettings.AllowEmbeddedAspNetBlocks"/> for more information.
        /// </summary>
        public bool JsAllowEmbeddedAspNetBlocks
        {
            get { return this.m_jsCodeSettings.AllowEmbeddedAspNetBlocks; }
            set { this.m_jsCodeSettings.AllowEmbeddedAspNetBlocks = value; }
        }

        /// <summary>
        /// <see cref="CodeSettings.PreprocessorDefineList"/> for more information.
        /// </summary>
        public string JsPreprocessorDefines
        {
            get { return this.m_jsCodeSettings.PreprocessorDefineList; }
            set { this.m_jsCodeSettings.PreprocessorDefineList = value; }
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
            get { return this.m_cssCodeSettings.ColorNames.ToString(); }
            set { this.m_cssCodeSettings.ColorNames = ParseEnumValue<CssColor>(value); }
        }
        
        /// <summary>
        /// <see cref="CssSettings.CommentMode"/> for more information.
        /// </summary>
        public string CssCommentMode
        {
            get { return this.m_cssCodeSettings.CommentMode.ToString(); }
            set { this.m_cssCodeSettings.CommentMode = ParseEnumValue<CssComment>(value); }
        }
        
        /// <summary>
        /// <see cref="CssSettings.ExpandOutput"/> for more information.
        /// </summary>
        public bool CssExpandOutput
        {
            get { return this.m_cssCodeSettings.ExpandOutput; }
            set { this.m_cssCodeSettings.ExpandOutput = value; }
        }

        /// <summary>
        /// <see cref="CssSettings.IndentSpaces"/> for more information.
        /// </summary>
        public int CssIndentSpaces
        {
            get { return this.m_cssCodeSettings.IndentSpaces; }
            set { this.m_cssCodeSettings.IndentSpaces = value; }
        }
        
        /// <summary>
        /// <see cref="CssSettings.TermSemicolons"/> for more information.
        /// </summary>
        public bool CssTermSemicolons
        {
            get { return this.m_cssCodeSettings.TermSemicolons; }
            set { this.m_cssCodeSettings.TermSemicolons = value; }
        }

        /// <summary>
        /// <see cref="CssSettings.MinifyExpressions"/> for more information.
        /// </summary>
        public bool CssMinifyExpressions
        {
            get { return this.m_cssCodeSettings.MinifyExpressions; }
            set { this.m_cssCodeSettings.MinifyExpressions = value; }
        }

        /// <summary>
        /// <see cref="CssSettings.AllowEmbeddedAspNetBlocks"/> for more information.
        /// </summary>
        public bool CssAllowEmbeddedAspNetBlocks
        {
            get { return this.m_cssCodeSettings.AllowEmbeddedAspNetBlocks; }
            set { this.m_cssCodeSettings.AllowEmbeddedAspNetBlocks = value; }
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
        [SecurityCritical]
        public override bool Execute()
        {
            m_minifier.WarningLevel = this.WarningLevel;

            // Deal with JS minification
            if (this.JsSourceFiles != null && this.JsSourceFiles.Length > 0)
            {
                if (this.JsSourceExtensionPattern.IsNullOrWhiteSpace())
                {
                    LogTaskError(StringEnum.RequiredParameterIsEmpty, "JsSourceExtensionPattern");
                    return false;
                }

                if (this.JsTargetExtension.IsNullOrWhiteSpace())
                {
                    LogTaskError(StringEnum.RequiredParameterIsEmpty, "JsTargetExtension");
                    return false;
                }

                MinifyJavaScript();
            }

            // Deal with CSS minification
            if (this.CssSourceFiles != null && this.CssSourceFiles.Length > 0)
            {
                if (this.CssSourceExtensionPattern.IsNullOrWhiteSpace())
                {
                    LogTaskError(StringEnum.RequiredParameterIsEmpty, "CssSourceExtensionPattern");
                    return false;
                }

                if (this.CssTargetExtension.IsNullOrWhiteSpace())
                {
                    LogTaskError(StringEnum.RequiredParameterIsEmpty, "CssTargetExtension");
                    return false;
                }

                MinifyStyleSheets();
            }

            return !Log.HasLoggedErrors;
        }

        /// <summary>
        /// Minifies JS files provided by the caller of the build task.
        /// </summary>
        private void MinifyJavaScript()
        {
            foreach (ITaskItem item in this.JsSourceFiles)
            {
                string path = Regex.Replace(item.ItemSpec, this.JsSourceExtensionPattern, this.JsTargetExtension,
                                            RegexOptions.IgnoreCase);
                if (FileIsWritable(path))
                {
                    try
                    {
                        string source = File.ReadAllText(item.ItemSpec);
                        this.m_minifier.FileName = item.ItemSpec;
                        string minifiedJs = this.m_minifier.MinifyJavaScript(source, this.m_jsCodeSettings);
                        if (this.m_minifier.ErrorList.Count > 0)
                        {
                            foreach (var error in this.m_minifier.ErrorList)
                            {
                                LogContextError(error);
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
                                LogFileError(item.ItemSpec, StringEnum.NoWritePermission, path);
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        LogFileError(item.ItemSpec, StringEnum.DidNotMinify, path, e.Message);
                        throw;
                    }
                }
                else
                {
                    // log a MESSAGE that the minification was skipped -- don't break the build
                    Log.LogMessage(MessageImportance.High, StringManager.GetString(StringEnum.DestinationIsReadOnly, Path.GetFileName(item.ItemSpec), path));
                }
            }
        }

        /// <summary>
        /// Minifies CSS files provided by the caller of the build task.
        /// </summary>
        private void MinifyStyleSheets()
        {
            foreach (ITaskItem item in this.CssSourceFiles)
            {
                string path = Regex.Replace(item.ItemSpec, this.CssSourceExtensionPattern, this.CssTargetExtension, RegexOptions.IgnoreCase);
                if (FileIsWritable(path))
                {
                    try
                    {
                        string source = File.ReadAllText(item.ItemSpec);
                        this.m_minifier.FileName = item.ItemSpec;
                        string contents = this.m_minifier.MinifyStyleSheet(source, this.m_cssCodeSettings);
                        if (this.m_minifier.ErrorList.Count > 0)
                        {
                            foreach (var error in this.m_minifier.ErrorList)
                            {
                                LogContextError(error);
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
                                LogFileError(item.ItemSpec, StringEnum.NoWritePermission, path);
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        LogFileError(item.ItemSpec, StringEnum.DidNotMinify, path, e.Message);
                        throw;
                    }
                }
                else
                {
                    // log a MESSAGE that the minification was skipped -- don't break the build
                    Log.LogMessage(MessageImportance.High, StringManager.GetString(StringEnum.DestinationIsReadOnly, Path.GetFileName(item.ItemSpec), path));
                }
            }
        }

        #region Logging methods

        /// <summary>
        /// Call this method to log an error against the build task itself, before any specific files are processed
        /// </summary>
        /// <param name="messageIdentifier">String resource identifier</param>
        /// <param name="messageArguments">any optional formatting arguments</param>
        private void LogTaskError(StringEnum messageIdentifier, params object[] messageArguments)
        {
            var message = StringManager.GetString(messageIdentifier);
            Log.LogError(message, messageArguments);
        }

        /// <summary>
        /// Call this method to log an error against the build of a particular source file
        /// </summary>
        /// <param name="path">path of the input source file</param>
        /// <param name="messageIdentifier">String resource identifier</param>
        /// <param name="messageArguments">any optional formatting arguments</param>
        private void LogFileError(string path, StringEnum messageIdentifier, params object[] messageArguments)
        {
            var message = StringManager.GetString(messageIdentifier);
            Log.LogError(
                null,
                null,
                null,
                path,
                0,
                0,
                0,
                0,
                message, 
                messageArguments);
        }

        /// <summary>
        /// Call this method to log an error using a ContentError object
        /// </summary>
        /// <param name="error">Error to log</param>
        private void LogContextError(ContextError error)
        {
            // log it either as an error or a warning
            if(TreatWarningsAsErrors || error.IsError)
            {
                Log.LogError(
                    error.Subcategory,  // subcategory 
                    error.ErrorCode,    // error code
                    error.HelpKeyword,  // help keyword
                    error.File,         // file
                    error.StartLine,    // start line
                    error.StartColumn,  // start column
                    error.EndLine > error.StartLine ? error.EndLine : 0,      // end line
                    error.EndLine > error.StartLine || error.EndColumn > error.StartColumn ? error.EndColumn : 0,    // end column
                    error.Message       // message
                    );
            }
            else
            {
                Log.LogWarning(
                    error.Subcategory,  // subcategory 
                    error.ErrorCode,    // error code
                    error.HelpKeyword,  // help keyword
                    error.File,         // file
                    error.StartLine,    // start line
                    error.StartColumn,  // start column
                    error.EndLine > error.StartLine ? error.EndLine : 0,      // end line
                    error.EndLine > error.StartLine || error.EndColumn > error.StartColumn ? error.EndColumn : 0,    // end column
                    error.Message       // message
                    );
            }
        }

        #endregion

        #region Utility methods

        private static bool FileIsWritable(string path)
        {
            var fileInfo = new FileInfo(path);
            return !(fileInfo.Exists && fileInfo.IsReadOnly);
        }

        /// <summary>
        /// Parses the enum value of the given enum type from the string.
        /// </summary>
        /// <typeparam name="T">Type of the enum.</typeparam>
        /// <param name="strValue">Value of the parameter in the string form.</param>
        /// <returns>Parsed enum value</returns>
        private T ParseEnumValue<T>(string strValue) where T: struct
        {
            if (!strValue.IsNullOrWhiteSpace())
            {
                try
                {
                    return (T)Enum.Parse(typeof(T), strValue, true);
                }
                catch (ArgumentNullException) { }
                catch (ArgumentException) { }
                catch (OverflowException) { }
            }

            // if we cannot parse it for any reason, post the error and stop the task.
            LogTaskError(StringEnum.InvalidInputParameter, strValue);
            return default(T);
        }

        #endregion
    }
}
