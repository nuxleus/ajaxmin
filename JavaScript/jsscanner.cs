// jsscanner.cs
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
using System.Globalization;
using System.Text;

namespace Microsoft.Ajax.Utilities
{
    public sealed class JSScanner
    {
        // scanner main data
        private string m_strSourceCode;

        private int m_startPos;

        private int m_endPos;

        private int m_currentPos;

        private int m_currentLine;

        private int m_startLinePos;

        // token information
        private Context m_currentToken;

        private String m_escapedString;

        private StringBuilder m_identifier;

        private int m_idLastPosOnBuilder;

        // flags
        private bool m_gotEndOfLine;

        private bool m_peekModeOn;

        // keyword table
        private JSKeyword[] m_keywords;

        private static readonly JSKeyword[] s_Keywords = JSKeyword.InitKeywords();

        // pre process information
        private bool m_preProcessorOn;

        private int m_ccIfLevel;

        private bool m_skipDebugBlocks;

        // for pre-processor
        private bool m_inConditionalComment;

        private bool m_inSingleLineComment;

        private bool m_inMultipleLineComment;

        public bool InComment
        {
            get
            {
                return m_inMultipleLineComment || m_inSingleLineComment;
            }
        }

        public Context ImportantComment 
        {
            get; set;
        }

        public bool EatUnnecessaryCCOn { get; set; }

        public JSScanner(Context sourceContext)
        {
            m_keywords = s_Keywords;
            EatUnnecessaryCCOn = true;
            SetSource(sourceContext);
        }

        public bool SkipDebugBlocks
        {
            get
            {
                return m_skipDebugBlocks;
            }

            set
            {
                m_skipDebugBlocks = value;
            }
        }

        public static bool IsKeyword(string name)
        {
            bool isKeyword = false;

            // get the index into the keywords array by taking the first letter of the string
            // and subtracting the character 'a' from it. Use a negative number if the string
            // is null or empty
            int index = string.IsNullOrEmpty(name) ? -1 : name[0] - 'a';

            // only proceed if the index is within the array length
            if (0 <= index && index < s_Keywords.Length)
            {
                // get the head of the list for this index (if any)
                JSKeyword keyword = s_Keywords[name[0] - 'a'];
                if (keyword != null)
                {
                    // and ask if the name is in the list
                    isKeyword = keyword.Exists(name);
                }
            }

            return isKeyword;
        }

        public void SetSource(Context sourceContext)
        {
            if (sourceContext == null)
            {
                throw new ArgumentException(StringMgr.GetString("InternalCompilerError"));
            }

            m_strSourceCode = sourceContext.SourceString;
            m_startPos = sourceContext.StartPosition;
            m_startLinePos = sourceContext.StartLinePosition;
            m_endPos = (0 < sourceContext.EndPosition && sourceContext.EndPosition < m_strSourceCode.Length) 
                ? sourceContext.EndPosition 
                : m_strSourceCode.Length;
            m_currentToken = sourceContext;
            m_escapedString = null;
            m_identifier = new StringBuilder(128);
            m_idLastPosOnBuilder = 0;
            m_currentPos = m_startPos;
            m_currentLine = (sourceContext.StartLineNumber > 0) ? sourceContext.StartLineNumber : 1;
            m_gotEndOfLine = false;
        }

        internal JSToken PeekToken()
        {
            int thisCurrentPos = m_currentPos;
            int thisCurrentLine = m_currentLine;
            int thisStartLinePos = m_startLinePos;
            bool thisGotEndOfLine = m_gotEndOfLine;
            int thisLastPosOnBuilder = m_idLastPosOnBuilder;
            m_peekModeOn = true;
            JSToken token;

            // temporary switch the token
            Context thisCurrentToken = m_currentToken;
            m_currentToken = m_currentToken.Clone();
            try
            {
                GetNextToken();
                token = m_currentToken.Token;
            }
            finally
            {
                m_currentToken = thisCurrentToken;
                m_currentPos = thisCurrentPos;
                m_currentLine = thisCurrentLine;
                m_startLinePos = thisStartLinePos;
                m_gotEndOfLine = thisGotEndOfLine;
                m_identifier.Length = 0;
                m_idLastPosOnBuilder = thisLastPosOnBuilder;
                m_peekModeOn = false;
                m_escapedString = null;
            }

            return token;
        }

