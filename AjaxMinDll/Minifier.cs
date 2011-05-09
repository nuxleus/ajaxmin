// Minifier.cs
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
using System.IO;
using System.Text;

namespace Microsoft.Ajax.Utilities
{
    /// <summary>
    /// Summary description for MainClass.
    /// </summary>
    public class Minifier
    {
        #region Properties

        /// <summary>
        /// Warning level threshold for reporting errors.
        /// Default value is zero: syntax/run-time errors.
        /// </summary>
        public int WarningLevel
        {
            get; set;
        }

        /// <summary>
        /// File name to use in error reporting.
        /// Default value is null: use Minify... method name.
        /// </summary>
        public string FileName
        {
            get; set;
        }

        /// <summary>
        /// Collection of ContextError objects found during minification process
        /// </summary>
        public ICollection<ContextError> ErrorList { get { return m_errorList; } }
        private List<ContextError> m_errorList; // = null;

        /// <summary>
        /// Collection of any error strings found during the crunch process.
        /// Deprecated; do not use this collection. Use the ErrorList collection instead.
        /// </summary>
        [Obsolete("This property is deprecated; use ErrorList instead")]
        public ICollection<string> Errors
        {
            get 
            { 
                var errorList = new List<string>(ErrorList.Count);
                foreach (var error in ErrorList)
                {
                    errorList.Add(error.ToString());
                }
                return errorList;
            }
        }

        #endregion

        #region JavaScript methods

        /// <summary>
        /// MinifyJavaScript JS string passed to it using default code minification settings.
        /// The ErrorList property will be set with any errors found during the minification process.
        /// </summary>
        /// <param name="source">source Javascript</param>
        /// <returns>minified Javascript</returns>
        public string MinifyJavaScript(string source)
        {
            // just pass in default settings
            return MinifyJavaScript(source, new CodeSettings());
        }

        /// <summary>
        /// Crunched JS string passed to it, returning crunched string.
        /// The ErrorList property will be set with any errors found during the minification process.
        /// </summary>
        /// <param name="source">source Javascript</param>
        /// <param name="codeSettings">code minification settings</param>
        /// <returns>minified Javascript</returns>
        public string MinifyJavaScript(string source, CodeSettings codeSettings)
        {
            // default is an empty string
            string crunched = string.Empty;

            // reset the errors builder
            m_errorList = new List<ContextError>();

            // create the parser from the source string.
            // pass null for the assumed globals array
            JSParser parser = new JSParser(source);

            // file context is a property on the parser
            parser.FileContext = FileName;

            // hook the engine error event
            parser.CompilerError += OnJavaScriptError;

            try
            {
                // parse the input
                Block scriptBlock = parser.Parse(codeSettings);
                if (scriptBlock != null)
                {
                    // we'll return the crunched code
                    crunched = scriptBlock.ToCode();
                }
            }
            catch (Exception e)
            {
                m_errorList.Add(new ContextError(
                    true,
                    0,
                    null,
                    null,
                    null,
                    this.FileName,
                    0,
                    0,
                    0,
                    0,
                    e.Message));
            }
            return crunched;
        }

        #endregion

        #region CSS methods

        /// <summary>
        /// MinifyJavaScript CSS string passed to it using default code minification settings.
        /// The ErrorList property will be set with any errors found during the minification process.
        /// </summary>
        /// <param name="source">source Javascript</param>
        /// <returns>minified Javascript</returns>
        public string MinifyStyleSheet(string source)
        {
            // just pass in default settings
            return MinifyStyleSheet(source, new CssSettings());
        }

        /// <summary>
        /// Minifies the CSS stylesheet passes to it using the given settings, returning the minified results
        /// The ErrorList property will be set with any errors found during the minification process.
        /// </summary>
        /// <param name="source">CSS Source</param>
        /// <param name="settings">CSS minification settings</param>
        /// <returns>Minified StyleSheet</returns>
        public string MinifyStyleSheet(string source, CssSettings settings)
        {
            // initialize some values, including the error list (which shoudl start off empty)
            string minifiedResults = string.Empty;
            m_errorList = new List<ContextError>();

            // create the parser object and if we specified some settings,
            // use it to set the Parser's settings object
            CssParser parser = new CssParser();
            parser.FileContext = FileName;
            if (settings != null)
            {
                parser.Settings = settings;
            }

            // hook the error handler
            parser.CssError += new EventHandler<CssErrorEventArgs>(OnCssError);

            // try parsing the source and return the results
            try
            {
                minifiedResults = parser.Parse(source);
            }
            catch (Exception e)
            {
                m_errorList.Add(new ContextError(
                    true,
                    0,
                    null,
                    null,
                    null,
                    this.FileName,
                    0,
                    0,
                    0,
                    0,
                    e.Message));
            }
            return minifiedResults;
        }

