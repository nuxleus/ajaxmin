// jsparser.cs
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
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;

namespace Microsoft.Ajax.Utilities
{

    //***************************************************************************************
    // JSParser
    //
    //  The JScript Parser.
    //***************************************************************************************
    public class JSParser
    {

        private Context m_sourceContext;
        private JSScanner m_scanner;
        private Context m_currentToken;
        private Context m_errorToken;  // used for errors to flag that the same token has to be returned.
        // We could have used just a boolean but having a Context does not
        // add any overhead and allow to really save the info, if that will ever be needed
        private int m_tokensSkipped;
        private const int c_MaxSkippedTokenNumber = 50;
        private NoSkipTokenSet m_noSkipTokenSet;
        private long m_goodTokensProcessed;

        private Block m_program;

        public ResourceStrings ResourceStrings { get; set; }

        // label related info
        private List<BlockType> m_blockType;
        private Dictionary<string, LabelInfo> m_labelTable;
        enum BlockType { Block, Loop, Switch, Finally }
        private int m_finallyEscaped;

        private class LabelInfo
        {
            public readonly int BlockIndex;
            public readonly int NestLevel;

            public LabelInfo(int blockIndex, int nestLevel)
            {
                BlockIndex = blockIndex;
                NestLevel = nestLevel;
            }
        }

        public CodeSettings Settings
        {
            get
            {
                // if it's null....
                if (m_settings == null)
                {
                    // just use the default settings
                    m_settings = new CodeSettings();
                }
                return m_settings;
            }
        }
        private CodeSettings m_settings;// = null;

        private int m_breakRecursion;// = 0;
        private static int s_cDummyName;
        private int m_severity;

        public event EventHandler<JScriptExceptionEventArgs> CompilerError;
        public event EventHandler<UndefinedReferenceEventArgs> UndefinedReference;

        internal GlobalScope GlobalScope
        {
            get
            {
                if (m_globalScope == null)
                {
                    m_globalScope = new GlobalScope(this);
                }
                return m_globalScope;
            }
        }
        private GlobalScope m_globalScope;

        internal Stack<ActivationObject> ScopeStack
        {
            get
            {
                if (m_scopeStack == null)
                {
                    // create the initial scope stack
                    m_scopeStack = new Stack<ActivationObject>();
                    m_scopeStack.Push(GlobalScope);
                }
                return m_scopeStack;
            }
        }
        private Stack<ActivationObject> m_scopeStack;// = null;

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Globalization", "CA1303:Do not pass literals as localized parameters", MessageId = "Microsoft.Ajax.Utilities.ContextError.#ctor(System.Boolean,System.Int32,System.String,System.String,System.String,System.String,System.Int32,System.Int32,System.Int32,System.Int32,System.String)")]
        internal bool OnCompilerError(JScriptException se)
        {
            if (CompilerError != null)
            {
                // get the offending line
                string line = se.LineText;

                // get the offending context
                string context = se.ErrorSegment;

                // if the context is empty, use the whole line
                if (string.IsNullOrEmpty(context))
                {
                    context = line;
                }

                CompilerError(this, new JScriptExceptionEventArgs(se, new ContextError(
                    se.IsError,
                    se.Severity,
                    GetSeverityString(se.Severity),
                    string.Format(CultureInfo.InvariantCulture, "JS{0}", (se.Error & (0xffff))),
                    se.HelpLink,
                    se.FileContext,
                    se.Line,
                    se.Column,
                    se.EndLine,
                    se.EndColumn,
                    se.Message + ": " + context)));
            }
            //true means carry on with compilation.
            return se.CanRecover;
        }

        private static string GetSeverityString(int severity)
        {
            // From jscriptexception.js:
            //
            //guide: 0 == there will be a run-time error if this code executes
            //       1 == the programmer probably did not intend to do this
            //       2 == this can lead to problems in the future.
            //       3 == this can lead to performance problems
            //       4 == this is just not right
            switch (severity)
            {
                case 0:
                    return StringMgr.GetString("Severity0");

                case 1:
                    return StringMgr.GetString("Severity1");

                case 2:
                    return StringMgr.GetString("Severity2");

                case 3:
                    return StringMgr.GetString("Severity3");

                case 4:
                    return StringMgr.GetString("Severity4");

                default:
                    return StringMgr.GetString("SeverityUnknown", severity);
            }
        }

        internal void OnUndefinedReference(UndefinedReferenceException ex)
        {
            if (UndefinedReference != null)
            {
                UndefinedReference(this, new UndefinedReferenceEventArgs(ex));
            }
        }

        //---------------------------------------------------------------------------------------
        // JSParser
        //
        // create a parser with a context. The context is the code that has to be compiled.
        // Typically used by the runtime
        //---------------------------------------------------------------------------------------
        public JSParser(string source)
        {
            Context context = new Context(new DocumentContext(this, source));

            m_sourceContext = context;
            m_currentToken = context.Clone();
            m_scanner = new JSScanner(m_currentToken);
            m_noSkipTokenSet = new NoSkipTokenSet();

            m_blockType = new List<BlockType>(16);
            m_labelTable = new Dictionary<string, LabelInfo>();
            m_severity = 5;
        }

        /// <summary>
        /// Create a new JSParser object.
        /// Obsolete -- the passed array of known global names will be ignored.
        /// </summary>
        /// <param name="source">JavaScript source to parse</param>
        /// <param name="globalVars">Obsolete - parameter IGNORED</param>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA1801:ReviewUnusedParameters", MessageId = "globalVars"), Obsolete("This constructor is obsolete - set known global names via the CodeSettings object")]
        public JSParser(string source, string[] globalVars)
            : this(source)
        {
        }

        public string FileContext
        {
            get { return m_sourceContext.Document.FileContext; }
            set 
            { 
                // make sure we set the file content on both the source context
                // AND the current token context
                m_currentToken.Document.FileContext = m_sourceContext.Document.FileContext = value; 
            }
        }

        private void InitializeScanner(CodeSettings settings, bool onlyRawTokens)
        {
            // save the settings
            // if we are passed null, just create a default settings object
            m_settings = settings ?? new CodeSettings();

            m_scanner.AllowEmbeddedAspNetBlocks = m_settings.AllowEmbeddedAspNetBlocks;
            m_scanner.IgnoreConditionalCompilation = m_settings.IgnoreConditionalCompilation;

            // set the skip-debug-blocks flag on the scanner if we are stripping debug statements
            m_scanner.SkipDebugBlocks = m_settings.StripDebugStatements
                 && m_settings.IsModificationAllowed(TreeModifications.StripDebugStatements);

            // set any defines
            m_scanner.UsePreprocessorDefines = m_settings.IsModificationAllowed(TreeModifications.PreprocessorDefines);
            if (m_scanner.UsePreprocessorDefines)
            {
                m_scanner.SetPreprocessorDefines(m_settings.PreprocessorDefines);
            }

            // the scanner will eat unnecessary @cc_on statements automatically so we are sure only
            // the first needed one is in the output. But we can turn that off with a kill switch, so
            // pass that flag to the scanner.
            m_scanner.EatUnnecessaryCCOn = m_settings.IsModificationAllowed(TreeModifications.RemoveUnnecessaryCCOnStatements);

            // set the raw tokens flag
            m_scanner.RawTokens = onlyRawTokens;
        }

        /// <summary>
        /// Preprocess the input only - don't generate an AST tree or do any other code analysis. 
        /// </summary>
        /// <param name="settings">settings to use in the scanner</param>
        /// <returns>the source as processed by the preprocessor</returns>
        public string PreprocessOnly(CodeSettings settings)
        {
            // initialize the scanner
            // make sure the RawTokens setting is on so that the scanner
            // just returns everything (after doing preprocessor evaluations)
            InitializeScanner(settings, true);

            // create an empty string builder
            var sb = new StringBuilder();

            // get the first token
            GetNextToken();

            // until we hit the end of the file...
            while (m_currentToken.Token != JSToken.EndOfFile)
            {
                // just output the token and grab the next one
                sb.Append(m_currentToken.Code);
                GetNextToken();
            }

            // return the resulting text
            return sb.ToString();
        }

        //---------------------------------------------------------------------------------------
        // Parse
        //
        // Parser main entry point. Parse all the source code and return the root of the AST
        //---------------------------------------------------------------------------------------
        public Block Parse(CodeSettings settings)
        {
            // initialize the scanner with our settings
            // make sure the RawTokens setting is OFF or we won't be able to create our AST
            InitializeScanner(settings, false);

            // make sure the global scope knows about our known global names
            GlobalScope.SetAssumedGlobals(m_settings.KnownGlobalNames);

            // parse a block of statements
            Block scriptBlock = ParseStatements();
            if (scriptBlock != null && Settings.MinifyCode)
            {
                // analyze the entire node tree (needed for hypercrunch)
                // root to leaf (top down)
                var analyzeVisitor = new AnalyzeNodeVisitor(this);
                analyzeVisitor.Visit(scriptBlock);

                // analyze the scope chain (also needed for hypercrunch)
                // root to leaf (top down)
                m_globalScope.AnalyzeScope();

                if (m_settings.CombineDuplicateLiterals)
                {
                    // check to see if we need to create a literal shortcuts and add them to
                    // the appropriate scope
                    m_globalScope.AnalyzeLiterals();
                }

                // then do a depth-first traversal of the scope tree. When we come to a global
                // field referenced by the scope, add it to the verboten set for this scope
                // and all its parent scopes, all the way up the chain. If we come across an
                // outer-scope local field, add it to the verboten field of this scope and
                // all scopes up to but not including the scope where it is actually defined.
                // leaf to root (bottom up)
                m_globalScope.ReserveFields();

                // if we want to crunch any names....
                if (m_settings.LocalRenaming != LocalRenaming.KeepAll
                    && m_settings.IsModificationAllowed(TreeModifications.LocalRenaming))
                {
                    // then do a top-down traversal of the scope tree. For each field that had not
                    // already been crunched (globals and outers will already be crunched), crunch
                    // the name with a crunch iterator that does not use any names in the verboten set.
                    m_globalScope.HyperCrunch();
                }

                // if we want to evaluate literal expressions, do so now
                if (m_settings.EvalLiteralExpressions)
                {
                    var visitor = new EvaluateLiteralVisitor(this);
                    visitor.Visit(scriptBlock);
                }

                // if any of the conditions we check for in the final pass are available, then
                // make the final pass
                if (m_settings.IsModificationAllowed(TreeModifications.BooleanLiteralsToNotOperators))
                {
                    var visitor = new FinalPassVisitor(this);
                    visitor.Visit(scriptBlock);
                }

                // we want to walk all the scopes to make sure that any generated
                // variables that haven't been crunched have been assigned valid
                // variable names that don't collide with any existing variables.
                m_globalScope.ValidateGeneratedNames();
            }
            return scriptBlock;
        }

        //---------------------------------------------------------------------------------------
        // ParseStatements
        //
        // statements :
        //   <empty> |
        //   statement statements
        //
        //---------------------------------------------------------------------------------------
        private Block ParseStatements()
        {
            m_program = new Block(m_sourceContext.Clone(), this);
            m_blockType.Add(BlockType.Block);
            m_errorToken = null;
            try
            {
                GetNextToken();
                m_noSkipTokenSet.Add(NoSkipTokenSet.s_StartStatementNoSkipTokenSet);
                m_noSkipTokenSet.Add(NoSkipTokenSet.s_TopLevelNoSkipTokenSet);

                try
                {
                    while (m_currentToken.Token != JSToken.EndOfFile)
                    {
                        AstNode ast = null;
                        try
                        {
                            // parse a statement -- pass true because we really want a SourceElement,
                            // which is a Statement OR a FunctionDeclaration. Technically, FunctionDeclarations
                            // are not statements!
                            ast = ParseStatement(true);
                        }
                        catch (RecoveryTokenException exc)
                        {
                            if (TokenInList(NoSkipTokenSet.s_TopLevelNoSkipTokenSet, exc)
                                || TokenInList(NoSkipTokenSet.s_StartStatementNoSkipTokenSet, exc))
                            {
                                ast = exc._partiallyComputedNode;
                                GetNextToken();
                            }
                            else
                            {
                                m_errorToken = null;
                                do
                                {
                                    GetNextToken();
                                } while (m_currentToken.Token != JSToken.EndOfFile && !TokenInList(NoSkipTokenSet.s_TopLevelNoSkipTokenSet, m_currentToken.Token)
                                  && !TokenInList(NoSkipTokenSet.s_StartStatementNoSkipTokenSet, m_currentToken.Token));
                            }
                        }

                        if (null != ast)
                            m_program.Append(ast);
                    }

                    if (m_scanner.HasImportantComments && m_settings.IsModificationAllowed(TreeModifications.PreserveImportantComments))
                    {
                        // we have important comments before the EOF. Add the comment(s) to the program.
                        Context commentContext;
                        while((commentContext = m_scanner.PopImportantComment()) != null)
                        {
                            m_program.Append(new ImportantComment(commentContext, this));
                        }
                    }
                }
                finally
                {
                    m_noSkipTokenSet.Remove(NoSkipTokenSet.s_TopLevelNoSkipTokenSet);
                    m_noSkipTokenSet.Remove(NoSkipTokenSet.s_StartStatementNoSkipTokenSet);
                }

            }
            catch (EndOfFileException)
            {
            }
            catch (ScannerException se)
            {
                // a scanner exception implies that the end of file has been reached with an error.
                // Mark the end of file as the error location
                EOFError(se.Error);
            }
            return m_program;
        }

        //---------------------------------------------------------------------------------------
        // ParseStatement
        //
        //  OptionalStatement:
        //    Statement |
        //    <empty>
        //
        //  Statement :
        //    Block |
        //  VariableStatement |
        //  EmptyStatement |
        //  ExpressionStatement |
        //  IfStatement |
        //  IterationStatement |
        //  ContinueStatement |
        //  BreakStatement |
        //  ReturnStatement |
        //  WithStatement |
        //  LabeledStatement |
        //  SwitchStatement |
        //  ThrowStatement |
        //  TryStatement |
        //  FunctionDeclaration
        //
        // IterationStatement :
        //    'for' '(' ForLoopControl ')' |                  ===> ForStatement
        //    'do' Statement 'while' '(' Expression ')' |     ===> DoStatement
        //    'while' '(' Expression ')' Statement            ===> WhileStatement
        //
        //---------------------------------------------------------------------------------------

        // ParseStatement deals with the end of statement issue (EOL vs ';') so if any of the
        // ParseXXX routine does it as well, it should return directly from the switch statement
        // without any further execution in the ParseStatement
        private AstNode ParseStatement(bool fSourceElement)
        {
            AstNode statement = null;
            if (m_scanner.HasImportantComments && m_settings.IsModificationAllowed(TreeModifications.PreserveImportantComments))
            {
                // we have at least one important comment before the upcoming statement.
                // pop the first important comment off the queue, return that node instead.
                // don't advance the token -- we'll probably be coming back again for the next one (if any)
                statement = new ImportantComment(m_scanner.PopImportantComment(), this);
            }
            else
            {
                String id = null;

                switch (m_currentToken.Token)
                {
                    case JSToken.EndOfFile:
                        EOFError(JSError.ErrorEndOfFile);
                        throw new EndOfFileException(); // abort parsing, get back to the main parse routine
                    case JSToken.Semicolon:
                        // make an empty statement
                        statement = new Block(m_currentToken.Clone(), this);
                        GetNextToken();
                        return statement;
                    case JSToken.RightCurly:
                        ReportError(JSError.SyntaxError);
                        SkipTokensAndThrow();
                        break;
                    case JSToken.LeftCurly:
                        return ParseBlock();
                    case JSToken.Debugger:
                        return ParseDebuggerStatement();
                    case JSToken.Var:
                        return ParseVariableStatement((FieldAttributes)0);
                    case JSToken.If:
                        return ParseIfStatement();
                    case JSToken.For:
                        return ParseForStatement();
                    case JSToken.Do:
                        return ParseDoStatement();
                    case JSToken.While:
                        return ParseWhileStatement();
                    case JSToken.Continue:
                        statement = ParseContinueStatement();
                        if (null == statement)
                            return new Block(CurrentPositionContext(), this);
                        else
                            return statement;
                    case JSToken.Break:
                        statement = ParseBreakStatement();
                        if (null == statement)
                            return new Block(CurrentPositionContext(), this);
                        else
                            return statement;
                    case JSToken.Return:
                        statement = ParseReturnStatement();
                        if (null == statement)
                            return new Block(CurrentPositionContext(), this);
                        else
                            return statement;
                    case JSToken.With:
                        return ParseWithStatement();
                    case JSToken.Switch:
                        return ParseSwitchStatement();
                    case JSToken.Throw:
                        statement = ParseThrowStatement();
                        if (statement == null)
                            return new Block(CurrentPositionContext(), this);
                        else
                            break;
                    case JSToken.Try:
                        return ParseTryStatement();
                    case JSToken.Function:
                        // parse a function declaration
                        FunctionObject function = ParseFunction(FunctionType.Declaration, m_currentToken.Clone());

                        // now, if we aren't parsing source elements (directly in global scope or function body)
                        // then we want to throw a warning that different browsers will treat this function declaration
                        // differently. Technically, this location is not allowed. IE and most other browsers will 
                        // simply treat it like every other function declaration in this scope. Firefox, however, won't
                        // add this function declaration's name to the containing scope until the function declaration
                        // is actually "executed." So if you try to call it BEFORE, you will get a "not defined" error.
                        if (!fSourceElement)
                        {
                            ReportError(JSError.MisplacedFunctionDeclaration, function.IdContext, true);
                        }

                        return function;
                    case JSToken.Else:
                        ReportError(JSError.InvalidElse);
                        SkipTokensAndThrow();
                        break;
                    case JSToken.ConditionalCommentStart:
                        return ParseStatementLevelConditionalComment(fSourceElement);
                    case JSToken.ConditionalCompilationOn:
                        {
                            ConditionalCompilationOn ccOn = new ConditionalCompilationOn(m_currentToken.Clone(), this);
                            GetNextToken();
                            return ccOn;
                        }
                    case JSToken.ConditionalCompilationSet:
                        return ParseConditionalCompilationSet();
                    case JSToken.ConditionalCompilationIf:
                        return ParseConditionalCompilationIf(false);
                    case JSToken.ConditionalCompilationElseIf:
                        return ParseConditionalCompilationIf(true);
                    case JSToken.ConditionalCompilationElse:
                        {
                            ConditionalCompilationElse elseStatement = new ConditionalCompilationElse(m_currentToken.Clone(), this);
                            GetNextToken();
                            return elseStatement;
                        }
                    case JSToken.ConditionalCompilationEnd:
                        {
                            ConditionalCompilationEnd endStatement = new ConditionalCompilationEnd(m_currentToken.Clone(), this);
                            GetNextToken();
                            return endStatement;
                        }

                    case JSToken.AspNetBlock:
                        return ParseAspNetBlock(consumeSemicolonIfPossible: true);

                    default:
                        m_noSkipTokenSet.Add(NoSkipTokenSet.s_EndOfStatementNoSkipTokenSet);
                        bool exprError = false;
                        try
                        {
                            bool bAssign, canBeAttribute = true;
                            // if this statement starts with a function within parens, we want to know now
                            bool parenFunction = (m_currentToken.Token == JSToken.LeftParenthesis && m_scanner.PeekToken() == JSToken.Function);
                            statement = ParseUnaryExpression(out bAssign, ref canBeAttribute, false);
                            if (statement != null && parenFunction)
                            {
                                FunctionObject functionObject = statement.LeftHandSide as FunctionObject;
                                if (functionObject != null)
                                {
                                    functionObject.LeftHandFunctionExpression = true;
                                }
                            }
                            if (canBeAttribute)
                            {
                                // look for labels
                                if (statement is Lookup)
                                {
                                    if (JSToken.Colon == m_currentToken.Token)
                                    {
                                        // can be a label
                                        id = statement.ToString();
                                        if (m_labelTable.ContainsKey(id))
                                        {
                                            // there is already a label with that name. Ignore the current label
                                            ReportError(JSError.BadLabel, statement.Context.Clone(), true);
                                            id = null;
                                            GetNextToken(); // skip over ':'
                                            return new Block(CurrentPositionContext(), this);
                                        }
                                        else
                                        {
                                            GetNextToken();
                                            int labelNestCount = m_labelTable.Count + 1;
                                            m_labelTable.Add(id, new LabelInfo(m_blockType.Count, labelNestCount));
                                            if (JSToken.EndOfFile != m_currentToken.Token)
                                            {
                                                statement = new LabeledStatement(
                                                  statement.Context.Clone(),
                                                  this,
                                                  id,
                                                  labelNestCount,
                                                  ParseStatement(fSourceElement)
                                                  );
                                            }
                                            else
                                            {
                                                // end of the file!
                                                //just pass null for the labeled statement
                                                statement = new LabeledStatement(
                                                  statement.Context.Clone(),
                                                  this,
                                                  id,
                                                  labelNestCount,
                                                  null
                                                  );
                                            }
                                            m_labelTable.Remove(id);
                                            return statement;
                                        }
                                    }
                                }
                            }
                            statement = ParseExpression(statement, false, bAssign, JSToken.None);
                        }
                        catch (RecoveryTokenException exc)
                        {
                            if (exc._partiallyComputedNode != null)
                                statement = exc._partiallyComputedNode;

                            if (statement == null)
                            {
                                m_noSkipTokenSet.Remove(NoSkipTokenSet.s_EndOfStatementNoSkipTokenSet);
                                exprError = true;
                                SkipTokensAndThrow();
                            }

                            if (IndexOfToken(NoSkipTokenSet.s_EndOfStatementNoSkipTokenSet, exc) == -1)
                            {
                                exc._partiallyComputedNode = statement;
                                throw;
                            }
                        }
                        finally
                        {
                            if (!exprError)
                                m_noSkipTokenSet.Remove(NoSkipTokenSet.s_EndOfStatementNoSkipTokenSet);
                        }
                        break;
                }

                if (JSToken.Semicolon == m_currentToken.Token)
                {
                    statement.Context.UpdateWith(m_currentToken);
                    GetNextToken();
                }
                else if (!m_scanner.GotEndOfLine && JSToken.RightCurly != m_currentToken.Token && JSToken.EndOfFile != m_currentToken.Token)
                {
                    ReportError(JSError.NoSemicolon, true);
                }
            }
            return statement;
        }