        public void GetNextToken()
        {
            JSToken token = JSToken.None;
            m_gotEndOfLine = false;
            ImportantComment = null;
            try
            {
                int thisCurrentLine = m_currentLine;
            nextToken:
                SkipBlanks();
                m_currentToken.StartPosition = m_startPos = m_currentPos;
                m_currentToken.StartLineNumber = m_currentLine;
                m_currentToken.StartLinePosition = m_startLinePos;
                char c = GetChar(m_currentPos++);
                switch (c)
                {
                    case (char)0:
                        if (m_currentPos >= m_endPos)
                        {
                            m_currentPos--;
                            token = JSToken.EndOfFile;
                            if (m_ccIfLevel > 0)
                            {
                                m_currentToken.EndLineNumber = m_currentLine;
                                m_currentToken.EndLinePosition = m_startLinePos;
                                m_currentToken.EndPosition = m_currentPos;
                                HandleError(JSError.NoCCEnd);
                            }

                            break;
                        }

                        goto nextToken;

                    case '=':
                        token = JSToken.Assign;
                        if ('=' == GetChar(m_currentPos))
                        {
                            m_currentPos++;
                            token = JSToken.Equal;
                            if ('=' == GetChar(m_currentPos))
                            {
                                m_currentPos++;
                                token = JSToken.StrictEqual;
                            }
                        }

                        break;

                    case '>':
                        token = JSToken.GreaterThan;
                        if ('>' == GetChar(m_currentPos))
                        {
                            m_currentPos++;
                            token = JSToken.RightShift;
                            if ('>' == GetChar(m_currentPos))
                            {
                                m_currentPos++;
                                token = JSToken.UnsignedRightShift;
                            }
                        }

                        if ('=' == GetChar(m_currentPos))
                        {
                            m_currentPos++;
                            switch (token)
                            {
                                case JSToken.GreaterThan:
                                    token = JSToken.GreaterThanEqual;
                                    break;

                                case JSToken.RightShift:
                                    token = JSToken.RightShiftAssign;
                                    break;

                                case JSToken.UnsignedRightShift:
                                    token = JSToken.UnsignedRightShiftAssign;
                                    break;
                            }
                        }

                        break;

                    case '<':
                        token = JSToken.LessThan;
                        if ('<' == GetChar(m_currentPos))
                        {
                            m_currentPos++;
                            token = JSToken.LeftShift;
                        }

                        if ('=' == GetChar(m_currentPos))
                        {
                            m_currentPos++;
                            if (token == JSToken.LessThan)
                            {
                                token = JSToken.LessThanEqual;
                            }
                            else
                            {
                                token = JSToken.LeftShiftAssign;
                            }
                        }

                        break;

                    case '!':
                        token = JSToken.LogicalNot;
                        if ('=' == GetChar(m_currentPos))
                        {
                            m_currentPos++;
                            token = JSToken.NotEqual;
                            if ('=' == GetChar(m_currentPos))
                            {
                                m_currentPos++;
                                token = JSToken.StrictNotEqual;
                            }
                        }

                        break;

                    case ',':
                        token = JSToken.Comma;
                        break;

                    case '~':
                        token = JSToken.BitwiseNot;
                        break;

                    case '?':
                        token = JSToken.ConditionalIf;
                        break;

                    case ':':
                        token = JSToken.Colon;
                        if (':' == GetChar(m_currentPos))
                        {
                            m_currentPos++;
                            token = JSToken.DoubleColon;
                        }

                        break;

                    case '.':
                        token = JSToken.AccessField;
                        c = GetChar(m_currentPos);
                        if (JSScanner.IsDigit(c))
                        {
                            token = ScanNumber('.');
                        }
                        else if ('.' == c)
                        {
                            c = GetChar(m_currentPos + 1);
                            if ('.' == c)
                            {
                                m_currentPos += 2;
                                token = JSToken.ParameterArray;
                            }
                        }

                        break;

                    case '&':
                        token = JSToken.BitwiseAnd;
                        c = GetChar(m_currentPos);
                        if ('&' == c)
                        {
                            m_currentPos++;
                            token = JSToken.LogicalAnd;
                        }
                        else if ('=' == c)
                        {
                            m_currentPos++;
                            token = JSToken.BitwiseAndAssign;
                        }

                        break;

                    case '|':
                        token = JSToken.BitwiseOr;
                        c = GetChar(m_currentPos);
                        if ('|' == c)
                        {
                            m_currentPos++;
                            token = JSToken.LogicalOr;
                        }
                        else if ('=' == c)
                        {
                            m_currentPos++;
                            token = JSToken.BitwiseOrAssign;
                        }

                        break;

                    case '+':
                        token = JSToken.Plus;
                        c = GetChar(m_currentPos);
                        if ('+' == c)
                        {
                            m_currentPos++;
                            token = JSToken.Increment;
                        }
                        else if ('=' == c)
                        {
                            m_currentPos++;
                            token = JSToken.PlusAssign;
                        }

                        break;

                    case '-':
                        token = JSToken.Minus;
                        c = GetChar(m_currentPos);
                        if ('-' == c)
                        {
                            m_currentPos++;
                            token = JSToken.Decrement;
                        }
                        else if ('=' == c)
                        {
                            m_currentPos++;
                            token = JSToken.MinusAssign;
                        }

                        break;

                    case '*':
                        token = JSToken.Multiply;
                        if ('=' == GetChar(m_currentPos))
                        {
                            m_currentPos++;
                            token = JSToken.MultiplyAssign;
                        }

                        break;

                    case '\\':
                        m_currentPos--;
                        if (IsIdentifierStartChar(ref c))
                        {
                            m_currentPos++;
                            ScanIdentifier();
                            token = JSToken.Identifier;
                            break;
                        }

                        m_currentPos++; // move on
                        c = GetChar(m_currentPos);
                        if ('a' <= c && c <= 'z')
                        {
                            JSKeyword keyword = m_keywords[c - 'a'];
                            if (null != keyword)
                            {
                                m_currentToken.StartPosition++;
                                token = ScanKeyword(keyword);
                                m_currentToken.StartPosition--;
                                if (token != JSToken.Identifier)
                                {
                                    token = JSToken.Identifier;
                                    break;
                                }
                            }
                        }

                        m_currentPos = m_currentToken.StartPosition + 1;
                        HandleError(JSError.IllegalChar);
                        break;

                    case '/':
                        token = JSToken.Divide;
                        c = GetChar(m_currentPos);
                        switch (c)
                        {
                            case '/':
                                m_inSingleLineComment = true;
                                c = GetChar(++m_currentPos);
                                if (c == '@' && !m_peekModeOn)
                                {
                                    // we got //@
                                    // if the NEXT character is not an identifier character, then we need to skip
                                    // the @ character -- otherwise leave it there
                                    if (!IsValidIdentifierStart(GetChar(m_currentPos + 1)))
                                    {
                                        ++m_currentPos;
                                    }

                                    // if we aren't already in a conditional comment
                                    if (!m_inConditionalComment)
                                    {
                                        // we are now
                                        m_inConditionalComment = true;
                                        token = JSToken.ConditionalCommentStart;
                                        break;
                                    }

                                    // already in conditional comment, so just ignore the start of a new
                                    // conditional comment. it's superfluous.
                                    goto nextToken;
                                }
                                
                                if (c == '/' &&
                                    '#' == GetChar(++m_currentPos) &&
                                    'D' == GetChar(++m_currentPos) &&
                                    'E' == GetChar(++m_currentPos) &&
                                    'B' == GetChar(++m_currentPos) &&
                                    'U' == GetChar(++m_currentPos) &&
                                    'G' == GetChar(++m_currentPos))
                                {
                                    if (m_skipDebugBlocks)
                                    {
                                        // skip until we hit ///#ENDDEBUG, but only if we are stripping debug statements
                                        PPSkipToEndDebug();
                                    }
                                }

                                SkipSingleLineComment();

                                // if we're still in a multiple-line comment, then we must've been in
                                // a multi-line CONDITIONAL comment, in which case this normal one-line comment
                                // won't turn off conditional comments just because we hit the end of line.
                                if (!m_inMultipleLineComment && m_inConditionalComment)
                                {
                                    m_inConditionalComment = false;
                                    token = JSToken.ConditionalCommentEnd;
                                    break;
                                }

                                goto nextToken; // read another token this last one was a comment

                            case '*':
                                m_inMultipleLineComment = true;
                                bool importantComment = false;
                                if (GetChar(++m_currentPos) == '@' && !m_peekModeOn)
                                {
                                    // we have /*@
                                    // if the NEXT character is not an identifier character, then we need to skip
                                    // the @ character -- otherwise leave it there
                                    if (!IsValidIdentifierStart(GetChar(m_currentPos + 1)))
                                    {
                                        ++m_currentPos;
                                    }

                                    // if we aren't already in a conditional comment
                                    if (!m_inConditionalComment)
                                    {
                                        // we are in one now
                                        m_inConditionalComment = true;
                                        token = JSToken.ConditionalCommentStart;
                                        break;
                                    }

                                    // we were already in a conditional comment, so ignore the superfluous
                                    // conditional comment start
                                    goto nextToken;
                                }
                                
                                if (GetChar(m_currentPos) == '!')
                                {
                                    // found an "important" comment that we want to preserve
                                    importantComment = true;
                                }

                                SkipMultilineComment(importantComment);
                                goto nextToken; // read another token this last one was a comment

                            case '=':
                                m_currentPos++;
                                token = JSToken.DivideAssign;
                                break;
                        }

                        break;

                    case '^':
                        token = JSToken.BitwiseXor;
                        if ('=' == GetChar(m_currentPos))
                        {
                            m_currentPos++;
                            token = JSToken.BitwiseXorAssign;
                        }

                        break;

                    case '%':
                        token = JSToken.Modulo;
                        if ('=' == GetChar(m_currentPos))
                        {
                            m_currentPos++;
                            token = JSToken.ModuloAssign;
                        }

                        break;

                    case '(':
                        token = JSToken.LeftParenthesis;
                        break;

                    case ')':
                        token = JSToken.RightParenthesis;
                        break;

                    case '{':
                        token = JSToken.LeftCurly;
                        break;

                    case '}':
                        token = JSToken.RightCurly;
                        break;

                    case '[':
                        token = JSToken.LeftBracket;
                        break;

                    case ']':
                        token = JSToken.RightBracket;
                        break;

                    case ';':
                        token = JSToken.Semicolon;
                        break;

                    case '"':
                        goto case '\'';

                    case '\'':
                        token = JSToken.StringLiteral;
                        ScanString(c);
                        break;

                    // line terminator crap
                    case '\r':
                        // if we are in a single-line conditional comment, then we want
                        // to return the end of comment token WITHOUT moving past the end of line 
                        // characters
                        if (m_inConditionalComment && m_inSingleLineComment)
                        {
                            token = JSToken.ConditionalCommentEnd;
                            m_inConditionalComment = m_inSingleLineComment = false;
                            break;
                        }

                        // \r\n is a valid SINGLE line-terminator. So if the \r is
                        // followed by a \n, we only want to process a single line terminator.
                        if (GetChar(m_currentPos) == '\n')
                        {
                            m_currentPos++;
                        }

                        // drop down into normal line-ending processing
                        goto case '\n';

                    case '\n':
                    case (char)0x2028:
                    case (char)0x2029:
                        // if we are in a single-line conditional comment, then
                        // clean up the flags and return the end of the conditional comment
                        // WITHOUT skipping past the end of line
                        if (m_inConditionalComment && m_inSingleLineComment)
                        {
                            token = JSToken.ConditionalCommentEnd;
                            m_inConditionalComment = m_inSingleLineComment = false;
                            break;
                        }

                        m_currentLine++;
                        m_startLinePos = m_currentPos;

                        m_inSingleLineComment = false;
                        goto nextToken;

                    case '@':
                        if (m_peekModeOn)
                        {
                            m_currentToken.Token = JSToken.PreprocessDirective;
                            break;
                        }

                        int startPosition = m_currentPos;
                        m_currentToken.StartPosition = startPosition;
                        m_currentToken.StartLineNumber = m_currentLine;
                        m_currentToken.StartLinePosition = m_startLinePos;
                        ScanIdentifier();
                        switch (m_currentPos - startPosition)
                        {
                            case 0:
                                // look for '@*/'.
                                if (/*m_preProcessorOn &&*/ '*' == GetChar(m_currentPos) && '/' == GetChar(++m_currentPos))
                                {
                                    m_currentPos++;
                                    m_inMultipleLineComment = false;
                                    m_inConditionalComment = false;
                                    token = JSToken.ConditionalCommentEnd;
                                    break;
                                }

                                // otherwise we just have a @ sitting by itself!
                                // throw an error and loop back to the next token.
                                HandleError(JSError.IllegalChar);
                                goto nextToken;

                            case 2:
                                if ('i' == m_strSourceCode[startPosition] &&
                                    'f' == m_strSourceCode[startPosition + 1])
                                {
                                    token = JSToken.ConditionalCompilationIf;

                                    // increment the if-level
                                    ++m_ccIfLevel;

                                    // if we're not in a conditional comment and we haven't explicitly
                                    // turned on conditional compilation when we encounter
                                    // a @if statement, then we can implicitly turn it on.
                                    if (!m_inConditionalComment && !m_preProcessorOn)
                                    {
                                        m_preProcessorOn = true;
                                    }

                                    break;
                                }

                                // the string isn't a known preprocessor command, so 
                                // fall into the default processing to handle it as a variable name
                                goto default;

                            case 3:
                                if ('s' == m_strSourceCode[startPosition] &&
                                    'e' == m_strSourceCode[startPosition + 1] &&
                                    't' == m_strSourceCode[startPosition + 2])
                                {
                                    token = JSToken.ConditionalCompilationSet;

                                    // if we're not in a conditional comment and we haven't explicitly
                                    // turned on conditional compilation when we encounter
                                    // a @set statement, then we can implicitly turn it on.
                                    if (!m_inConditionalComment && !m_preProcessorOn)
                                    {
                                        m_preProcessorOn = true;
                                    }

                                    break;
                                }

                                if ('e' == m_strSourceCode[startPosition] &&
                                    'n' == m_strSourceCode[startPosition + 1] &&
                                    'd' == m_strSourceCode[startPosition + 2])
                                {
                                    token = JSToken.ConditionalCompilationEnd;
                                    if (m_ccIfLevel > 0)
                                    {
                                        // down one more @if level
                                        m_ccIfLevel--;
                                    }
                                    else
                                    {
                                        // not corresponding @if -- invalid @end statement
                                        HandleError(JSError.CCInvalidEnd);
                                    }

                                    break;
                                }

                                // the string isn't a known preprocessor command, so 
                                // fall into the default processing to handle it as a variable name
                                goto default;

                            case 4:
                                if ('e' == m_strSourceCode[startPosition] &&
                                    'l' == m_strSourceCode[startPosition + 1] &&
                                    's' == m_strSourceCode[startPosition + 2] &&
                                    'e' == m_strSourceCode[startPosition + 3])
                                {
                                    token = JSToken.ConditionalCompilationElse;

                                    // if we don't have a corresponding @if statement, then throw and error
                                    // (but keep processing)
                                    if (m_ccIfLevel <= 0)
                                    {
                                        HandleError(JSError.CCInvalidElse);
                                    }

                                    break;
                                }

                                if ('e' == m_strSourceCode[startPosition] &&
                                    'l' == m_strSourceCode[startPosition + 1] &&
                                    'i' == m_strSourceCode[startPosition + 2] &&
                                    'f' == m_strSourceCode[startPosition + 3])
                                {
                                    token = JSToken.ConditionalCompilationElseIf;

                                    // if we don't have a corresponding @if statement, then throw and error
                                    // (but keep processing)
                                    if (m_ccIfLevel <= 0)
                                    {
                                        HandleError(JSError.CCInvalidElseIf);
                                    }

                                    break;
                                }

                                // the string isn't a known preprocessor command, so 
                                // fall into the default processing to handle it as a variable name
                                goto default;

                            case 5:
                                if ('c' == m_strSourceCode[startPosition] &&
                                    'c' == m_strSourceCode[startPosition + 1] &&
                                    '_' == m_strSourceCode[startPosition + 2] &&
                                    'o' == m_strSourceCode[startPosition + 3] &&
                                    'n' == m_strSourceCode[startPosition + 4])
                                {
                                    // if we have already turned on conditional compilation....
                                    if (m_preProcessorOn && EatUnnecessaryCCOn)
                                    {
                                        // we'll just eat the token here because we don't even 
                                        // need to expose it to the parser at this time.
                                        goto nextToken;
                                    }

                                    // turn it on and return the @cc_on token
                                    m_preProcessorOn = true;
                                    token = JSToken.ConditionalCompilationOn;
                                    break;
                                }

                                // the string isn't a known preprocessor command, so 
                                // fall into the default processing to handle it as a variable name
                                goto default;

                            default:
                                // we have @[id], where [id] is a valid identifier.
                                // if we haven't explicitly turned on conditional compilation,
                                // we'll keep processing, but we need to fire an error to indicate
                                // that the code should turn it on first.
                                if (!m_preProcessorOn)
                                {
                                    HandleError(JSError.CCOff);
                                }

                                token = JSToken.PreprocessorConstant;
                                break;
                        }

                        break;

                    case '$':
                        goto case '_';

                    case '_':
                        ScanIdentifier();
                        token = JSToken.Identifier;
                        break;

                    default:
                        if ('a' <= c && c <= 'z')
                        {
                            JSKeyword keyword = m_keywords[c - 'a'];
                            if (null != keyword)
                            {
                                token = ScanKeyword(keyword);
                            }
                            else
                            {
                                token = JSToken.Identifier;
                                ScanIdentifier();
                            }
                        }
                        else if (IsDigit(c))
                        {
                            token = ScanNumber(c);
                        }
                        else if (IsValidIdentifierStart(c))
                        {
                            token = JSToken.Identifier;
                            ScanIdentifier();
                        }
                        else
                        {
                            m_currentToken.EndLineNumber = m_currentLine;
                            m_currentToken.EndLinePosition = m_startLinePos;
                            m_currentToken.EndPosition = m_currentPos;
                            HandleError(JSError.IllegalChar);
                            goto nextToken;
                        }

                        break;
                }
                m_currentToken.EndLineNumber = m_currentLine;
                m_currentToken.EndLinePosition = m_startLinePos;
                m_currentToken.EndPosition = m_currentPos;
                m_gotEndOfLine = (m_currentLine > thisCurrentLine || token == JSToken.EndOfFile) ? true : false;
                if (m_gotEndOfLine && token == JSToken.StringLiteral && m_currentToken.StartLineNumber == thisCurrentLine)
                {
                    m_gotEndOfLine = false;
                }
            }
            catch (IndexOutOfRangeException)
            {
                m_currentToken.Token = JSToken.None;
                m_currentToken.EndPosition = m_currentPos;
                m_currentToken.EndLineNumber = m_currentLine;
                m_currentToken.EndLinePosition = m_startLinePos;
                throw new ScannerException(JSError.ErrorEndOfFile);
            }

            m_currentToken.Token = token;
        }