        #endregion

        #region Error-handling Members

        private void OnCssError(object sender, CssErrorEventArgs e)
        {
            ContextError error = e.Error;
            if (error.Severity <= WarningLevel)
            {
                m_errorList.Add(error);
            }
        }

        private void OnJavaScriptError(object sender, JScriptExceptionEventArgs e)
        {
            ContextError error = e.Error;
            if (error.Severity <= WarningLevel)
            {
                m_errorList.Add(error);
            }
        }

        #endregion

        #region deprecated methods

        /// <summary>
        /// This method is deprecated. The list of known global names has been moved to the CodeSettings
        /// object. If codeSettings.KnownGlobalNames is null and the globalNames parameter is not, this
        /// API will populate the codeSettings object. Otherwise the globalNames parameter will be ignored.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="codeSettings"></param>
        /// <param name="globalNames"></param>
        /// <returns></returns>
        [Obsolete("This method is deprecated; use the CodeSettings object to pass known global names")]
        public string MinifyJavaScript(string source, CodeSettings codeSettings, params string[] globalNames)
        {
            if (codeSettings != null && codeSettings.KnownGlobalNames == null && globalNames != null)
            {
                codeSettings.SetKnownGlobalNames(globalNames);
            }
            return MinifyJavaScript(source, codeSettings);
        }


        /// <summary>
        /// This method is deprecated. The list of known global names has been moved to the CodeSettings object. 
        /// </summary>
        /// <param name="source">source Javascript</param>
        /// <param name="globalNames">array of known global object names</param>
        /// <returns>minified Javascript</returns>
        [Obsolete("This method is deprecated; use the CodeSettings object to pass known global names")]
        public string MinifyJavaScript(string source, params string[] globalNames)
        {
            // just pass in default settings
            CodeSettings codeSettings = new CodeSettings();
            if (globalNames != null)
            {
                codeSettings.SetKnownGlobalNames(globalNames);
            }
            return MinifyJavaScript(source, codeSettings);
        }

        #endregion
    }

    #region deprecated class

    /// <summary>
    /// This class is deprecated; it will be removed in future versions.
    /// Please use Microsoft.Ajax.Utilities.Minifier instead.
    /// </summary>
    [Obsolete("This class is deprecated; use Microsoft.Ajax.Utilities.Minifier instead")]
    public class ScriptCruncher : Minifier
    {
        /// <summary>
        /// Crunched JS string passed to it using default code settings, returning crunched string.
        /// This API is deprecated and will be removed in future versions. Please use
        /// Microsoft.Ajax.Utilities.Minifier.MinifyJavaScript instead.
        /// </summary>
        /// <param name="source">source Javascript</param>
        /// <returns>crunched Javascript</returns>
        [Obsolete("This API is deprecated; use Microsoft.Ajax.Utilties.Minifier.MinifyJavaScript instead")]
        public string Crunch(string source)
        {
            // just pass in default settings
            return MinifyJavaScript(source, new CodeSettings());
        }

        /// <summary>
        /// Crunched JS string passed to it, returning crunched string.
        /// This API is deprecated and will be removed in future versions. Please use
        /// Microsoft.Ajax.Utilities.Minifier.MinifyJavaScript instead.
        /// </summary>
        /// <param name="source">source Javascript</param>
        /// <param name="codeSettings">code minification settings</param>
        /// <returns>crunched Javascript</returns>
        [Obsolete("This API is deprecated; use Microsoft.Ajax.Utilties.Minifier.MinifyJavaScript instead")]
        public string Crunch(string source, CodeSettings codeSettings)
        {
            // just pass in default settings
            return MinifyJavaScript(source, codeSettings);
        }
    }

    #endregion
}