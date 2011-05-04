// MainClass.cs
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
using System.IO.Compression;
using System.Reflection;
using System.Resources;
using System.Runtime.Serialization;
using System.Security;
using System.Text;
using System.Xml;

namespace Microsoft.Ajax.Utilities
{
    /// <summary>
    /// Application entry point
    /// </summary>
    public partial class MainClass
    {
        #region common fields

        // default resource object name if not specified
        private const string c_defaultResourceObjectName = "Strings";

        /// <summary>
        /// This field is initially false, and it set to true if any errors were
        /// found parsing the javascript. The return value for the application
        /// will be set to non-zero if this flag is true.
        /// Use the -W argument to limit the severity of errors caught by this flag.
        /// </summary>
        private bool m_errorsFound;// = false;

        /// <summary>
        /// Set to true when header is written
        /// </summary>
        private bool m_headerWritten;

        /// <summary>
        /// list of preprocessor "defines" specified on the command-line
        /// </summary>
        private List<string> m_defines;

        #endregion

        #region common settings

        // whether to clobber existing output files
        private bool m_clobber; // = false

        // Whether to allow embedded asp.net blocks.
        private bool m_allowAspNet; // = false

        /// <summary>
        /// Bitfield for turning off individual AST modifications if so desired
        /// </summary>
        private long m_killSwitch;// = 0;

        // simply echo the input code, not the crunched code
        private bool m_echoInput;// = false;

        // encoding to use on input
        private Encoding m_encodingInput;// = null;

        // encoding to use for output file
        private Encoding m_encodingOutput;// = null;
        private string m_encodingOutputName;// = null;

        // "pretty" indent size
        private int m_indentSize = 4;

        /// <summary>
        /// File name of the source file or directory (if in recursive mode)
        /// </summary>
        private string[] m_inputFiles;// = null;

        /// <summary>
        /// Input type: JS or CSS
        /// </summary>
        private InputType m_inputType = InputType.Unknown;

        /// <summary>
        /// Output mode
        /// </summary>
        private ConsoleOutputMode m_outputMode = ConsoleOutputMode.Console;

        /// <summary>
        /// Whether or not we are outputting the crunched code to one or more files (false) or to stdout (true)
        /// </summary>
        private bool m_outputToStandardOut;// = false;

        // output "pretty" instead of crunched
        private bool m_prettyPrint;// = false;

        /// <summary>
        /// Optional file name of the destination file. Must be blank for in-place processing.
        /// If not in-place, a blank destination output to STDOUT
        /// </summary>
        private string m_outputFile = string.Empty;

        /// <summary>
        /// Optional resource file name. This file should contain a single global variable
        /// assignment using an object literal. The properties on that object are localizable
        /// values that get replaced in the input files with the actual values.
        /// </summary>
        private string m_resourceFile = string.Empty;

        /// <summary>
        /// Optional name for the global resource object to be created from a .resx or .resources
        /// file specified by the path in m_resourceFile
        /// </summary>
        private string m_resourceObjectName = string.Empty;

        /// <summary>
        /// This field is false by default. If it is set to true by an optional
        /// command-line parameter, then the crunched stream will always be terminated
        /// with a semi-colon.
        /// </summary>
        private bool m_terminateWithSemicolon;// = false;

        // default warning level ignores warnings
        private int m_warningLevel;// = 0;

        /// <summary>
        /// Optionally specify an XML file that indicates the input and output file(s)
        /// instead of specifying a single output and the input file(s) on the command line.
        /// </summary>
        private string m_xmlInputFile;// = null;

        #endregion

        #region startup code

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static int Main(string[] args)
        {
            int retVal;
            try
            {
                MainClass app = new MainClass(args);
                retVal = app.Run();
            }
            catch (UsageException e)
            {
                Usage(e);
                retVal = 1;
            }

            return retVal;
        }

        #endregion

        #region Constructor

        private MainClass(string[] args)
        {
            if (args != null && args.Length > 0)
            {
                // process the arguments
                ProcessArgs(args);
            }
            else
            {
                // no args -- output the usage
                throw new UsageException(ConsoleOutputMode.Console);
            }

            // if no output encoding was specified, we default to ascii
            if (m_encodingOutput == null)
            {
                // pick the right encoder from our file type
                EncoderFallback encoderFallback = null;
                // set the appropriate encoder fallback
                if (m_inputType == InputType.JavaScript)
                {
                    // set the fallback handler to our own code. we will take any character not
                    // displayable by the output encoding and change it into \uXXXX escapes.
                    encoderFallback = new JSEncoderFallback();
                }
                else if (m_inputType == InputType.Css)
                {
                    // set the fallback handler to our own code. we will take any character not
                    // displayable by the output encoding and change it into \uXXXX escapes.
                    encoderFallback = new CssEncoderFallback();
                }

                if (string.IsNullOrEmpty(m_encodingOutputName))
                {
                    // clone the ascii encoder so we can change the fallback handler
                    m_encodingOutput = (Encoding)Encoding.ASCII.Clone();
                    m_encodingOutput.EncoderFallback = encoderFallback;
                }
                else
                {
                    try
                    {
                        // try to create an encoding from the encoding argument
                        m_encodingOutput = Encoding.GetEncoding(
                            m_encodingOutputName,
                            encoderFallback,
                            new DecoderReplacementFallback("?")
                            );
                    }
                    catch (ArgumentException e)
                    {
                        System.Diagnostics.Debug.WriteLine(e.ToString());
                        throw new UsageException(m_outputMode, "InvalidOutputEncoding", m_encodingOutputName);
                    }
                }
            }

            // if no input encoding was specified, we use UTF-8
            if (m_encodingInput == null)
            {
                m_encodingInput = Encoding.UTF8;
            }
        }

        #endregion

        #region ProcessArgs method