        private char GetChar(int index)
        {
            if (index < m_endPos)
            {
                return m_strSourceCode[index];
            }

            return (char)0;
        }

        public int CurrentLine
        {
            get
            {
                return m_currentLine;
            }
        }

        public int StartLinePosition
        {
            get
            {
                return m_startLinePos;
            }
        }

        public string StringLiteral
        {
            get
            {
                return m_escapedString;
            }
        }

        public bool GotEndOfLine
        {
            get
            {
                return m_gotEndOfLine;
            }
        }

        internal string GetIdentifier()
        {
            string id = null;
            if (m_identifier.Length > 0)
            {
                id = m_identifier.ToString();
                m_identifier.Length = 0;
            }
            else
            {
                id = m_currentToken.Code;
            }

            return id;
        }

        private void ScanIdentifier()
        {
            for (;;)
            {
                char c = GetChar(m_currentPos);
                if (!IsIdentifierPartChar(c))
                {
                    break;
                }

                ++m_currentPos;
            }

            if (m_idLastPosOnBuilder > 0)
            {
                m_identifier.Append(m_strSourceCode.Substring(m_idLastPosOnBuilder, m_currentPos - m_idLastPosOnBuilder));
                m_idLastPosOnBuilder = 0;
            }
        }

        private JSToken ScanKeyword(JSKeyword keyword)
        {
            for (;;)
            {
                char c = GetChar(m_currentPos);
                if ('a' <= c && c <= 'z')
                {
                    m_currentPos++;
                    continue;
                }

                if (IsIdentifierPartChar(c))
                {
                    ScanIdentifier();
                    return JSToken.Identifier;
                }

                break;
            }

            return keyword.GetKeyword(m_currentToken, m_currentPos - m_currentToken.StartPosition);
        }