        AstNode ParseStatementLevelConditionalComment(bool fSourceElement)
        {
            Context context = m_currentToken.Clone();
            ConditionalCompilationComment conditionalComment = new ConditionalCompilationComment(context, this);

            GetNextToken();
            while(m_currentToken.Token != JSToken.ConditionalCommentEnd)
            {
                conditionalComment.Append(ParseStatement(fSourceElement));
            }

            GetNextToken();

            // if the conditional comment is empty (for whatever reason), then
            // we don't want to return anything -- we found nothing.
            return conditionalComment.Statements.Count > 0 ? conditionalComment : null;
        }

        ConditionalCompilationSet ParseConditionalCompilationSet()
        {
            Context context = m_currentToken.Clone();
            string variableName = null;
            AstNode value = null;
            GetNextToken();
            if (m_currentToken.Token == JSToken.PreprocessorConstant)
            {
                context.UpdateWith(m_currentToken);
                variableName = m_currentToken.Code;
                GetNextToken();
                if (m_currentToken.Token == JSToken.Assign)
                {
                    context.UpdateWith(m_currentToken);
                    GetNextToken();
                    value = ParseExpression(false);
                    if (value != null)
                    {
                        context.UpdateWith(value.Context);
                    }
                    else
                    {
                        m_currentToken.HandleError(JSError.ExpressionExpected);
                    }
                }
                else
                {
                    m_currentToken.HandleError(JSError.NoEqual);
                }
            }
            else
            {
                m_currentToken.HandleError(JSError.NoIdentifier);
            }

            return new ConditionalCompilationSet(context, this, variableName, value);
        }

        ConditionalCompilationStatement ParseConditionalCompilationIf(bool isElseIf)
        {
            Context context = m_currentToken.Clone();
            AstNode condition = null;
            GetNextToken();
            if (m_currentToken.Token == JSToken.LeftParenthesis)
            {
                context.UpdateWith(m_currentToken);
                GetNextToken();
                condition = ParseExpression(false);
                if (condition != null)
                {
                    context.UpdateWith(condition.Context);
                }
                else
                {
                    m_currentToken.HandleError(JSError.ExpressionExpected);
                }

                if (m_currentToken.Token == JSToken.RightParenthesis)
                {
                    context.UpdateWith(m_currentToken);
                    GetNextToken();
                }
                else
                {
                    m_currentToken.HandleError(JSError.NoRightParenthesis);
                }
            }
            else
            {
                m_currentToken.HandleError(JSError.NoLeftParenthesis);
            }

            if (isElseIf)
            {
                return new ConditionalCompilationElseIf(context, this, condition);
            }

            return new ConditionalCompilationIf(context, this, condition);
        }

        //---------------------------------------------------------------------------------------
        // ParseBlock
        //
        //  Block :
        //    '{' OptionalStatements '}'
        //---------------------------------------------------------------------------------------
        Block ParseBlock()
        {
            Context ctx;
            return ParseBlock(out ctx);
        }

        Block ParseBlock(out Context closingBraceContext)
        {
            closingBraceContext = null;
            m_blockType.Add(BlockType.Block);
            Block codeBlock = new Block(m_currentToken.Clone(), this);
            GetNextToken();

            m_noSkipTokenSet.Add(NoSkipTokenSet.s_StartStatementNoSkipTokenSet);
            m_noSkipTokenSet.Add(NoSkipTokenSet.s_BlockNoSkipTokenSet);
            try
            {
                try
                {
                    while (JSToken.RightCurly != m_currentToken.Token)
                    {
                        try
                        {
                            // pass false because we really only want Statements, and FunctionDeclarations
                            // are technically not statements. We'll still handle them, but we'll issue a warning.
                            codeBlock.Append(ParseStatement(false));
                        }
                        catch (RecoveryTokenException exc)
                        {
                            if (exc._partiallyComputedNode != null)
                                codeBlock.Append(exc._partiallyComputedNode);
                            if (IndexOfToken(NoSkipTokenSet.s_StartStatementNoSkipTokenSet, exc) == -1)
                                throw;
                        }
                    }
                }
                catch (RecoveryTokenException exc)
                {
                    if (IndexOfToken(NoSkipTokenSet.s_BlockNoSkipTokenSet, exc) == -1)
                    {
                        exc._partiallyComputedNode = codeBlock;
                        throw;
                    }
                }
            }
            finally
            {
                m_noSkipTokenSet.Remove(NoSkipTokenSet.s_BlockNoSkipTokenSet);
                m_noSkipTokenSet.Remove(NoSkipTokenSet.s_StartStatementNoSkipTokenSet);
                m_blockType.RemoveAt(m_blockType.Count - 1);
            }

            closingBraceContext = m_currentToken.Clone();
            // update the block context
            codeBlock.Context.UpdateWith(m_currentToken);
            GetNextToken();
            return codeBlock;
        }

        private AstNode ParseAspNetBlock(bool consumeSemicolonIfPossible)
        {
            Context context = m_currentToken.Clone();
            GetNextToken();
            string aspNetBlockText = context.Code;
            bool blockTerminatedByExplicitSemicolon = false;

            // This token may have a semi-colon after it or the semi-colon may come from the asp.net 
            // block. If we have one consume it.
            if (JSToken.Semicolon == m_currentToken.Token &&
                consumeSemicolonIfPossible)
            {
                // add the semicolon to the cloned context
                context.UpdateWith(m_currentToken);
                // and skip it
                GetNextToken();
                blockTerminatedByExplicitSemicolon = true;
            }

            // return the new AST object
            return new AspNetBlockNode(context, this, aspNetBlockText, blockTerminatedByExplicitSemicolon);
        }

        //---------------------------------------------------------------------------------------
        // ParseDebuggerStatement
        //
        //  DebuggerStatement :
        //    'debugger'
        //
        // This function may return a null AST under error condition. The caller should handle
        // that case.
        // Regardless of error conditions, on exit the parser points to the first token after
        // the debugger statement
        //---------------------------------------------------------------------------------------
        private AstNode ParseDebuggerStatement()
        {
            // clone the current context
            Context context = m_currentToken.Clone();

            // move to the next token
            GetNextToken();

            // this token can only be a stand-alone statement
            if (JSToken.Semicolon == m_currentToken.Token)
            {
                // add the semicolon to the cloned context
                context.UpdateWith(m_currentToken);
                // and skip it
                GetNextToken();
            }
            else if (JSToken.RightCurly != m_currentToken.Token && !m_scanner.GotEndOfLine)
            {
                // if it is anything else, it's an error
                ReportError(JSError.NoSemicolon, true);
            }
            // return the new AST object
            return new DebuggerNode(context, this);
        }

        //---------------------------------------------------------------------------------------
        // ParseVariableStatement
        //
        //  VariableStatement :
        //    'var' VariableDeclarationList
        //
        //  VariableDeclarationList :
        //    VariableDeclaration |
        //    VariableDeclaration ',' VariableDeclarationList
        //
        //  VariableDeclaration :
        //    Identifier Initializer
        //
        //  Initializer :
        //    <empty> |
        //    '=' AssignmentExpression
        //---------------------------------------------------------------------------------------
        private AstNode ParseVariableStatement(FieldAttributes visibility)
        {
            Var varList = new Var(m_currentToken.Clone(), this);
            bool single = true;
            AstNode vdecl = null;
            AstNode identInit = null;

            for (; ; )
            {
                m_noSkipTokenSet.Add(NoSkipTokenSet.s_EndOfLineToken);
                try
                {
                    identInit = ParseIdentifierInitializer(JSToken.None, visibility);
                }
                catch (RecoveryTokenException exc)
                {
                    // an exception is passing by, possibly bringing some info, save the info if any
                    if (exc._partiallyComputedNode != null)
                    {
                        if (!single)
                        {
                            varList.Append(exc._partiallyComputedNode);
                            varList.Context.UpdateWith(exc._partiallyComputedNode.Context);
                            exc._partiallyComputedNode = varList;
                        }
                    }
                    if (IndexOfToken(NoSkipTokenSet.s_EndOfLineToken, exc) == -1)
                        throw;
                    else
                    {
                        if (single)
                            identInit = exc._partiallyComputedNode;
                    }
                }
                finally
                {
                    m_noSkipTokenSet.Remove(NoSkipTokenSet.s_EndOfLineToken);
                }

                if (identInit != null)
                {
                    vdecl = identInit;
                    varList.Append(vdecl);
                }

                if (JSToken.Semicolon == m_currentToken.Token
                    || JSToken.RightCurly == m_currentToken.Token)
                {
                    if (JSToken.Semicolon == m_currentToken.Token)
                    {
                        vdecl.Context.UpdateWith(m_currentToken);
                        GetNextToken();
                    }
                    break;
                }
                
                if (JSToken.Comma == m_currentToken.Token)
                {
                    single = false;
                    continue;
                }
                
                if (m_scanner.GotEndOfLine)
                {
                    break;
                }

                // assume the variable statement was terminated and move on
                ReportError(JSError.NoSemicolon, true);
                break;
            }

            if (vdecl != null)
            {
                varList.Context.UpdateWith(vdecl.Context);
            }
            return varList;
        }

        //---------------------------------------------------------------------------------------
        // ParseIdentifierInitializer
        //
        //  Does the real work of parsing a single variable declaration.
        //  inToken is JSToken.In whenever the potential expression that initialize a variable
        //  cannot contain an 'in', as in the for statement. inToken is JSToken.None otherwise
        //---------------------------------------------------------------------------------------
        private AstNode ParseIdentifierInitializer(JSToken inToken,
                                                                FieldAttributes visibility)
        {
            string variableName = null;
            AstNode assignmentExpr = null;
            RecoveryTokenException except = null;

            GetNextToken();
            if (JSToken.Identifier != m_currentToken.Token)
            {
                String identifier = JSKeyword.CanBeIdentifier(m_currentToken.Token);
                if (null != identifier)
                {
                    ForceReportInfo(JSError.KeywordUsedAsIdentifier);
                    variableName = identifier;
                }
                else
                {
                    // make up an identifier assume we're done with the var statement
                    ReportError(JSError.NoIdentifier);
                    return null;
                }
            }
            else
            {
                variableName = m_scanner.GetIdentifier();
            }
            Context idContext = m_currentToken.Clone();
            Context context = m_currentToken.Clone();

            bool ccSpecialCase = false;
            bool ccOn = false;
            GetNextToken();

            m_noSkipTokenSet.Add(NoSkipTokenSet.s_VariableDeclNoSkipTokenSet);
            try
            {
                if (m_currentToken.Token == JSToken.ConditionalCommentStart)
                {
                    ccSpecialCase = true;

                    GetNextToken();
                    if (m_currentToken.Token == JSToken.ConditionalCompilationOn)
                    {
                        GetNextToken();
                        if (m_currentToken.Token == JSToken.ConditionalCommentEnd)
                        {
                            // forget about it; just ignore the whole thing because it's empty
                            ccSpecialCase = false;
                        }
                        else
                        {
                            ccOn = true;
                        }
                    }
                }

                if (JSToken.Assign == m_currentToken.Token || JSToken.Equal == m_currentToken.Token)
                {
                    if (JSToken.Equal == m_currentToken.Token)
                    {
                        ReportError(JSError.NoEqual, true);
                    }

                    // move past the equals sign
                    GetNextToken();
                    if (m_currentToken.Token == JSToken.ConditionalCommentEnd)
                    {
                        // so we have var id/*@ =@*/ or var id//@=<EOL>
                        // we only support the equal sign inside conditional comments IF
                        // the initializer value is there as well.
                        ccSpecialCase = false;
                        m_currentToken.HandleError(JSError.ConditionalCompilationTooComplex);
                        GetNextToken();
                    }

                    try
                    {
                        assignmentExpr = ParseExpression(true, inToken);
                    }
                    catch (RecoveryTokenException exc)
                    {
                        assignmentExpr = exc._partiallyComputedNode;
                        throw;
                    }
                    finally
                    {
                        if (null != assignmentExpr)
                        {
                            context.UpdateWith(assignmentExpr.Context);
                        }
                    }
                }
                else if (ccSpecialCase)
                {
                    // so we have "var id /*@" or "var id //@", but the next character is NOT an equal sign.
                    // we don't support this structure, either.
                    ccSpecialCase = false;
                    m_currentToken.HandleError(JSError.ConditionalCompilationTooComplex);

                    // skip to end of conditional comment
                    while (m_currentToken.Token != JSToken.EndOfFile && m_currentToken.Token != JSToken.ConditionalCommentEnd)
                    {
                        GetNextToken();
                    }
                    GetNextToken();
                }

                // if the current token is not an end-of-conditional-comment token now,
                // then we're not in our special case scenario
                if (m_currentToken.Token == JSToken.ConditionalCommentEnd)
                {
                    GetNextToken();
                }
                else if (ccSpecialCase)
                {
                    // we have "var id/*@=expr" but the next token is not the closing comment.
                    // we don't support this structure, either.
                    ccSpecialCase = false;
                    m_currentToken.HandleError(JSError.ConditionalCompilationTooComplex);

                    // the assignment expression was apparently wiothin the conditional compilation
                    // comment, but we're going to ignore it. So clear it out.
                    assignmentExpr = null;

                    // skip to end of conditional comment
                    while (m_currentToken.Token != JSToken.EndOfFile && m_currentToken.Token != JSToken.ConditionalCommentEnd)
                    {
                        GetNextToken();
                    }
                    GetNextToken();
                }
            }
            catch (RecoveryTokenException exc)
            {
                // If the exception is in the vardecl no-skip set then we successfully
                // recovered to the end of the declaration and can just return
                // normally.  Otherwise we re-throw after constructing the partial result.  
                if (IndexOfToken(NoSkipTokenSet.s_VariableDeclNoSkipTokenSet, exc) == -1)
                    except = exc;
            }
            finally
            {
                m_noSkipTokenSet.Remove(NoSkipTokenSet.s_VariableDeclNoSkipTokenSet);
            }

            VariableDeclaration result = new VariableDeclaration(context, this, variableName, idContext, assignmentExpr, visibility);

            result.IsCCSpecialCase = ccSpecialCase;
            if (ccSpecialCase)
            {
                // by default, set the flag depending on whether we encountered a @cc_on statement.
                // might be overridden by the node in analyze phase
                result.UseCCOn = ccOn;
            }

            if (null != except)
            {
                except._partiallyComputedNode = result;
                throw except;
            }

            return result;
        }

