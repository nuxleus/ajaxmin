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

        private int ProcessCssFile(string sourceFileName, ResourceStrings resourceStrings, StringBuilder outputBuilder, ref long sourceLength)
        {
            int retVal = 0;
            try
            {
                // read our chunk of code
                string source;
                if (sourceFileName.Length > 0)
                {
                    using (StreamReader reader = new StreamReader(sourceFileName, m_encodingInput))
                    {
                        WriteProgress(
                          StringMgr.GetString("CrunchingFile", Path.GetFileName(sourceFileName))
                          );
                        source = reader.ReadToEnd();
                    }

                    // add the actual file length in to the input source length
                    FileInfo inputFileInfo = new FileInfo(sourceFileName);
                    sourceLength += inputFileInfo.Length;
                }
                else
                {
                    WriteProgress(StringMgr.GetString("CrunchingStdIn"));
                    try
                    {
                        // try setting the input encoding
                        Console.InputEncoding = m_encodingInput;
                    }
                    catch (IOException e)
                    {
                        // error setting the encoding input; just use whatever the default is
                        Debug.WriteLine(e.ToString());
                    }
                    source = Console.In.ReadToEnd();

                    if (m_analyze)
                    {
                        // calculate the actual number of bytes read using the input encoding
                        // and the string that we just read and
                        // add the number of bytes read into the input length.
                        sourceLength += Console.InputEncoding.GetByteCount(source);
                    }
                    else
                    {
                        // don't bother calculating the actual bytes -- the number of characters
                        // is sufficient if we're not doing the analysis
                        sourceLength += source.Length;
                    }
                }

                // process input source...
                CssParser parser = new CssParser();
                parser.CssError += new EventHandler<CssErrorEventArgs>(OnCssError);
                parser.FileContext = sourceFileName;

                parser.Settings.CommentMode = m_cssComments;
                parser.Settings.ExpandOutput = m_prettyPrint;
                parser.Settings.IndentSpaces = m_indentSize;
                parser.Settings.Severity = m_warningLevel;
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
                WriteError(CreateBuildError(
                    null,
                    null,
                    true,
                    "AM-IO",
                    e.Message
                    ));
            }

            return retVal;
        }

        void OnCssError(object sender, CssErrorEventArgs e)
        {
            CssException error = e.Exception;
            // ignore severity values greater than our severity level
            if (error.Severity <= m_warningLevel)
            {
                // we found an error
                m_errorsFound = true;

                // the error code is the lower half of the error number, in decimal, prepended with "JS"
                // again, NOT LOCALIZABLE so the format is not in the resources
                string code = string.Format(
                    CultureInfo.InvariantCulture,
                    "CSS{0}",
                    (error.Error & (0xffff))
                    );

                // the location is the file name followed by the line and start/end columns within parens.
                // if the file context is empty, use "stdin" as the file name.
                // this string is NOT LOCALIZABLE, so not putting the format in the resources
                string context = ((CssParser)sender).FileContext;
                string location = string.Format(
                    CultureInfo.InvariantCulture,
                    "{0}({1},{2})",
                    (string.IsNullOrEmpty(context) ? "stdin" : context),
                    error.Line,
                    error.Char
                    );

                WriteError(CreateBuildError(
                    location,
                    GetSeverityString(error.Severity),
                    (error.Severity < 2), // severity 0 and 1 are errors; rest are warnings
                    code,
                    error.Message
                    ));
                WriteError(string.Empty);
            }
        }

        #endregion
    }
}