        private JSToken ScanNumber(char leadChar)
        {
            bool noMoreDot = '.' == leadChar;
            JSToken token = noMoreDot ? JSToken.NumericLiteral : JSToken.IntegerLiteral;
            bool exponent = false;
            char c;

            if ('0' == leadChar)
            {
                c = GetChar(m_currentPos);
                if ('x' == c || 'X' == c)
                {
                    if (!JSScanner.IsHexDigit(GetChar(m_currentPos + 1)))
                    {
                        // bump it up two characters to pick up the 'x' and the bad digit
                        m_currentPos += 2;
                        HandleError(JSError.BadHexDigit);
                        // bump it down three characters to go back to the 0
                        m_currentPos -= 3;
                    }

                    while (JSScanner.IsHexDigit(GetChar(++m_currentPos)))
                    {
                        // empty
                    }

                    return token;
                }
            }

            for (;;)
            {
                c = GetChar(m_currentPos);
                if (!JSScanner.IsDigit(c))
                {
                    if ('.' == c)
                    {
                        if (noMoreDot)
                        {
                            break;
                        }

                        noMoreDot = true;
                        token = JSToken.NumericLiteral;
                    }
                    else if ('e' == c || 'E' == c)
                    {
                        if (exponent)
                        {
                            break;
                        }

                        exponent = true;
                        token = JSToken.NumericLiteral;
                    }
                    else if ('+' == c || '-' == c)
                    {
                        char e = GetChar(m_currentPos - 1);
                        if ('e' != e && 'E' != e)
                        {
                            break;
                        }
                    }
                    else
                    {
                        break;
                    }
                }

                m_currentPos++;
            }

            c = GetChar(m_currentPos - 1);
            if ('+' == c || '-' == c)
            {
                m_currentPos--;
                c = GetChar(m_currentPos - 1);
            }

            if ('e' == c || 'E' == c)
            {
                m_currentPos--;
            }

            return token;
        }

        internal String ScanRegExp()
        {
            int pos = m_currentPos;
            bool isEscape = false;
            bool isInSet = false;
            char c;
            while (!IsEndLineOrEOF(c = GetChar(m_currentPos++), 0))
            {
                if (isEscape)
                {
                    isEscape = false;
                }
                else if (c == '[')
                {
                    isInSet = true;
                }
                else if (isInSet)
                {
                    if (c == ']')
                    {
                        isInSet = false;
                    }
                }
                else if (c == '/')
                {
                    if (pos == m_currentPos)
                    {
                        return null;
                    }

                    m_currentToken.EndPosition = m_currentPos;
                    m_currentToken.EndLinePosition = m_startLinePos;
                    m_currentToken.EndLineNumber = m_currentLine;
                    return m_strSourceCode.Substring(
                        m_currentToken.StartPosition + 1,
                        m_currentToken.EndPosition - m_currentToken.StartPosition - 2);
                }
                else if (c == '\\')
                {
                    isEscape = true;
                }
            }

            // reset and return null. Assume it is not a reg exp
            m_currentPos = pos;
            return null;
        }

        internal String ScanRegExpFlags()
        {
            int pos = m_currentPos;
            while (JSScanner.IsAsciiLetter(GetChar(m_currentPos)))
            {
                m_currentPos++;
            }

            if (pos != m_currentPos)
            {
                m_currentToken.EndPosition = m_currentPos;
                m_currentToken.EndLineNumber = m_currentLine;
                m_currentToken.EndLinePosition = m_startLinePos;
                return m_strSourceCode.Substring(pos, m_currentToken.EndPosition - pos);
            }

            return null;
        }