        //---------------------------------------------------------------------------------------
        // ParseIfStatement
        //
        //  IfStatement :
        //    'if' '(' Expression ')' Statement ElseStatement
        //
        //  ElseStatement :
        //    <empty> |
        //    'else' Statement
        //---------------------------------------------------------------------------------------
        private IfNode ParseIfStatement()
        {
            Context ifCtx = m_currentToken.Clone();
            AstNode condition = null;
            AstNode trueBranch = null;
            AstNode falseBranch = null;

            m_blockType.Add(BlockType.Block);
            try
            {
                // parse condition
                GetNextToken();
                m_noSkipTokenSet.Add(NoSkipTokenSet.s_BlockConditionNoSkipTokenSet);
                try
                {
                    if (JSToken.LeftParenthesis != m_currentToken.Token)
                        ReportError(JSError.NoLeftParenthesis);
                    GetNextToken();
                    condition = ParseExpression();

                    // parse statements
                    if (JSToken.RightParenthesis != m_currentToken.Token)
                    {
                        ifCtx.UpdateWith(condition.Context);
                        ReportError(JSError.NoRightParenthesis);
                    }
                    else
                        ifCtx.UpdateWith(m_currentToken);

                    GetNextToken();
                }
                catch (RecoveryTokenException exc)
                {
                    // make up an if condition
                    if (exc._partiallyComputedNode != null)
                        condition = exc._partiallyComputedNode;
                    else
                        condition = new ConstantWrapper(true, PrimitiveType.Boolean, CurrentPositionContext(), this);

                    if (IndexOfToken(NoSkipTokenSet.s_BlockConditionNoSkipTokenSet, exc) == -1)
                    {
                        exc._partiallyComputedNode = null; // really not much to pass up
                        // the if condition was so bogus we do not have a chance to make an If node, give up
                        throw;
                    }
                    else
                    {
                        if (exc._token == JSToken.RightParenthesis)
                            GetNextToken();
                    }
                }
                finally
                {
                    m_noSkipTokenSet.Remove(NoSkipTokenSet.s_BlockConditionNoSkipTokenSet);
                }

                // if this is an assignment, throw a warning in case the developer
                // meant to use == instead of =
                // but no warning if the condition is wrapped in parens. We can know if it's wrapped in parens
                // if the first character of the context is a paren and it's BEFORE the context of the leftmost
                // context of the condition.
                BinaryOperator binOp = condition as BinaryOperator;
                if (binOp != null && binOp.OperatorToken == JSToken.Assign
                    && condition.Context != null && condition.Context.Code != null 
                    && !(condition.Context.Code.StartsWith("(", StringComparison.Ordinal) && condition.Context.IsBefore(binOp.LeftHandSide.Context)))
                {
                    condition.Context.HandleError(JSError.SuspectAssignment);
                }

                m_noSkipTokenSet.Add(NoSkipTokenSet.s_IfBodyNoSkipTokenSet);
                if (JSToken.Semicolon == m_currentToken.Token)
                {
                    ForceReportInfo(JSError.SuspectSemicolon);
                }
                else if (JSToken.LeftCurly != m_currentToken.Token)
                {
                    // if the statements aren't withing curly-braces, throw a possible error
                    ReportError(JSError.StatementBlockExpected, ifCtx, true);
                }

                try
                {
                    // parse a Statement, not a SourceElement
                    trueBranch = ParseStatement(false);
                }
                catch (RecoveryTokenException exc)
                {
                    // make up a block for the if part
                    if (exc._partiallyComputedNode != null)
                        trueBranch = exc._partiallyComputedNode;
                    else
                        trueBranch = new Block(CurrentPositionContext(), this);
                    if (IndexOfToken(NoSkipTokenSet.s_IfBodyNoSkipTokenSet, exc) == -1)
                    {
                        // we have to pass the exception to someone else, make as much as you can from the if
                        exc._partiallyComputedNode = new IfNode(ifCtx, this, condition, trueBranch, falseBranch);
                        throw;
                    }
                }
                finally
                {
                    m_noSkipTokenSet.Remove(NoSkipTokenSet.s_IfBodyNoSkipTokenSet);
                }

                // parse else, if any
                if (JSToken.Else == m_currentToken.Token)
                {
                    Context elseCtx = m_currentToken.Clone();
                    GetNextToken();
                    if (JSToken.Semicolon == m_currentToken.Token)
                        ForceReportInfo(JSError.SuspectSemicolon);
                    // if the statements aren't withing curly-braces, throw a possible error
                    if (JSToken.LeftCurly != m_currentToken.Token
                      && JSToken.If != m_currentToken.Token)
                    {
                        ReportError(JSError.StatementBlockExpected, elseCtx, true);
                    }
                    try
                    {
                        // parse a Statement, not a SourceElement
                        falseBranch = ParseStatement(false);
                    }
                    catch (RecoveryTokenException exc)
                    {
                        // make up a block for the else part
                        if (exc._partiallyComputedNode != null)
                            falseBranch = exc._partiallyComputedNode;
                        else
                            falseBranch = new Block(CurrentPositionContext(), this);
                        exc._partiallyComputedNode = new IfNode(ifCtx, this, condition, trueBranch, falseBranch);
                        throw;
                    }
                }
            }
            finally
            {
                m_blockType.RemoveAt(m_blockType.Count - 1);
            }

            return new IfNode(ifCtx, this, condition, trueBranch, falseBranch);
        }

        //---------------------------------------------------------------------------------------
        // ParseForStatement
        //
        //  ForStatement :
        //    'for' '(' OptionalExpressionNoIn ';' OptionalExpression ';' OptionalExpression ')'
        //    'for' '(' 'var' VariableDeclarationListNoIn ';' OptionalExpression ';' OptionalExpression ')'
        //    'for' '(' LeftHandSideExpression 'in' Expression')'
        //    'for' '(' 'var' Identifier OptionalInitializerNoIn 'in' Expression')'
        //
        //  OptionalExpressionNoIn :
        //    <empty> |
        //    ExpressionNoIn // same as Expression but does not process 'in' as an operator
        //
        //  OptionalInitializerNoIn :
        //    <empty> |
        //    InitializerNoIn // same as initializer but does not process 'in' as an operator
        //---------------------------------------------------------------------------------------
        private AstNode ParseForStatement()
        {
            m_blockType.Add(BlockType.Loop);
            AstNode forNode = null;
            try
            {
                Context forCtx = m_currentToken.Clone();
                GetNextToken();
                if (JSToken.LeftParenthesis != m_currentToken.Token)
                    ReportError(JSError.NoLeftParenthesis);
                GetNextToken();
                bool isForIn = false, recoveryInForIn = false;
                AstNode lhs = null, initializer = null, condOrColl = null, increment = null;

                try
                {
                    if (JSToken.Var == m_currentToken.Token)
                    {
                        isForIn = true;
                        Var varList = new Var(m_currentToken.Clone(), this);
                        varList.Append(ParseIdentifierInitializer(JSToken.In, (FieldAttributes)0));

                        // a list of variable initializers is allowed only in a for(;;)
                        while (JSToken.Comma == m_currentToken.Token)
                        {
                            isForIn = false;
                            varList.Append(ParseIdentifierInitializer(JSToken.In, (FieldAttributes)0));
                            //initializer = new Comma(initializer.context.CombineWith(var.context), initializer, var);
                        }

                        initializer = varList;

                        // if it could still be a for..in, now it's time to get the 'in'
                        if (isForIn)
                        {
                            if (JSToken.In == m_currentToken.Token)
                            {
                                GetNextToken();
                                condOrColl = ParseExpression();
                            }
                            else
                                isForIn = false;
                        }
                    }
                    else
                    {
                        if (JSToken.Semicolon != m_currentToken.Token)
                        {
                            bool isLHS;
                            initializer = ParseUnaryExpression(out isLHS, false);
                            if (isLHS && JSToken.In == m_currentToken.Token)
                            {
                                isForIn = true;
                                lhs = initializer;
                                initializer = null;
                                GetNextToken();
                                m_noSkipTokenSet.Add(NoSkipTokenSet.s_BlockConditionNoSkipTokenSet);
                                try
                                {
                                    condOrColl = ParseExpression();
                                }
                                catch (RecoveryTokenException exc)
                                {
                                    if (IndexOfToken(NoSkipTokenSet.s_BlockConditionNoSkipTokenSet, exc) == -1)
                                    {
                                        exc._partiallyComputedNode = null;
                                        throw;
                                    }
                                    else
                                    {
                                        if (exc._partiallyComputedNode == null)
                                            condOrColl = new ConstantWrapper(true, PrimitiveType.Boolean, CurrentPositionContext(), this); // what could we put here?
                                        else
                                            condOrColl = exc._partiallyComputedNode;
                                    }
                                    if (exc._token == JSToken.RightParenthesis)
                                    {
                                        GetNextToken();
                                        recoveryInForIn = true;
                                    }
                                }
                                finally
                                {
                                    m_noSkipTokenSet.Remove(NoSkipTokenSet.s_BlockConditionNoSkipTokenSet);
                                }
                            }
                            else
                                initializer = ParseExpression(initializer, false, isLHS, JSToken.In);
                        }
                    }
                }
                catch (RecoveryTokenException exc)
                {
                    // error is too early abort for
                    exc._partiallyComputedNode = null;
                    throw;
                }

                // at this point we know whether or not is a for..in
                if (isForIn)
                {
                    if (!recoveryInForIn)
                    {
                        if (JSToken.RightParenthesis != m_currentToken.Token)
                            ReportError(JSError.NoRightParenthesis);
                        forCtx.UpdateWith(m_currentToken);
                        GetNextToken();
                    }
                    AstNode body = null;
                    // if the statements aren't withing curly-braces, throw a possible error
                    if (JSToken.LeftCurly != m_currentToken.Token)
                    {
                        ReportError(JSError.StatementBlockExpected, forCtx, true);
                    }
                    try
                    {
                        // parse a Statement, not a SourceElement
                        body = ParseStatement(false);
                    }
                    catch (RecoveryTokenException exc)
                    {
                        if (exc._partiallyComputedNode == null)
                            body = new Block(CurrentPositionContext(), this);
                        else
                            body = exc._partiallyComputedNode;
                        exc._partiallyComputedNode = new ForIn(forCtx, this, (lhs != null ? lhs : initializer), condOrColl, body);
                        throw;
                    }
                    // for (a in b)
                    //      lhs = a, initializer = null
                    // for (var a in b)
                    //      lhs = null, initializer = var a
                    forNode = new ForIn(forCtx, this, (lhs != null ? lhs : initializer), condOrColl, body);
                }
                else
                {
                    m_noSkipTokenSet.Add(NoSkipTokenSet.s_BlockConditionNoSkipTokenSet);
                    try
                    {
                        if (JSToken.Semicolon != m_currentToken.Token)
                        {
                            ReportError(JSError.NoSemicolon);
                            if (JSToken.Colon == m_currentToken.Token)
                            {
                                m_noSkipTokenSet.Add(NoSkipTokenSet.s_VariableDeclNoSkipTokenSet);
                                try
                                {
                                    SkipTokensAndThrow();
                                }
                                catch (RecoveryTokenException)
                                {
                                    if (JSToken.Semicolon == m_currentToken.Token)
                                        m_errorToken = null;
                                    else
                                        throw;
                                }
                                finally
                                {
                                    m_noSkipTokenSet.Remove(NoSkipTokenSet.s_VariableDeclNoSkipTokenSet);
                                }
                            }
                        }
                        GetNextToken();
                        if (JSToken.Semicolon != m_currentToken.Token)
                        {
                            condOrColl = ParseExpression();
                            if (JSToken.Semicolon != m_currentToken.Token)
                                ReportError(JSError.NoSemicolon);
                        }

                        GetNextToken();
                        if (JSToken.RightParenthesis != m_currentToken.Token)
                            increment = ParseExpression();
                        if (JSToken.RightParenthesis != m_currentToken.Token)
                            ReportError(JSError.NoRightParenthesis);
                        forCtx.UpdateWith(m_currentToken);
                        GetNextToken();
                    }
                    catch (RecoveryTokenException exc)
                    {
                        if (IndexOfToken(NoSkipTokenSet.s_BlockConditionNoSkipTokenSet, exc) == -1)
                        {
                            exc._partiallyComputedNode = null;
                            throw;
                        }
                        else
                        {
                            // discard any partial info, just genrate empty condition and increment and keep going
                            exc._partiallyComputedNode = null;
                            if (condOrColl == null)
                                condOrColl = new ConstantWrapper(true, PrimitiveType.Boolean, CurrentPositionContext(), this);
                        }
                        if (exc._token == JSToken.RightParenthesis)
                        {
                            GetNextToken();
                        }
                    }
                    finally
                    {
                        m_noSkipTokenSet.Remove(NoSkipTokenSet.s_BlockConditionNoSkipTokenSet);
                    }
                    AstNode body = null;
                    // if the statements aren't withing curly-braces, throw a possible error
                    if (JSToken.LeftCurly != m_currentToken.Token)
                    {
                        ReportError(JSError.StatementBlockExpected, forCtx, true);
                    }
                    try
                    {
                        // parse a Statement, not a SourceElement
                        body = ParseStatement(false);
                    }
                    catch (RecoveryTokenException exc)
                    {
                        if (exc._partiallyComputedNode == null)
                            body = new Block(CurrentPositionContext(), this);
                        else
                            body = exc._partiallyComputedNode;
                        exc._partiallyComputedNode = new ForNode(forCtx, this, initializer, condOrColl, increment, body);
                        throw;
                    }
                    forNode = new ForNode(forCtx, this, initializer, condOrColl, increment, body);
                }
            }
            finally
            {
                m_blockType.RemoveAt(m_blockType.Count - 1);
            }

            return forNode;
        }

        //---------------------------------------------------------------------------------------
        // ParseDoStatement
        //
        //  DoStatement:
        //    'do' Statement 'while' '(' Expression ')'
        //---------------------------------------------------------------------------------------
        private DoWhile ParseDoStatement()
        {
            Context doCtx = null;
            AstNode body = null;
            AstNode condition = null;
            m_blockType.Add(BlockType.Loop);
            try
            {
                GetNextToken();
                m_noSkipTokenSet.Add(NoSkipTokenSet.s_DoWhileBodyNoSkipTokenSet);
                // if the statements aren't withing curly-braces, throw a possible error
                if (JSToken.LeftCurly != m_currentToken.Token)
                {
                    ReportError(JSError.StatementBlockExpected, m_currentToken.Clone(), true);
                }
                try
                {
                    // parse a Statement, not a SourceElement
                    body = ParseStatement(false);
                }
                catch (RecoveryTokenException exc)
                {
                    // make up a block for the do while
                    if (exc._partiallyComputedNode != null)
                        body = exc._partiallyComputedNode;
                    else
                        body = new Block(CurrentPositionContext(), this);
                    if (IndexOfToken(NoSkipTokenSet.s_DoWhileBodyNoSkipTokenSet, exc) == -1)
                    {
                        // we have to pass the exception to someone else, make as much as you can from the 'do while'
                        exc._partiallyComputedNode = new DoWhile(CurrentPositionContext(), this,
                                                                  body,
                                                                  new ConstantWrapper(false, PrimitiveType.Boolean, CurrentPositionContext(), this));
                        throw;
                    }
                }
                finally
                {
                    m_noSkipTokenSet.Remove(NoSkipTokenSet.s_DoWhileBodyNoSkipTokenSet);
                }
                if (JSToken.While != m_currentToken.Token)
                {
                    ReportError(JSError.NoWhile);
                }
                doCtx = m_currentToken.Clone();
                GetNextToken();
                if (JSToken.LeftParenthesis != m_currentToken.Token)
                {
                    ReportError(JSError.NoLeftParenthesis);
                }
                GetNextToken();
                // catch here so the body of the do_while is not thrown away
                m_noSkipTokenSet.Add(NoSkipTokenSet.s_BlockConditionNoSkipTokenSet);
                try
                {
                    condition = ParseExpression();
                    if (JSToken.RightParenthesis != m_currentToken.Token)
                    {
                        ReportError(JSError.NoRightParenthesis);
                        doCtx.UpdateWith(condition.Context);
                    }
                    else
                        doCtx.UpdateWith(m_currentToken);
                    GetNextToken();
                }
                catch (RecoveryTokenException exc)
                {
                    // make up a condition
                    if (exc._partiallyComputedNode != null)
                        condition = exc._partiallyComputedNode;
                    else
                        condition = new ConstantWrapper(false, PrimitiveType.Boolean, CurrentPositionContext(), this);

                    if (IndexOfToken(NoSkipTokenSet.s_BlockConditionNoSkipTokenSet, exc) == -1)
                    {
                        exc._partiallyComputedNode = new DoWhile(doCtx, this, body, condition);
                        throw;
                    }
                    else
                    {
                        if (JSToken.RightParenthesis == m_currentToken.Token)
                            GetNextToken();
                    }
                }
                finally
                {
                    m_noSkipTokenSet.Remove(NoSkipTokenSet.s_BlockConditionNoSkipTokenSet);
                }
                if (JSToken.Semicolon == m_currentToken.Token)
                {
                    // JScript 5 allowed statements like
                    //   do{print(++x)}while(x<10) print(0)
                    // even though that does not strictly follow the automatic semicolon insertion
                    // rules for the required semi after the while().  For backwards compatibility
                    // we should continue to support this.
                    doCtx.UpdateWith(m_currentToken);
                    GetNextToken();
                }

            }
            finally
            {
                m_blockType.RemoveAt(m_blockType.Count - 1);
            }

            return new DoWhile(doCtx, this, body, condition);
        }

        //---------------------------------------------------------------------------------------
        // ParseWhileStatement
        //
        //  WhileStatement :
        //    'while' '(' Expression ')' Statement
        //---------------------------------------------------------------------------------------
        private WhileNode ParseWhileStatement()
        {
            Context whileCtx = m_currentToken.Clone();
            AstNode condition = null;
            AstNode body = null;
            m_blockType.Add(BlockType.Loop);
            try
            {
                GetNextToken();
                if (JSToken.LeftParenthesis != m_currentToken.Token)
                {
                    ReportError(JSError.NoLeftParenthesis);
                }
                GetNextToken();
                m_noSkipTokenSet.Add(NoSkipTokenSet.s_BlockConditionNoSkipTokenSet);
                try
                {
                    condition = ParseExpression();
                    if (JSToken.RightParenthesis != m_currentToken.Token)
                    {
                        ReportError(JSError.NoRightParenthesis);
                        whileCtx.UpdateWith(condition.Context);
                    }
                    else
                        whileCtx.UpdateWith(m_currentToken);

                    GetNextToken();
                }
                catch (RecoveryTokenException exc)
                {
                    if (IndexOfToken(NoSkipTokenSet.s_BlockConditionNoSkipTokenSet, exc) == -1)
                    {
                        // abort the while there is really no much to do here
                        exc._partiallyComputedNode = null;
                        throw;
                    }
                    else
                    {
                        // make up a condition
                        if (exc._partiallyComputedNode != null)
                            condition = exc._partiallyComputedNode;
                        else
                            condition = new ConstantWrapper(false, PrimitiveType.Boolean, CurrentPositionContext(), this);

                        if (JSToken.RightParenthesis == m_currentToken.Token)
                            GetNextToken();
                    }
                }
                finally
                {
                    m_noSkipTokenSet.Remove(NoSkipTokenSet.s_BlockConditionNoSkipTokenSet);
                }

                // if the statements aren't withing curly-braces, throw a possible error
                if (JSToken.LeftCurly != m_currentToken.Token)
                {
                    ReportError(JSError.StatementBlockExpected, whileCtx, true);
                }
                try
                {
                    // parse a Statement, not a SourceElement
                    body = ParseStatement(false);
                }
                catch (RecoveryTokenException exc)
                {
                    if (exc._partiallyComputedNode != null)
                        body = exc._partiallyComputedNode;
                    else
                        body = new Block(CurrentPositionContext(), this);

                    exc._partiallyComputedNode = new WhileNode(whileCtx, this, condition, body);
                    throw;
                }

            }
            finally
            {
                m_blockType.RemoveAt(m_blockType.Count - 1);
            }

            return new WhileNode(whileCtx, this, condition, body);
        }

        //---------------------------------------------------------------------------------------
        // ParseContinueStatement
        //
        //  ContinueStatement :
        //    'continue' OptionalLabel
        //
        //  OptionalLabel :
        //    <empty> |
        //    Identifier
        //
        // This function may return a null AST under error condition. The caller should handle
        // that case.
        // Regardless of error conditions, on exit the parser points to the first token after
        // the continue statement
        //---------------------------------------------------------------------------------------
        private ContinueNode ParseContinueStatement()
        {
            Context context = m_currentToken.Clone();
            int blocks = 0;
            int nestLevel = 0;
            GetNextToken();
            string label = null;
            if (!m_scanner.GotEndOfLine && (JSToken.Identifier == m_currentToken.Token || (label = JSKeyword.CanBeIdentifier(m_currentToken.Token)) != null))
            {
                context.UpdateWith(m_currentToken);
                // get the label block
                if (null != label)
                    ForceReportInfo(JSError.KeywordUsedAsIdentifier);
                else
                    label = m_scanner.GetIdentifier();
                if (!m_labelTable.ContainsKey(label))
                {
                    // the label does not exist. Continue anyway
                    ReportError(JSError.NoLabel, true);
                }
                else
                {
                    LabelInfo labelInfo = m_labelTable[label];
                    blocks = labelInfo.BlockIndex;
                    nestLevel = labelInfo.NestLevel;
                    if (m_blockType[blocks] != BlockType.Loop)
                    {
                        ReportError(JSError.BadContinue, context.Clone(), true);
                    }
                }
                GetNextToken();
            }
            else
            {
                blocks = m_blockType.Count - 1;
                while (blocks >= 0 && m_blockType[blocks] != BlockType.Loop) blocks--;
                if (blocks < 0)
                {
                    // the continue is malformed. Continue as if there was no continue at all
                    ReportError(JSError.BadContinue, context, true);
                    return null;
                }
            }

            if (JSToken.Semicolon == m_currentToken.Token)
            {
                context.UpdateWith(m_currentToken);
                GetNextToken();
            }
            else if (JSToken.RightCurly != m_currentToken.Token && !m_scanner.GotEndOfLine)
            {
                ReportError(JSError.NoSemicolon, true);
            }

            // must ignore the Finally block
            int finallyNum = 0;
            for (int i = blocks, n = m_blockType.Count; i < n; i++)
                if (m_blockType[i] == BlockType.Finally)
                {
                    blocks++;
                    finallyNum++;
                }
            if (finallyNum > m_finallyEscaped)
                m_finallyEscaped = finallyNum;

            return new ContinueNode(context, this, nestLevel, label);
        }

