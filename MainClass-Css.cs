// MainClass-Css.cs
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
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;

namespace Microsoft.Ajax.Utilities
{
    public partial class MainClass
    {
        #region CSS-only settings

        // how to treat comments in the sources.
        // by default, we want to keep important comments.
        private CssComment m_cssComments = CssComment.Important;

        // whether to use color names as possible output values
        // (rather than always using #rrggbb or #rgb)
        private CssColor m_colorNames;// = CssColor.Strict;

        // how to treat content of expression functions
        // default is to try to minify it.
        private bool m_minifyExpressions = true;

        #endregion

        #region ProcessCssFile method

        private int ProcessCssFile(string sourceFileName, string encodingName, ResourceStrings resourceStrings, StringBuilder outputBuilder, ref long sourceLength)
        {
            int retVal = 0;

            // blank line before
            WriteProgress();

            try
            {
                // read the input file
                var source = ReadInputFile(sourceFileName, encodingName, ref sourceLength);

                // process input source...
                CssParser parser = new CssParser();
                parser.CssError += new EventHandler<CssErrorEventArgs>(OnCssError);
                parser.FileContext = string.IsNullOrEmpty(sourceFileName) ? "stdin" : sourceFileName;

                parser.Settings.CommentMode = m_cssComments;
                parser.Settings.ExpandOutput = m_prettyPrint;
                parser.Settings.IndentSpaces = m_indentSize;
                parser.Settings.TermSemicolons = m_terminateWithSemicolon;
                parser.Settings.ColorNames = m_colorNames;
                parser.Settings.MinifyExpressions = m_minifyExpressions;
				parser.Settings.AllowEmbeddedAspNetBlocks = m_allowAspNet;
                parser.ValueReplacements = resourceStrings;

                // if the kill switch was set to 1 (don't preserve important comments), then
                // we just want to set the comment mode to none, regardless of what the actual comment
                // mode may be. 
                if ((m_killSwitch & 1) != 0)
                {
                    parser.Settings.CommentMode = CssComment.None;
                }

                // crunch the source and output to the string builder we were passed
                string crunchedStyles = parser.Parse(source);
                if (crunchedStyles != null)
                {
                    Debug.WriteLine(crunchedStyles);                  
                }
                else
                {
                    // there was an error and no output was generated
                    retVal = 1;
                }

                if (m_echoInput)
                {
                    // just echo the input to the output
                    outputBuilder.Append(source);
                }
                else if (!string.IsNullOrEmpty(crunchedStyles))
                {
                    // send the crunched styles to the output
                    outputBuilder.Append(crunchedStyles);
                }
            }
            catch (IOException e)
            {
                // probably an error with the input file
                retVal = 1;
                System.Diagnostics.Debug.WriteLine(e.ToString());
                WriteError("AM-IO", e.Message);
            }

            return retVal;
        }

        void OnCssError(object sender, CssErrorEventArgs e)
        {
            ContextError error = e.Error;
            // ignore severity values greater than our severity level
            if (error.Severity <= m_warningLevel)
            {
                // we found an error
                m_errorsFound = true;

                WriteError(error.ToString());
            }
        }

        #endregion
    }
}