        //--------------------------------------------------------------------------------------------------
        // ScanString
        //
        //  Scan a string dealing with escape sequences.
        //  On exit this.escapedString will contain the string with all escape sequences replaced
        //  On exit this.currentPos must be at the next char to scan after the string
        //  This method wiil report an error when the string is unterminated or for a bad escape sequence
        //--------------------------------------------------------------------------------------------------
        private void ScanString(char cStringTerminator)
        {
            char ch;
            int start = m_currentPos;
            m_escapedString = null;
            StringBuilder result = null;
            do
            {
                ch = GetChar(m_currentPos++);

                if (ch != '\\')
                {
                    // this is the common non escape case
                    if (IsLineTerminator(ch, 0))
                    {
                        HandleError(JSError.UnterminatedString);
                        --m_currentPos;
                        if (GetChar(m_currentPos - 1) == '\r')
                        {
                            --m_currentPos;
                        }

                        break;
                    }
                    
                    if ((char)0 == ch)
                    {
                        m_currentPos--;
                        HandleError(JSError.UnterminatedString);
                        break;
                    }
                }
                else
                {
                    // ESCAPE CASE

                    // got an escape of some sort. Have to use the StringBuilder
                    if (null == result)
                    {
                        result = new StringBuilder(128);
                    }

                    // start points to the first position that has not been written to the StringBuilder.
                    // The first time we get in here that position is the beginning of the string, after that
                    // is the character immediately following the escape sequence
                    if (m_currentPos - start - 1 > 0)
                    {
                        // append all the non escape chars to the string builder
                        result.Append(m_strSourceCode, start, m_currentPos - start - 1);
                    }

                    // state variable to be reset
                    bool seqOfThree = false;
                    int esc = 0;

                    ch = GetChar(m_currentPos++);
                    switch (ch)
                    {
                        // line terminator crap
                        case '\r':
                            if ('\n' == GetChar(m_currentPos))
                            {
                                m_currentPos++;
                            }

                            goto case '\n';

                        case '\n':
                        case (char)0x2028:
                        case (char)0x2029:
                            m_currentLine++;
                            m_startLinePos = m_currentPos;
                            break;

                        // classic single char escape sequences
                        case 'b':
                            result.Append((char)8);
                            break;

                        case 't':
                            result.Append((char)9);
                            break;

                        case 'n':
                            result.Append((char)10);
                            break;

                        case 'v':
                            result.Append((char)11);
                            break;

                        case 'f':
                            result.Append((char)12);
                            break;

                        case 'r':
                            result.Append((char)13);
                            break;

                        case '"':
                            result.Append('"');
                            ch = (char)0; // so it does not exit the loop
                            break;

                        case '\'':
                            result.Append('\'');
                            ch = (char)0; // so it does not exit the loop
                            break;

                        case '\\':
                            result.Append('\\');
                            break;

                        // hexadecimal escape sequence /xHH
                        case 'x':
                            ch = GetChar(m_currentPos++);
                            if (unchecked((uint)(ch - '0')) <= '9' - '0')
                            {
                                esc = (ch - '0') << 4;
                            }
                            else if (unchecked((uint)(ch - 'A')) <= 'F' - 'A')
                            {
                                esc = (ch + 10 - 'A') << 4;
                            }
                            else if (unchecked((uint)(ch - 'a')) <= 'f' - 'a')
                            {
                                esc = (ch + 10 - 'a') << 4;
                            }
                            else
                            {
                                HandleError(JSError.BadHexDigit);
                                if (ch != cStringTerminator)
                                {
                                    --m_currentPos; // do not skip over this char we have to read it back
                                }

                                break;
                            }

                            ch = GetChar(m_currentPos++);
                            if (unchecked((uint)(ch - '0')) <= '9' - '0')
                            {
                                esc |= (ch - '0');
                            }
                            else if (unchecked((uint)(ch - 'A')) <= 'F' - 'A')
                            {
                                esc |= (ch + 10 - 'A');
                            }
                            else if (unchecked((uint)(ch - 'a')) <= 'f' - 'a')
                            {
                                esc |= (ch + 10 - 'a');
                            }
                            else
                            {
                                HandleError(JSError.BadHexDigit);
                                if (ch != cStringTerminator)
                                {
                                    --m_currentPos; // do not skip over this char we have to read it back
                                }
                                break;
                            }

                            result.Append((char)esc);
                            break;

                        // unicode escape sequence /uHHHH
                        case 'u':
                            ch = GetChar(m_currentPos++);
                            if (unchecked((uint)(ch - '0')) <= '9' - '0')
                            {
                                esc = (ch - '0') << 12;
                            }
                            else if (unchecked((uint)(ch - 'A')) <= 'F' - 'A')
                            {
                                esc = (ch + 10 - 'A') << 12;
                            }
                            else if (unchecked((uint)(ch - 'a')) <= 'f' - 'a')
                            {
                                esc = (ch + 10 - 'a') << 12;
                            }
                            else
                            {
                                HandleError(JSError.BadHexDigit);
                                if (ch != cStringTerminator)
                                {
                                    --m_currentPos; // do not skip over this char we have to read it back
                                }

                                break;
                            }

                            ch = GetChar(m_currentPos++);
                            if (unchecked((uint)(ch - '0')) <= '9' - '0')
                            {
                                esc |= (ch - '0') << 8;
                            }
                            else if (unchecked((uint)(ch - 'A')) <= 'F' - 'A')
                            {
                                esc |= (ch + 10 - 'A') << 8;
                            }
                            else if (unchecked((uint)(ch - 'a')) <= 'f' - 'a')
                            {
                                esc |= (ch + 10 - 'a') << 8;
                            }
                            else
                            {
                                HandleError(JSError.BadHexDigit);
                                if (ch != cStringTerminator)
                                {
                                    --m_currentPos; // do not skip over this char we have to read it back
                                }

                                break;
                            }

                            ch = GetChar(m_currentPos++);
                            if (unchecked((uint)(ch - '0')) <= '9' - '0')
                            {
                                esc |= (ch - '0') << 4;
                            }
                            else if (unchecked((uint)(ch - 'A')) <= 'F' - 'A')
                            {
                                esc |= (ch + 10 - 'A') << 4;
                            }
                            else if (unchecked((uint)(ch - 'a')) <= 'f' - 'a')
                            {
                                esc |= (ch + 10 - 'a') << 4;
                            }
                            else
                            {
                                HandleError(JSError.BadHexDigit);
                                if (ch != cStringTerminator)
                                {
                                    --m_currentPos; // do not skip over this char we have to read it back
                                }

                                break;
                            }

                            ch = GetChar(m_currentPos++);
                            if (unchecked((uint)(ch - '0')) <= '9' - '0')
                            {
                                esc |= (ch - '0');
                            }
                            else if (unchecked((uint)(ch - 'A')) <= 'F' - 'A')
                            {
                                esc |= (ch + 10 - 'A');
                            }
                            else if (unchecked((uint)(ch - 'a')) <= 'f' - 'a')
                            {
                                esc |= (ch + 10 - 'a');
                            }
                            else
                            {
                                HandleError(JSError.BadHexDigit);
                                if (ch != cStringTerminator)
                                {
                                    --m_currentPos; // do not skip over this char we have to read it back
                                }

                                break;
                            }

                            result.Append((char)esc);
                            break;

                        case '0':
                        case '1':
                        case '2':
                        case '3':
                            seqOfThree = true;
                            esc = (ch - '0') << 6;
                            goto case '4';

                        case '4':
                        case '5':
                        case '6':
                        case '7':
                            // esc is reset at the beginning of the loop and it is used to check that we did not go through the cases 1, 2 or 3
                            if (!seqOfThree)
                            {
                                esc = (ch - '0') << 3;
                            }

                            ch = GetChar(m_currentPos++);
                            if (unchecked((UInt32)(ch - '0')) <= '7' - '0')
                            {
                                if (seqOfThree)
                                {
                                    esc |= (ch - '0') << 3;
                                    ch = GetChar(m_currentPos++);
                                    if (unchecked((UInt32)(ch - '0')) <= '7' - '0')
                                    {
                                        esc |= ch - '0';
                                        result.Append((char)esc);
                                    }
                                    else
                                    {
                                        result.Append((char)(esc >> 3));
                                        if (ch != cStringTerminator)
                                        {
                                            --m_currentPos; // do not skip over this char we have to read it back
                                        }
                                    }
                                }
                                else
                                {
                                    esc |= ch - '0';
                                    result.Append((char)esc);
                                }
                            }
                            else
                            {
                                if (seqOfThree)
                                {
                                    result.Append((char)(esc >> 6));
                                }
                                else
                                {
                                    result.Append((char)(esc >> 3));
                                }

                                if (ch != cStringTerminator)
                                {
                                    --m_currentPos; // do not skip over this char we have to read it back
                                }
                            }

                            break;

                        default:
                            // not an octal number, ignore the escape '/' and simply append the current char
                            result.Append(ch);
                            break;
                    }

                    start = m_currentPos;
                }
            } while (ch != cStringTerminator);

            // update this.escapedString
            if (null != result)
            {
                if (m_currentPos - start - 1 > 0)
                {
                    // append all the non escape chars to the string builder
                    result.Append(m_strSourceCode, start, m_currentPos - start - 1);
                }
                m_escapedString = result.ToString();
            }
            else
            {
                if (m_currentPos <= m_currentToken.StartPosition + 2)
                {
                    m_escapedString = "";
                }
                else
                {
                    int numDelimiters = (GetChar(m_currentPos - 1) == cStringTerminator ? 2 : 1);
                    m_escapedString = m_currentToken.SourceString.Substring(m_currentToken.StartPosition + 1, m_currentPos - m_currentToken.StartPosition - numDelimiters);
                }
            }
        }

        private void SkipSingleLineComment()
        {
            while (!IsEndLineOrEOF(GetChar(m_currentPos++), 0)) ;
            m_currentLine++;
            m_startLinePos = m_currentPos;
            m_inSingleLineComment = false;
        }

        // this method is public because it's used from the authoring code
        public int SkipMultilineComment(bool importantComment)
        {
            for (; ; )
            {
                char c = GetChar(m_currentPos);
                while ('*' == c)
                {
                    c = GetChar(++m_currentPos);
                    if ('/' == c)
                    {
                        m_currentPos++;
                        m_inMultipleLineComment = false;
                        if (importantComment)
                        {
                            SaveImportantComment();
                        }
                        return m_currentPos;
                    }

                    if ((char)0 == c)
                    {
                        break;
                    }
                    
                    if (IsLineTerminator(c, 1))
                    {
                        c = GetChar(++m_currentPos);
                        m_currentLine++;
                        m_startLinePos = m_currentPos + 1;
                    }
                }

                if ((char)0 == c && m_currentPos >= m_endPos)
                {
                    break;
                }

                if (IsLineTerminator(c, 1))
                {
                    m_currentLine++;
                    m_startLinePos = m_currentPos + 1;
                }

                ++m_currentPos;
            }

            // if we are here we got EOF
            if (importantComment)
            {
                SaveImportantComment();
            }

            m_currentToken.EndPosition = m_currentPos;
            m_currentToken.EndLinePosition = m_startLinePos;
            m_currentToken.EndLineNumber = m_currentLine;
            throw new ScannerException(JSError.NoCommentEnd);
        }