        //---------------------------------------------------------------------------------------
        // ParseBreakStatement
        //
        //  BreakStatement :
        //    'break' OptionalLabel
        //
        // This function may return a null AST under error condition. The caller should handle
        // that case.
        // Regardless of error conditions, on exit the parser points to the first token after
        // the break statement.
        //---------------------------------------------------------------------------------------
        private Break ParseBreakStatement()
        {
            Context context = m_currentToken.Clone();
            int blocks = 0;
            int nestLevel = 0;
            GetNextToken();
            string label = null;
            if (!m_scanner.GotEndOfLine && (JSToken.Identifier == m_currentToken.Token || (label = JSKeyword.CanBeIdentifier(m_currentToken.Token)) != null))
            {
                context.UpdateWith(m_currentToken);
                // get the label block
                if (null != label)
                    ForceReportInfo(JSError.KeywordUsedAsIdentifier);
                else
                    label = m_scanner.GetIdentifier();
                if (!m_labelTable.ContainsKey(label))
                {
                    // as if it was a non label case
                    ReportError(JSError.NoLabel, true);
                }
                else
                {
                    LabelInfo labelInfo = m_labelTable[label];
                    blocks = labelInfo.BlockIndex - 1; // the outer block
                    nestLevel = labelInfo.NestLevel;
                    Debug.Assert(m_blockType[blocks] != BlockType.Finally);
                }
                GetNextToken();
            }
            else
            {
                blocks = m_blockType.Count - 1;
                // search for an enclosing loop, if there is no loop it is an error
                while ((m_blockType[blocks] == BlockType.Block || m_blockType[blocks] == BlockType.Finally) && --blocks >= 0) ;
                --blocks;
                if (blocks < 0)
                {
                    ReportError(JSError.BadBreak, context, true);
                    return null;
                }
            }

            if (JSToken.Semicolon == m_currentToken.Token)
            {
                context.UpdateWith(m_currentToken);
                GetNextToken();
            }
            else if (JSToken.RightCurly != m_currentToken.Token && !m_scanner.GotEndOfLine)
            {
                ReportError(JSError.NoSemicolon, true);
            }

            // must ignore the Finally block
            int finallyNum = 0;
            for (int i = blocks, n = m_blockType.Count; i < n; i++)
            {
                if (m_blockType[i] == BlockType.Finally)
                {
                    blocks++;
                    finallyNum++;
                }
            }
            if (finallyNum > m_finallyEscaped)
            {
                m_finallyEscaped = finallyNum;
            }
            return new Break(context, this, nestLevel, label);
        }

        //---------------------------------------------------------------------------------------
        // ParseReturnStatement
        //
        //  ReturnStatement :
        //    'return' Expression
        //
        // This function may return a null AST under error condition. The caller should handle
        // that case.
        // Regardless of error conditions, on exit the parser points to the first token after
        // the return statement.
        //---------------------------------------------------------------------------------------
        private ReturnNode ParseReturnStatement()
        {
            Context retCtx = m_currentToken.Clone();
            AstNode expr = null;
            GetNextToken();
            if (!m_scanner.GotEndOfLine)
            {
                if (JSToken.Semicolon != m_currentToken.Token && JSToken.RightCurly != m_currentToken.Token)
                {
                    m_noSkipTokenSet.Add(NoSkipTokenSet.s_EndOfStatementNoSkipTokenSet);
                    try
                    {
                        expr = ParseExpression();
                    }
                    catch (RecoveryTokenException exc)
                    {
                        expr = exc._partiallyComputedNode;
                        if (IndexOfToken(NoSkipTokenSet.s_EndOfStatementNoSkipTokenSet, exc) == -1)
                        {
                            if (expr != null)
                                retCtx.UpdateWith(expr.Context);
                            exc._partiallyComputedNode = new ReturnNode(retCtx, this, expr);
                            throw;
                        }
                    }
                    finally
                    {
                        m_noSkipTokenSet.Remove(NoSkipTokenSet.s_EndOfStatementNoSkipTokenSet);
                    }
                    if (JSToken.Semicolon != m_currentToken.Token
                        && JSToken.RightCurly != m_currentToken.Token
                        && !m_scanner.GotEndOfLine)
                    {
                        ReportError(JSError.NoSemicolon, true);
                    }
                }
                if (JSToken.Semicolon == m_currentToken.Token)
                {
                    retCtx.UpdateWith(m_currentToken);
                    GetNextToken();
                }
                else if (expr != null)
                    retCtx.UpdateWith(expr.Context);
            }
            return new ReturnNode(retCtx, this, expr);
        }

        //---------------------------------------------------------------------------------------
        // ParseWithStatement
        //
        //  WithStatement :
        //    'with' '(' Expression ')' Statement
        //---------------------------------------------------------------------------------------
        private WithNode ParseWithStatement()
        {
            Context withCtx = m_currentToken.Clone();
            AstNode obj = null;
            Block block = null;
            m_blockType.Add(BlockType.Block);
            try
            {
                GetNextToken();
                if (JSToken.LeftParenthesis != m_currentToken.Token)
                    ReportError(JSError.NoLeftParenthesis);
                GetNextToken();
                m_noSkipTokenSet.Add(NoSkipTokenSet.s_BlockConditionNoSkipTokenSet);
                try
                {
                    obj = ParseExpression();
                    if (JSToken.RightParenthesis != m_currentToken.Token)
                    {
                        withCtx.UpdateWith(obj.Context);
                        ReportError(JSError.NoRightParenthesis);
                    }
                    else
                        withCtx.UpdateWith(m_currentToken);
                    GetNextToken();
                }
                catch (RecoveryTokenException exc)
                {
                    if (IndexOfToken(NoSkipTokenSet.s_BlockConditionNoSkipTokenSet, exc) == -1)
                    {
                        // give up
                        exc._partiallyComputedNode = null;
                        throw;
                    }
                    else
                    {
                        if (exc._partiallyComputedNode == null)
                            obj = new ConstantWrapper(true, PrimitiveType.Boolean, CurrentPositionContext(), this);
                        else
                            obj = exc._partiallyComputedNode;
                        withCtx.UpdateWith(obj.Context);

                        if (exc._token == JSToken.RightParenthesis)
                            GetNextToken();
                    }
                }
                finally
                {
                    m_noSkipTokenSet.Remove(NoSkipTokenSet.s_BlockConditionNoSkipTokenSet);
                }

                // if the statements aren't withing curly-braces, throw a possible error
                if (JSToken.LeftCurly != m_currentToken.Token)
                {
                    ReportError(JSError.StatementBlockExpected, withCtx, true);
                }

                WithScope withScope = new WithScope(ScopeStack.Peek(), withCtx, this);
                ScopeStack.Push(withScope);
                try
                {
                    // parse a Statement, not a SourceElement
                    AstNode statement = ParseStatement(false);

                    // but make sure we save it as a block
                    block = statement as Block;
                    if (block == null)
                    {
                        block = new Block(statement.Context, this);
                        block.Append(statement);
                    }
                }
                catch (RecoveryTokenException exc)
                {
                    if (exc._partiallyComputedNode == null)
                    {
                        block = new Block(CurrentPositionContext(), this);
                    }
                    else
                    {
                        block = exc._partiallyComputedNode as Block;
                        if (block == null)
                        {
                            block = new Block(exc._partiallyComputedNode.Context, this);
                            block.Append(exc._partiallyComputedNode);
                        }
                    }
                    block.BlockScope = withScope;
                    exc._partiallyComputedNode = new WithNode(withCtx, this, obj, block);
                    throw;
                }
                finally
                {
                    // pop off the with-scope
                    ScopeStack.Pop();

                    // save the with-scope on the block
                    block.BlockScope = withScope;
                }
            }
            finally
            {
                m_blockType.RemoveAt(m_blockType.Count - 1);
            }

            return new WithNode(withCtx, this, obj, block);
        }

        //---------------------------------------------------------------------------------------
        // ParseSwitchStatement
        //
        //  SwitchStatement :
        //    'switch' '(' Expression ')' '{' CaseBlock '}'
        //
        //  CaseBlock :
        //    CaseList DefaultCaseClause CaseList
        //
        //  CaseList :
        //    <empty> |
        //    CaseClause CaseList
        //
        //  CaseClause :
        //    'case' Expression ':' OptionalStatements
        //
        //  DefaultCaseClause :
        //    <empty> |
        //    'default' ':' OptionalStatements
        //---------------------------------------------------------------------------------------
        private AstNode ParseSwitchStatement()
        {
            Context switchCtx = m_currentToken.Clone();
            AstNode expr = null;
            AstNodeList cases = null;
            m_blockType.Add(BlockType.Switch);
            try
            {
                // read switch(expr)
                GetNextToken();
                if (JSToken.LeftParenthesis != m_currentToken.Token)
                    ReportError(JSError.NoLeftParenthesis);
                GetNextToken();
                m_noSkipTokenSet.Add(NoSkipTokenSet.s_BlockConditionNoSkipTokenSet);
                m_noSkipTokenSet.Add(NoSkipTokenSet.s_SwitchNoSkipTokenSet);
                try
                {
                    expr = ParseExpression();

                    if (JSToken.RightParenthesis != m_currentToken.Token)
                    {
                        ReportError(JSError.NoRightParenthesis);
                    }

                    GetNextToken();
                    if (JSToken.LeftCurly != m_currentToken.Token)
                    {
                        ReportError(JSError.NoLeftCurly);
                    }
                    GetNextToken();

                }
                catch (RecoveryTokenException exc)
                {
                    if (IndexOfToken(NoSkipTokenSet.s_BlockConditionNoSkipTokenSet, exc) == -1
                          && IndexOfToken(NoSkipTokenSet.s_SwitchNoSkipTokenSet, exc) == -1)
                    {
                        // give up
                        exc._partiallyComputedNode = null;
                        throw;
                    }
                    else
                    {
                        if (exc._partiallyComputedNode == null)
                            expr = new ConstantWrapper(true, PrimitiveType.Boolean, CurrentPositionContext(), this);
                        else
                            expr = exc._partiallyComputedNode;

                        if (IndexOfToken(NoSkipTokenSet.s_BlockConditionNoSkipTokenSet, exc) != -1)
                        {
                            if (exc._token == JSToken.RightParenthesis)
                                GetNextToken();

                            if (JSToken.LeftCurly != m_currentToken.Token)
                            {
                                ReportError(JSError.NoLeftCurly);
                            }
                            GetNextToken();
                        }

                    }
                }
                finally
                {
                    m_noSkipTokenSet.Remove(NoSkipTokenSet.s_SwitchNoSkipTokenSet);
                    m_noSkipTokenSet.Remove(NoSkipTokenSet.s_BlockConditionNoSkipTokenSet);
                }

                // parse the switch body
                cases = new AstNodeList(m_currentToken.Clone(), this);
                bool defaultStatement = false;
                m_noSkipTokenSet.Add(NoSkipTokenSet.s_BlockNoSkipTokenSet);
                try
                {
                    while (JSToken.RightCurly != m_currentToken.Token)
                    {
                        SwitchCase caseClause = null;
                        AstNode caseValue = null;
                        Context caseCtx = m_currentToken.Clone();
                        m_noSkipTokenSet.Add(NoSkipTokenSet.s_CaseNoSkipTokenSet);
                        try
                        {
                            if (JSToken.Case == m_currentToken.Token)
                            {
                                // get the case
                                GetNextToken();
                                caseValue = ParseExpression();
                            }
                            else if (JSToken.Default == m_currentToken.Token)
                            {
                                // get the default
                                if (defaultStatement)
                                {
                                    // we report an error but we still accept the default
                                    ReportError(JSError.DupDefault, true);
                                }
                                else
                                {
                                    defaultStatement = true;
                                }
                                GetNextToken();
                            }
                            else
                            {
                                // This is an error, there is no case or default. Assume a default was missing and keep going
                                defaultStatement = true;
                                ReportError(JSError.BadSwitch);
                            }
                            if (JSToken.Colon != m_currentToken.Token)
                            {
                                ReportError(JSError.NoColon);
                            }

                            // read the statements inside the case or default
                            GetNextToken();
                        }
                        catch (RecoveryTokenException exc)
                        {
                            // right now we can only get here for the 'case' statement
                            if (IndexOfToken(NoSkipTokenSet.s_CaseNoSkipTokenSet, exc) == -1)
                            {
                                // ignore the current case or default
                                exc._partiallyComputedNode = null;
                                throw;
                            }
                            else
                            {
                                caseValue = exc._partiallyComputedNode;

                                if (exc._token == JSToken.Colon)
                                {
                                    GetNextToken();
                                }
                            }
                        }
                        finally
                        {
                            m_noSkipTokenSet.Remove(NoSkipTokenSet.s_CaseNoSkipTokenSet);
                        }

                        m_blockType.Add(BlockType.Block);
                        try
                        {
                            Block statements = new Block(m_currentToken.Clone(), this);
                            m_noSkipTokenSet.Add(NoSkipTokenSet.s_SwitchNoSkipTokenSet);
                            m_noSkipTokenSet.Add(NoSkipTokenSet.s_StartStatementNoSkipTokenSet);
                            try
                            {
                                while (JSToken.RightCurly != m_currentToken.Token && JSToken.Case != m_currentToken.Token && JSToken.Default != m_currentToken.Token)
                                {
                                    try
                                    {
                                        // parse a Statement, not a SourceElement
                                        statements.Append(ParseStatement(false));
                                    }
                                    catch (RecoveryTokenException exc)
                                    {
                                        if (exc._partiallyComputedNode != null)
                                        {
                                            statements.Append(exc._partiallyComputedNode);
                                            exc._partiallyComputedNode = null;
                                        }
                                        if (IndexOfToken(NoSkipTokenSet.s_StartStatementNoSkipTokenSet, exc) == -1)
                                        {
                                            throw;
                                        }
                                    }
                                }
                            }
                            catch (RecoveryTokenException exc)
                            {
                                if (IndexOfToken(NoSkipTokenSet.s_SwitchNoSkipTokenSet, exc) == -1)
                                {
                                    caseClause = new SwitchCase(caseCtx, this, caseValue, statements);
                                    cases.Append(caseClause);
                                    throw;
                                }
                            }
                            finally
                            {
                                m_noSkipTokenSet.Remove(NoSkipTokenSet.s_StartStatementNoSkipTokenSet);
                                m_noSkipTokenSet.Remove(NoSkipTokenSet.s_SwitchNoSkipTokenSet);
                            }
                            if (JSToken.RightCurly == m_currentToken.Token)
                            {
                                statements.Context.UpdateWith(m_currentToken);
                            }
                            caseCtx.UpdateWith(statements.Context);
                            caseClause = new SwitchCase(caseCtx, this, caseValue, statements);
                            cases.Append(caseClause);
                        }
                        finally
                        {
                            m_blockType.RemoveAt(m_blockType.Count - 1);
                        }
                    }
                }
                catch (RecoveryTokenException exc)
                {
                    if (IndexOfToken(NoSkipTokenSet.s_BlockNoSkipTokenSet, exc) == -1)
                    {
                        //save what you can a rethrow
                        switchCtx.UpdateWith(CurrentPositionContext());
                        exc._partiallyComputedNode = new Switch(switchCtx, this, expr, cases);
                        throw;
                    }
                }
                finally
                {
                    m_noSkipTokenSet.Remove(NoSkipTokenSet.s_BlockNoSkipTokenSet);
                }
                switchCtx.UpdateWith(m_currentToken);
                GetNextToken();
            }
            finally
            {
                m_blockType.RemoveAt(m_blockType.Count - 1);
            }

            return new Switch(switchCtx, this, expr, cases);
        }

        //---------------------------------------------------------------------------------------
        // ParseThrowStatement
        //
        //  ThrowStatement :
        //    throw |
        //    throw Expression
        //---------------------------------------------------------------------------------------
        private AstNode ParseThrowStatement()
        {
            Context throwCtx = m_currentToken.Clone();
            GetNextToken();
            AstNode operand = null;
            if (!m_scanner.GotEndOfLine)
            {
                if (JSToken.Semicolon != m_currentToken.Token)
                {
                    m_noSkipTokenSet.Add(NoSkipTokenSet.s_EndOfStatementNoSkipTokenSet);
                    try
                    {
                        operand = ParseExpression();
                    }
                    catch (RecoveryTokenException exc)
                    {
                        operand = exc._partiallyComputedNode;
                        if (IndexOfToken(NoSkipTokenSet.s_EndOfStatementNoSkipTokenSet, exc) == -1)
                        {
                            if (operand != null)
                                exc._partiallyComputedNode = new ThrowNode(throwCtx, this, exc._partiallyComputedNode);
                            throw;
                        }
                    }
                    finally
                    {
                        m_noSkipTokenSet.Remove(NoSkipTokenSet.s_EndOfStatementNoSkipTokenSet);
                    }
                }
            }

            if (operand != null)
                throwCtx.UpdateWith(operand.Context);
            return new ThrowNode(throwCtx, this, operand);
        }