        private void ProcessArgs(string[] args)
        {
            List<string> inputFiles = new List<string>();
            bool levelSpecified = false;
            bool renamingSpecified = false;
            for (int ndx = 0; ndx < args.Length; ++ndx)
            {
                // parameter switch
                string thisArg = args[ndx];
                if (thisArg.Length > 1
                  && (thisArg.StartsWith("-", StringComparison.Ordinal) // this is a normal hyphen (minus character)
                  || thisArg.StartsWith("–", StringComparison.Ordinal) // this character is what Word will convert a hyphen to
                  || thisArg.StartsWith("/", StringComparison.Ordinal)))
                {
                    // general switch syntax is -switch:param
                    string[] parts = thisArg.Substring(1).Split(':');
                    string switchPart = parts[0].ToUpper(CultureInfo.InvariantCulture);
                    string paramPart = (parts.Length == 1 ? null : parts[1].ToUpper(CultureInfo.InvariantCulture));

                    // switch off the switch part
                    switch (switchPart)
                    {
                        case "ANALYZE":
                        case "A": // <-- old-style
                            // ignore any arguments
                            m_analyze = true;
                            break;

                        case "ASPNET":
                            BooleanSwitch(paramPart, switchPart, false, out m_allowAspNet);
                            break;

                        case "CC":
                            BooleanSwitch(paramPart, switchPart, true, out m_ignoreConditionalCompilation);

                            // actually, the flag is the opposite of the member -- turn CC ON and we DON'T
                            // want to ignore them; turn CC OFF and we DO want to ignore them
                            m_ignoreConditionalCompilation = !m_ignoreConditionalCompilation;

                            JavaScriptOnly();
                            break;

                        case "CLOBBER":
                            // just putting the clobber switch on the command line without any arguments
                            // is the same as putting -clobber:true and perfectly valid.
                            BooleanSwitch(paramPart, switchPart, true, out m_clobber);
                            break;

                        case "COLORS":
                            // two options: hex or names
                            if (paramPart == "HEX")
                            {
                                m_colorNames = CssColor.Hex;
                            }
                            else if (paramPart == "STRICT")
                            {
                                m_colorNames = CssColor.Strict;
                            }
                            else if (paramPart == "MAJOR")
                            {
                                m_colorNames = CssColor.Major;
                            }
                            else if (paramPart == null)
                            {
                                throw new UsageException(m_outputMode, "SwitchRequiresArg", switchPart);
                            }
                            else
                            {
                                throw new UsageException(m_outputMode, "InvalidSwitchArg", paramPart, switchPart);
                            }
                            CssOnly();
                            break;

                        case "COMMENTS":
                            // four options: none, all, important, or hacks
                            // (default is important)
                            if (paramPart == "NONE")
                            {
                                m_cssComments = CssComment.None;
                            }
                            else if (paramPart == "ALL")
                            {
                                m_cssComments = CssComment.All;
                            }
                            else if (paramPart == "IMPORTANT")
                            {
                                m_cssComments = CssComment.Important;
                            }
                            else if (paramPart == "HACKS")
                            {
                                m_cssComments = CssComment.Hacks;
                            }
                            else if (paramPart == null)
                            {
                                throw new UsageException(m_outputMode, "SwitchRequiresArg", switchPart);
                            }
                            else
                            {
                                throw new UsageException(m_outputMode, "InvalidSwitchArg", paramPart, switchPart);
                            }
                            CssOnly();
                            break;

                        case "CSS":
                            // if we've already declared JS, then error
                            CssOnly();
                            break;

                        case "DEBUG":
                            // just putting the debug switch on the command line without any arguments
                            // is the same as putting -debug:true and perfectly valid.
                            BooleanSwitch(paramPart, switchPart, true, out m_stripDebugStatements);

                            // actually the inverse - a TRUE on the -debug switch means we DON'T want to
                            // strip debug statements, and a FALSE means we DO want to strip them
                            m_stripDebugStatements = !m_stripDebugStatements;

                            // this is a JS-only switch
                            JavaScriptOnly();
                            break;

                        case "DEFINE":
                            // the parts can be a comma-separate list of identifiers
                            if (string.IsNullOrEmpty(paramPart))
                            {
                                throw new UsageException(m_outputMode, "SwitchRequiresArg", switchPart);
                            }

                            // use parts[1] rather than paramParts because paramParts has been forced to upper-case
                            foreach (string defineName in parts[1].Split(','))
                            {
                                // this is supposed to be case-INsensitive, so convert to upper-case
                                var upperCaseName = defineName.ToUpperInvariant();

                                // better be a valid JavaScript identifier
                                if (!JSScanner.IsValidIdentifier(upperCaseName))
                                {
                                    throw new UsageException(m_outputMode, "InvalidSwitchArg", defineName, switchPart);
                                }

                                // if we haven't created the list yet, do it now
                                if (m_defines == null)
                                {
                                    m_defines = new List<string>();
                                }

                                // don't add duplicates
                                if (!m_defines.Contains(upperCaseName))
                                {
                                    m_defines.Add(upperCaseName);
                                }
                            }

                            break;

                        case "ECHO":
                        case "I": // <-- old style
                            // ignore any arguments
                            m_echoInput = true;
                            // -pretty and -echo are not compatible
                            if (m_prettyPrint)
                            {
                                throw new UsageException(m_outputMode, "PrettyAndEchoArgs");
                            }
                            break;

                        case "ENC":
                            // the encoding is the next argument
                            if (ndx >= args.Length - 1)
                            {
                                // must be followed by an encoding
                                throw new UsageException(m_outputMode, "EncodingArgMustHaveEncoding", switchPart);
                            }
                            string encoding = args[++ndx];

                            // whether this is an in or an out encoding
                            if (paramPart == "IN")
                            {
                                try
                                {
                                    // try to create an encoding from the encoding argument
                                    m_encodingInput = Encoding.GetEncoding(encoding);
                                }
                                catch (ArgumentException e)
                                {
                                    System.Diagnostics.Debug.WriteLine(e.ToString());
                                    throw new UsageException(m_outputMode, "InvalidInputEncoding", encoding);
                                }
                            }
                            else if (paramPart == "OUT")
                            {
                                // just save the name -- we'll create the encoding later because we need
                                // to know whether we are JS or CSS to pick the right encoding fallback
                                m_encodingOutputName = encoding;
                            }
                            else if (paramPart == null)
                            {
                                throw new UsageException(m_outputMode, "SwitchRequiresArg", switchPart);
                            }
                            else
                            {
                                throw new UsageException(m_outputMode, "InvalidSwitchArg", paramPart, switchPart);
                            }
                            break;

                        case "EVALS":
                            // three options: ignore, make immediate scope safe, or make all scopes safe
                            if (paramPart == "IGNORE")
                            {
                                m_evalTreatment = EvalTreatment.Ignore;
                            }
                            else if (paramPart == "IMMEDIATE")
                            {
                                m_evalTreatment = EvalTreatment.MakeImmediateSafe;
                            }
                            else if (paramPart == "SAFEALL")
                            {
                                m_evalTreatment = EvalTreatment.MakeAllSafe;
                            }
                            else if (paramPart == null)
                            {
                                throw new UsageException(m_outputMode, "SwitchRequiresArg", switchPart);
                            }
                            else
                            {
                                throw new UsageException(m_outputMode, "InvalidSwitchArg", paramPart, switchPart);
                            }

                            // this is a JS-only switch
                            JavaScriptOnly();
                            break;

                        case "EXPR":
                            // two options: minify (default) or raw
                            if (paramPart == "MINIFY")
                            {
                                m_minifyExpressions = true;
                            }
                            else if (paramPart == "RAW")
                            {
                                m_minifyExpressions = false;
                            }
                            else
                            {
                                throw new UsageException(m_outputMode, "InvalidSwitchArg", paramPart, switchPart);
                            }

                            CssOnly();
                            break;

                        case "FNAMES":
                            // three options: 
                            // LOCK    -> keep all NFE names, don't allow renaming of function names
                            // KEEP    -> keep all NFE names, but allow function names to be renamed
                            // ONLYREF -> remove unref'd NFE names, allow function named to be renamed (DEFAULT)
                            if (paramPart == "LOCK")
                            {
                                // don't remove function expression names
                                m_removeFunctionExpressionNames = false;

                                // and preserve the names (don't allow renaming)
                                m_preserveFunctionNames = true;
                            }
                            else if (paramPart == "KEEP")
                            {
                                // don't remove function expression names
                                m_removeFunctionExpressionNames = false;

                                // but it's okay to rename them
                                m_preserveFunctionNames = false;
                            }
                            else if (paramPart == "ONLYREF")
                            {
                                // remove function expression names if they aren't referenced
                                m_removeFunctionExpressionNames = true;

                                // and rename them if we so desire
                                m_preserveFunctionNames = false;
                            }
                            else if (paramPart == null)
                            {
                                throw new UsageException(m_outputMode, "SwitchRequiresArg", switchPart);
                            }
                            else
                            {
                                throw new UsageException(m_outputMode, "InvalidSwitchArg", paramPart, switchPart);
                            }

                            // this is a JS-only switch
                            JavaScriptOnly();
                            break;

                        case "GLOBAL":
                        case "G": // <-- old style
                            // the parts can be a comma-separate list of identifiers
                            if (string.IsNullOrEmpty(paramPart))
                            {
                                throw new UsageException(m_outputMode, "SwitchRequiresArg", switchPart);
                            }

                            // use parts[1] rather than paramParts because paramParts has been forced to upper-case
                            foreach (string global in parts[1].Split(','))
                            {
                                // better be a valid JavaScript identifier
                                if (!JSScanner.IsValidIdentifier(global))
                                {
                                    throw new UsageException(m_outputMode, "InvalidSwitchArg", global, switchPart);
                                }

                                // if we haven't created the list yet, do it now
                                if (m_globals == null)
                                {
                                    m_globals = new List<string>();
                                }

                                // don't add duplicates
                                if (!m_globals.Contains(global))
                                {
                                    m_globals.Add(global);
                                }
                            }

                            // this is a JS-only switch
                            JavaScriptOnly();
                            break;

                        case "HELP":
                        case "?":
                            // just show usage
                            throw new UsageException(m_outputMode);

                        case "INLINE":
                            // set safe for inline to the same boolean.
                            // if no param part, will return false (indicating the default)
                            // if invalid param part, will throw error
                            if (!BooleanSwitch(paramPart, switchPart, true, out m_safeForInline))
                            {
                                throw new UsageException(m_outputMode, "SwitchRequiresArg", switchPart);
                            }

                            // this is a JS-only switch
                            JavaScriptOnly();
                            break;

                        case "JS":
                            // if we've already declared CSS, then error
                            JavaScriptOnly();
                            break;

                        case "KILL":
                            // optional integer switch argument
                            if (paramPart == null)
                            {
                                throw new UsageException(m_outputMode, "SwitchRequiresArg", switchPart);
                            }

                            // get the numeric portion
                            if (!long.TryParse(paramPart, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out m_killSwitch))
                            {
                                throw new UsageException(m_outputMode, "InvalidKillSwitchArg", paramPart);
                            }

                            break;

                        case "LITERALS":
                            // two options: keep or combine
                            if (paramPart == "KEEP")
                            {
                                m_combineDuplicateLiterals = false;
                            }
                            else if (paramPart == "COMBINE")
                            {
                                m_combineDuplicateLiterals = true;
                            }
                            else if (paramPart == "EVAL")
                            {
                                m_evalLiteralExpressions = true;
                            }
                            else if (paramPart == "NOEVAL")
                            {
                                m_evalLiteralExpressions = false;
                            }
                            else if (paramPart == null)
                            {
                                throw new UsageException(m_outputMode, "SwitchRequiresArg", switchPart);
                            }
                            else
                            {
                                throw new UsageException(m_outputMode, "InvalidSwitchArg", paramPart, switchPart);
                            }

                            // this is a JS-only switch
                            JavaScriptOnly();
                            break;

                        case "MAC":
                            // optional boolean switch
                            // no arg is valid scenario (default is true)
                            BooleanSwitch(paramPart, switchPart, true, out m_macSafariQuirks);

                            // this is a JS-only switch
                            JavaScriptOnly();
                            break;

                        case "NEW":
                            // two options: keep and collapse
                            if (paramPart == "KEEP")
                            {
                                m_collapseToLiteral = false;
                            }
                            else if (paramPart == "COLLAPSE")
                            {
                                m_collapseToLiteral = true;
                            }
                            else if (paramPart == null)
                            {
                                throw new UsageException(m_outputMode, "SwitchRequiresArg", switchPart);
                            }
                            else
                            {
                                throw new UsageException(m_outputMode, "InvalidSwitchArg", paramPart, switchPart);
                            }

                            // this is a JS-only switch
                            JavaScriptOnly();
                            break;

                        case "NFE": // <-- deprecate; use FNAMES option instead
                            if (paramPart == "KEEPALL")
                            {
                                m_removeFunctionExpressionNames = false;
                            }
                            else if (paramPart == "ONLYREF")
                            {
                                m_removeFunctionExpressionNames = true;
                            }
                            else if (paramPart == null)
                            {
                                throw new UsageException(m_outputMode, "SwitchRequiresArg", switchPart);
                            }
                            else
                            {
                                throw new UsageException(m_outputMode, "InvalidSwitchArg", paramPart, switchPart);
                            }

                            // this is a JS-only switch
                            JavaScriptOnly();
                            break;

                        case "NORENAME":
                            // the parts can be a comma-separate list of identifiers
                            if (string.IsNullOrEmpty(paramPart))
                            {
                                throw new UsageException(m_outputMode, "SwitchRequiresArg", switchPart);
                            }

                            // use parts[1] rather than paramParts because paramParts has been forced to upper-case
                            foreach (string ident in parts[1].Split(','))
                            {
                                // better be a valid JavaScript identifier
                                if (!JSScanner.IsValidIdentifier(ident))
                                {
                                    throw new UsageException(m_outputMode, "InvalidSwitchArg", ident, switchPart);
                                }

                                // if we haven't created the list yet, do it now
                                if (m_noAutoRename == null)
                                {
                                    m_noAutoRename = new List<string>();
                                }

                                // don't add duplicates
                                if (!m_noAutoRename.Contains(ident))
                                {
                                    m_noAutoRename.Add(ident);
                                }
                            }

                            // this is a JS-only switch
                            JavaScriptOnly();
                            break;

                        case "OUT":
                        case "O": // <-- old style
                            // next argument is the output path
                            // cannot have two out arguments
                            if (!string.IsNullOrEmpty(m_outputFile))
                            {
                                throw new UsageException(m_outputMode, "MultipleOutputArg");
                            }
                            else if (ndx >= args.Length - 1)
                            {
                                throw new UsageException(m_outputMode, "OutputArgNeedsPath");
                            }
                            m_outputFile = args[++ndx];
                            break;

                        case "PPONLY":
                            // just putting the pponly switch on the command line without any arguments
                            // is the same as putting -pponly:true and perfectly valid.
                            BooleanSwitch(paramPart, switchPart, true, out m_preprocessOnly);

                            // this is a JS-only switch
                            JavaScriptOnly();
                            break;

                        case "PRETTY":
                        case "P": // <-- old style
                            m_prettyPrint = true;
                            // pretty-print and echo-input not compatible
                            if (m_echoInput)
                            {
                                throw new UsageException(m_outputMode, "PrettyAndEchoArgs");
                            }

                            // if renaming hasn't been specified yet, turn it off for prety-print
                            if (!renamingSpecified)
                            {
                                m_localRenaming = LocalRenaming.KeepAll;
                            }

                            // optional integer switch argument
                            if (paramPart != null)
                            {
                                // get the numeric portion
                                try
                                {
                                    // must be an integer value
                                    int indentSize = int.Parse(paramPart, CultureInfo.InvariantCulture);
                                    if (indentSize >= 0)
                                    {
                                        m_indentSize = indentSize;
                                    }
                                    else
                                    {
                                        throw new UsageException(m_outputMode, "InvalidTabSizeArg", paramPart);
                                    }
                                }
                                catch (FormatException e)
                                {
                                    Debug.WriteLine(e.ToString());
                                    throw new UsageException(m_outputMode, "InvalidTabSizeArg", paramPart);
                                }
                            }
                            break;

                        case "RENAME":
                            if (paramPart == null)
                            {
                                // there are no other parts after -rename
                                // the next argument should be a filename from which we can pull the
                                // variable renaming information
                                if (ndx >= args.Length - 1)
                                {
                                    // must be followed by an encoding
                                    throw new UsageException(m_outputMode, "RenameArgMissingParameterOrFilePath", switchPart);
                                }

                                // the renaming file is specified as the NEXT argument
                                string renameFilePath = args[++ndx];

                                // and it needs to exist
                                EnsureInputFileExists(renameFilePath);

                                // process the renaming file
                                ProcessRenamingFile(renameFilePath);
                            }
                            else if (paramPart.IndexOf('=') > 0)
                            {
                                // there is at least one equal sign -- treat this as a set of JS identifier
                                // pairs. split on commas -- multiple pairs can be specified
                                var paramPairs = parts[1].Split(',');
                                foreach (var paramPair in paramPairs)
                                {
                                    // split on the equal sign -- each pair needs to have an equal sige
                                    var pairParts = paramPair.Split('=');
                                    if (pairParts.Length == 2)
                                    {
                                        // there is an equal sign. The first part is the source name and the
                                        // second part is the new name to which to rename those entities.
                                        string fromIdentifier = pairParts[0];
                                        string toIdentifier = pairParts[1];
                                
                                        // make sure both parts are valid JS identifiers
                                        var fromIsValid = JSScanner.IsValidIdentifier(fromIdentifier);
                                        var toIsValid = JSScanner.IsValidIdentifier(toIdentifier);
                                        if (fromIsValid && toIsValid)
                                        {
                                            // create the map if it hasn't been created yet.
                                            if (m_renameMap == null)
                                            {
                                                m_renameMap = new Dictionary<string, string>();
                                            }

                                            m_renameMap.Add(fromIdentifier, toIdentifier);
                                        }
                                        else if (fromIsValid)
                                        {
                                            // the toIdentifier is invalid!
                                            throw new UsageException(m_outputMode, "InvalidRenameToIdentifier", toIdentifier);
                                        }
                                        else if (toIsValid)
                                        {
                                            // the fromIdentifier is invalid!
                                            throw new UsageException(m_outputMode, "InvalidRenameFromIdentifier", fromIdentifier);
                                        }
                                        else
                                        {
                                            // NEITHER of the rename parts are valid identifiers! BOTH are required to
                                            // be valid JS identifiers
                                            throw new UsageException(m_outputMode, "InvalidRenameIdentifiers", fromIdentifier, toIdentifier);
                                        }
                                    }
                                    else
                                    {
                                        // either zero or more than one equal sign. Invalid.
                                        throw new UsageException(m_outputMode, "InvalidSwitchArg", paramPart, switchPart);
                                    }
                                }
                            }
                            else
                            {
                                // no equal sign; just a plain option
                                // three options: all, localization, none
                                if (paramPart == "ALL")
                                {
                                    m_localRenaming = LocalRenaming.CrunchAll;

                                    // automatic renaming strategy has been specified by this option
                                    renamingSpecified = true;
                                }
                                else if (paramPart == "LOCALIZATION")
                                {
                                    m_localRenaming = LocalRenaming.KeepLocalizationVars;

                                    // automatic renaming strategy has been specified by this option
                                    renamingSpecified = true;
                                }
                                else if (paramPart == "NONE")
                                {
                                    m_localRenaming = LocalRenaming.KeepAll;

                                    // automatic renaming strategy has been specified by this option
                                    renamingSpecified = true;
                                }
                                else if (paramPart == "NOPROPS")
                                {
                                    // manual-renaming does not change property names
                                    m_renameProperties = false;
                                }
                                else
                                {
                                    throw new UsageException(m_outputMode, "InvalidSwitchArg", paramPart, switchPart);
                                }
                            }

                            // this is a JS-only switch
                            JavaScriptOnly();
                            break;

                        case "RES":
                        case "R": // <-- old style
                            // -res:id path
                            // can't specify -R more than once
                            if (!string.IsNullOrEmpty(m_resourceFile))
                            {
                                throw new UsageException(m_outputMode, "MultipleResourceArgs");
                            }
                            // must have resource file on next parameter
                            if (ndx >= args.Length - 1)
                            {
                                throw new UsageException(m_outputMode, "ResourceArgNeedsPath");
                            }

                            // the resource file name is the NEXT argument
                            m_resourceFile = args[++ndx];
                            EnsureInputFileExists(m_resourceFile);

                            // check the extension to see if the resource file is a supported file type.
                            switch (Path.GetExtension(m_resourceFile).ToUpper(CultureInfo.InvariantCulture))
                            {
                                case ".RESOURCES":
                                case ".RESX":
                                    if (!string.IsNullOrEmpty(paramPart))
                                    {
                                        // reset paramPart to not be the forced-to-upper version
                                        paramPart = parts[1];

                                        // must be valid JS identifier
                                        if (JSScanner.IsValidIdentifier(paramPart))
                                        {
                                            // save the name portion
                                            m_resourceObjectName = paramPart;
                                        }
                                        else
                                        {
                                            throw new UsageException(m_outputMode, "ResourceArgInvalidName", paramPart);
                                        }
                                    }
                                    else
                                    {
                                        // no name specified -- use Strings as the default
                                        // (not recommended)
                                        m_resourceObjectName = c_defaultResourceObjectName;
                                    }
                                    break;

                                default:
                                    // not a supported resource file type
                                    throw new UsageException(m_outputMode, "ResourceArgInvalidType");
                            }
                            break;

                        case "SILENT":
                        case "S": // <-- old style
                            // ignore any argument part
                            m_outputMode = ConsoleOutputMode.Silent;
                            break;

                        case "TERM":
                            // optional boolean argument, defaults to true
                            BooleanSwitch(paramPart, switchPart, true, out m_terminateWithSemicolon);
                            break;

                        case "UNUSED":
                            // two options: keep and remove
                            if (paramPart == "KEEP")
                            {
                                m_removeUnneededCode = false;
                            }
                            else if (paramPart == "REMOVE")
                            {
                                m_removeUnneededCode = true;
                            }
                            else if (paramPart == null)
                            {
                                throw new UsageException(m_outputMode, "SwitchRequiresArg", switchPart);
                            }
                            else
                            {
                                throw new UsageException(m_outputMode, "InvalidSwitchArg", paramPart, switchPart);
                            }

                            // this is a JS-only switch
                            JavaScriptOnly();
                            break;

                        case "WARN":
                        case "W": // <-- old style
                            if (string.IsNullOrEmpty(paramPart))
                            {
                                // just "-warn" without anything else means all errors and warnings
                                m_warningLevel = int.MaxValue;
                            }
                            else
                            {
                                try
                                {
                                    // must be an unsigned integer value
                                    m_warningLevel = int.Parse(paramPart, NumberStyles.None, CultureInfo.InvariantCulture);
                                }
                                catch (FormatException e)
                                {
                                    Debug.WriteLine(e.ToString());
                                    throw new UsageException(m_outputMode, "InvalidWarningArg", paramPart);
                                }
                            }
                            levelSpecified = true;
                            break;

                        case "XML":
                        case "X": // <-- old style
                            if (!string.IsNullOrEmpty(m_xmlInputFile))
                            {
                                throw new UsageException(m_outputMode, "MultipleXmlArgs");
                            }
                            // cannot have input files
                            if (inputFiles.Count > 0)
                            {
                                throw new UsageException(m_outputMode, "XmlArgHasInputFiles");
                            }

                            if (ndx >= args.Length - 1)
                            {
                                throw new UsageException(m_outputMode, "XmlArgNeedsPath");
                            }

                            // the xml file name is the NEXT argument
                            m_xmlInputFile = args[++ndx];

                            // and it must exist
                            EnsureInputFileExists(m_xmlInputFile);
                            break;

                        // Backward-compatibility switches different from new switches

                        case "D":
                            // equivalent to -debug:true (default behavior)
                            m_stripDebugStatements = true;
                            JavaScriptOnly();
                            break;

                        case "E":
                        case "EO":
                            // equivalent to -enc:out <encoding>
                            if(parts.Length < 2)
                            {
                                // must be followed by an encoding
                                throw new UsageException(m_outputMode, "EncodingArgMustHaveEncoding", switchPart);
                            }

                            // just save the name -- we'll create the encoding later because we need
                            // to know whether we are JS or CSS to pick the right encoding fallback
                            m_encodingOutputName = parts[1];
                            break;

                        case "EI":
                            // equivalent to -enc:in <encoding>
                            if (parts.Length < 2)
                            {
                                // must be followed by an encoding
                                throw new UsageException(m_outputMode, "EncodingArgMustHaveEncoding", switchPart);
                            }

                            try
                            {
                                // try to create an encoding from the encoding argument
                                m_encodingInput = Encoding.GetEncoding(parts[1]);
                            }
                            catch (ArgumentException e)
                            {
                                System.Diagnostics.Debug.WriteLine(e.ToString());
                                throw new UsageException(m_outputMode, "InvalidInputEncoding", parts[1]);
                            }
                            break;

                        case "H":
                            // equivalent to -rename:all -unused:remove (default behavior)
                            m_localRenaming = LocalRenaming.CrunchAll;
                            m_removeUnneededCode = true;
                            JavaScriptOnly();

                            // renaming is specified by this option
                            renamingSpecified = true;
                            break;

                        case "HL":
                            // equivalent to -rename:localization -unused:remove
                            m_localRenaming = LocalRenaming.KeepLocalizationVars;
                            m_removeUnneededCode = true;
                            JavaScriptOnly();

                            // renaming is specified by this option
                            renamingSpecified = true;
                            break;

                        case "HC":
                            // equivalent to -literals:combine -rename:all -unused:remove
                            m_combineDuplicateLiterals = true;
                            goto case "H";

                        case "HLC":
                        case "HCL":
                            // equivalent to -literals:combine -rename:localization -unused:remove
                            m_combineDuplicateLiterals = true;
                            goto case "HL";

                        case "J":
                            // equivalent to -evals:ignore (default behavior)
                            m_evalTreatment = EvalTreatment.Ignore;
                            JavaScriptOnly();
                            break;

                        case "K":
                            // equivalent to -inline:true (default behavior)
                            m_safeForInline = true;
                            JavaScriptOnly();
                            break;

                        case "L":
                            // equivalent to -new:keep (default is collapse)
                            m_collapseToLiteral = false;
                            JavaScriptOnly();
                            break;

                        case "M":
                            // equivalent to -mac:true (default behavior)
                            m_macSafariQuirks = true;
                            JavaScriptOnly();
                            break;

                        case "Z":
                            // equivalent to -term:true (default is false)
                            m_terminateWithSemicolon = true;
                            break;

                        case "CL":
                        case "CS":
                        case "V":
                        case "3":
                            // ignore -- we don't use these switches anymore.
                            // but for backwards-compatibility, don't throw an error.
                            break;

                        // end backward-compatible section

                        default:
                            throw new UsageException(m_outputMode, "InvalidArgument", args[ndx]);
                    }
                }
                else // must be an input file!
                {
                    // cannot coexist with XML option
                    if (!string.IsNullOrEmpty(m_xmlInputFile))
                    {
                        throw new UsageException(m_outputMode, "XmlArgHasInputFiles");
                    }

                    // shortcut
                    string fileName = args[ndx];

                    // make sure it exists
                    EnsureInputFileExists(fileName);

                    // we don't want duplicates
                    if (!inputFiles.Contains(fileName))
                    {
                        inputFiles.Add(fileName);
                    }
                }
            }

            // if we didn't specify the type (JS or CSS), then look at the extension of
            // the input files and see if we can divine what we are
            foreach (string path in inputFiles)
            {
                string extension = Path.GetExtension(path).ToUpperInvariant();
                switch (m_inputType)
                {
                    case InputType.Unknown:
                        // we don't know yet. If the extension is JS or CSS set to the
                        // appropriate input type
                        if (extension == ".JS")
                        {
                            m_inputType = InputType.JavaScript;
                        }
                        else if (extension == ".CSS")
                        {
                            m_inputType = InputType.Css;
                        }
                        break;

                    case InputType.JavaScript:
                        // we know we are JS -- if we find a CSS file, throw an error
                        if (extension == ".CSS")
                        {
                            throw new UsageException(m_outputMode, "ConflictingInputType");
                        }
                        break;

                    case InputType.Css:
                        // we know we are CSS -- if we find a JS file, throw an error
                        if (extension == ".JS")
                        {
                            throw new UsageException(m_outputMode, "ConflictingInputType");
                        }
                        break;
                }
            }

            // if we don't know by now, then throw an exception
            if (m_inputType == InputType.Unknown)
            {
                throw new UsageException(m_outputMode, "UnknownInputType");
            }

            m_inputFiles = inputFiles.ToArray();

            // if analyze was specified but no warning level, jack up the warning level
            // so everything is shown
            // TODO: we want to do this for CSS also, but need to fix the error scheme first.
            if (m_analyze && !levelSpecified)
            {
                // we want to analyze, and we didn't specify a particular warning level.
                // go ahead and report all errors
                m_warningLevel = int.MaxValue;
            }
        }