        private void SaveImportantComment()
        {
            // save the context of the important comment
            ImportantComment = m_currentToken.Clone();
            ImportantComment.EndPosition = m_currentPos;
            ImportantComment.EndLineNumber = m_currentLine;
            ImportantComment.EndLinePosition = m_startLinePos;
        }

        private void SkipBlanks()
        {
            char c = GetChar(m_currentPos);
            while (JSScanner.IsBlankSpace(c))
            {
                c = GetChar(++m_currentPos);
            }
        }

        private static bool IsBlankSpace(char c)
        {
            switch (c)
            {
                case (char)0x09:
                case (char)0x0B:
                case (char)0x0C:
                case (char)0x20:
                case (char)0xA0:
                    return true;
                default:
                    return (c < 128) ? false : char.GetUnicodeCategory(c) == UnicodeCategory.SpaceSeparator;
            }
        }

        private bool IsLineTerminator(char c, int increment)
        {
            switch (c)
            {
                case (char)0x0D:
                    // treat 0x0D0x0A as a single character
                    if (0x0A == GetChar(m_currentPos + increment))
                    {
                        m_currentPos++;
                    }

                    return true;

                case (char)0x0A:
                    return true;

                case (char)0x2028:
                    return true;

                case (char)0x2029:
                    return true;

                default:
                    return false;
            }
        }

        private bool IsEndLineOrEOF(char c, int increment)
        {
            return IsLineTerminator(c, increment) || (char)0 == c && m_currentPos >= m_endPos;
        }

        private static int GetHexValue(char hex)
        {
            int hexValue;
            if ('0' <= hex && hex <= '9')
            {
                hexValue = hex - '0';
            }
            else if ('a' <= hex && hex <= 'f')
            {
                hexValue = hex - 'a' + 10;
            }
            else
            {
                hexValue = hex - 'A' + 10;
            }

            return hexValue;
        }

        // string might contain escaped characters
        public static bool StartsWithIdentifierPart(string text)
        {
            bool startsWithIdentifierPart = false;
            if (!string.IsNullOrEmpty(text))
            {
                char ch = text[0];
                if (ch == '\\')
                {
                    if (text.Length >= 6 && text[1] == 'u')
                    {
                        // unescape the escaped character
                        char h1 = text[2];
                        char h2 = text[3];
                        char h3 = text[4];
                        char h4 = text[5];
                        if (IsHexDigit(h1) && IsHexDigit(h2) && IsHexDigit(h3) && IsHexDigit(h4))
                        {
                            ch = (char)(GetHexValue(h1) << 12
                                | GetHexValue(h2) << 8
                                | GetHexValue(h3) << 4
                                | GetHexValue(h4));
                        }
                    }
                }

                // is it a valid identifier part?
                startsWithIdentifierPart = IsValidIdentifierPart(ch);
            }

            return startsWithIdentifierPart;
        }

        // string might contain escaped characters
        public static bool EndsWithIdentifierPart(string text)
        {
            bool endsWithIdentifierPart = false;
            if (!string.IsNullOrEmpty(text))
            {
                // get last character. If it's not an identifier part,
                // then we know it's not an identifier part and we can
                // stop looking. 
                // But if it is an identifier, AND it's a hex digit,
                // we need to step back and make sure it's not part of a unicode
                // escape sequence. If it is, we need to decode the escape seqence
                // to see if THAT'S an identifier part.
                int lastIndex = text.Length - 1;
                char ch = text[lastIndex];
                endsWithIdentifierPart = IsValidIdentifierPart(ch);
                if (endsWithIdentifierPart && IsHexDigit(ch) 
                    && text.Length >= 6
                    && IsHexDigit(text[lastIndex - 1])
                    && IsHexDigit(text[lastIndex - 2])
                    && IsHexDigit(text[lastIndex - 3])
                    && text[lastIndex-4] == 'u'
                    && text[lastIndex - 5] == '\\')
                {
                    endsWithIdentifierPart = IsValidIdentifierPart((char)
                        (GetHexValue(text[lastIndex - 3]) << 12
                        | GetHexValue(text[lastIndex - 2]) << 8
                        | GetHexValue(text[lastIndex - 1]) << 4
                        | GetHexValue(ch)));
                }
            }

            return endsWithIdentifierPart;
        }

        // assumes all unicode characters in the string -- NO escape sequences
        public static bool IsValidIdentifier(string name)
        {
            bool isValid = false;
            if (!string.IsNullOrEmpty(name))
            {
                if (IsValidIdentifierStart(name[0]))
                {
                    // loop through all the rest
                    for (int ndx = 1; ndx < name.Length; ++ndx)
                    {
                        char ch = name[ndx];
                        if (!IsValidIdentifierPart(ch))
                        {
                            // fail!
                            return false;
                        }
                    }

                    // if we get here, everything is okay
                    isValid = true;
                }
            }

            return isValid;
        }

        // assumes all unicode characters in the string -- NO escape sequences
        public static bool IsSafeIdentifier(string name)
        {
            bool isValid = false;
            if (!string.IsNullOrEmpty(name))
            {
                if (IsSafeIdentifierStart(name[0]))
                {
                    // loop through all the rest
                    for (int ndx = 1; ndx < name.Length; ++ndx)
                    {
                        char ch = name[ndx];
                        if (!IsSafeIdentifierPart(ch))
                        {
                            // fail!
                            return false;
                        }
                    }

                    // if we get here, everything is okay
                    isValid = true;
                }
            }

            return isValid;
        }

        // unescaped unicode characters
        public static bool IsValidIdentifierStart(char letter)
        {
            if (('a' <= letter && letter <= 'z') || ('A' <= letter && letter <= 'Z') || letter == '_' || letter == '$')
            {
                // good
                return true;
            }

            if (letter >= 128)
            {
                // check the unicode category
                UnicodeCategory cat = char.GetUnicodeCategory(letter);
                switch (cat)
                {
                    case UnicodeCategory.UppercaseLetter:
                    case UnicodeCategory.LowercaseLetter:
                    case UnicodeCategory.TitlecaseLetter:
                    case UnicodeCategory.ModifierLetter:
                    case UnicodeCategory.OtherLetter:
                    case UnicodeCategory.LetterNumber:
                        // okay
                        return true;
                }
            }

            return false;
        }

        // unescaped unicode characters.
        // the same as the "IsValid" method, except various browsers have problems with some
        // of the Unicode characters in the ModifierLetter, OtherLetter, and LetterNumber categories.
        public static bool IsSafeIdentifierStart(char letter)
        {
            if (('a' <= letter && letter <= 'z') || ('A' <= letter && letter <= 'Z') || letter == '_' || letter == '$')
            {
                // good
                return true;
            }

            return false;
        }

        // unescaped unicode characters
        public static bool IsValidIdentifierPart(char letter)
        {
            // look for valid ranges
            if (('a' <= letter && letter <= 'z')
                || ('A' <= letter && letter <= 'Z')
                || ('0' <= letter && letter <= '9')
                || letter == '_'
                || letter == '$')
            {
                return true;
            }

            if (letter >= 128)
            {
                UnicodeCategory unicodeCategory = Char.GetUnicodeCategory(letter);
                switch (unicodeCategory)
                {
                    case UnicodeCategory.UppercaseLetter:
                    case UnicodeCategory.LowercaseLetter:
                    case UnicodeCategory.TitlecaseLetter:
                    case UnicodeCategory.ModifierLetter:
                    case UnicodeCategory.OtherLetter:
                    case UnicodeCategory.LetterNumber:
                    case UnicodeCategory.NonSpacingMark:
                    case UnicodeCategory.SpacingCombiningMark:
                    case UnicodeCategory.DecimalDigitNumber:
                    case UnicodeCategory.ConnectorPunctuation:
                        return true;
                }
            }

            return false;
        }