        //---------------------------------------------------------------------------------------
        // ParseTryStatement
        //
        //  TryStatement :
        //    'try' Block Catch Finally
        //
        //  Catch :
        //    <empty> | 'catch' '(' Identifier ')' Block
        //
        //  Finally :
        //    <empty> |
        //    'finally' Block
        //---------------------------------------------------------------------------------------
        private AstNode ParseTryStatement()
        {
            Context tryCtx = m_currentToken.Clone();
            Context tryEndContext = null;
            Block body = null;
            Context idContext = null;
            Block handler = null;
            CatchScope catchScope = null;
            Block finally_block = null;
            RecoveryTokenException excInFinally = null;
            m_blockType.Add(BlockType.Block);
            try
            {
                bool catchOrFinally = false;
                GetNextToken();
                if (JSToken.LeftCurly != m_currentToken.Token)
                {
                    ReportError(JSError.NoLeftCurly);
                }
                m_noSkipTokenSet.Add(NoSkipTokenSet.s_NoTrySkipTokenSet);
                try
                {
                    body = ParseBlock(out tryEndContext);
                }
                catch (RecoveryTokenException exc)
                {
                    if (IndexOfToken(NoSkipTokenSet.s_NoTrySkipTokenSet, exc) == -1)
                    {
                        // do nothing and just return the containing block, if any
                        throw;
                    }
                    else
                    {
                        body = exc._partiallyComputedNode as Block;
                        if (body == null)
                        {
                            body = new Block(exc._partiallyComputedNode.Context, this);
                            body.Append(exc._partiallyComputedNode);
                        }
                    }
                }
                finally
                {
                    m_noSkipTokenSet.Remove(NoSkipTokenSet.s_NoTrySkipTokenSet);
                }
                if (JSToken.Catch == m_currentToken.Token)
                {
                    m_noSkipTokenSet.Add(NoSkipTokenSet.s_NoTrySkipTokenSet);
                    try
                    {
                        catchOrFinally = true;
                        GetNextToken();
                        if (JSToken.LeftParenthesis != m_currentToken.Token)
                        {
                            ReportError(JSError.NoLeftParenthesis);
                        }

                        GetNextToken();
                        if (JSToken.Identifier != m_currentToken.Token)
                        {
                            string identifier = JSKeyword.CanBeIdentifier(m_currentToken.Token);
                            if (null != identifier)
                            {
                                ForceReportInfo(JSError.KeywordUsedAsIdentifier);
                                idContext = m_currentToken.Clone();
                            }
                            else
                            {
                                ReportError(JSError.NoIdentifier);
                            }
                        }
                        else
                        {
                            idContext = m_currentToken.Clone();
                        }

                        GetNextToken();
                        m_noSkipTokenSet.Add(NoSkipTokenSet.s_BlockConditionNoSkipTokenSet);
                        try
                        {
                            if (JSToken.RightParenthesis != m_currentToken.Token)
                            {
                                ReportError(JSError.NoRightParenthesis);
                            }
                            GetNextToken();
                        }
                        catch (RecoveryTokenException exc)
                        {
                            if (IndexOfToken(NoSkipTokenSet.s_BlockConditionNoSkipTokenSet, exc) == -1)
                            {
                                exc._partiallyComputedNode = null;
                                // rethrow
                                throw;
                            }
                            else
                            {
                                if (m_currentToken.Token == JSToken.RightParenthesis)
                                {
                                    GetNextToken();
                                }
                            }
                        }
                        finally
                        {
                            m_noSkipTokenSet.Remove(NoSkipTokenSet.s_BlockConditionNoSkipTokenSet);
                        }

                        if (JSToken.LeftCurly != m_currentToken.Token)
                        {
                            ReportError(JSError.NoLeftCurly);
                        }

                        // create the catch-scope and push it onto the stack
                        catchScope = new CatchScope(ScopeStack.Peek(), idContext, this);
                        ScopeStack.Push(catchScope);

                        try
                        {
                            // parse the block
                            handler = ParseBlock();
                        }
                        finally
                        {
                            // pop off the catch scope and assign it to the block
                            ScopeStack.Pop();
                        }

                        tryCtx.UpdateWith(handler.Context);
                    }
                    catch (RecoveryTokenException exc)
                    {
                        if (exc._partiallyComputedNode == null)
                        {
                            handler = new Block(CurrentPositionContext(), this);
                        }
                        else
                        {
                            handler = exc._partiallyComputedNode as Block;
                            if (handler == null)
                            {
                                handler = new Block(exc._partiallyComputedNode.Context, this);
                                handler.Append(exc._partiallyComputedNode);
                            }
                        }
                        handler.BlockScope = catchScope;
                        if (IndexOfToken(NoSkipTokenSet.s_NoTrySkipTokenSet, exc) == -1)
                        {
                            throw;
                        }
                    }
                    finally
                    {
                        m_noSkipTokenSet.Remove(NoSkipTokenSet.s_NoTrySkipTokenSet);
                    }
                }

                try
                {
                    if (JSToken.Finally == m_currentToken.Token)
                    {
                        GetNextToken();
                        m_blockType.Add(BlockType.Finally);
                        try
                        {
                            finally_block = ParseBlock();
                            catchOrFinally = true;
                        }
                        finally
                        {
                            m_blockType.RemoveAt(m_blockType.Count - 1);
                        }
                        tryCtx.UpdateWith(finally_block.Context);
                    }
                }
                catch (RecoveryTokenException exc)
                {
                    excInFinally = exc; // thrown later so we can execute code below
                }

                if (!catchOrFinally)
                {
                    ReportError(JSError.NoCatch, true);
                    finally_block = new Block(CurrentPositionContext(), this); // make a dummy empty block
                }
            }
            finally
            {
                m_blockType.RemoveAt(m_blockType.Count - 1);
                if (handler != null)
                {
                    handler.BlockScope = catchScope;
                }
            }

            string catchVariableName = idContext == null ? null : idContext.Code;
            if (excInFinally != null)
            {
                excInFinally._partiallyComputedNode = new TryNode(tryCtx, this, body, catchVariableName, handler, finally_block);
                throw excInFinally;
            }
            return new TryNode(tryCtx, this, body, catchVariableName, handler, finally_block);
        }

        //---------------------------------------------------------------------------------------
        // ParseFunction
        //
        //  FunctionDeclaration :
        //    VisibilityModifier 'function' Identifier '('
        //                          FormalParameterList ')' '{' FunctionBody '}'
        //
        //  FormalParameterList :
        //    <empty> |
        //    IdentifierList Identifier
        //
        //  IdentifierList :
        //    <empty> |
        //    Identifier, IdentifierList
        //---------------------------------------------------------------------------------------
        private FunctionObject ParseFunction(FunctionType functionType, Context fncCtx)
        {
            Lookup name = null;
            List<ParameterDeclaration> formalParameters = null;
            Block body = null;
            bool inExpression = (functionType == FunctionType.Expression);

            GetNextToken();

            // get the function name or make an anonymous function if in expression "position"
            if (JSToken.Identifier == m_currentToken.Token)
            {
                name = new Lookup(m_scanner.GetIdentifier(), m_currentToken.Clone(), this);
                GetNextToken();
            }
            else
            {
                string identifier = JSKeyword.CanBeIdentifier(m_currentToken.Token);
                if (null != identifier)
                {
                    ForceReportInfo(JSError.KeywordUsedAsIdentifier, false);
                    name = new Lookup(identifier, m_currentToken.Clone(), this);
                    GetNextToken();
                }
                else
                {
                    if (!inExpression)
                    {
                        // if this isn't a function expression, then we need to throw an error because
                        // function DECLARATIONS always need a valid identifier name
                        ReportError(JSError.NoIdentifier, true);

                        // BUT if the current token is a left paren, we don't want to use it as the name.
                        // (fix for issue #14152)
                        if (m_currentToken.Token != JSToken.LeftParenthesis
                            && m_currentToken.Token != JSToken.LeftCurly)
                        {
                            identifier = m_currentToken.Code;
                            name = new Lookup(identifier, CurrentPositionContext(), this);
                            GetNextToken();
                        }
                    }
                }
            }

            // make a new state and save the old one
            List<BlockType> blockType = m_blockType;
            m_blockType = new List<BlockType>(16);
            Dictionary<string, LabelInfo> labelTable = m_labelTable;
            m_labelTable = new Dictionary<string, LabelInfo>();

            // create the function scope and stick it onto the scope stack
            FunctionScope functionScope = new FunctionScope(
              ScopeStack.Peek(),
              (functionType != FunctionType.Declaration),
              this
              );
            ScopeStack.Push(functionScope);

            try
            {
                // get the formal parameters
                if (JSToken.LeftParenthesis != m_currentToken.Token)
                {
                    // we expect a left paren at this point for standard cross-browser support.
                    // BUT -- some versions of IE allow an object property expression to be a function name, like window.onclick. 
                    // we still want to throw the error, because it syntax errors on most browsers, but we still want to
                    // be able to parse it and return the intended results. 
                    // Skip to the open paren and use whatever is in-between as the function name. Doesn't matter that it's 
                    // an invalid identifier; it won't be accessible as a valid field anyway.
                    bool expandedIndentifier = false;
                    while (m_currentToken.Token != JSToken.LeftParenthesis
                        && m_currentToken.Token != JSToken.LeftCurly
                        && m_currentToken.Token != JSToken.Semicolon
                        && m_currentToken.Token != JSToken.EndOfFile)
                    {
                        name.Context.UpdateWith(m_currentToken);
                        GetNextToken();
                        expandedIndentifier = true;
                    }

                    // if we actually expanded the identifier context, then we want to report that
                    // the function name needs to be an indentifier. Otherwise we didn't expand the 
                    // name, so just report that we expected an open parent at this point.
                    if (expandedIndentifier)
                    {
                        name.Name = name.Context.Code;
                        name.Context.HandleError(JSError.FunctionNameMustBeIdentifier, true);
                    }
                    else
                    {
                        ReportError(JSError.NoLeftParenthesis, true);
                    }
                }

                if (m_currentToken.Token == JSToken.LeftParenthesis)
                {
                    // skip the open paren
                    GetNextToken();

                    Context paramArrayContext = null;
                    formalParameters = new List<ParameterDeclaration>();

                    // create the list of arguments and update the context
                    while (JSToken.RightParenthesis != m_currentToken.Token)
                    {
                        if (paramArrayContext != null)
                        {
                            ReportError(JSError.ParameterListNotLast, paramArrayContext, true);
                            paramArrayContext = null;
                        }
                        String id = null;
                        m_noSkipTokenSet.Add(NoSkipTokenSet.s_FunctionDeclNoSkipTokenSet);
                        try
                        {
                            if (JSToken.ParameterArray == m_currentToken.Token)
                            {
                                paramArrayContext = m_currentToken.Clone();
                                GetNextToken();
                            }
                            else if (JSToken.Identifier != m_currentToken.Token && (id = JSKeyword.CanBeIdentifier(m_currentToken.Token)) == null)
                            {
                                if (JSToken.LeftCurly == m_currentToken.Token)
                                {
                                    ReportError(JSError.NoRightParenthesis);
                                    break;
                                }
                                else if (JSToken.Comma == m_currentToken.Token)
                                {
                                    // We're missing an argument (or previous argument was malformed and
                                    // we skipped to the comma.)  Keep trying to parse the argument list --
                                    // we will skip the comma below.
                                    ReportError(JSError.SyntaxError, true);
                                }
                                else
                                {
                                    ReportError(JSError.SyntaxError, true);
                                    SkipTokensAndThrow();
                                }
                            }
                            else
                            {
                                if (null == id)
                                    id = m_scanner.GetIdentifier();
                                else
                                    ForceReportInfo(JSError.KeywordUsedAsIdentifier);
                                Context paramCtx = m_currentToken.Clone();
                                GetNextToken();

                                formalParameters.Add(new ParameterDeclaration(paramCtx, this, id));
                            }

                            // got an arg, it should be either a ',' or ')'
                            if (JSToken.RightParenthesis == m_currentToken.Token)
                                break;
                            else if (JSToken.Comma != m_currentToken.Token)
                            {
                                // deal with error in some "intelligent" way
                                if (JSToken.LeftCurly == m_currentToken.Token)
                                {
                                    ReportError(JSError.NoRightParenthesis);
                                    break;
                                }
                                else
                                {
                                    if (JSToken.Identifier == m_currentToken.Token)
                                    {
                                        // it's possible that the guy was writing the type in C/C++ style (i.e. int x)
                                        ReportError(JSError.NoCommaOrTypeDefinitionError);
                                    }
                                    else
                                        ReportError(JSError.NoComma);
                                }
                            }
                            GetNextToken();
                        }
                        catch (RecoveryTokenException exc)
                        {
                            if (IndexOfToken(NoSkipTokenSet.s_FunctionDeclNoSkipTokenSet, exc) == -1)
                                throw;
                        }
                        finally
                        {
                            m_noSkipTokenSet.Remove(NoSkipTokenSet.s_FunctionDeclNoSkipTokenSet);
                        }
                    }

                    fncCtx.UpdateWith(m_currentToken);
                    GetNextToken();
                }

                // read the function body of non-abstract functions.
                if (JSToken.LeftCurly != m_currentToken.Token)
                    ReportError(JSError.NoLeftCurly, true);

                m_blockType.Add(BlockType.Block);
                m_noSkipTokenSet.Add(NoSkipTokenSet.s_BlockNoSkipTokenSet);
                m_noSkipTokenSet.Add(NoSkipTokenSet.s_StartStatementNoSkipTokenSet);
                try
                {
                    // parse the block locally to get the exact end of function
                    body = new Block(m_currentToken.Clone(), this);
                    GetNextToken();

                    while (JSToken.RightCurly != m_currentToken.Token)
                    {
                        try
                        {
                            // function body's are SourceElements (Statements + FunctionDeclarations)
                            body.Append(ParseStatement(true));
                        }
                        catch (RecoveryTokenException exc)
                        {
                            if (exc._partiallyComputedNode != null)
                            {
                                body.Append(exc._partiallyComputedNode);
                            }
                            if (IndexOfToken(NoSkipTokenSet.s_StartStatementNoSkipTokenSet, exc) == -1)
                                throw;
                        }
                    }

                    body.Context.UpdateWith(m_currentToken);
                    fncCtx.UpdateWith(m_currentToken);
                }
                catch (RecoveryTokenException exc)
                {
                    if (IndexOfToken(NoSkipTokenSet.s_BlockNoSkipTokenSet, exc) == -1)
                    {
                        exc._partiallyComputedNode = new FunctionObject(
                          name,
                          this,
                          (inExpression ? FunctionType.Expression : FunctionType.Declaration),
                          formalParameters == null ? null : formalParameters.ToArray(),
                          body,
                          fncCtx,
                          functionScope
                          );
                        throw;
                    }
                }
                finally
                {
                    m_blockType.RemoveAt(m_blockType.Count - 1);
                    m_noSkipTokenSet.Remove(NoSkipTokenSet.s_StartStatementNoSkipTokenSet);
                    m_noSkipTokenSet.Remove(NoSkipTokenSet.s_BlockNoSkipTokenSet);
                }

                GetNextToken();
            }
            finally
            {
                // pop the scope off the stack
                ScopeStack.Pop();

                // restore state
                m_blockType = blockType;
                m_labelTable = labelTable;
            }

            return new FunctionObject(
                name,
                this,
                functionType,
                formalParameters == null ? null : formalParameters.ToArray(),
                body,
                fncCtx,
                functionScope);
        }

        //---------------------------------------------------------------------------------------
        // ParseExpression
        //
        //  Expression :
        //    AssignmentExpressionList AssignmentExpression
        //
        //  AssignmentExpressionList :
        //    <empty> |
        //    AssignmentExpression ',' AssignmentExpressionList
        //
        //  AssignmentExpression :
        //    ConditionalExpression |
        //    LeftHandSideExpression AssignmentOperator AssignmentExpression
        //
        //  ConditionalExpression :
        //    LogicalORExpression OptionalConditionalExpression
        //
        //  OptionalConditionalExpression :
        //    <empty> |
        //    '?' AssignmentExpression ':' AssignmentExpression
        //
        //  LogicalORExpression :
        //    LogicalANDExpression OptionalLogicalOrExpression
        //
        //  OptionalLogicalOrExpression :
        //    <empty> |
        //    '||' LogicalANDExpression OptionalLogicalOrExpression
        //
        //  LogicalANDExpression :
        //    BitwiseORExpression OptionalLogicalANDExpression
        //
        //  OptionalLogicalANDExpression :
        //    <empty> |
        //    '&&' BitwiseORExpression OptionalLogicalANDExpression
        //
        //  BitwiseORExpression :
        //    BitwiseXORExpression OptionalBitwiseORExpression
        //
        //  OptionalBitwiseORExpression :
        //    <empty> |
        //    '|' BitwiseXORExpression OptionalBitwiseORExpression
        //
        //  BitwiseXORExpression :
        //    BitwiseANDExpression OptionalBitwiseXORExpression
        //
        //  OptionalBitwiseXORExpression :
        //    <empty> |
        //    '^' BitwiseANDExpression OptionalBitwiseXORExpression
        //
        //  BitwiseANDExpression :
        //    EqualityExpression OptionalBitwiseANDExpression
        //
        //  OptionalBitwiseANDExpression :
        //    <empty> |
        //    '&' EqualityExpression OptionalBitwiseANDExpression
        //
        //  EqualityExpression :
        //    RelationalExpression |
        //    RelationalExpression '==' EqualityExpression |
        //    RelationalExpression '!=' EqualityExpression |
        //    RelationalExpression '===' EqualityExpression |
        //    RelationalExpression '!==' EqualityExpression
        //
        //  RelationalExpression :
        //    ShiftExpression |
        //    ShiftExpression '<' RelationalExpression |
        //    ShiftExpression '>' RelationalExpression |
        //    ShiftExpression '<=' RelationalExpression |
        //    ShiftExpression '>=' RelationalExpression
        //
        //  ShiftExpression :
        //    AdditiveExpression |
        //    AdditiveExpression '<<' ShiftExpression |
        //    AdditiveExpression '>>' ShiftExpression |
        //    AdditiveExpression '>>>' ShiftExpression
        //
        //  AdditiveExpression :
        //    MultiplicativeExpression |
        //    MultiplicativeExpression '+' AdditiveExpression |
        //    MultiplicativeExpression '-' AdditiveExpression
        //
        //  MultiplicativeExpression :
        //    UnaryExpression |
        //    UnaryExpression '*' MultiplicativeExpression |
        //    UnaryExpression '/' MultiplicativeExpression |
        //    UnaryExpression '%' MultiplicativeExpression
        //---------------------------------------------------------------------------------------
        private AstNode ParseExpression()
        {
            bool bAssign;
            AstNode lhs = ParseUnaryExpression(out bAssign, false);
            return ParseExpression(lhs, false, bAssign, JSToken.None);
        }

        private AstNode ParseExpression(bool single)
        {
            bool bAssign;
            AstNode lhs = ParseUnaryExpression(out bAssign, false);
            return ParseExpression(lhs, single, bAssign, JSToken.None);
        }

        private AstNode ParseExpression(bool single, JSToken inToken)
        {
            bool bAssign;
            AstNode lhs = ParseUnaryExpression(out bAssign, false);
            return ParseExpression(lhs, single, bAssign, inToken);
        }