        private bool BooleanSwitch(string booleanText, string switchPart, bool defaultValue, out bool booleanValue)
        {
            switch (booleanText)
            {
                case "Y":
                case "YES":
                case "T":
                case "TRUE":
                case "ON":
                case "1":
                    booleanValue = true;
                    return true;

                case "N":
                case "NO":
                case "F":
                case "FALSE":
                case "OFF":
                case "0":
                    booleanValue = false;
                    return true;

                case null:
                    booleanValue = defaultValue;
                    return false;

                default:
                    // not a valid value
                    throw new UsageException(m_outputMode, "InvalidSwitchArg", booleanText, switchPart);
            }
        }

        private void EnsureInputFileExists(string fileName)
        {
            // make sure it exists
            if (!File.Exists(fileName))
            {
                // file doesn't exist -- is it a folder?
                if (Directory.Exists(fileName))
                {
                    // cannot be a folder
                    throw new UsageException(m_outputMode, "SourceFileIsFolder", fileName);
                }
                else
                {
                    // just plain doesn't exist
                    throw new UsageException(m_outputMode, "SourceFileNotExist", fileName);
                }
            }
        }

        private void JavaScriptOnly()
        {
            // this is a JS-only switch
            switch (m_inputType)
            {
                case InputType.Unknown:
                    m_inputType = InputType.JavaScript;
                    break;

                case InputType.Css:
                    throw new UsageException(m_outputMode, "ConflictingInputType");
            }
        }