        // unescaped unicode characters.
        // the same as the "IsValid" method, except various browsers have problems with some
        // of the Unicode characters in the ModifierLetter, OtherLetter, LetterNumber,
        // NonSpacingMark, SpacingCombiningMark, DecimalDigitNumber, and ConnectorPunctuation categories.
        public static bool IsSafeIdentifierPart(char letter)
        {
            // look for valid ranges
            if (('a' <= letter && letter <= 'z')
                || ('A' <= letter && letter <= 'Z')
                || ('0' <= letter && letter <= '9')
                || letter == '_'
                || letter == '$')
            {
                return true;
            }

            return false;
        }

        // pulling unescaped characters off the input stream
        internal bool IsIdentifierPartChar(char c)
        {
            return IsIdentifierStartChar(ref c) || IsValidIdentifierPart(c);
        }

        // pulling unescaped characters off the input stream
        internal bool IsIdentifierStartChar(ref char c)
        {
            bool isEscapeChar = false;
            if ('\\' == c)
            {
                if ('u' == GetChar(m_currentPos + 1))
                {
                    char h1 = GetChar(m_currentPos + 2);
                    if (IsHexDigit(h1))
                    {
                        char h2 = GetChar(m_currentPos + 3);
                        if (IsHexDigit(h2))
                        {
                            char h3 = GetChar(m_currentPos + 4);
                            if (IsHexDigit(h3))
                            {
                                char h4 = GetChar(m_currentPos + 5);
                                if (IsHexDigit(h4))
                                {
                                    isEscapeChar = true;
                                    c = (char)(GetHexValue(h1) << 12 | GetHexValue(h2) << 8 | GetHexValue(h3) << 4 | GetHexValue(h4));
                                }
                            }
                        }
                    }
                }
            }

            if (!IsValidIdentifierStart(c))
            {
                return false;
            }

            // if we get here, we're a good character!
            if (isEscapeChar)
            {
                int startPosition = (m_idLastPosOnBuilder > 0) ? m_idLastPosOnBuilder : m_currentToken.StartPosition;
                if (m_currentPos - startPosition > 0)
                {
                    m_identifier.Append(m_strSourceCode.Substring(startPosition, m_currentPos - startPosition));
                }

                m_identifier.Append(c);
                m_currentPos += 5;
                m_idLastPosOnBuilder = m_currentPos + 1;
            }

            return true;
        }

        internal static bool IsDigit(char c)
        {
            return '0' <= c && c <= '9';
        }

        internal static bool IsHexDigit(char c)
        {
            return ('0' <= c && c <= '9') || ('A' <= c && c <= 'F') || ('a' <= c && c <= 'f');
        }

        internal static bool IsAsciiLetter(char c)
        {
            return ('A' <= c && c <= 'Z') || ('a' <= c && c <= 'z');
        }

        private void PPSkipToEndDebug()
        {
            while (true)
            {
                char c = GetChar(m_currentPos++);
                switch (c)
                {
                    // EOF
                    case (char)0:
                        if (m_currentPos >= m_endPos)
                        {
                            m_currentPos--;
                            m_currentToken.EndPosition = m_currentPos;
                            m_currentToken.EndLineNumber = m_currentLine;
                            m_currentToken.EndLinePosition = m_startLinePos;
                            HandleError(JSError.NoCCEnd);
                            throw new ScannerException(JSError.ErrorEndOfFile);
                        }

                        break;

                    // line terminator crap
                    case '\r':
                        if (GetChar(m_currentPos) == '\n')
                        {
                            m_currentPos++;
                        }

                        m_currentLine++;
                        m_startLinePos = m_currentPos;
                        break;
                    case '\n':
                        m_currentLine++;
                        m_startLinePos = m_currentPos;
                        break;
                    case (char)0x2028:
                        m_currentLine++;
                        m_startLinePos = m_currentPos;
                        break;
                    case (char)0x2029:
                        m_currentLine++;
                        m_startLinePos = m_currentPos;
                        break;

                    // check for ///#ENDDEBUG
                    case '/':
                        if (GetChar(m_currentPos) == '/'
                          && GetChar(++m_currentPos) == '/'
                          && GetChar(++m_currentPos) == '#'
                          && GetChar(++m_currentPos) == 'E'
                          && GetChar(++m_currentPos) == 'N'
                          && GetChar(++m_currentPos) == 'D'
                          && GetChar(++m_currentPos) == 'D'
                          && GetChar(++m_currentPos) == 'E'
                          && GetChar(++m_currentPos) == 'B'
                          && GetChar(++m_currentPos) == 'U'
                          && GetChar(++m_currentPos) == 'G')
                        {
                            // found it -- bail
                            return;
                        }

                        break;
                }
            }
        }

        private void HandleError(JSError error)
        {
            m_currentToken.EndPosition = m_currentPos;
            m_currentToken.EndLinePosition = m_startLinePos;
            m_currentToken.EndLineNumber = m_currentLine;
            m_currentToken.HandleError(error);
        }

        internal static bool IsAssignmentOperator(JSToken token)
        {
            return JSToken.Assign <= token && token <= JSToken.LastAssign;
        }

        internal static bool IsRightAssociativeOperator(JSToken token)
        {
            return JSToken.Assign <= token && token <= JSToken.ConditionalIf;
        }

        // This function return whether an operator is processable in ParseExpression.
        // Comma is out of this list and so are the unary ops
        internal static bool IsProcessableOperator(JSToken token)
        {
            return JSToken.FirstBinaryOperator <= token && token <= JSToken.ConditionalIf;
        }

        private static readonly OpPrec[] s_OperatorsPrec = InitOperatorsPrec();

        internal static OpPrec GetOperatorPrecedence(JSToken token)
        {
            return token == JSToken.None ? OpPrec.precNone : JSScanner.s_OperatorsPrec[token - JSToken.FirstBinaryOperator];
        }

        private static readonly string[] s_OperatorsString = InitOperatorsString();

        internal static string GetOperatorString(JSToken token)
        {
            return JSScanner.s_OperatorsString[token - JSToken.FirstOperator];
        }