        private AstNode ParseExpression(AstNode leftHandSide, bool single, bool bCanAssign, JSToken inToken)
        {
            // new op stack with dummy op
            Stack<JSToken> opsStack = new Stack<JSToken>();
            opsStack.Push(JSToken.None);

            // term stack, push left-hand side onto it
            Stack<AstNode> termStack = new Stack<AstNode>();
            termStack.Push(leftHandSide);

            AstNode expr = null;

            try
            {
                for (; ; )
                {
                    // if 'binary op' or 'conditional' but not 'comma'
                    // inToken is a special case because of the for..in crap. When ParseExpression is called from
                    // for, inToken = JSToken.In which excludes JSToken.In from the list of operators, otherwise
                    // inToken = JSToken.None which is always true if the first condition is true
                    if (JSScanner.IsProcessableOperator(m_currentToken.Token) && inToken != m_currentToken.Token)
                    {

                        OpPrec prec = JSScanner.GetOperatorPrecedence(m_currentToken.Token);
                        bool rightAssoc = JSScanner.IsRightAssociativeOperator(m_currentToken.Token);
                        // the current operator has lower precedence than the operator at the top of the stack
                        // or it has the same precedence and it is left associative (that is, no 'assign op' or 'conditional')
                        OpPrec stackPrec = JSScanner.GetOperatorPrecedence(opsStack.Peek());
                        while (prec < stackPrec || prec == stackPrec && !rightAssoc)
                        {
                            AstNode operand2 = termStack.Pop();
                            AstNode operand1 = termStack.Pop();
                            //Console.Out.WriteLine("lower prec or same and left assoc");
                            expr = CreateExpressionNode(opsStack.Pop(), operand1, operand2);

                            // push node onto the stack
                            termStack.Push(expr);
                            stackPrec = JSScanner.GetOperatorPrecedence(opsStack.Peek());
                        }

                        // the current operator has higher precedence that every scanned operators on the stack, or
                        // it has the same precedence as the one at the top of the stack and it is right associative

                        // push operator and next term

                        // special case conditional '?:'
                        if (JSToken.ConditionalIf == m_currentToken.Token)
                        {
                            //Console.Out.WriteLine("Condition expression");

                            // pop term stack
                            AstNode condition = termStack.Pop();

                            GetNextToken();

                            // get expr1 in logOrExpr ? expr1 : expr2
                            AstNode operand1 = ParseExpression(true);

                            if (JSToken.Colon != m_currentToken.Token)
                                ReportError(JSError.NoColon);
                            GetNextToken();

                            // get expr2 in logOrExpr ? expr1 : expr2
                            AstNode operand2 = ParseExpression(true, inToken);

                            expr = new Conditional(condition.Context.CombineWith(operand2.Context), this, condition, operand1, operand2);
                            termStack.Push(expr);
                        }
                        else
                        {
                            //Console.Out.WriteLine("higher prec or right assoc");

                            if (JSScanner.IsAssignmentOperator(m_currentToken.Token))
                            {
                                if (!bCanAssign)
                                {
                                    ReportError(JSError.IllegalAssignment);
                                    SkipTokensAndThrow();
                                }
                            }
                            else
                                bCanAssign = false;

                            // push the operator onto the operators stack
                            opsStack.Push(m_currentToken.Token);
                            // push new term
                            GetNextToken();
                            if (bCanAssign)
                                termStack.Push(ParseUnaryExpression(out bCanAssign, false));
                            else
                            {
                                bool dummy;
                                termStack.Push(ParseUnaryExpression(out dummy, false));
                            }
                        }
                    }
                    else
                        break; // done, go and unwind the stack of expressions/operators
                }

                //Console.Out.WriteLine("unwinding stack");
                // there are still operators to be processed
                while (opsStack.Peek() != JSToken.None)
                {
                    AstNode operand2 = termStack.Pop();
                    AstNode operand1 = termStack.Pop();
                    // make the ast operator node
                    expr = CreateExpressionNode(opsStack.Pop(), operand1, operand2);

                    // push node onto the stack
                    termStack.Push(expr);
                }

                // if we have a ',' and we are not looking for a single expression reenter
                if (!single && JSToken.Comma == m_currentToken.Token)
                {
                    //Console.Out.WriteLine("Next expr");
                    GetNextToken();
                    AstNode expr2 = ParseExpression(false, inToken);
                    AstNode term = termStack.Pop();
                    termStack.Push(new BinaryOperator(term.Context.CombineWith(expr2.Context), this, term, expr2, JSToken.Comma));
                }

                Debug.Assert(termStack.Count == 1);
                return termStack.Pop();
            }
            catch (RecoveryTokenException exc)
            {
                exc._partiallyComputedNode = leftHandSide;
                throw;
            }
        }

        //---------------------------------------------------------------------------------------
        // ParseUnaryExpression
        //
        //  UnaryExpression :
        //    PostfixExpression |
        //    'delete' UnaryExpression |
        //    'void' UnaryExpression |
        //    'typeof' UnaryExpression |
        //    '++' UnaryExpression |
        //    '--' UnaryExpression |
        //    '+' UnaryExpression |
        //    '-' UnaryExpression |
        //    '~' UnaryExpression |
        //    '!' UnaryExpression
        //
        //---------------------------------------------------------------------------------------
        private AstNode ParseUnaryExpression(out bool isLeftHandSideExpr, bool isMinus)
        {
            bool canBeAttribute = false;
            return ParseUnaryExpression(out isLeftHandSideExpr, ref canBeAttribute, isMinus, false);
        }

        private AstNode ParseUnaryExpression(out bool isLeftHandSideExpr, ref bool canBeAttribute, bool isMinus)
        {
            return ParseUnaryExpression(out isLeftHandSideExpr, ref canBeAttribute, isMinus, true);
        }

        private AstNode ParseUnaryExpression(out bool isLeftHandSideExpr, ref bool canBeAttribute, bool isMinus, bool warnForKeyword)
        {
            AstNode ast = null;
            isLeftHandSideExpr = false;
            bool dummy = false;
            Context exprCtx = null;
            AstNode expr = null;
            switch (m_currentToken.Token)
            {
                case JSToken.Void:
                    exprCtx = m_currentToken.Clone();
                    GetNextToken();
                    canBeAttribute = false;
                    expr = ParseUnaryExpression(out dummy, ref canBeAttribute, false);
                    exprCtx.UpdateWith(expr.Context);
                    ast = new VoidNode(exprCtx, this, expr);
                    break;
                case JSToken.TypeOf:
                    exprCtx = m_currentToken.Clone();
                    GetNextToken();
                    canBeAttribute = false;
                    expr = ParseUnaryExpression(out dummy, ref canBeAttribute, false);
                    exprCtx.UpdateWith(expr.Context);
                    ast = new TypeOfNode(exprCtx, this, expr);
                    break;
                case JSToken.Plus:
                    exprCtx = m_currentToken.Clone();
                    GetNextToken();
                    canBeAttribute = false;
                    expr = ParseUnaryExpression(out dummy, ref canBeAttribute, false);
                    exprCtx.UpdateWith(expr.Context);
                    ast = new NumericUnary(exprCtx, this, expr, JSToken.Plus);
                    break;
                case JSToken.Minus:
                    exprCtx = m_currentToken.Clone();
                    GetNextToken();
                    canBeAttribute = false;
                    expr = ParseUnaryExpression(out dummy, ref canBeAttribute, true);
                    exprCtx.UpdateWith(expr.Context);
                    ast = new NumericUnary(exprCtx, this, expr, JSToken.Minus);
                    break;
                case JSToken.BitwiseNot:
                    exprCtx = m_currentToken.Clone();
                    GetNextToken();
                    canBeAttribute = false;
                    expr = ParseUnaryExpression(out dummy, ref canBeAttribute, false);
                    exprCtx.UpdateWith(expr.Context);
                    ast = new NumericUnary(exprCtx, this, expr, JSToken.BitwiseNot);
                    break;
                case JSToken.LogicalNot:
                    exprCtx = m_currentToken.Clone();
                    GetNextToken();
                    canBeAttribute = false;
                    expr = ParseUnaryExpression(out dummy, ref canBeAttribute, false);
                    exprCtx.UpdateWith(expr.Context);
                    ast = new NumericUnary(exprCtx, this, expr, JSToken.LogicalNot);
                    break;
                case JSToken.Delete:
                    exprCtx = m_currentToken.Clone();
                    GetNextToken();
                    canBeAttribute = false;
                    expr = ParseUnaryExpression(out dummy, ref canBeAttribute, false);
                    exprCtx.UpdateWith(expr.Context);
                    ast = new Delete(exprCtx, this, expr);
                    break;
                case JSToken.Increment:
                    exprCtx = m_currentToken.Clone();
                    GetNextToken();
                    canBeAttribute = false;
                    expr = ParseUnaryExpression(out dummy, ref canBeAttribute, false);
                    exprCtx.UpdateWith(expr.Context);
                    ast = new PostOrPrefixOperator(exprCtx, this, expr, m_currentToken.Token, PostOrPrefix.PrefixIncrement);
                    break;
                case JSToken.Decrement:
                    exprCtx = m_currentToken.Clone();
                    GetNextToken();
                    canBeAttribute = false;
                    expr = ParseUnaryExpression(out dummy, ref canBeAttribute, false);
                    exprCtx.UpdateWith(expr.Context);
                    ast = new PostOrPrefixOperator(exprCtx, this, expr, m_currentToken.Token, PostOrPrefix.PrefixDecrement);
                    break;
                default:
                    m_noSkipTokenSet.Add(NoSkipTokenSet.s_PostfixExpressionNoSkipTokenSet);
                    try
                    {
                        ast = ParseLeftHandSideExpression(isMinus, ref canBeAttribute, warnForKeyword);
                    }
                    catch (RecoveryTokenException exc)
                    {
                        if (IndexOfToken(NoSkipTokenSet.s_PostfixExpressionNoSkipTokenSet, exc) == -1)
                        {
                            throw;
                        }
                        else
                        {
                            if (exc._partiallyComputedNode == null)
                                SkipTokensAndThrow();
                            else
                                ast = exc._partiallyComputedNode;
                        }
                    }
                    finally
                    {
                        m_noSkipTokenSet.Remove(NoSkipTokenSet.s_PostfixExpressionNoSkipTokenSet);
                    }
                    ast = ParsePostfixExpression(ast, out isLeftHandSideExpr, ref canBeAttribute);
                    break;
            }

            return ast;
        }

        //---------------------------------------------------------------------------------------
        // ParsePostfixExpression
        //
        //  PostfixExpression:
        //    LeftHandSideExpression |
        //    LeftHandSideExpression '++' |
        //    LeftHandSideExpression  '--'
        //
        //---------------------------------------------------------------------------------------
        private AstNode ParsePostfixExpression(AstNode ast, out bool isLeftHandSideExpr, ref bool canBeAttribute)
        {
            isLeftHandSideExpr = true;
            Context exprCtx = null;
            if (null != ast)
            {
                if (!m_scanner.GotEndOfLine)
                {
                    if (JSToken.Increment == m_currentToken.Token)
                    {
                        isLeftHandSideExpr = false;
                        exprCtx = ast.Context.Clone();
                        exprCtx.UpdateWith(m_currentToken);
                        canBeAttribute = false;
                        ast = new PostOrPrefixOperator(exprCtx, this, ast, m_currentToken.Token, PostOrPrefix.PostfixIncrement);
                        GetNextToken();
                    }
                    else if (JSToken.Decrement == m_currentToken.Token)
                    {
                        isLeftHandSideExpr = false;
                        exprCtx = ast.Context.Clone();
                        exprCtx.UpdateWith(m_currentToken);
                        canBeAttribute = false;
                        ast = new PostOrPrefixOperator(exprCtx, this, ast, m_currentToken.Token, PostOrPrefix.PostfixDecrement);
                        GetNextToken();
                    }
                }
            }
            return ast;
        }

        //---------------------------------------------------------------------------------------
        // ParseLeftHandSideExpression
        //
        //  LeftHandSideExpression :
        //    PrimaryExpression Accessor  |
        //    'new' LeftHandSideExpression |
        //    FunctionExpression
        //
        //  PrimaryExpression :
        //    'this' |
        //    Identifier |
        //    Literal |
        //    '(' Expression ')'
        //
        //  FunctionExpression :
        //    'function' OptionalFuncName '(' FormalParameterList ')' { FunctionBody }
        //
        //  OptionalFuncName :
        //    <empty> |
        //    Identifier
        //---------------------------------------------------------------------------------------
        private AstNode ParseLeftHandSideExpression(bool isMinus, ref bool canBeAttribute, bool warnForKeyword)
        {
            AstNode ast = null;
            bool isFunction = false;
            List<Context> newContexts = null;

            TryItAgain:

            // new expression
            while (JSToken.New == m_currentToken.Token)
            {
                if (null == newContexts)
                    newContexts = new List<Context>(4);
                newContexts.Add(m_currentToken.Clone());
                GetNextToken();
            }
            JSToken token = m_currentToken.Token;
            switch (token)
            {
                // primary expression
                case JSToken.Identifier:
                    ast = new Lookup(m_scanner.GetIdentifier(), m_currentToken.Clone(), this);
                    break;

                case JSToken.ConditionalCommentStart:
                    // skip past the start to the next token
                    GetNextToken();
                    if (m_currentToken.Token == JSToken.PreprocessorConstant)
                    {
                        // we have /*@id
                        ast = new ConstantWrapperPP(m_currentToken.Code, true, m_currentToken.Clone(), this);
                        GetNextToken();

                        if (m_currentToken.Token == JSToken.ConditionalCommentEnd)
                        {
                            // skip past the closing comment
                            GetNextToken();
                        }
                        else
                        {
                            // we ONLY support /*@id@*/ in expressions right now. If there's not
                            // a closing comment after the ID, then we don't support it.
                            // throw an error, skip to the end of the comment, then ignore it and start
                            // looking for the next token.
                            m_currentToken.HandleError(JSError.ConditionalCompilationTooComplex);

                            // skip to end of conditional comment
                            while (m_currentToken.Token != JSToken.EndOfFile && m_currentToken.Token != JSToken.ConditionalCommentEnd)
                            {
                                GetNextToken();
                            }
                            GetNextToken();
                            goto TryItAgain;
                        }
                    }
                    else if (m_currentToken.Token == JSToken.ConditionalCommentEnd)
                    {
                        // empty conditional comment! Ignore.
                        GetNextToken();
                        goto TryItAgain;
                    }
                    else
                    {
                        // we DON'T have "/*@IDENT". We only support "/*@IDENT @*/", so since this isn't
                        // and id, throw the error, skip to the end of the comment, and ignore it
                        // by looping back and looking for the NEXT token.
                        m_currentToken.HandleError(JSError.ConditionalCompilationTooComplex);

                        // skip to end of conditional comment
                        while (m_currentToken.Token != JSToken.EndOfFile && m_currentToken.Token != JSToken.ConditionalCommentEnd)
                        {
                            GetNextToken();
                        }
                        GetNextToken();
                        goto TryItAgain;
                    }
                    break;

                case JSToken.This:
                    canBeAttribute = false;
                    ast = new ThisLiteral(m_currentToken.Clone(), this);
                    break;

                case JSToken.StringLiteral:
                    canBeAttribute = false;
                    ast = new ConstantWrapper(m_scanner.StringLiteral, PrimitiveType.String, m_currentToken.Clone(), this);
                    break;

                case JSToken.IntegerLiteral:
                case JSToken.NumericLiteral:
                    {
                        canBeAttribute = false;
                        Context numericContext = m_currentToken.Clone();
                        double doubleValue;
                        if (ConvertNumericLiteralToDouble(m_currentToken.Code, (token == JSToken.IntegerLiteral), out doubleValue))
                        {
                            // conversion worked fine
                            // check for some boundary conditions
                            if (doubleValue == double.MaxValue)
                            {
                                ReportError(JSError.NumericMaximum, numericContext, true);
                            }
                            else if (isMinus && -doubleValue == double.MinValue)
                            {
                                ReportError(JSError.NumericMinimum, numericContext, true);
                            }

                            // create the constant wrapper from the value
                            ast = new ConstantWrapper(doubleValue, PrimitiveType.Number, numericContext, this);
                        }
                        else
                        {
                            // check to see if we went overflow
                            if (double.IsInfinity(doubleValue))
                            {
                                ReportError(JSError.NumericOverflow, numericContext, true);
                            }

                            // regardless, we're going to create a special constant wrapper
                            // that simply echos the input as-is
                            ast = new ConstantWrapper(m_currentToken.Code, PrimitiveType.Other, numericContext, this);
                        }
                        break;
                    }

                case JSToken.True:
                    canBeAttribute = false;
                    ast = new ConstantWrapper(true, PrimitiveType.Boolean, m_currentToken.Clone(), this);
                    break;

                case JSToken.False:
                    canBeAttribute = false;
                    ast = new ConstantWrapper(false, PrimitiveType.Boolean, m_currentToken.Clone(), this);
                    break;

                case JSToken.Null:
                    canBeAttribute = false;
                    ast = new ConstantWrapper(null, PrimitiveType.Null, m_currentToken.Clone(), this);
                    break;

                case JSToken.PreprocessorConstant:
                    canBeAttribute = false;
                    ast = new ConstantWrapperPP(m_currentToken.Code, false, m_currentToken.Clone(), this);
                    break;

                case JSToken.DivideAssign:
                // normally this token is not allowed on the left-hand side of an expression.
                // BUT, this might be the start of a regular expression that begins with an equals sign!
                // we need to test to see if we can parse a regular expression, and if not, THEN
                // we can fail the parse.

                case JSToken.Divide:
                    canBeAttribute = false;
                    // could it be a regexp?
                    String source = m_scanner.ScanRegExp();
                    if (source != null)
                    {
                        // parse the flags (if any)
                        String flags = m_scanner.ScanRegExpFlags();
                        // create the literal
                        ast = new RegExpLiteral(source, flags, m_currentToken.Clone(), this);
                        break;
                    }
                    goto default;

                // expression
                case JSToken.LeftParenthesis:
                    {
                        canBeAttribute = false;
                        // save the current context reference
                        Context openParenContext = m_currentToken.Clone();
                        GetNextToken();
                        m_noSkipTokenSet.Add(NoSkipTokenSet.s_ParenExpressionNoSkipToken);
                        try
                        {
                            // parse an expression
                            ast = ParseExpression();
                            
                            // update the expression's context with the context of the open paren
                            ast.Context.UpdateWith(openParenContext);
                            if (JSToken.RightParenthesis != m_currentToken.Token)
                            {
                                ReportError(JSError.NoRightParenthesis);
                            }
                            else
                            {
                                // add the closing paren to the expression context
                                ast.Context.UpdateWith(m_currentToken);
                            }
                        }
                        catch (RecoveryTokenException exc)
                        {
                            if (IndexOfToken(NoSkipTokenSet.s_ParenExpressionNoSkipToken, exc) == -1)
                                throw;
                            else
                                ast = exc._partiallyComputedNode;
                        }
                        finally
                        {
                            m_noSkipTokenSet.Remove(NoSkipTokenSet.s_ParenExpressionNoSkipToken);
                        }
                        if (ast == null) //this can only happen when catching the exception and nothing was sent up by the caller
                            SkipTokensAndThrow();
                    }
                    break;

                // array initializer
                case JSToken.LeftBracket:
                    canBeAttribute = false;
                    Context listCtx = m_currentToken.Clone();
                    AstNodeList list = new AstNodeList(m_currentToken.Clone(), this);
                    GetNextToken();
                    while (JSToken.RightBracket != m_currentToken.Token)
                    {
                        if (JSToken.Comma != m_currentToken.Token)
                        {
                            m_noSkipTokenSet.Add(NoSkipTokenSet.s_ArrayInitNoSkipTokenSet);
                            try
                            {
                                list.Append(ParseExpression(true));
                                if (JSToken.Comma != m_currentToken.Token)
                                {
                                    if (JSToken.RightBracket != m_currentToken.Token)
                                        ReportError(JSError.NoRightBracket);
                                    break;
                                }
                                else
                                {
                                    // we have a comma -- skip it
                                    GetNextToken();

                                    // if the next token is the closing brackets, then we need to
                                    // add a missing value to the array because we end in a comma and
                                    // we need to keep it for cross-platform compat.
                                    // TECHNICALLY, that puts an extra item into the array for most modern browsers, but not ALL.
                                    if (m_currentToken.Token == JSToken.RightBracket)
                                    {
                                        list.Append(new ConstantWrapper(Missing.Value, PrimitiveType.Other, m_currentToken.Clone(), this));
                                    }
                                }
                            }
                            catch (RecoveryTokenException exc)
                            {
                                if (exc._partiallyComputedNode != null)
                                    list.Append(exc._partiallyComputedNode);
                                if (IndexOfToken(NoSkipTokenSet.s_ArrayInitNoSkipTokenSet, exc) == -1)
                                {
                                    listCtx.UpdateWith(CurrentPositionContext());
                                    exc._partiallyComputedNode = new ArrayLiteral(listCtx, this, list);
                                    throw;
                                }
                                else
                                {
                                    if (JSToken.RightBracket == m_currentToken.Token)
                                        break;
                                }
                            }
                            finally
                            {
                                m_noSkipTokenSet.Remove(NoSkipTokenSet.s_ArrayInitNoSkipTokenSet);
                            }
                        }
                        else
                        {
                            // comma -- missing array item in the list
                            list.Append(new ConstantWrapper(Missing.Value, PrimitiveType.Other, m_currentToken.Clone(), this));

                            // skip over the comma
                            GetNextToken();

                            // if the next token is the closing brace, then we end with a comma -- and we need to
                            // add ANOTHER missing value to make sure this last comma doesn't get left off.
                            // TECHNICALLY, that puts an extra item into the array for most modern browsers, but not ALL.
                            if (m_currentToken.Token == JSToken.RightBracket)
                            {
                                list.Append(new ConstantWrapper(Missing.Value, PrimitiveType.Other, m_currentToken.Clone(), this));
                            }
                        }
                    }
                    listCtx.UpdateWith(m_currentToken);
                    ast = new ArrayLiteral(listCtx, this, list);
                    break;

                // object initializer
                case JSToken.LeftCurly:
                    canBeAttribute = false;
                    Context objCtx = m_currentToken.Clone();
                    GetNextToken();

                    // we're going to keep the keys and values in separate lists, but make sure
                    // that the indexes correlate (keyList[n] is associated with valueList[n])
                    List<ObjectLiteralField> keyList = new List<ObjectLiteralField>();
                    List<AstNode> valueList = new List<AstNode>();

                    if (JSToken.RightCurly != m_currentToken.Token)
                    {
                        for (; ; )
                        {
                            ObjectLiteralField field = null;
                            AstNode value = null;
                            bool getterSetter = false;
                            string ident;

                            switch (m_currentToken.Token)
                            {
                                case JSToken.Identifier:
                                    field = new ObjectLiteralField(m_scanner.GetIdentifier(), PrimitiveType.String, m_currentToken.Clone(), this);
                                    break;

                                case JSToken.StringLiteral:
                                    field = new ObjectLiteralField(m_scanner.StringLiteral, PrimitiveType.String, m_currentToken.Clone(), this);
                                    break;

                                case JSToken.IntegerLiteral:
                                case JSToken.NumericLiteral:
                                    {
                                        double doubleValue;
                                        if (ConvertNumericLiteralToDouble(m_currentToken.Code, (m_currentToken.Token == JSToken.IntegerLiteral), out doubleValue))
                                        {
                                            // conversion worked fine
                                            field = new ObjectLiteralField(
                                              doubleValue,
                                              PrimitiveType.Number,
                                              m_currentToken.Clone(),
                                              this
                                              );
                                        }
                                        else
                                        {
                                            // something went wrong and we're not sure the string representation in the source is 
                                            // going to convert to a numeric value well
                                            if (double.IsInfinity(doubleValue))
                                            {
                                                ReportError(JSError.NumericOverflow, m_currentToken.Clone(), true);
                                            }

                                            // use the source as the field name, not the numeric value
                                            field = new ObjectLiteralField(
                                                m_currentToken.Code,
                                                PrimitiveType.Other,
                                                m_currentToken.Clone(),
                                                this);
                                        }
                                        break;
                                    }

                                case JSToken.Get:
                                case JSToken.Set:
                                    if (m_scanner.PeekToken() == JSToken.Colon)
                                    {
                                        // the field is either "get" or "set" and isn't the special Mozilla getter/setter
                                        field = new ObjectLiteralField(m_currentToken.Code, PrimitiveType.String, m_currentToken.Clone(), this);
                                    }
                                    else
                                    {
                                        // Mozilla-specific get/set property construct
                                        getterSetter = true;
                                        bool isGet = (m_currentToken.Token == JSToken.Get);
                                        value = ParseFunction(
                                          (JSToken.Get == m_currentToken.Token ? FunctionType.Getter : FunctionType.Setter),
                                          m_currentToken.Clone()
                                          );
                                        FunctionObject funcExpr = value as FunctionObject;
                                        if (funcExpr != null)
                                        {
                                            // getter/setter is just the literal name with a get/set flag
                                            field = new GetterSetter(
                                              funcExpr.Name,
                                              isGet,
                                              funcExpr.IdContext.Clone(),
                                              this
                                              );
                                        }
                                        else
                                        {
                                            ReportError(JSError.FunctionExpressionExpected);
                                        }
                                    }
                                    break;

                                default:
                                    // NOT: identifier, string, number, or getter/setter.
                                    // see if it's a token that COULD be an identifier.
                                    ident = JSKeyword.CanBeIdentifier(m_currentToken.Token);
                                    if (ident != null)
                                    {
                                        // don't throw a warning -- it's okay to have a keyword that
                                        // can be an identifier here.
                                        field = new ObjectLiteralField(ident, PrimitiveType.String, m_currentToken.Clone(), this);
                                    }
                                    else
                                    {
                                        ReportError(JSError.NoMemberIdentifier);
                                        field = new ObjectLiteralField("_#Missing_Field#_" + s_cDummyName++, PrimitiveType.String, CurrentPositionContext(), this);
                                    }
                                    break;
                            }

                            if (field != null)
                            {
                                if (!getterSetter)
                                {
                                    GetNextToken();
                                }

                                m_noSkipTokenSet.Add(NoSkipTokenSet.s_ObjectInitNoSkipTokenSet);
                                try
                                {
                                    if (!getterSetter)
                                    {
                                        // get the value
                                        if (JSToken.Colon != m_currentToken.Token)
                                        {
                                            ReportError(JSError.NoColon, true);
                                            value = ParseExpression(true);
                                        }
                                        else
                                        {
                                            GetNextToken();
                                            value = ParseExpression(true);
                                        }
                                    }

                                    // put the pair into the list of fields
                                    keyList.Add(field);
                                    valueList.Add(value);

                                    if (JSToken.RightCurly == m_currentToken.Token)
                                        break;
                                    else
                                    {
                                        if (JSToken.Comma == m_currentToken.Token)
                                        {
                                            // skip the comma
                                            GetNextToken();

                                            // if the next token is the right-curly brace, then we ended 
                                            // the list with a comma, which is perfectly fine
                                            if (m_currentToken.Token == JSToken.RightCurly)
                                            {
                                                break;
                                            }
                                        }
                                        else
                                        {
                                            if (m_scanner.GotEndOfLine)
                                            {
                                                ReportError(JSError.NoRightCurly);
                                            }
                                            else
                                                ReportError(JSError.NoComma, true);
                                            SkipTokensAndThrow();
                                        }
                                    }
                                }
                                catch (RecoveryTokenException exc)
                                {
                                    if (exc._partiallyComputedNode != null)
                                    {
                                        // the problem was in ParseExpression trying to determine value
                                        value = exc._partiallyComputedNode;
                                        keyList.Add(field);
                                        valueList.Add(value);
                                    }
                                    if (IndexOfToken(NoSkipTokenSet.s_ObjectInitNoSkipTokenSet, exc) == -1)
                                    {
                                        exc._partiallyComputedNode = new ObjectLiteral(objCtx, this, keyList.ToArray(), valueList.ToArray());
                                        throw;
                                    }
                                    else
                                    {
                                        if (JSToken.Comma == m_currentToken.Token)
                                            GetNextToken();
                                        if (JSToken.RightCurly == m_currentToken.Token)
                                            break;
                                    }
                                }
                                finally
                                {
                                    m_noSkipTokenSet.Remove(NoSkipTokenSet.s_ObjectInitNoSkipTokenSet);
                                }
                            }
                        }
                    }
                    objCtx.UpdateWith(m_currentToken);
                    ast = new ObjectLiteral(objCtx, this, keyList.ToArray(), valueList.ToArray());
                    break;

                // function expression
                case JSToken.Function:
                    canBeAttribute = false;
                    ast = ParseFunction(FunctionType.Expression, m_currentToken.Clone());
                    isFunction = true;
                    break;

                case JSToken.AspNetBlock:
                    ast = ParseAspNetBlock(consumeSemicolonIfPossible: false);
                    break;

                default:
                    string identifier = JSKeyword.CanBeIdentifier(m_currentToken.Token);
                    if (null != identifier)
                    {
                        if (warnForKeyword)
                        {
                            switch (m_currentToken.Token)
                            {
                                case JSToken.Boolean:
                                case JSToken.Byte:
                                case JSToken.Char:
                                case JSToken.Double:
                                case JSToken.Float:
                                case JSToken.Int:
                                case JSToken.Long:
                                case JSToken.Short:
                                case JSToken.Void:
                                    break;
                                default:
                                    ForceReportInfo(JSError.KeywordUsedAsIdentifier);
                                    break;
                            }
                        }
                        canBeAttribute = false;
                        ast = new Lookup(identifier, m_currentToken.Clone(), this);
                    }
                    else
                    {
                        ReportError(JSError.ExpressionExpected);
                        SkipTokensAndThrow();
                    }
                    break;
            }

            // can be a CallExpression, that is, followed by '.' or '(' or '['
            if (!isFunction)
                GetNextToken();

            return MemberExpression(ast, newContexts, ref canBeAttribute);
        }