        private void CssOnly()
        {
            // this is a JS-only switch
            switch (m_inputType)
            {
                case InputType.Unknown:
                    m_inputType = InputType.Css;
                    break;

                case InputType.JavaScript:
                    throw new UsageException(m_outputMode, "ConflictingInputType");
            }
        }

        #endregion

        #region Usage method

        private static void Usage(UsageException e)
        {
            string fileName = Path.GetFileName(
              Assembly.GetExecutingAssembly().Location
              );

            // only output the header if we aren't supposed to be silent
            if (e.OutputMode != ConsoleOutputMode.Silent)
            {
                Console.Error.WriteLine(GetHeaderString());
            }

            // only output the usage is we aren't silent, or if we have no error string
            if (e.OutputMode != ConsoleOutputMode.Silent)
            {
                Console.Error.WriteLine(StringMgr.GetString("Usage", fileName));
            }

            // only output the message if we have one
            if (e.Message.Length > 0)
            {
                Console.Error.WriteLine();
                Console.Error.WriteLine(CreateBuildError(
                    null,
                    null,
                    true,
                    "AM-USAGE", // NON-LOCALIZABLE error code
                    e.Message
                    ));
            }
        }

        #endregion

        #region Run method

        private int Run()
        {
            int retVal = 0;
            CrunchGroup[] crunchGroups;

            // see if we have an XML file to process
            if (!string.IsNullOrEmpty(m_xmlInputFile))
            {
                // process the XML file, using the output path as an optional output root folder
                crunchGroups = ProcessXmlFile(m_xmlInputFile, m_resourceFile, m_resourceObjectName, m_outputFile);
            }
            else
            {
                // just pass the input and output files specified in the command line
                // to the processing method (normal operation)
                crunchGroups = new CrunchGroup[] { new CrunchGroup(m_outputFile, m_resourceFile, m_resourceObjectName, m_inputFiles) };
            }

            if (crunchGroups.Length > 0)
            {
                // if any one crunch group is writing to stdout, then we need to make sure
                // that no progress or informational messages go to stdout or we will output 
                // invalid JavaScript/CSS. Loop through the crunch groups and if any one is
                // outputting to stdout, set the appropriate flag.
                for (var ndxGroup = 0; ndxGroup < crunchGroups.Length; ++ndxGroup)
                {
                    if (string.IsNullOrEmpty(crunchGroups[ndxGroup].Output))
                    {
                        // set the flag; no need to check any more
                        m_outputToStandardOut = true;
                        break;
                    }
                }

                // loop through all the crunch groups
                for (int ndxGroup = 0; ndxGroup < crunchGroups.Length; ++ndxGroup)
                {
                    // shortcut
                    CrunchGroup crunchGroup = crunchGroups[ndxGroup];

                    // process the crunch group
                    int crunchResult = ProcessCrunchGroup(crunchGroup);
                    // if the result contained an error...
                    if (crunchResult != 0)
                    {
                        // if we're processing more than one group, we should output an
                        // error message indicating that this group encountered an error
                        if (crunchGroups.Length > 1)
                        {
                            // non-localized string, so format is not in the resources
                            string errorCode = string.Format(CultureInfo.InvariantCulture, "AM{0:D4}", crunchResult);

                            // if there is an output file name, use it.
                            if (crunchGroup.Output.Length > 0)
                            {
                                WriteError(CreateBuildError(
                                               crunchGroup.Output,
                                               StringMgr.GetString("OutputFileErrorSubCat"),
                                               true,
                                               errorCode,
                                               StringMgr.GetString("OutputFileError", crunchResult)
                                               ));
                            }
                            else if (!string.IsNullOrEmpty(m_xmlInputFile))
                            {
                                // use the XML file as the location, and the index of the group for more info
                                // inside the message
                                WriteError(CreateBuildError(
                                               m_xmlInputFile,
                                               StringMgr.GetString("OutputGroupErrorSubCat"),
                                               true,
                                               errorCode,
                                               StringMgr.GetString("OutputGroupError", ndxGroup, crunchResult)
                                               ));
                            }
                            else
                            {
                                // no output file name, and not from an XML file. If it's not from an XML
                                // file, then there really should only be one crunch group.
                                // but just in case, use "stdout" as the output file and the index of the group 
                                // in the list (which should probably just be zero)
                                WriteError(CreateBuildError(
                                               "stdout",
                                               StringMgr.GetString("OutputGroupErrorSubCat"),
                                               true,
                                               errorCode,
                                               StringMgr.GetString("OutputGroupError", ndxGroup, crunchResult)
                                               ));
                            }
                        }
                        // return the error. Only the last one will be used
                        retVal = crunchResult;
                    }
                }
            }
            else
            {
                // no crunch groups
                throw new UsageException(ConsoleOutputMode.Console, "NoInput");
            }

            return retVal;
        }