        private static OpPrec[] InitOperatorsPrec()
        {
            OpPrec[] operatorsPrec = new OpPrec[JSToken.LastOperator - JSToken.FirstBinaryOperator + 1];

            operatorsPrec[JSToken.Plus - JSToken.FirstBinaryOperator] = OpPrec.precAdditive;
            operatorsPrec[JSToken.Minus - JSToken.FirstBinaryOperator] = OpPrec.precAdditive;

            operatorsPrec[JSToken.LogicalOr - JSToken.FirstBinaryOperator] = OpPrec.precLogicalOr;
            operatorsPrec[JSToken.LogicalAnd - JSToken.FirstBinaryOperator] = OpPrec.precLogicalAnd;
            operatorsPrec[JSToken.BitwiseOr - JSToken.FirstBinaryOperator] = OpPrec.precBitwiseOr;
            operatorsPrec[JSToken.BitwiseXor - JSToken.FirstBinaryOperator] = OpPrec.precBitwiseXor;
            operatorsPrec[JSToken.BitwiseAnd - JSToken.FirstBinaryOperator] = OpPrec.precBitwiseAnd;

            operatorsPrec[JSToken.Equal - JSToken.FirstBinaryOperator] = OpPrec.precEquality;
            operatorsPrec[JSToken.NotEqual - JSToken.FirstBinaryOperator] = OpPrec.precEquality;
            operatorsPrec[JSToken.StrictEqual - JSToken.FirstBinaryOperator] = OpPrec.precEquality;
            operatorsPrec[JSToken.StrictNotEqual - JSToken.FirstBinaryOperator] = OpPrec.precEquality;

            operatorsPrec[JSToken.InstanceOf - JSToken.FirstBinaryOperator] = OpPrec.precRelational;
            operatorsPrec[JSToken.In - JSToken.FirstBinaryOperator] = OpPrec.precRelational;
            operatorsPrec[JSToken.GreaterThan - JSToken.FirstBinaryOperator] = OpPrec.precRelational;
            operatorsPrec[JSToken.LessThan - JSToken.FirstBinaryOperator] = OpPrec.precRelational;
            operatorsPrec[JSToken.LessThanEqual - JSToken.FirstBinaryOperator] = OpPrec.precRelational;
            operatorsPrec[JSToken.GreaterThanEqual - JSToken.FirstBinaryOperator] = OpPrec.precRelational;

            operatorsPrec[JSToken.LeftShift - JSToken.FirstBinaryOperator] = OpPrec.precShift;
            operatorsPrec[JSToken.RightShift - JSToken.FirstBinaryOperator] = OpPrec.precShift;
            operatorsPrec[JSToken.UnsignedRightShift - JSToken.FirstBinaryOperator] = OpPrec.precShift;

            operatorsPrec[JSToken.Multiply - JSToken.FirstBinaryOperator] = OpPrec.precMultiplicative;
            operatorsPrec[JSToken.Divide - JSToken.FirstBinaryOperator] = OpPrec.precMultiplicative;
            operatorsPrec[JSToken.Modulo - JSToken.FirstBinaryOperator] = OpPrec.precMultiplicative;

            operatorsPrec[JSToken.Assign - JSToken.FirstBinaryOperator] = OpPrec.precAssignment;
            operatorsPrec[JSToken.PlusAssign - JSToken.FirstBinaryOperator] = OpPrec.precAssignment;
            operatorsPrec[JSToken.MinusAssign - JSToken.FirstBinaryOperator] = OpPrec.precAssignment;
            operatorsPrec[JSToken.MultiplyAssign - JSToken.FirstBinaryOperator] = OpPrec.precAssignment;
            operatorsPrec[JSToken.DivideAssign - JSToken.FirstBinaryOperator] = OpPrec.precAssignment;
            operatorsPrec[JSToken.BitwiseAndAssign - JSToken.FirstBinaryOperator] = OpPrec.precAssignment;
            operatorsPrec[JSToken.BitwiseOrAssign - JSToken.FirstBinaryOperator] = OpPrec.precAssignment;
            operatorsPrec[JSToken.BitwiseXorAssign - JSToken.FirstBinaryOperator] = OpPrec.precAssignment;
            operatorsPrec[JSToken.ModuloAssign - JSToken.FirstBinaryOperator] = OpPrec.precAssignment;
            operatorsPrec[JSToken.LeftShiftAssign - JSToken.FirstBinaryOperator] = OpPrec.precAssignment;
            operatorsPrec[JSToken.RightShiftAssign - JSToken.FirstBinaryOperator] = OpPrec.precAssignment;
            operatorsPrec[JSToken.UnsignedRightShiftAssign - JSToken.FirstBinaryOperator] = OpPrec.precAssignment;

            operatorsPrec[JSToken.ConditionalIf - JSToken.FirstBinaryOperator] = OpPrec.precConditional;
            operatorsPrec[JSToken.Colon - JSToken.FirstBinaryOperator] = OpPrec.precConditional;

            operatorsPrec[JSToken.Comma - JSToken.FirstBinaryOperator] = OpPrec.precComma;

            return operatorsPrec;
        }

        private static string[] InitOperatorsString()
        {
            string[] operatorsString = new string[JSToken.LastOperator - JSToken.FirstOperator + 1];

            operatorsString[JSToken.LogicalNot - JSToken.FirstOperator] = "!";
            operatorsString[JSToken.BitwiseNot - JSToken.FirstOperator] = "~";
            operatorsString[JSToken.Void - JSToken.FirstOperator] = "void";
            operatorsString[JSToken.TypeOf - JSToken.FirstOperator] = "typeof";
            operatorsString[JSToken.Delete - JSToken.FirstOperator] = "delete";
            operatorsString[JSToken.Increment - JSToken.FirstOperator] = "++";
            operatorsString[JSToken.Decrement - JSToken.FirstOperator] = "--";

            operatorsString[JSToken.Plus - JSToken.FirstOperator] = "+";
            operatorsString[JSToken.Minus - JSToken.FirstOperator] = "-";

            operatorsString[JSToken.LogicalOr - JSToken.FirstOperator] = "||";
            operatorsString[JSToken.LogicalAnd - JSToken.FirstOperator] = "&&";
            operatorsString[JSToken.BitwiseOr - JSToken.FirstOperator] = "|";
            operatorsString[JSToken.BitwiseXor - JSToken.FirstOperator] = "^";
            operatorsString[JSToken.BitwiseAnd - JSToken.FirstOperator] = "&";

            operatorsString[JSToken.Equal - JSToken.FirstOperator] = "==";
            operatorsString[JSToken.NotEqual - JSToken.FirstOperator] = "!=";
            operatorsString[JSToken.StrictEqual - JSToken.FirstOperator] = "===";
            operatorsString[JSToken.StrictNotEqual - JSToken.FirstOperator] = "!==";

            operatorsString[JSToken.InstanceOf - JSToken.FirstOperator] = "instanceof";
            operatorsString[JSToken.In - JSToken.FirstOperator] = "in";
            operatorsString[JSToken.GreaterThan - JSToken.FirstOperator] = ">";
            operatorsString[JSToken.LessThan - JSToken.FirstOperator] = "<";
            operatorsString[JSToken.LessThanEqual - JSToken.FirstOperator] = "<=";
            operatorsString[JSToken.GreaterThanEqual - JSToken.FirstOperator] = ">=";

            operatorsString[JSToken.LeftShift - JSToken.FirstOperator] = "<<";
            operatorsString[JSToken.RightShift - JSToken.FirstOperator] = ">>";
            operatorsString[JSToken.UnsignedRightShift - JSToken.FirstOperator] = ">>>";

            operatorsString[JSToken.Multiply - JSToken.FirstOperator] = "*";
            operatorsString[JSToken.Divide - JSToken.FirstOperator] = "/";
            operatorsString[JSToken.Modulo - JSToken.FirstOperator] = "%";

            operatorsString[JSToken.Assign - JSToken.FirstOperator] = "=";
            operatorsString[JSToken.PlusAssign - JSToken.FirstOperator] = "+=";
            operatorsString[JSToken.MinusAssign - JSToken.FirstOperator] = "-=";
            operatorsString[JSToken.MultiplyAssign - JSToken.FirstOperator] = "*=";
            operatorsString[JSToken.DivideAssign - JSToken.FirstOperator] = "/=";
            operatorsString[JSToken.BitwiseAndAssign - JSToken.FirstOperator] = "&=";
            operatorsString[JSToken.BitwiseOrAssign - JSToken.FirstOperator] = "|=";
            operatorsString[JSToken.BitwiseXorAssign - JSToken.FirstOperator] = "^=";
            operatorsString[JSToken.ModuloAssign - JSToken.FirstOperator] = "%=";
            operatorsString[JSToken.LeftShiftAssign - JSToken.FirstOperator] = "<<=";
            operatorsString[JSToken.RightShiftAssign - JSToken.FirstOperator] = ">>=";
            operatorsString[JSToken.UnsignedRightShiftAssign - JSToken.FirstOperator] = ">>>=";

            operatorsString[JSToken.ConditionalIf - JSToken.FirstOperator] = "?";
            operatorsString[JSToken.Colon - JSToken.FirstOperator] = ":";

            operatorsString[JSToken.Comma - JSToken.FirstOperator] = ",";

            return operatorsString;
        }
    }
}