        /// <summary>
        /// Convert the given numeric string to a double value
        /// </summary>
        /// <param name="str">string representation of a number</param>
        /// <param name="isInteger">we should know alreasdy if it's an integer or not</param>
        /// <param name="doubleValue">output value</param>
        /// <returns>true if there were no problems; false if there were</returns>
        private bool ConvertNumericLiteralToDouble(string str, bool isInteger, out double doubleValue)
        {
            try
            {
                if (isInteger)
                {
                    if (str[0] == '0' && str.Length > 1)
                    {
                        if (str[1] == 'x' || str[1] == 'X')
                        {
                            if (str.Length == 2)
                            {
                                // 0x???? must be a parse error. Just return zero
                                doubleValue = 0;
                                return false;
                            }

                            // parse the number as a hex integer, converted to a double
                            doubleValue = (double)System.Convert.ToInt64(str, 16);
                        }
                        else
                        {
                            // might be an octal value... try converting to octal
                            // and if it fails, just convert to decimal
                            try
                            {
                                doubleValue = (double)System.Convert.ToInt64(str, 8);

                                // if we got here, we successfully converted it to octal.
                                // now, octal literals are deprecated -- not all JS implementations will
                                // decode them. If this decoded as an octal, it can also be a decimal. Check
                                // the decimal value, and if it's the same, then we'll just treat it
                                // as a normal decimal value. Otherwise we'll throw a warning and treat it
                                // as a special no-convert literal.
                                double decimalValue = (double)System.Convert.ToInt64(str, 10);
                                if (decimalValue != doubleValue)
                                {
                                    // throw a warning!
                                    ReportError(JSError.OctalLiteralsDeprecated, m_currentToken.Clone(), true);

                                    // return false because octals are deprecated and might have
                                    // cross-browser issues
                                    return false;
                                }
                            }
                            catch (FormatException)
                            {
                                // ignore the format exception and fall through to parsing
                                // the value as a base-10 decimal value
                                doubleValue = Convert.ToDouble(str, CultureInfo.InvariantCulture);
                            }
                        }
                    }
                    else
                    {
                        // just parse the integer as a decimal value
                        doubleValue = Convert.ToDouble(str, CultureInfo.InvariantCulture);
                    }

                    // check for out-of-bounds integer values -- if the integer can't EXACTLY be represented
                    // as a double, then we don't want to consider it "successful"
                    if (doubleValue < -0x20000000000000 || 0x20000000000000 < doubleValue)
                    {
                        return false;
                    }
                }
                else
                {
                    // use the system to convert the string to a double
                    doubleValue = Convert.ToDouble(str, CultureInfo.InvariantCulture);
                }

                // if we got here, we should have an appropriate value in doubleValue
                return true;
            }
            catch (OverflowException)
            {
                // overflow mean just return one of the infinity values
                doubleValue = (str[0] == '-'
                  ? Double.NegativeInfinity
                  : Double.PositiveInfinity
                  );

                // and it wasn't "successful"
                return false;
            }
            catch (FormatException)
            {
                // format exception converts to NaN
                doubleValue = double.NaN;

                // not successful
                return false;
            }
        }

        //---------------------------------------------------------------------------------------
        // MemberExpression
        //
        // Accessor :
        //  <empty> |
        //  Arguments Accessor
        //  '[' Expression ']' Accessor |
        //  '.' Identifier Accessor |
        //
        //  Don't have this function throwing an exception without checking all the calling sites.
        //  There is state in instance variable that is saved on the calling stack in some function
        //  (i.e ParseFunction and ParseClass) and you don't want to blow up the stack
        //---------------------------------------------------------------------------------------
        private AstNode MemberExpression(AstNode expression, List<Context> newContexts, ref bool canBeAttribute)
        {
            bool canBeQualid;
            return MemberExpression(expression, newContexts, out canBeQualid, ref canBeAttribute);
        }

        private AstNode MemberExpression(AstNode expression, List<Context> newContexts, out bool canBeQualid, ref bool canBeAttribute)
        {
            bool noMoreForAttr = false;
            canBeQualid = true;
            for (; ; )
            {
                m_noSkipTokenSet.Add(NoSkipTokenSet.s_MemberExprNoSkipTokenSet);
                try
                {
                    switch (m_currentToken.Token)
                    {
                        case JSToken.LeftParenthesis:
                            if (noMoreForAttr)
                                canBeAttribute = false;
                            else
                                noMoreForAttr = true;
                            canBeQualid = false;

                            AstNodeList args = null;
                            RecoveryTokenException callError = null;
                            m_noSkipTokenSet.Add(NoSkipTokenSet.s_ParenToken);
                            try
                            {
                                args = ParseExpressionList(JSToken.RightParenthesis);
                            }
                            catch (RecoveryTokenException exc)
                            {
                                args = (AstNodeList)exc._partiallyComputedNode;
                                if (IndexOfToken(NoSkipTokenSet.s_ParenToken, exc) == -1)
                                    callError = exc; // thrown later on
                            }
                            finally
                            {
                                m_noSkipTokenSet.Remove(NoSkipTokenSet.s_ParenToken);
                            }

                            //treat eval and print specially
                            if (expression is Lookup)
                            {
                                String name = expression.ToString();
                                if (name.Equals("eval"))
                                {
                                    expression.Context.UpdateWith(args.Context);
                                    if (args.Count > 0)
                                        expression = new EvaluateNode(expression.Context, this, args[0]);
                                    else
                                        expression = new EvaluateNode(expression.Context, this, new ConstantWrapper("", PrimitiveType.String, CurrentPositionContext(), this));
                                }
                                else
                                {
                                    expression = new CallNode(expression.Context.CombineWith(args.Context), this, expression, args, false);
                                }
                            }
                            else
                                expression = new CallNode(expression.Context.CombineWith(args.Context), this, expression, args, false);

                            if (null != newContexts && newContexts.Count > 0)
                            {
                                (newContexts[newContexts.Count - 1]).UpdateWith(expression.Context);
                                if (!(expression is CallNode))
                                    expression = new CallNode(newContexts[newContexts.Count - 1], this, expression, new AstNodeList(CurrentPositionContext(), this), false);
                                else
                                    expression.Context = newContexts[newContexts.Count - 1];
                                ((CallNode)expression).IsConstructor = true;
                                newContexts.RemoveAt(newContexts.Count - 1);
                            }

                            if (callError != null)
                            {
                                callError._partiallyComputedNode = expression;
                                throw callError;
                            }

                            GetNextToken();
                            break;

                        case JSToken.LeftBracket:
                            canBeQualid = false;
                            canBeAttribute = false;
                            m_noSkipTokenSet.Add(NoSkipTokenSet.s_BracketToken);
                            try
                            {
                                //
                                // ROTOR parses a[b,c] as a call to a, passing in the arguments b and c.
                                // the correct parse is a member lookup on a of c -- the "b,c" should be
                                // a single expression with a comma operator that evaluates b but only
                                // returns c.
                                // So we'll change the default behavior from parsing an expression list to
                                // parsing a single expression, but returning a single-item list (or an empty
                                // list if there is no expression) so the rest of the code will work.
                                //
                                //args = ParseExpressionList(JSToken.RightBracket);
                                GetNextToken();
                                args = new AstNodeList(m_currentToken.Clone(), this);

                                AstNode accessor = ParseExpression();
                                if (accessor != null)
                                {
                                    args.Append(accessor);
                                }
                            }
                            catch (RecoveryTokenException exc)
                            {
                                if (IndexOfToken(NoSkipTokenSet.s_BracketToken, exc) == -1)
                                {
                                    if (exc._partiallyComputedNode != null)
                                    {
                                        exc._partiallyComputedNode =
                                           new CallNode(expression.Context.CombineWith(m_currentToken.Clone()), this, expression, (AstNodeList)exc._partiallyComputedNode, true);
                                    }
                                    else
                                    {
                                        exc._partiallyComputedNode = expression;
                                    }
                                    throw;
                                }
                                else
                                    args = (AstNodeList)exc._partiallyComputedNode;
                            }
                            finally
                            {
                                m_noSkipTokenSet.Remove(NoSkipTokenSet.s_BracketToken);
                            }
                            expression = new CallNode(expression.Context.CombineWith(m_currentToken.Clone()), this, expression, args, true);

                            // there originally was code here in the ROTOR sources that checked the new context list and
                            // changed this member call to a constructor call, effectively combining the two. I believe they
                            // need to remain separate.

                            // remove the close bracket token
                            GetNextToken();
                            break;

                        case JSToken.AccessField:
                            if (noMoreForAttr)
                                canBeAttribute = false;
                            ConstantWrapper id = null;
                            GetNextToken();
                            if (JSToken.Identifier != m_currentToken.Token)
                            {
                                string identifier = JSKeyword.CanBeIdentifier(m_currentToken.Token);
                                if (null != identifier)
                                {
                                    // don't report an error here -- it's actually okay to have a property name
                                    // that is a keyword which is okay to be an identifier. For instance,
                                    // jQuery has a commonly-used method named "get" to make an ajax request
                                    //ForceReportInfo(JSError.KeywordUsedAsIdentifier);
                                    id = new ConstantWrapper(identifier, PrimitiveType.String, m_currentToken.Clone(), this);
                                }
                                else if (JSScanner.IsValidIdentifier(m_currentToken.Code))
                                {
                                    // it must be a keyword, because it can't technically be an indentifier,
                                    // but it IS a valid identifier format. Throw the error but still
                                    // create the constant wrapper so we can output it as-is
                                    ReportError(JSError.NoIdentifier, m_currentToken.Clone(), true);
                                    id = new ConstantWrapper(m_currentToken.Code, PrimitiveType.String, m_currentToken.Clone(), this);
                                }
                                else
                                {
                                    ReportError(JSError.NoIdentifier);
                                    SkipTokensAndThrow(expression);
                                }
                            }
                            else
                            {
                                id = new ConstantWrapper(m_scanner.GetIdentifier(), PrimitiveType.String, m_currentToken.Clone(), this);
                            }
                            GetNextToken();
                            expression = new Member(expression.Context.CombineWith(id.Context), this, expression, id.Context.Code);
                            break;
                        default:
                            if (null != newContexts)
                            {
                                while (newContexts.Count > 0)
                                {
                                    (newContexts[newContexts.Count - 1]).UpdateWith(expression.Context);
                                    expression = new CallNode(newContexts[newContexts.Count - 1],
                                                          this,
                                                          expression,
                                                          new AstNodeList(CurrentPositionContext(), this),
                                                          false);
                                    ((CallNode)expression).IsConstructor = true;
                                    newContexts.RemoveAt(newContexts.Count - 1);
                                }
                            }
                            return expression;
                    }
                }
                catch (RecoveryTokenException exc)
                {
                    if (IndexOfToken(NoSkipTokenSet.s_MemberExprNoSkipTokenSet, exc) != -1)
                        expression = exc._partiallyComputedNode;
                    else
                    {
                        Debug.Assert(exc._partiallyComputedNode == expression);
                        throw;
                    }
                }
                finally
                {
                    m_noSkipTokenSet.Remove(NoSkipTokenSet.s_MemberExprNoSkipTokenSet);
                }
            }
        }