        #endregion

        #region ProcessCrunchGroup method

        private int ProcessCrunchGroup(CrunchGroup crunchGroup)
        {
            int retVal = 0;

            // length of all the source files combined
            long sourceLength = 0;

            // create a string builder we'll dump our output into
            StringBuilder outputBuilder = new StringBuilder();
            try
            {
                // if we have a resource file, process it now
                ResourceStrings resourceStrings = null;
                if (crunchGroup.Resource.Length > 0)
                {
                    try
                    {
                        resourceStrings = ProcessResourceFile(crunchGroup.Resource);
                    }
                    catch (IOException e)
                    {
                        Debug.WriteLine(e.ToString());
                    }
                    catch (BadImageFormatException e)
                    {
                        Debug.WriteLine(e.ToString());
                    }
                }

                switch (m_inputType)
                {
                    case InputType.Css:
                        // see how many input files there are
                        if (crunchGroup.Count == 0)
                        {
                            // no input files -- take from stdin
                            retVal = ProcessCssFile(string.Empty, resourceStrings, outputBuilder, ref sourceLength);
                        }
                        else
                        {
                            // process each input file
                            for (int ndx = 0; retVal == 0 && ndx < crunchGroup.Count; ++ndx)
                            {
                                retVal = ProcessCssFile(crunchGroup[ndx], resourceStrings, outputBuilder, ref sourceLength);
                            }
                        }
                        break;

                    case InputType.JavaScript:
                        if (resourceStrings != null)
                        {
                            // make sure the resource strings know the name of the resource object
                            resourceStrings.Name = crunchGroup.ResourceObjectName;
                        }

                        if (m_echoInput && resourceStrings != null)
                        {
                            // we're just echoing the output -- so output a JS version of the dictionary
                            // create JS from the dictionary and output it to the stream
                            // leave the object null
                            string resourceObject = CreateJSFromResourceStrings(resourceStrings);
                            outputBuilder.Append(resourceObject);

                            // just add the number of characters to the length 
                            // it's just an approximation
                            // NO! We don't want to include this code in the calculations.
                            // it's not actually part of the sources
                            //sourceLength += resourceObject.Length;
                        }

                        // process each input file
                        // we'll keep track of whether the last file ended in a semi-colon.
                        // we start with true so we don't add one before the first block
                        bool lastEndedSemiColon = true;
                        try
                        {
                            // see how many input files there are
                            if (crunchGroup.Count == 0)
                            {
                                // take input from stdin
                                retVal = ProcessJSFile(string.Empty, resourceStrings, outputBuilder, ref lastEndedSemiColon, ref sourceLength);
                            }
                            else
                            {
                                // process each input file in turn
                                for (int ndx = 0; retVal == 0 && ndx < crunchGroup.Count; ++ndx)
                                {
                                    retVal = ProcessJSFile(crunchGroup[ndx], resourceStrings, outputBuilder, ref lastEndedSemiColon, ref sourceLength);
                                }
                            }
                        }
                        catch (JScriptException e)
                        {
                            retVal = 1;
                            System.Diagnostics.Debug.WriteLine(e.ToString());
                            WriteError(CreateBuildError(
                                null,
                                null,
                                true,
                                string.Format(CultureInfo.InvariantCulture, "JS{0}", (e.Error & 0xffff)),
                                e.Message
                                ));
                        }

                        // if we want to ensure the stream ends in a semi-colon (command-line option)
                        // and the last file didn't...
                        if (m_terminateWithSemicolon && !lastEndedSemiColon)
                        {
                            // add one now
                            outputBuilder.Append(';');
                        }
                        break;

                    default:
                        throw new UsageException(m_outputMode, "ConflictingInputType");
                }

                // if we are pretty-printing, add a newline
                if (m_prettyPrint)
                {
                    outputBuilder.AppendLine();
                }
            }
            catch (Exception e)
            {
                retVal = 1;
                System.Diagnostics.Debug.WriteLine(e.ToString());
                WriteError(CreateBuildError(
                    null,
                    null,
                    true,
                    "AM-EXCEPTION",
                    e.Message
                    ));
            }

            string crunchedCode = outputBuilder.ToString();

            // now write the final output file
            if (crunchGroup.Output.Length == 0)
            {
                // if the code is empty, don't bother outputting it to the console
                if (!string.IsNullOrEmpty(crunchedCode))
                {
                    // set the console encoding
                    try
                    {
                        // try setting the appropriate output encoding
                        Console.OutputEncoding = m_encodingOutput;
                    }
                    catch (IOException e)
                    {
                        // sometimes they will error, in which case we'll just set it to ascii
                        Debug.WriteLine(e.ToString());
                        Console.OutputEncoding = Encoding.ASCII;
                    }

                    // however, for some reason when I set the output encoding it
                    // STILL doesn't call the EncoderFallback to Unicode-escape characters
                    // not supported by the encoding scheme. So instead we need to run the
                    // translation outselves. Still need to set the output encoding, though,
                    // so the translated bytes get displayed properly in the console.
                    byte[] encodedBytes = m_encodingOutput.GetBytes(crunchedCode);

                    // only output the size analysis if we are in analyze mode
                    // change: no, output the size analysis all the time.
                    // (unless in silent mode, but WriteProgess will take care of that)
                    ////if (m_analyze)
                    {
                        // if we are echoing the input, don't bother reporting the
                        // minify savings because we don't have the minified output --
                        // we have the original output
                        double percentage;
                        if (!m_echoInput)
                        {
                            // calculate the percentage saved
                            percentage = Math.Round((1 - ((double) encodedBytes.Length)/sourceLength)*100, 1);
                            WriteProgress(StringMgr.GetString(
                                              "SavingsMessage",
                                              sourceLength,
                                              encodedBytes.Length,
                                              percentage
                                              ));
                        }
                        else
                        {
                            
                            WriteProgress(StringMgr.GetString(
                                "SavingsOutputMessage",
                                encodedBytes.Length
                                ));
                        }

                        // calculate how much a simple gzip compression would compress the output
                        long gzipLength = CalculateGzipSize(encodedBytes);

                        // calculate the savings and display the result
                        percentage = Math.Round((1 - ((double)gzipLength) / encodedBytes.Length) * 100, 1);
                        WriteProgress(StringMgr.GetString("SavingsGzipMessage", gzipLength, percentage));

                        // blank line
                        WriteProgress();
                    }

                    // send to console out
                    Console.Out.Write(Console.OutputEncoding.GetChars(encodedBytes));
                    //Console.Out.Write(crunchedCode);
                }
            }
            else
            {
                try
                {
                    // make sure the destination folder exists
                    FileInfo fileInfo = new FileInfo(crunchGroup.Output);
                    DirectoryInfo destFolder = new DirectoryInfo(fileInfo.DirectoryName);
                    if (!destFolder.Exists)
                    {
                        destFolder.Create();
                    }

                    if (!File.Exists(crunchGroup.Output) || m_clobber)
                    {
                        if (m_clobber
                            && File.Exists(crunchGroup.Output) 
                            && (File.GetAttributes(crunchGroup.Output) & FileAttributes.ReadOnly) != 0)
                        {
                            // the file exists, we said we want to clobber it, but it's marked as
                            // read-only. Reset that flag.
                            File.SetAttributes(
                                crunchGroup.Output, 
                                (File.GetAttributes(crunchGroup.Output) & ~FileAttributes.ReadOnly)
                                );
                        }

                        // create the output file using the given encoding
                        using (StreamWriter outputStream = new StreamWriter(
                           crunchGroup.Output,
                           false,
                           m_encodingOutput
                           ))
                        {
                            outputStream.Write(crunchedCode);
                        }

                        // only output the size analysis if there is actually some output to measure
                        if (File.Exists(crunchGroup.Output))
                        {
                            // get the size of the resulting file
                            FileInfo crunchedFileInfo = new FileInfo(crunchGroup.Output);
                            long crunchedLength = crunchedFileInfo.Length;
                            if (crunchedLength > 0)
                            {
                                // if we are just echoing the input, don't bother calculating
                                // the minify savings because there aren't any
                                double percentage;
                                if (!m_echoInput)
                                {
                                    // calculate the percentage saved by minification
                                    percentage = Math.Round((1 - ((double) crunchedLength)/sourceLength)*100, 1);
                                    WriteProgress(StringMgr.GetString(
                                                      "SavingsMessage",
                                                      sourceLength,
                                                      crunchedLength,
                                                      percentage
                                                      ));
                                }
                                else
                                {

                                    WriteProgress(StringMgr.GetString(
                                        "SavingsOutputMessage",
                                        crunchedLength
                                        ));
                                }

                                // compute how long a simple gzip might compress the resulting file
                                long gzipLength = CalculateGzipSize(File.ReadAllBytes(crunchGroup.Output));

                                // calculate the percentage of compression and display the results
                                percentage = Math.Round((1 - ((double) gzipLength)/crunchedLength)*100, 1);
                                WriteProgress(StringMgr.GetString("SavingsGzipMessage", gzipLength, percentage));

                                // blank line
                                WriteProgress();
                            }
                        }
                    }
                    else
                    {
                        retVal = 1;
                        WriteError(CreateBuildError(
                            null,
                            null,
                            true,
                            "AM-IO",
                            StringMgr.GetString("NoClobberError", crunchGroup.Output)
                            ));
                    }

                }
                catch (ArgumentException e)
                {
                    retVal = 1;
                    System.Diagnostics.Debug.WriteLine(e.ToString());
                    WriteError(CreateBuildError(
                        null,
                        null,
                        true,
                        "AM-PATH",
                        e.Message
                        ));
                }
                catch (UnauthorizedAccessException e)
                {
                    retVal = 1;
                    System.Diagnostics.Debug.WriteLine(e.ToString());
                    WriteError(CreateBuildError(
                        null,
                        null,
                        true,
                        "AM-AUTH",
                        e.Message
                        ));
                }
                catch (PathTooLongException e)
                {
                    retVal = 1;
                    System.Diagnostics.Debug.WriteLine(e.ToString());
                    WriteError(CreateBuildError(
                        null,
                        null,
                        true,
                        "AM-PATH",
                        e.Message
                        ));
                }
                catch (SecurityException e)
                {
                    retVal = 1;
                    System.Diagnostics.Debug.WriteLine(e.ToString());
                    WriteError(CreateBuildError(
                        null,
                        null,
                        true,
                        "AM-SEC",
                        e.Message
                        ));
                }
                catch (IOException e)
                {
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
            }

            if (retVal == 0 && m_errorsFound)
            {
                // make sure we report an error
                retVal = 1;
            }
            return retVal;
        }

        #endregion

        #region CrunchGroup class

        private class CrunchGroup
        {
            // the output file for the group. May be empty string.
            private string m_outputPath;
            public string Output { get { return m_outputPath; } }

            // optional resource file for this group
            private string m_resourcePath = string.Empty;
            public string Resource
            {
                get { return m_resourcePath; }
                set
                {
                    // make sure we don't set this value to null.
                    // if the value is null, use an empty string
                    m_resourcePath = (value == null ? string.Empty : value);
                }
            }

            // optional resource object name
            private string m_resourceObjectName = string.Empty;
            public string ResourceObjectName
            {
                get { return m_resourceObjectName; }
                set
                {
                    // make sure we don't set this value to null.
                    // if the value is null, use an empty string
                    m_resourceObjectName = (value == null ? string.Empty : value);
                }
            }

            // list of input files -- may not be empty.
            private List<string> m_sourcePaths;// = null;
            // if we don't even have a list yet, return 0; otherwise the count in the list
            public int Count { get { return (m_sourcePaths == null ? 0 : m_sourcePaths.Count); } }
            public string this[int ndx]
            {
                get
                {
                    // return the object (which may throw an index exception itself)
                    return m_sourcePaths[ndx];
                }
            }

            public CrunchGroup(string outputPath)
            {
                m_outputPath = outputPath;
            }

            public CrunchGroup(string outputPath, string resourcePath, string resourceObjectName, string[] inputFiles)
                : this(outputPath)
            {
                // save the optional resource path and object name.
                // use the property setters so we can make sure they aren't set to null
                Resource = resourcePath;
                ResourceObjectName = resourceObjectName;

                // create a list with the same number of initial items as the array
                m_sourcePaths = new List<string>(inputFiles.Length);
                // add the array in one fell swoop
                m_sourcePaths.AddRange(inputFiles);
            }

            public void Add(string inputPath)
            {
                // if we haven't yet created the list...
                if (m_sourcePaths == null)
                {
                    // do so now
                    m_sourcePaths = new List<string>();
                }
                // add this item to the list
                m_sourcePaths.Add(inputPath);
            }
        }

        #endregion

        #region ProcessXmlFile method

        private static CrunchGroup[] ProcessXmlFile(string xmlPath, string resourcePath, string resourceObjectName, string outputFolder)
        {
            // list of crunch groups we're going to create by reading the XML file
            List<CrunchGroup> crunchGroups = new List<CrunchGroup>();
            try
            {
                // save the XML file's directory name because we'll use it as a root
                // for all the other paths in the file
                string rootPath = Path.GetDirectoryName(xmlPath);

                // open the xml file
                XmlDocument xmlDoc = new XmlDocument();
                xmlDoc.Load(xmlPath);

                // get a list of all <output> nodes
                XmlNodeList outputNodes = xmlDoc.SelectNodes("//output");
                if (outputNodes.Count > 0)
                {
                    // process each <output> node
                    for (int ndxOutput = 0; ndxOutput < outputNodes.Count; ++ndxOutput)
                    {
                        // shortcut
                        XmlNode outputNode = outputNodes[ndxOutput];

                        // get the output file path from the path attribute (if any)
                        // it's okay for ther eto be no output file; if that's the case,
                        // the output is sent to the STDOUT stream
                        XmlAttribute pathAttribute = outputNode.Attributes["path"];
                        string outputPath = (pathAttribute == null ? string.Empty : pathAttribute.Value);
                        // if we have a value and it's a relative path...
                        if (outputPath.Length > 0 && !Path.IsPathRooted(outputPath))
                        {
                            if (string.IsNullOrEmpty(outputFolder))
                            {
                                // make it relative to the XML file
                                outputPath = Path.Combine(rootPath, outputPath);
                            }
                            else
                            {
                                // make it relative to the output folder
                                outputPath = Path.Combine(outputFolder, outputPath);
                            }
                        }
                        CrunchGroup crunchGroup = new CrunchGroup(outputPath);

                        // see if there is a resource node
                        XmlNode resourceNode = outputNode.SelectSingleNode("./resource");
                        if (resourceNode != null)
                        {
                            // the path attribute MUST exist, or we will throw an error
                            pathAttribute = resourceNode.Attributes["path"];
                            if (pathAttribute != null)
                            {
                                // get the value from the attribute
                                string resourceFile = pathAttribute.Value;
                                // if it's a relative path...
                                if (!Path.IsPathRooted(resourceFile))
                                {
                                    // make it relative from the XML file
                                    resourceFile = Path.Combine(rootPath, resourceFile);
                                }
                                // make sure the resource file actually exists! It's an error if it doesn't.
                                if (!File.Exists(resourceFile))
                                {
                                    throw new XmlException(StringMgr.GetString(
                                      "XmlResourceNotExist",
                                      pathAttribute.Value
                                      ));
                                }

                                // add it to the group
                                crunchGroup.Resource = resourceFile;
                            }
                            else
                            {
                                throw new XmlException(StringMgr.GetString("ResourceNoPathAttr"));
                            }

                            // if there is a name attribute, we will use it for the object name
                            XmlAttribute nameAttribute = resourceNode.Attributes["name"];
                            if (nameAttribute != null)
                            {
                                // but first make sure it isn't empty
                                string objectName = nameAttribute.Value;
                                if (!string.IsNullOrEmpty(objectName))
                                {
                                    crunchGroup.ResourceObjectName = objectName;
                                }
                            }
                            // if no name was specified, use our default name
                            if (string.IsNullOrEmpty(crunchGroup.ResourceObjectName))
                            {
                                crunchGroup.ResourceObjectName = c_defaultResourceObjectName;
                            }
                        }
                        else if (!string.IsNullOrEmpty(resourcePath))
                        {
                            // just use the global resource path and object name passed to us
                            // (if anything was passed at all)
                            crunchGroup.Resource = resourcePath;
                            crunchGroup.ResourceObjectName = resourceObjectName;
                        }

                        // get a list of <input> nodes
                        XmlNodeList inputNodes = outputNode.SelectNodes("./input");
                        if (inputNodes.Count > 0)
                        {
                            // for each <input> element under the <output> node
                            for (int ndxInput = 0; ndxInput < inputNodes.Count; ++ndxInput)
                            {
                                // add the path attribute value to the string list.
                                // the path attribute MUST exist, or we will throw an error
                                pathAttribute = inputNodes[ndxInput].Attributes["path"];
                                if (pathAttribute != null)
                                {
                                    // get the value from the attribute
                                    string inputFile = pathAttribute.Value;
                                    // if it's a relative path...
                                    if (!Path.IsPathRooted(inputFile))
                                    {
                                        // make it relative from the XML file
                                        inputFile = Path.Combine(rootPath, inputFile);
                                    }
                                    // make sure the input file actually exists! It's an error if it doesn't.
                                    if (!File.Exists(inputFile))
                                    {
                                        throw new XmlException(StringMgr.GetString(
                                          "XmlInputNotExist",
                                          pathAttribute.Value
                                          ));
                                    }

                                    // add it to the group
                                    crunchGroup.Add(inputFile);
                                }
                                else
                                {
                                    // no required path attribute on the <input> element
                                    throw new XmlException(StringMgr.GetString("InputNoPathAttr"));
                                }
                            }
                            // add the crunch group to the list
                            crunchGroups.Add(crunchGroup);
                        }
                        else
                        {
                            // no required <input> nodes inside the <output> node
                            throw new XmlException(StringMgr.GetString("OutputNoInputNodes"));
                        }
                    }
                }
                else
                {
                    // no required <output> nodes
                    // throw an error to end all processing
                    throw new UsageException(ConsoleOutputMode.Console, "XmlNoOutputNodes");
                }
            }
            catch (XmlException e)
            {
                // throw an error indicating the XML error
                System.Diagnostics.Debug.WriteLine(e.ToString());
                throw new UsageException(ConsoleOutputMode.Console, "InputXmlError", e.Message);
            }
            // return an array of CrunchGroup objects
            return crunchGroups.ToArray();
        }

        #endregion

        #region resource processing

        private ResourceStrings ProcessResourceFile(string resourceFileName)
        {
            WriteProgress(
                StringMgr.GetString("ReadingResourceFile", Path.GetFileName(resourceFileName))
                );

            // which meethod we call to process the resources depends on the file extension
            // of the resources path given to us.
            switch (Path.GetExtension(resourceFileName).ToUpper(CultureInfo.InvariantCulture))
            {
                case ".RESX":
                    // process the resource file as a RESX xml file
                    return ProcessResXResources(resourceFileName);

                case ".RESOURCES":
                    // process the resource file as a compiles RESOURCES file
                    return ProcessResources(resourceFileName);

                default:
                    // no other types are supported
                    throw new UsageException(m_outputMode, "ResourceArgInvalidType");
            }
        }

        private static ResourceStrings ProcessResources(string resourceFileName)
        {
            // default return object is null, meaning we are outputting the JS code directly
            // and don't want to replace any referenced resources in the sources
            ResourceStrings resourceStrings = null;
            using (ResourceReader reader = new ResourceReader(resourceFileName))
            {
                // get an enumerator so we can itemize all the key/value pairs
                IDictionaryEnumerator enumerator = reader.GetEnumerator();

                // create an object out of the dictionary
                resourceStrings = new ResourceStrings(enumerator);
            }
            return resourceStrings;
        }

        private static ResourceStrings ProcessResXResources(string resourceFileName)
        {
            // default return object is null, meaning we are outputting the JS code directly
            // and don't want to replace any referenced resources in the sources
            ResourceStrings resourceStrings = null;
            using (ResXResourceReader reader = new ResXResourceReader(resourceFileName))
            {
                // get an enumerator so we can itemize all the key/value pairs
                IDictionaryEnumerator enumerator = reader.GetEnumerator();

                // create an object out of the dictionary
                resourceStrings = new ResourceStrings(enumerator);
            }
            return resourceStrings;
        }

        #endregion

        #region Utility methods

        /// <summary>
        /// Write an empty progress line
        /// </summary>
        private void WriteProgress()
        {
            WriteProgress(string.Empty);
        }

        /// <summary>
        /// Writes a progress string to the stderr stream.
        /// if in SILENT mode, writes to debug stream, not stderr!!!!
        /// </summary>
        /// <param name="format">format string</param>
        /// <param name="args">optional arguments</param>
        private void WriteProgress(string format, params object[] args)
        {
            if (m_outputMode != ConsoleOutputMode.Silent)
            {
                // if we are writing all output to one or more files, then progress messages will go
                // to stdout. If we are sending any minified output to stdout, then progress messages will
                // goto stderr; in that case, use the -silent option to suppress progress messages
                // from the stderr stream.
                var outputStream = m_outputToStandardOut ? Console.Error : Console.Out;

                // if we haven't yet output our header, do so now
                if (!m_headerWritten)
                {
                    outputStream.WriteLine(GetHeaderString());
                    outputStream.WriteLine();
                    m_headerWritten = true;
                }

                try
                {
                    outputStream.WriteLine(format, args);
                }
                catch (FormatException)
                {
                    // not enough args -- so don't use any
                    outputStream.WriteLine(format);
                }
            }
            else
            {
                // silent -- output to debug only
                try
                {
                    Debug.WriteLine(string.Format(CultureInfo.CurrentCulture, format, args));
                }
                catch (FormatException)
                {
                    // something wrong with the number of args -- don't use any
                    Debug.WriteLine(format);
                }
            }
        }

        /// <summary>
        /// Always writes string to stderr, even if in silent mode
        /// </summary>
        /// <param name="format">format string</param>
        /// <param name="args">optional arguments</param>
        private void WriteError(string format, params object[] args)
        {
            // don't output the header if in silent mode
            if (m_outputMode != ConsoleOutputMode.Silent && !m_headerWritten)
            {
                Console.Error.WriteLine(GetHeaderString());
                Console.Error.WriteLine();
                m_headerWritten = true;
            }

            // try outputting the error message
            try
            {
                Console.Error.WriteLine(format, args);
            }
            catch (FormatException)
            {
                // not enough args -- so don't use any
                Console.Error.WriteLine(format);
            }
        }

        /// <summary>
        /// Output a build error in a style consistent with MSBuild/Visual Studio standards
        /// so that the error gets properly picked up as a build-breaking error and displayed
        /// in the error pane
        /// </summary>
        /// <param name="location">source file(line,col), or empty for general tool error</param>
        /// <param name="subcategory">optional localizable subcategory (such as severity message)</param>
        /// <param name="isError">will output "error" if true, "warning" if false</param>
        /// <param name="code">non-localizable code indicating the error -- cannot contain spaces</param>
        /// <param name="format">localized text for error, can contain format placeholders</param>
        /// <param name="args">optional arguments for the format string</param>
        private static string CreateBuildError(string location, string subcategory, bool isError, string code, string message)
        {
            // if we didn't specify a location string, just use the name of this tool
            if (string.IsNullOrEmpty(location))
            {
                location = Path.GetFileName(
                    Assembly.GetExecutingAssembly().Location
                    );
            }

            // code cannot contain any spaces. If there are, trim it 
            // and replace any remaining spaces with underscores
            if (code.IndexOf(' ') >= 0)
            {
                code = code.Trim().Replace(' ', '_');
            }

            // if subcategory isn't null or empty and doesn't already end in a space, add it
            if (string.IsNullOrEmpty(subcategory))
            {
                // we are null or empty. empty is okay -- we can leave it along. But let's
                // turn nulls into emptys, too
                if (subcategory == null)
                {
                    subcategory = string.Empty;
                }
            }
            else if (!subcategory.EndsWith(" ", StringComparison.Ordinal))
            {
                // we are not empty and we don't end in a space -- add one now
                subcategory += " ";
            }
            // else we are not empty and we already end in a space, so all is good

            return string.Format(
                CultureInfo.CurrentCulture,
                "{0}: {1}{2} {3}: {4}",
                location, // not localized
                subcategory, // localizable, optional
                (isError ? "error" : "warning"), // NOT localized, only two options
                code, // not localized, cannot contain spaces
                message // localizable with optional arguments
                );
        }

        private static string GetHeaderString()
        {
            string description = string.Empty;
            string copyright = string.Empty;

            Assembly assembly = Assembly.GetExecutingAssembly();
            foreach (Attribute attr in assembly.GetCustomAttributes(false))
            {
                if (attr.GetType() == typeof(AssemblyDescriptionAttribute))
                {
                    description = ((AssemblyDescriptionAttribute)attr).Description;
                }
                else if (attr.GetType() == typeof(AssemblyCopyrightAttribute))
                {
                    copyright = ((AssemblyCopyrightAttribute)attr).Copyright;
                    copyright = copyright.Replace("©", "(c)");
                }
            }

            // combine the information for output
            StringBuilder sb = new StringBuilder();
            if (!string.IsNullOrEmpty(description)) { sb.AppendLine(description); }
            if (!string.IsNullOrEmpty(copyright)) { sb.AppendLine(copyright); }
            return sb.ToString();
        }

        private static long CalculateGzipSize(byte[] bytes)
        {
            using (MemoryStream memoryStream = new MemoryStream())
            {
                using (GZipStream gzipStream = new GZipStream(memoryStream, CompressionMode.Compress, true))
                {
                    gzipStream.Write(bytes, 0, bytes.Length);
                }
                return memoryStream.Position;
            }
        }

        #endregion
    }