        //---------------------------------------------------------------------------------------
        // ParseExpressionList
        //
        //  Given a starting this.currentToken '(' or '[', parse a list of expression separated by
        //  ',' until matching ')' or ']'
        //---------------------------------------------------------------------------------------
        private AstNodeList ParseExpressionList(JSToken terminator)
        {
            Context listCtx = m_currentToken.Clone();
            GetNextToken();
            AstNodeList list = new AstNodeList(listCtx, this);
            if (terminator != m_currentToken.Token)
            {
                for (; ; )
                {
                    m_noSkipTokenSet.Add(NoSkipTokenSet.s_ExpressionListNoSkipTokenSet);
                    try
                    {
                        if (JSToken.Comma == m_currentToken.Token)
                        {
                            list.Append(new ConstantWrapper(Missing.Value, PrimitiveType.Other, m_currentToken.Clone(), this));
                        }
                        else if (terminator == m_currentToken.Token)
                        {
                            break;
                        }
                        else
                            list.Append(ParseExpression(true));

                        if (terminator == m_currentToken.Token)
                            break;
                        else
                        {
                            if (JSToken.Comma != m_currentToken.Token)
                            {
                                if (terminator == JSToken.RightParenthesis)
                                {
                                    //  in ASP+ it's easy to write a semicolon at the end of an expression
                                    //  not realizing it is going to go inside a function call
                                    //  (ie. Response.Write()), so make a special check here
                                    if (JSToken.Semicolon == m_currentToken.Token)
                                    {
                                        if (JSToken.RightParenthesis == m_scanner.PeekToken())
                                        {
                                            ReportError(JSError.UnexpectedSemicolon, true);
                                            GetNextToken();
                                            break;
                                        }
                                    }
                                    ReportError(JSError.NoRightParenthesisOrComma);
                                }
                                else
                                    ReportError(JSError.NoRightBracketOrComma);
                                SkipTokensAndThrow();
                            }
                        }
                    }
                    catch (RecoveryTokenException exc)
                    {
                        if (exc._partiallyComputedNode != null)
                            list.Append(exc._partiallyComputedNode);
                        if (IndexOfToken(NoSkipTokenSet.s_ExpressionListNoSkipTokenSet, exc) == -1)
                        {
                            exc._partiallyComputedNode = list;
                            throw;
                        }
                    }
                    finally
                    {
                        m_noSkipTokenSet.Remove(NoSkipTokenSet.s_ExpressionListNoSkipTokenSet);
                    }
                    GetNextToken();
                }
            }
            listCtx.UpdateWith(m_currentToken);
            return list;
        }

        //---------------------------------------------------------------------------------------
        // CreateExpressionNode
        //
        //  Create the proper AST object according to operator
        //---------------------------------------------------------------------------------------
        private AstNode CreateExpressionNode(JSToken op, AstNode operand1, AstNode operand2)
        {
            Context context = operand1.Context.CombineWith(operand2.Context);
            switch (op)
            {
                case JSToken.Assign:
                case JSToken.BitwiseAnd:
                case JSToken.BitwiseAndAssign:
                case JSToken.BitwiseOr:
                case JSToken.BitwiseOrAssign:
                case JSToken.BitwiseXor:
                case JSToken.BitwiseXorAssign:
                case JSToken.Comma:
                case JSToken.Divide:
                case JSToken.DivideAssign:
                case JSToken.Equal:
                case JSToken.GreaterThan:
                case JSToken.GreaterThanEqual:
                case JSToken.In:
                case JSToken.InstanceOf:
                case JSToken.LeftShift:
                case JSToken.LeftShiftAssign:
                case JSToken.LessThan:
                case JSToken.LessThanEqual:
                case JSToken.LogicalAnd:
                case JSToken.LogicalOr:
                case JSToken.Minus:
                case JSToken.MinusAssign:
                case JSToken.Modulo:
                case JSToken.ModuloAssign:
                case JSToken.Multiply:
                case JSToken.MultiplyAssign:
                case JSToken.NotEqual:
                case JSToken.Plus:
                case JSToken.PlusAssign:
                case JSToken.RightShift:
                case JSToken.RightShiftAssign:
                case JSToken.StrictEqual:
                case JSToken.StrictNotEqual:
                case JSToken.UnsignedRightShift:
                case JSToken.UnsignedRightShiftAssign:
                    return new BinaryOperator(context, this, operand1, operand2, op);
                default:
                    Debug.Assert(false);
                    return null;
            }
        }

        //---------------------------------------------------------------------------------------
        // GetNextToken
        //
        //  Return the next token or peeked token if this.errorToken is not null.
        //  Usually this.errorToken is set by AddError even though any code can look ahead
        //  by assigning this.errorToken.
        //  At this point the context is not saved so if position information is needed
        //  they have to be saved explicitely
        //---------------------------------------------------------------------------------------
        private void GetNextToken()
        {
            try
            {
                if (null != m_errorToken)
                {
                    if (m_breakRecursion > 10)
                    {
                        m_errorToken = null;
                        m_scanner.GetNextToken();
                        return;
                    }
                    m_breakRecursion++;
                    m_currentToken = m_errorToken;
                    m_errorToken = null;
                }
                else
                {
                    m_goodTokensProcessed++;
                    m_breakRecursion = 0;
                    // the scanner shares this.currentToken with the parser
                    m_scanner.GetNextToken();
                }
            }
            catch (ScannerException e)
            {
                if (e.Error != JSError.NoCommentEnd)
                {
                    // rethrow anything that isn't an unterminated comment
                    throw;
                }
                else
                {
                    m_currentToken.Token = JSToken.EndOfFile;
                    m_currentToken.HandleError(JSError.NoCommentEnd);
                }
            }
        }

        private Context CurrentPositionContext()
        {
            Context context = m_currentToken.Clone();
            context.EndPosition = (context.StartPosition < context.Document.Source.Length) ? context.StartPosition + 1 : context.StartPosition;
            return context;
        }

        //---------------------------------------------------------------------------------------
        // ReportError
        //
        //  Generate a parser error.
        //  When no context is provided the token is missing so the context is the current position
        //---------------------------------------------------------------------------------------
        private void ReportError(JSError errorId)
        {
            ReportError(errorId, false);
        }

        //---------------------------------------------------------------------------------------
        // ReportError
        //
        //  Generate a parser error.
        //  When no context is provided the token is missing so the context is the current position
        //  The function is told whether or not next call to GetToken() should return the same
        //  token or not
        //---------------------------------------------------------------------------------------
        private void ReportError(JSError errorId, bool skipToken)
        {
            // get the current position token
            Context context = m_currentToken.Clone();
            context.EndPosition = context.StartPosition + 1;
            ReportError(errorId, context, skipToken);
        }

        //---------------------------------------------------------------------------------------
        // ReportError
        //
        //  Generate a parser error.
        //  The function is told whether or not next call to GetToken() should return the same
        //  token or not
        //---------------------------------------------------------------------------------------
        private void ReportError(JSError errorId, Context context, bool skipToken)
        {
            Debug.Assert(context != null);
            int previousSeverity = m_severity;
            m_severity = (new JScriptException(errorId)).Severity;
            // EOF error is special and it's the last error we can possibly get
            if (JSToken.EndOfFile == context.Token)
                EOFError(errorId); // EOF context is special
            else
            {
                // report the error if not in error condition and the
                // error for this token is not worse than the one for the
                // previous token
                if (m_goodTokensProcessed > 0 || m_severity < previousSeverity)
                    context.HandleError(errorId);

                // reset proper info
                if (skipToken)
                    m_goodTokensProcessed = -1;
                else
                {
                    m_errorToken = m_currentToken;
                    m_goodTokensProcessed = 0;
                }
            }
        }

        //---------------------------------------------------------------------------------------
        // ForceReportInfo
        //
        //  Generate a parser error (info), does not change the error state in the parse
        //---------------------------------------------------------------------------------------
        private void ForceReportInfo(JSError errorId)
        {
            ForceReportInfo(m_currentToken.Clone(), errorId);
        }

        //---------------------------------------------------------------------------------------
        // ForceReportInfo
        //
        //  Generate a parser error (info), does not change the error state in the parse
        //---------------------------------------------------------------------------------------
        private static void ForceReportInfo(Context context, JSError errorId)
        {
            Debug.Assert(context != null);
            context.HandleError(errorId);
        }

        //---------------------------------------------------------------------------------------
        // ForceReportInfo
        //
        //  Generate a parser error (info), does not change the error state in the parse
        //---------------------------------------------------------------------------------------
        private void ForceReportInfo(JSError errorId, bool treatAsError)
        {
            m_currentToken.Clone().HandleError(errorId, treatAsError);
        }

        //---------------------------------------------------------------------------------------
        // EOFError
        //
        //  Create a context for EOF error. The created context points to the end of the source
        //  code. Assume the the scanner actually reached the end of file
        //---------------------------------------------------------------------------------------
        private void EOFError(JSError errorId)
        {
            Context eofCtx = m_sourceContext.Clone();
            eofCtx.StartLineNumber = m_scanner.CurrentLine;
            eofCtx.EndLineNumber = eofCtx.StartLineNumber;
            eofCtx.StartLinePosition = m_scanner.StartLinePosition;
            eofCtx.EndLinePosition = eofCtx.StartLinePosition;
            eofCtx.StartPosition = m_sourceContext.EndPosition;
            eofCtx.EndPosition++;
            eofCtx.HandleError(errorId);
        }

        //---------------------------------------------------------------------------------------
        // SkipTokensAndThrow
        //
        //  Skip tokens until one in the no skip set is found.
        //  A call to this function always ends in a throw statement that will be caught by the
        //  proper rule
        //---------------------------------------------------------------------------------------
        private void SkipTokensAndThrow()
        {
            SkipTokensAndThrow(null);
        }

        private void SkipTokensAndThrow(AstNode partialAST)
        {
            m_errorToken = null; // make sure we go to the next token
            bool checkForEndOfLine = m_noSkipTokenSet.HasToken(JSToken.EndOfLine);
            while (!m_noSkipTokenSet.HasToken(m_currentToken.Token))
            {
                if (checkForEndOfLine)
                {
                    if (m_scanner.GotEndOfLine)
                    {
                        m_errorToken = m_currentToken;
                        throw new RecoveryTokenException(JSToken.EndOfLine, partialAST);
                    }
                }
                GetNextToken();
                if (++m_tokensSkipped > c_MaxSkippedTokenNumber)
                {
                    ForceReportInfo(JSError.TooManyTokensSkipped);
                    throw new EndOfFileException();
                }
                if (JSToken.EndOfFile == m_currentToken.Token)
                    throw new EndOfFileException();
            }
            m_errorToken = m_currentToken;
            // got a token in the no skip set, throw
            throw new RecoveryTokenException(m_currentToken.Token, partialAST);
        }

        //---------------------------------------------------------------------------------------
        // IndexOfToken
        //
        //  check whether the recovery token is a good one for the caller
        //---------------------------------------------------------------------------------------
        private int IndexOfToken(JSToken[] tokens, RecoveryTokenException exc)
        {
            return IndexOfToken(tokens, exc._token);
        }

        private int IndexOfToken(JSToken[] tokens, JSToken token)
        {
            int i, c;
            for (i = 0, c = tokens.Length; i < c; i++)
                if (tokens[i] == token)
                    break;
            if (i >= c)
                i = -1;
            else
            {
                // assume that the caller will deal with the token so move the state back to normal
                m_errorToken = null;
            }
            return i;
        }

        private bool TokenInList(JSToken[] tokens, JSToken token)
        {
            return (-1 != IndexOfToken(tokens, token));
        }

        private bool TokenInList(JSToken[] tokens, RecoveryTokenException exc)
        {
            return (-1 != IndexOfToken(tokens, exc._token));
        }
    }

    // helper classesoi8
    //***************************************************************************************
    //
    //***************************************************************************************
#if !SILVERLIGHT
    [Serializable]
#endif
    public class ParserException : Exception
    {
        private static string s_errorMsg = GetLocalizedMsg();
        private static string GetLocalizedMsg()
        {
            string code = ((int)JSError.JSParserException).ToString(CultureInfo.InvariantCulture);
            return StringMgr.GetString(code);
        }

        public ParserException() : base(s_errorMsg) { }
        public ParserException(string message) : base(message) { }
        public ParserException(string message, Exception innerException) : base(message, innerException) { }

#if !SILVERLIGHT
        protected ParserException(SerializationInfo info, StreamingContext context) : base(info, context) { }
#endif
    }

#if !SILVERLIGHT
    [Serializable]
#endif
    public class UnexpectedTokenException : ParserException
    {
        public UnexpectedTokenException() : base() { }
        public UnexpectedTokenException(string message) : base(message) { }
        public UnexpectedTokenException(string message, Exception innerException) : base(message, innerException) { }
#if !SILVERLIGHT
        protected UnexpectedTokenException(SerializationInfo info, StreamingContext context) : base(info, context) { }
#endif
    }

#if !SILVERLIGHT
    [Serializable]
#endif
    public class EndOfFileException : ParserException
    {
        public EndOfFileException() : base() { }
        public EndOfFileException(string message) : base(message) { }
        public EndOfFileException(string message, Exception innerException) : base(message, innerException) { }
#if !SILVERLIGHT
        protected EndOfFileException(SerializationInfo info, StreamingContext context) : base(info, context) { }
#endif
    }

#if !SILVERLIGHT
    [Serializable]
#endif
    internal class RecoveryTokenException : ParserException
    {
        internal JSToken _token;
        internal AstNode _partiallyComputedNode;

        internal RecoveryTokenException(JSToken token, AstNode partialAST)
            : base()
        {
            _token = token;
            _partiallyComputedNode = partialAST;
        }
#if !SILVERLIGHT
        protected RecoveryTokenException(SerializationInfo info, StreamingContext context) : base(info, context) { }
#endif
    }

    //***************************************************************************************
    // NoSkipTokenSet
    //
    //  This class is a possible implementation of the no skip token set. It relies on the
    //  fact that the array passed in are static. Should you change it, this implementation
    //  should change as well.
    //  It keeps a linked list of token arrays that are passed in during parsing, on error
    //  condition the list is traversed looking for a matching token. If a match is found
    //  the token should not be skipped and an exception is thrown to let the proper
    //  rule deal with the token
    //***************************************************************************************
    internal class NoSkipTokenSet
    {
        private List<JSToken[]> m_tokenSetList;

        internal NoSkipTokenSet()
        {
            m_tokenSetList = new List<JSToken[]>();
        }

        internal void Add(JSToken[] tokens)
        {
            m_tokenSetList.Add(tokens);
        }

        internal void Remove(JSToken[] tokens)
        {
            bool wasRemoved = m_tokenSetList.Remove(tokens);
            Debug.Assert(wasRemoved, "Token set not in no-skip list");
        }

        internal bool HasToken(JSToken token)
        {
            foreach (JSToken[] tokenSet in m_tokenSetList)
            {
                for (int ndx = 0; ndx < tokenSet.Length; ++ndx)
                {
                    if (tokenSet[ndx] == token)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        // list of static no skip token set for specifc rules
        internal static readonly JSToken[] s_ArrayInitNoSkipTokenSet = new JSToken[]{JSToken.RightBracket,
                                                                                           JSToken.Comma};
        internal static readonly JSToken[] s_BlockConditionNoSkipTokenSet = new JSToken[]{JSToken.RightParenthesis,
                                                                                           JSToken.LeftCurly,
                                                                                           JSToken.EndOfLine};
        internal static readonly JSToken[] s_BlockNoSkipTokenSet = new JSToken[] { JSToken.RightCurly };
        internal static readonly JSToken[] s_BracketToken = new JSToken[] { JSToken.RightBracket };
        internal static readonly JSToken[] s_CaseNoSkipTokenSet = new JSToken[]{JSToken.Case,
                                                                                           JSToken.Default,
                                                                                           JSToken.Colon,
                                                                                           JSToken.EndOfLine};
        internal static readonly JSToken[] s_DoWhileBodyNoSkipTokenSet = new JSToken[] { JSToken.While };
        internal static readonly JSToken[] s_EndOfLineToken = new JSToken[] { JSToken.EndOfLine };
        internal static readonly JSToken[] s_EndOfStatementNoSkipTokenSet = new JSToken[]{JSToken.Semicolon,
                                                                                           JSToken.EndOfLine};
        internal static readonly JSToken[] s_ExpressionListNoSkipTokenSet = new JSToken[] { JSToken.Comma };
        internal static readonly JSToken[] s_FunctionDeclNoSkipTokenSet = new JSToken[]{JSToken.RightParenthesis,
                                                                                           JSToken.LeftCurly,
                                                                                           JSToken.Comma};
        internal static readonly JSToken[] s_IfBodyNoSkipTokenSet = new JSToken[] { JSToken.Else };
        internal static readonly JSToken[] s_MemberExprNoSkipTokenSet = new JSToken[]{JSToken.LeftBracket,
                                                                                           JSToken.LeftParenthesis,
                                                                                           JSToken.AccessField};
        internal static readonly JSToken[] s_NoTrySkipTokenSet = new JSToken[]{JSToken.Catch,
                                                                                           JSToken.Finally};
        internal static readonly JSToken[] s_ObjectInitNoSkipTokenSet = new JSToken[]{JSToken.RightCurly,
                                                                                           JSToken.Comma};
        internal static readonly JSToken[] s_ParenExpressionNoSkipToken = new JSToken[] { JSToken.RightParenthesis };
        internal static readonly JSToken[] s_ParenToken = new JSToken[] { JSToken.RightParenthesis };
        internal static readonly JSToken[] s_PostfixExpressionNoSkipTokenSet = new JSToken[]{JSToken.Increment,
                                                                                           JSToken.Decrement};
        internal static readonly JSToken[] s_StartStatementNoSkipTokenSet = new JSToken[]{JSToken.LeftCurly,
                                                                                           JSToken.Var,
                                                                                           JSToken.Const,
                                                                                           JSToken.If,
                                                                                           JSToken.For,
                                                                                           JSToken.Do,
                                                                                           JSToken.While,
                                                                                           JSToken.With,
                                                                                           JSToken.Switch,
                                                                                           JSToken.Try};
        internal static readonly JSToken[] s_SwitchNoSkipTokenSet = new JSToken[]{JSToken.Case,
                                                                                           JSToken.Default};
        internal static readonly JSToken[] s_TopLevelNoSkipTokenSet = new JSToken[]{JSToken.Function};
        internal static readonly JSToken[] s_VariableDeclNoSkipTokenSet = new JSToken[]{JSToken.Comma,
                                                                                           JSToken.Semicolon};
    }

    public enum FunctionType
    {
        Declaration,
        Expression,
        Getter,
        Setter
    }
}