    #region usage exceptions

#if !SILVERLIGHT
    [Serializable]
#endif
    public class UsageException : Exception
    {
#if !SILVERLIGHT
        [NonSerialized]
#endif
        private ConsoleOutputMode m_outputMode;
        public ConsoleOutputMode OutputMode { get { return m_outputMode; } }

        public UsageException(ConsoleOutputMode outputMode)
            : base(string.Empty)
        {
            m_outputMode = outputMode;
        }

        public UsageException(ConsoleOutputMode outputMode, string format, params object[] args)
            : base(StringMgr.GetString(format, args))
        {
            m_outputMode = outputMode;
        }

#if !SILVERLIGHT
        protected UsageException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            if (info == null)
            {
                throw new ArgumentException(StringMgr.GetString("InternalCompilerError"));
            }
            m_outputMode = ConsoleOutputMode.Console;
        }
#endif
        public UsageException()
        {
            m_outputMode = ConsoleOutputMode.Console;
        }

        public UsageException(string message)
            : this(ConsoleOutputMode.Console, message)
        {
        }

        public UsageException(string message, Exception innerException)
            : base(message, innerException)
        {
            m_outputMode = ConsoleOutputMode.Console;
        }
    }
    #endregion

    #region custom enumeration

    /// <summary>
    /// Method of outputting information
    /// </summary>
    public enum ConsoleOutputMode
    {
        Silent,
        Console
    }

    public enum InputType
    {
        Unknown = 0,
        JavaScript,
        Css
    }

    #endregion
}