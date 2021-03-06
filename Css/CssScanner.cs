// CssScanner.cs
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

namespace Microsoft.Ajax.Utilities
{
    using System;
    using System.Globalization;
    using System.IO;
    using System.Runtime.Serialization;
    using System.Text;
    using System.Text.RegularExpressions;

    /// <summary>
    /// Scanner takes input stream and breaks it into Tokens
    /// </summary>
    internal class CssScanner
    {
        #region Constant strings

        // these strings are NOT to be localized -- they are CSS language features!
        private const string c_scanIncludes = "~=";
        private const string c_dashMatch = "|=";
        private const string c_prefixMatch = "^=";
        private const string c_suffixMatch = "$=";
        private const string c_substringMatch = "*=";
        private const string c_commentStart = "<!--";
        private const string c_commentEnd = "-->";

        #endregion

        private TextReader m_reader;
        private string m_readAhead;

        private char m_currentChar;

        private CssContext m_context;

        private static Regex s_leadingZeros = new Regex(
            "^0*([0-9]+?)$"
#if !SILVERLIGHT
            , RegexOptions.Compiled
#endif
            );
        private static Regex s_trailingZeros = new Regex(
            "^([0-9]+?)0*$"
#if !SILVERLIGHT
            , RegexOptions.Compiled
#endif
            );
		
		public bool AllowEmbeddedAspNetBlocks { get; set; }

        private bool m_isAtEOF;// = false;
        public bool EndOfFile
        {
            get { return m_isAtEOF; }
        }

        public CssScanner(TextReader reader)
        {
            m_context = new CssContext();

            m_reader = reader;
            //m_readAhead = null;

            // get the first character
            NextChar();
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Globalization", "CA1303:Do not pass literals as localized parameters", MessageId = "Microsoft.Ajax.Utilities.CssToken.#ctor(Microsoft.Ajax.Utilities.TokenType,System.String,Microsoft.Ajax.Utilities.CssContext)")]
        public CssToken NextToken()
        {
            // advance the context
            m_context.Advance();

            CssToken token = null;
            switch (m_currentChar)
            {
                case '\0':
                    // end of file
                    m_isAtEOF = true;
                    break;

                case ' ':
                case '\t':
                case '\r':
                case '\n':
                case '\f':
                    // no matter how much whitespace is actually in
                    // the stream, we're just going to encode a single
                    // space in the token itself
                    while (IsSpace(m_currentChar))
                    {
                        NextChar();
                    }
                    token = new CssToken(TokenType.Space, ' ', m_context);
                    break;

                case '/':
                    token = ScanComment();
                    break;

                case '<':
					if (AllowEmbeddedAspNetBlocks && PeekChar() == '%')
					{
						token = ScanAspNetBlock();
					}
					else
					{
						token = ScanCDO();
					}
                    break;

                case '-':
                    token = ScanCDC();
                    if (token == null)
                    {
                        // identifier in CSS2.1 and CSS3 can start with a hyphen
                        // to indicate vendor-specific identifiers.
                        string ident = GetIdent();
                        if (ident != null)
                        {
                            // vendor-specific identifier
                            // but first see if it's a vendor-specific function!
                            if (m_currentChar == '(')
                            {
                                // it is -- consume the parenthesis; it's part of the token
                                NextChar();
                                token = new CssToken(TokenType.Function, "-" + ident + '(', m_context);
                            }
                            else
                            {
                                // nope -- just a regular identifier
                                token = new CssToken(TokenType.Identifier, "-" + ident, m_context);
                            }
                        }
                        else
                        {
                            // just a hyphen character
                            token = new CssToken(TokenType.Character, '-', m_context);
                        }
                    }
                    break;

                case '~':
                    token = ScanIncludes();
                    break;

                case '|':
                    token = ScanDashMatch();
                    break;

                case '^':
                    token = ScanPrefixMatch();
                    break;

                case '$':
                    token = ScanSuffixMatch();
                    break;

                case '*':
                    token = ScanSubstringMatch();
                    break;

                case '\'':
                case '"':
                    token = ScanString();
                    break;

                case '#':
                    token = ScanHash();
                    break;

                case '@':
                    token = ScanAtKeyword();
                    break;

                case '!':
                    token = ScanImportant();
                    break;

                case 'U':
                case 'u':
                    token = ScanUrl();
                    break;

                case '0':
                case '1':
                case '2':
                case '3':
                case '4':
                case '5':
                case '6':
                case '7':
                case '8':
                case '9':
                case '.':
                    token = ScanNum();
                    break;

                default:
                    token = ScanIdent();
                    break;
            }
            return token;
        }

        #region Scan... methods

        private CssToken ScanComment()
        {
            CssToken token = null;
            NextChar();
            if (m_currentChar == '*')
            {
                NextChar();
                // everything is a comment until we get to */
                StringBuilder sb = new StringBuilder();
                sb.Append("/*");

                bool terminated = false;
                while (m_currentChar != '\0')
                {
                    sb.Append(m_currentChar);
                    if (m_currentChar == '*' && PeekChar() == '/')
                    {
                        sb.Append('/');
                        NextChar(); // now points to /
                        NextChar(); // now points to following character

                        // check for comment-hack 2 -- NS4 sees /*/*//*/ as a single comment
                        // while everyone else properly parses that as two comments, which hides everything
                        // after this construct until the next comment. So this hack shows the stuff
                        // between ONLY to NS4. But we still want to crunch it, so if we just found
                        // a comment like /*/*/, check to see if the next characters are /*/. If so,
                        // treat it like the single comment NS4 sees.
                        // (and don't forget that if we want to keep them, we've turned them both into important comments)
                        if (sb.ToString() == "/*!/*/" && ReadString("/*/"))
                        {
                            // read string will leave the current character after the /*/ string,
                            // so add the part we just read to the string builder and we'll break
                            // out of the loop
                            sb.Append("/*/");
                        }
                        terminated = true;
                        break;
                    }
                    NextChar();
                }
                if (!terminated)
                {
                    ReportError(0, StringEnum.UnterminatedComment);
                }
                token = new CssToken(TokenType.Comment, sb.ToString(), m_context);
            }
            else
            {
                // not a comment
                token = new CssToken(TokenType.Character, '/', m_context);
            }
            return token;
        }

		private CssToken ScanAspNetBlock()
		{
			StringBuilder sb = new StringBuilder();
			char prev = ' ';
			while (m_currentChar != '\0' &&
				   !(m_currentChar == '>' &&
					 prev == '%'))
			{
				sb.Append(m_currentChar);
				prev = m_currentChar;
				NextChar();
			}
			if (m_currentChar != '\0')
			{
				sb.Append(m_currentChar);
				// Read the last '>'
				NextChar();
			}
			return new CssToken(TokenType.AspNetBlock, sb.ToString(), m_context);
		}

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Globalization", "CA1303:Do not pass literals as localized parameters", MessageId = "Microsoft.Ajax.Utilities.CssToken.#ctor(Microsoft.Ajax.Utilities.TokenType,System.String,Microsoft.Ajax.Utilities.CssContext)")]
        private CssToken ScanCDO()
        {
            CssToken token = null;
            NextChar(); // points to !?
            if (m_currentChar == '!')
            {
                if (PeekChar() == '-')
                {
                    NextChar(); // points to -
                    if (PeekChar() == '-')
                    {
                        NextChar(); // points to second hyphen
                        NextChar();
                        token = new CssToken(TokenType.CommentOpen, c_commentStart, m_context);
                    }
                    else
                    {
                        // we want to just return the < character, but
                        // we're currently pointing to the first hyphen,
                        // so we need to add the ! to the read ahead buffer
                        PushChar('!');
                    }
                }
            }
            return (token != null ? token : token = new CssToken(TokenType.Character, '<', m_context));
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Globalization", "CA1303:Do not pass literals as localized parameters", MessageId = "Microsoft.Ajax.Utilities.CssToken.#ctor(Microsoft.Ajax.Utilities.TokenType,System.String,Microsoft.Ajax.Utilities.CssContext)")]
        private CssToken ScanCDC()
        {
            CssToken token = null;
            NextChar(); // points to second hyphen?
            if (m_currentChar == '-')
            {
                if (PeekChar() == '>')
                {
                    NextChar(); // points to >
                    NextChar();
                    token = new CssToken(TokenType.CommentClose, c_commentEnd, m_context);
                }
            }
            return token;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Globalization", "CA1303:Do not pass literals as localized parameters", MessageId = "Microsoft.Ajax.Utilities.CssToken.#ctor(Microsoft.Ajax.Utilities.TokenType,System.String,Microsoft.Ajax.Utilities.CssContext)")]
        private CssToken ScanIncludes()
        {
            CssToken token = null;
            NextChar();
            if (m_currentChar == '=')
            {
                NextChar();
                token = new CssToken(TokenType.Includes, c_scanIncludes, m_context);
            }
            return (token != null ? token : new CssToken(TokenType.Character, '~', m_context));
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Globalization", "CA1303:Do not pass literals as localized parameters", MessageId = "Microsoft.Ajax.Utilities.CssToken.#ctor(Microsoft.Ajax.Utilities.TokenType,System.String,Microsoft.Ajax.Utilities.CssContext)")]
        private CssToken ScanDashMatch()
        {
            CssToken token = null;
            // if the next character is an equals sign, then we have a dash-match
            if (PeekChar() == '=')
            {
                // skip the two characters
                NextChar();
                NextChar();
                token = new CssToken(TokenType.DashMatch, c_dashMatch, m_context);
            }
            else
            {
                // see if this is the start of a namespace ident
                token = ScanIdent();
            }

            // if we haven't computed a token yet, it's just a character
            if (token == null)
            {
                NextChar();
                token = new CssToken(TokenType.Character, '|', m_context);
            }
            return token;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Globalization", "CA1303:Do not pass literals as localized parameters", MessageId = "Microsoft.Ajax.Utilities.CssToken.#ctor(Microsoft.Ajax.Utilities.TokenType,System.String,Microsoft.Ajax.Utilities.CssContext)")]
        private CssToken ScanPrefixMatch()
        {
            CssToken token = null;
            NextChar();
            if (m_currentChar == '=')
            {
                NextChar();
                token = new CssToken(TokenType.PrefixMatch, c_prefixMatch, m_context);
            }
            return (token != null ? token : new CssToken(TokenType.Character, '^', m_context));
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Globalization", "CA1303:Do not pass literals as localized parameters", MessageId = "Microsoft.Ajax.Utilities.CssToken.#ctor(Microsoft.Ajax.Utilities.TokenType,System.String,Microsoft.Ajax.Utilities.CssContext)")]
        private CssToken ScanSuffixMatch()
        {
            CssToken token = null;
            NextChar();
            if (m_currentChar == '=')
            {
                NextChar();
                token = new CssToken(TokenType.SuffixMatch, c_suffixMatch, m_context);
            }
            return (token != null ? token : new CssToken(TokenType.Character, '$', m_context));
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Globalization", "CA1303:Do not pass literals as localized parameters", MessageId = "Microsoft.Ajax.Utilities.CssToken.#ctor(Microsoft.Ajax.Utilities.TokenType,System.String,Microsoft.Ajax.Utilities.CssContext)")]
        private CssToken ScanSubstringMatch()
        {
            CssToken token = null;
            if (PeekChar() == '=')
            {
                // skip the two characters and return a substring match
                NextChar();
                NextChar();
                token = new CssToken(TokenType.SubstringMatch, c_substringMatch, m_context);
            }
            else
            {
                // see if this asterisk is a namespace portion of an identifier
                token = ScanIdent();
            }
            if (token == null)
            {
                // skip the * and return a character token
                NextChar();
                token = new CssToken(TokenType.Character, '*', m_context);
            }
            return token;
        }

        private CssToken ScanString()
        {
            // get the string literal
            string stringLiteral = GetString();

            // the literal must include both delimiters to be valid, so it has to AT LEAST
            // be two characters long. And then the first and the last characters should be
            // the same
            bool isValidString = (stringLiteral.Length >= 2
                && stringLiteral[0] == stringLiteral[stringLiteral.Length - 1]);

            return new CssToken(
              (isValidString ? TokenType.String : TokenType.Error),
              stringLiteral,
              m_context
              );
        }

        private CssToken ScanHash()
        {
            NextChar();
            string name = GetName();
            return (
              name == null
              ? new CssToken(TokenType.Character, '#', m_context)
              : new CssToken(TokenType.Hash, '#' + name, m_context)
              );
        }

        private CssToken ScanAtKeyword()
        {
            NextChar();

            // by default we're just going to return a character token for the "@" sign -- unless it
            // is followed by an identifier, in which case it's an at-symbol.
            TokenType tokenType = TokenType.Character;

            // if the next character is a hyphen, then we're going to want to pull it off and see if the
            // NEXT token is an identifier. If it's not, we'll stuff the hyphen back into the read buffer.
            bool startsWithHyphen = m_currentChar == '-';
            if (startsWithHyphen)
            {
                NextChar();
            }

            string ident = GetIdent();
            if (ident != null)
            {
                // if this started with a hyphen, then we need to add it to the start of our identifier now
                if (startsWithHyphen)
                {
                    ident = '-' + ident;
                }

                switch (ident.ToUpperInvariant())
                {
                    case "IMPORT":
                        tokenType = TokenType.ImportSymbol;
                        break;

                    case "PAGE":
                        tokenType = TokenType.PageSymbol;
                        break;

                    case "MEDIA":
                        tokenType = TokenType.MediaSymbol;
                        break;

                    case "FONT-FACE":
                        tokenType = TokenType.FontFaceSymbol;
                        break;

                    case "CHARSET":
                        tokenType = TokenType.CharacterSetSymbol;
                        break;

                    case "NAMESPACE":
                        tokenType = TokenType.NamespaceSymbol;
                        break;

                    case "TOP-LEFT-CORNER":
                        tokenType = TokenType.TopLeftCornerSymbol;
                        break;

                    case "TOP-LEFT":
                        tokenType = TokenType.TopLeftSymbol;
                        break;

                    case "TOP-CENTER":
                        tokenType = TokenType.TopCenterSymbol;
                        break;

                    case "TOP-RIGHT":
                        tokenType = TokenType.TopRightSymbol;
                        break;

                    case "TOP-RIGHT-CORNER":
                        tokenType = TokenType.TopRightCornerSymbol;
                        break;

                    case "BOTTOM-LEFT-CORNER":
                        tokenType = TokenType.BottomLeftCornerSymbol;
                        break;

                    case "BOTTOM-LEFT":
                        tokenType = TokenType.BottomLeftSymbol;
                        break;

                    case "BOTTOM-CENTER":
                        tokenType = TokenType.BottomCenterSymbol;
                        break;

                    case "BOTTOM-RIGHT":
                        tokenType = TokenType.BottomRightSymbol;
                        break;

                    case "BOTTOM-RIGHT-CORNER":
                        tokenType = TokenType.BottomRightCornerSymbol;
                        break;

                    case "LEFT-TOP":
                        tokenType = TokenType.LeftTopSymbol;
                        break;

                    case "LEFT-MIDDLE":
                        tokenType = TokenType.LeftMiddleSymbol;
                        break;

                    case "LEFT-BOTTOM":
                        tokenType = TokenType.LeftBottomSymbol;
                        break;

                    case "RIGHT-TOP":
                        tokenType = TokenType.RightTopSymbol;
                        break;

                    case "RIGHT-MIDDLE":
                        tokenType = TokenType.RightMiddleSymbol;
                        break;

                    case "RIGHT-BOTTOM":
                        tokenType = TokenType.RightBottomSymbol;
                        break;

                    default:
                        tokenType = TokenType.AtKeyword;
                        break;
                }
            }
            else if (startsWithHyphen)
            {
                // we didn't find an identifier after the "@-".
                // we're going to return a character token for the @, but we need to push the hyphen
                // back into the read buffer for next time
                PushChar('-');
            }

            return new CssToken(tokenType, '@' + (ident == null ? string.Empty : ident), m_context);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Globalization", "CA1303:Do not pass literals as localized parameters", MessageId = "Microsoft.Ajax.Utilities.CssToken.#ctor(Microsoft.Ajax.Utilities.TokenType,System.String,Microsoft.Ajax.Utilities.CssContext)")]
        private CssToken ScanImportant()
        {
            CssToken token = null;
            NextChar();

            string w = GetW();
            if (char.ToUpperInvariant(m_currentChar) == 'I')
            {
                if (ReadString("IMPORTANT"))
                {
                    // no matter what the case or whether or not there is space between the ! and 
                    // the important, we're going to represent this token as having no space and all
                    // lower-case.
                    token = new CssToken(TokenType.ImportantSymbol, "!important", m_context);
                }
            }
            // if the token is still null but we had found some whitespace,
            // we need to push a whitespace char back onto the read-ahead
            if (token == null && w.Length > 0)
            {
                PushChar(' ');
            }

            return (token != null ? token : new CssToken(TokenType.Character, '!', m_context));
        }

        private CssToken ScanUnicodeRange()
        {
            // when called, the current character is the character *after* U+
            CssToken token = null;
            StringBuilder sb = new StringBuilder();
            sb.Append("U+");

            bool hasQuestions = false;
            int count = 0;
            bool leadingZero = true;
            int firstValue = 0;
            while (m_currentChar != '\0'
                && count < 6
                && (m_currentChar == '?' || (!hasQuestions && IsH(m_currentChar))))
            {
                // if this isn't a leading zero, reset the flag
                if (leadingZero && m_currentChar != '0')
                {
                    leadingZero = false;
                }

                if (m_currentChar == '?')
                {
                    hasQuestions = true;
                    
                    // assume the digit is an "F" for maximum value
                    firstValue = firstValue*16 + HValue('F');
                }
                else
                {
                    firstValue = firstValue*16 + HValue(m_currentChar);
                }

                if (!leadingZero)
                {
                    sb.Append(m_currentChar);
                }

                ++count;
                NextChar();
            }

            if (count > 0)
            {
                // if the unicode value is out of range, throw an error
                if (firstValue < 0 || 0x10ffff < firstValue)
                {
                    // throw an error
                    ReportError(0, StringEnum.InvalidUnicodeRange, sb.ToString());
                }

                // if we still have the leading zero flag, then all the numbers were zero
                // and we didn't output any of them.
                if (leadingZero)
                {
                    // add one zero to keep it proper
                    sb.Append('0');
                }

                if (hasQuestions)
                {
                    // if there are question marks, then we're done
                    token = new CssToken(
                        TokenType.UnicodeRange,
                        sb.ToString(),
                        m_context);
                }
                else if (m_currentChar == '-')
                {
                    sb.Append('-');
                    NextChar();

                    count = 0;
                    leadingZero = true;
                    int secondValue = 0;
                    while (m_currentChar != '\0' && count < 6 && IsH(m_currentChar))
                    {
                        // if this isn't a leading zero, reset the flag
                        if (leadingZero && m_currentChar != '0')
                        {
                            leadingZero = false;
                        }

                        secondValue = secondValue * 16 + HValue(m_currentChar);

                        if (!leadingZero)
                        {
                            sb.Append(m_currentChar);
                        }

                        ++count;
                        NextChar();
                    }

                    if (count > 0)
                    {
                        // if we still have the leading zero flag, then all the numbers were zero
                        // and we didn't output any of them.
                        if (leadingZero)
                        {
                            // add one zero to keep it proper
                            sb.Append('0');
                        }

                        // check to make sure the second value is within range
                        // AND is greater than the first
                        if (secondValue < 0 || 0x10ffff < secondValue
                            || firstValue >= secondValue)
                        {
                            // throw an error
                            ReportError(0, StringEnum.InvalidUnicodeRange, sb.ToString());
                        }

                        token = new CssToken(
                            TokenType.UnicodeRange,
                            sb.ToString(),
                            m_context);
                    }
                }
                else
                {
                    // single code-point with at least one character
                    token = new CssToken(
                        TokenType.UnicodeRange,
                        sb.ToString(),
                        m_context);
                }
            }

            // if we don't hve a unicode range,
            // we need to return an ident token from the U we already scanned
            if (token == null)
            {
                // push everything back onto the buffer
                PushString(sb.ToString());
                token = ScanIdent();
            }

            return token;
        }

        private CssToken ScanUrl()
        {
            CssToken token = null;
            if (PeekChar() == '+')
            {
                NextChar(); // now current is the +
                NextChar(); // now current is the first character after the +
                token = ScanUnicodeRange();
            }
            else if (ReadString("URL("))
            {
                StringBuilder sb = new StringBuilder();
                sb.Append("url(");

                GetW();

                string url = GetString();
                if (url == null)
                {
                    url = GetUrl();
                }

                if (url != null)
                {
                    sb.Append(url);
                    GetW();
                    if (m_currentChar == ')')
                    {
                        sb.Append(')');
                        NextChar();

                        token = new CssToken(
                          TokenType.Uri,
                          sb.ToString(),
                          m_context
                          );
                    }
                }
            }
            return (token != null ? token : ScanIdent());
        }

        private CssToken ScanNum()
        {
            CssToken token = null;
            string num = GetNum();
            if (num != null)
            {
                if (m_currentChar == '%')
                {
                    NextChar();

                    // let's always keep the percentage on the number, even if it's
                    // zero -- some things require it (like the rgb function)
                    token = new CssToken(
                      TokenType.Percentage,
                      num + '%',
                      m_context
                      );
                }
                else
                {
                    string dimen = GetIdent();
                    if (dimen == null)
                    {
                        // if there is no identifier, it's a num.
                        token = new CssToken(TokenType.Number, num, m_context);
                    }
                    else
                    {
                        // classify the dimension type
                        TokenType tokenType = TokenType.Dimension;
                        switch (dimen.ToUpperInvariant())
                        {
                            case "EM":
                            case "EX":
                            case "PX":
                            case "GD":
                            case "REM":
                            case "VW":
                            case "VH":
                            case "VM":
                            case "CH":
                                tokenType = TokenType.RelativeLength;
                                break;

                            case "CM":
                            case "MM":
                            case "IN":
                            case "PT":
                            case "PC":
                                tokenType = TokenType.AbsoluteLength;
                                break;

                            case "DEG":
                            case "RAD":
                            case "GRAD":
                            case "TURN":
                                tokenType = TokenType.Angle;
                                break;

                            case "MS":
                            case "S":
                                tokenType = TokenType.Time;
                                break;

                            case "DPI":
                            case "DPCM":
                                tokenType = TokenType.Resolution;
                                break;

                            case "HZ":
                            case "KHZ":
                                tokenType = TokenType.Frequency;
                                break;
                        }

                        // if the number is zero, it really doesn't matter what the dimensions are so we can remove it.
                        // HOWEVER, if we don't recognize the dimension, leave it -- it could be a browser hack or some
                        // other intentional construct.
                        if (num == "0" && tokenType != TokenType.Dimension)
                        {
                            // dumb warning message -- this kind of thing is what the minimizer is 
                            // SUPPOSED to handle automatically without any "problems."
                            // Maybe if we are in Analyze mode or something.
                            /*if (dimen != null)
                            {
                                ReportError(4, StringEnum.UnnecessaryUnits);
                            }*/
                            token = new CssToken(TokenType.Number, num, m_context);
                        }
                        else
                        {
                            token = new CssToken(tokenType, num + dimen, m_context);
                        }
                    }
                }
            }
            else if (m_currentChar == '.')
            {
                token = new CssToken(TokenType.Character, '.', m_context);
                NextChar();
            }
            else
            {
                // this function is only called when the first character is 
                // a digit or a period. So this block should never execute, since
                // a digit will produce a num, and if it doesn't, the previous block
                // picks up the period.
                ReportError(1, StringEnum.UnexpectedNumberCharacter, m_currentChar);
            }

            return token;
        }

        private CssToken ScanIdent()
        {
            CssToken token = null;

            string ident = GetIdent();
            if (ident != null)
            {
                if (m_currentChar == '(')
                {
                    NextChar();
                    if (string.Compare(ident, "not", StringComparison.OrdinalIgnoreCase) == 0)
                    {
                        token = new CssToken(TokenType.Not, ident + '(', m_context);
                    }
                    else
                    {
                        token = new CssToken(TokenType.Function, ident + '(', m_context);
                    }
                }
                else if (string.Compare(ident, "progid", StringComparison.OrdinalIgnoreCase) == 0 && m_currentChar == ':')
                {
                    NextChar();
                    token = ScanProgId();
                }
                else
                {
                    token = new CssToken(TokenType.Identifier, ident, m_context);
                }
            }

            // if we failed somewhere in the processing...
            if (ident == null)
            {
                if (m_currentChar != '\0')
                {
                    // create a character token
                    token = new CssToken(TokenType.Character, m_currentChar, m_context);
                    NextChar();
                }
            }
            return token;
        }

        private CssToken ScanProgId()
        {
            CssToken token = null;
            StringBuilder sb = new StringBuilder();
            sb.Append("progid:");
            string ident = GetIdent();
            while (ident != null)
            {
                sb.Append(ident);
                if (m_currentChar == '.')
                {
                    sb.Append('.');
                    NextChar();
                }
                ident = GetIdent();
            }
            if (m_currentChar == '(')
            {
                sb.Append('(');
                NextChar();

                token = new CssToken(TokenType.ProgId, sb.ToString(), m_context);
            }
            else
            {
                ReportError(1, StringEnum.ExpectedOpenParen);
            }
            return token;
        }

        #endregion

        #region Is... methods

        private static bool IsSpace(char ch)
        {
            switch (ch)
            {
                case ' ':
                case '\t':
                case '\r':
                case '\n':
                case '\f':
                    return true;

                default:
                    return false;
            }
        }

        private static int HValue(char ch)
        {
            if ('0' <= ch && ch <= '9')
            {
                return ch - '0';
            }
            if ('a' <= ch && ch <= 'f')
            {
                return (ch - 'a') + 10;
            }
            if ('A' <= ch && ch <= 'F')
            {
                return (ch - 'A') + 10;
            }
            return 0;
        }

        public static bool IsH(char ch)
        {
            return (
              ('0' <= ch && ch <= '9')
              || ('a' <= ch && ch <= 'f')
              || ('A' <= ch && ch <= 'F')
              );
        }

        private static bool IsD(char ch)
        {
            return ('0' <= ch && ch <= '9');
        }

        private static bool IsNonAscii(char ch)
        {
            return (128 <= ch && ch <= 65535);
        }

        #endregion

        #region Get... methods

        /// <summary>
        /// returns the VALUE of a unicode number, up to six hex digits
        /// </summary>
        /// <returns>int value representing up to 6 hex digits</returns>
        private int GetUnicodeEncodingValue()
        {
            int unicodeValue = 0;

            // loop for no more than 6 hex characters
            int count = 0;
            while (m_currentChar != '\0' && count++ < 6 && IsH(m_currentChar))
            {
                unicodeValue = (unicodeValue * 16) + HValue(m_currentChar);
                NextChar();
            }

            // if there is a space character afterwards, skip it
            // (but only skip one space character if present)
            if (IsSpace(m_currentChar))
            {
                NextChar();
            }
            return unicodeValue;
        }

        private string GetUnicode()
        {
            string unicode = null;
            if (m_currentChar == '\\')
            {
                char ch = PeekChar();
                if (IsH(ch))
                {
                    // let's actually decode the escapes so another encoding
                    // format might actually save us some space.
                    // skip over the slash
                    NextChar();

                    // decode the hexadecimal digits at the current character point,
                    // up to six characters
                    int unicodeValue = GetUnicodeEncodingValue();

                    // we shouldn't NEED to check for surrogate pairs here because
                    // the encoding is up to six digits, which encompasses all the
                    // available Unicode characters without having to resort to
                    // surrogate pairs. However, some bone-head can always manually
                    // encode two surrogate pair values in their source.
                    if (0xd800 <= unicodeValue && unicodeValue <= 0xdbff)
                    {
                        // this is a high-surrogate value.
                        int hi = unicodeValue;
                        // the next encoding BETTER be a unicode value
                        if (m_currentChar == '\\' && IsH(PeekChar()))
                        {
                            // skip the slash
                            NextChar();
                            // get the lo value
                            int lo = GetUnicodeEncodingValue();
                            if (0xdc00 <= lo && lo <= 0xdfff)
                            {
                                // combine the hi/lo pair into one character value
                                unicodeValue = 0x10000
                                  + (hi - 0xd800) * 0x400
                                  + (lo - 0xdc00);
                            }
                            else
                            {
                                // ERROR! not a valid unicode lower-surrogate value!
                                ReportError(
                                  0,
                                  StringEnum.InvalidLowSurrogate, hi, lo
                                  );
                            }
                        }
                        else
                        {
                            // ERROR! the high-surrogate is not followed by a low surrogate!
                            ReportError(
                              0,
                              StringEnum.HighSurrogateNoLow, unicodeValue
                              );
                        }
                    }

                    // get the unicode character. might be multiple characters because
                    // the 21-bit value stired in the int might be encoded into a surrogate pair.
                    //unicode = char.ConvertFromUtf32(unicodeValue);
                    unicode = ConvertUtf32ToUtf16(unicodeValue);
                }
            }
            return unicode;
        }

        private static string ConvertUtf32ToUtf16(int unicodeValue)
        {
#if !SILVERLIGHT
            return char.ConvertFromUtf32(unicodeValue);
#else
            string text;
            if (unicodeValue <= 0xffff)
            {
                if (0xd8000 <= unicodeValue && unicodeValue <= 0xdfff)
                {
                    throw new ArgumentException("UTF32 value cannot be in surrogate range");
                }
                else
                {
                    // single-character normal results
                    text = new string((char)unicodeValue, 1);
                }
            }
            else if (unicodeValue < 0x10ffff)
            {
                // need to calculate the surrogate pair representation
                unicodeValue -= 0x10000;
                text = new string(new char[2]
                {
                    (char)((unicodeValue >> 10) + 0xd800),
                    (char)((unicodeValue & 0x3ff) + 0xdc00)
                });
            }
            else
            {
                throw new ArgumentException("UTF32 value out of range");
            }
            return text;
#endif
        }

        private string GetEscape()
        {
            string escape = GetUnicode();
            if (escape == null && m_currentChar == '\\')
            {
                char ch = PeekChar();
                if ((' ' <= ch && ch <= '~')
                  || IsNonAscii(ch))
                {
                    NextChar();
                    NextChar();
                    return "\\" + ch;
                }
            }
            return escape;
        }

        private string GetNmStart()
        {
            string nmStart = GetEscape();
            if (nmStart == null)
            {
                if (IsNonAscii(m_currentChar)
                  || (m_currentChar == '_')
                  || ('a' <= m_currentChar && m_currentChar <= 'z')
                  || ('A' <= m_currentChar && m_currentChar <= 'Z'))
                {
                    // actually, CSS1 and CSS2 don't allow underscores in
                    // identifier names, especially not the first character
                    if (m_currentChar == '_')
                    {
                        ReportError(
                          4,
                          StringEnum.UnderscoreNotValid
                          );
                    }

                    nmStart = char.ToString(m_currentChar);
                    NextChar();
                }
            }
            return nmStart;
        }

        private string GetNmChar()
        {
            string nmChar = GetEscape();
            if (nmChar == null)
            {
                if (IsNonAscii(m_currentChar)
                  || (m_currentChar == '-')
                  || (m_currentChar == '_')
                  || ('0' <= m_currentChar && m_currentChar <= '9')
                  || ('a' <= m_currentChar && m_currentChar <= 'z')
                  || ('A' <= m_currentChar && m_currentChar <= 'Z'))
                {
                    // actually, CSS1 and CSS2 don't allow underscores in
                    // identifier names.
                    if (m_currentChar == '_')
                    {
                        ReportError(
                          4,
                          StringEnum.UnderscoreNotValid
                          );
                    }
                    nmChar = char.ToString(m_currentChar);
                    NextChar();
                }
            }
            return nmChar;
        }

        private string GetString()
        {
            string str = null;
            if (m_currentChar == '\'' || m_currentChar == '"')
            {
                char delimiter = m_currentChar;
                NextChar();

                StringBuilder sb = new StringBuilder();
                sb.Append(delimiter);

                while (m_currentChar != '\0' && m_currentChar != delimiter)
                {
                    str = GetEscape();
                    if (str != null)
                    {
                        // if this is a one-character string, and that one character
                        // if the same as our string delimiter, then this was probably
                        // a unicode-encoded character. We will need to escape it in the
                        // output or the string will be invalid.
                        if (str.Length == 1 && str[0] == delimiter)
                        {
                            // instead of escaping it as unicode again (\22 or \27), we
                            // can save a byte by encoding is as \" or \'
                            str = "\\" + delimiter;
                        }
                        sb.Append(str);
                    }
                    else if (IsNonAscii(m_currentChar))
                    {
                        sb.Append(m_currentChar);
                        NextChar();
                    }
                    else if (m_currentChar == '\\')
                    {
                        NextChar();
                        str = GetNewline();
                        if (str != null)
                        {
                            // new-lines in strings are "for aesthetic or other reasons,"
                            // but are not actually part of the string. We can remove
                            // then to crunch the string a bit.
                            //sb.Append( '\\' );
                            //sb.Append( str );
                        }
                        else
                        {
                            // unexpected escape sequence
                            ReportError(
                              0,
                              StringEnum.UnexpectedEscape, m_currentChar
                              );
                        }
                    }
                    else if ((m_currentChar == ' ')
                      || (m_currentChar == '\t')
                      || (m_currentChar == '!')
                      || (m_currentChar == '#')
                      || (m_currentChar == '$')
                      || (m_currentChar == '%')
                      || (m_currentChar == '&')
                      || ('(' <= m_currentChar && m_currentChar <= '~')
                      || (m_currentChar == (delimiter == '"' ? '\'' : '"')))
                    {
                        sb.Append(m_currentChar);
                        NextChar();
                    }
                    else if (m_currentChar == '\n'
                        || m_currentChar == '\r')
                    {
                        // unterminated string
                        ReportError(
                          0,
                          StringEnum.UnterminatedString, sb.ToString()
                          );
                        // add the newline to the string so it will line-break in the output
                        sb.AppendLine();

                        // skip the block of whitespace we just encountered so that the current
                        // character will be the first non-whitespace character after the bogus
                        // string
                        while (IsSpace(m_currentChar))
                        {
                            NextChar();
                        }
                        // return early
                        return sb.ToString();
                    }
                    else
                    {
                        // unexpected string character
                        ReportError(
                          0,
                          StringEnum.UnexpectedStringCharacter, m_currentChar
                          );
                    }
                }
                if (m_currentChar == delimiter)
                {
                    sb.Append(delimiter);
                    NextChar(); // pass delimiter
                }
                str = sb.ToString();
            }
            return str;
        }

        private string GetIdent()
        {
            string ident = GetNmStart();
            if (ident != null)
            {
                StringBuilder sb = new StringBuilder();
                sb.Append(ident);
                while (m_currentChar != '\0' && (ident = GetNmChar()) != null)
                {
                    sb.Append(ident);
                }
                ident = sb.ToString();
            }
            return ident;
        }

        private string GetName()
        {
            string name = GetNmChar();
            if (name != null)
            {
                StringBuilder sb = new StringBuilder();
                sb.Append(name);
                while (m_currentChar != '\0' && (name = GetNmChar()) != null)
                {
                    sb.Append(name);
                }
                name = sb.ToString();
            }
            return name;
        }

        private string GetNum()
        {
            string num = null;
            string units = null;
            string fraction = null;
            if (IsD(m_currentChar))
            {
                StringBuilder sb = new StringBuilder();
                sb.Append(m_currentChar);
                NextChar();
                while (IsD(m_currentChar))
                {
                    sb.Append(m_currentChar);
                    NextChar();
                }
                units = sb.ToString();
            }
            if (m_currentChar == '.')
            {
                if (IsD(PeekChar()))
                {
                    // move over the decimal point
                    NextChar();

                    StringBuilder sb = new StringBuilder();
                    // check for extra digits
                    while (IsD(m_currentChar))
                    {
                        sb.Append(m_currentChar);
                        NextChar();
                    }
                    fraction = sb.ToString();
                }
                else if (units != null)
                {
                    // REVIEW: it looks like there must be at least one digit
                    // after a decimal point, but let's let it slack a bit and
                    // let decimal point be a part of a number if it starts with
                    // digits
                    ReportError(
                      2,
                      StringEnum.DecimalNoDigit
                      );
                    fraction = string.Empty;
                    NextChar();
                }
            }
            if (units != null || fraction != null)
            {
                //string rawNum = units + (fraction != null ? "." + fraction : null);
                //if (m_collapseNumbers)
                {
                    if (units != null)
                    {
                        // remove leading zeros from units
                        units = s_leadingZeros.Replace(units, "$1");
                    }
                    if (fraction != null)
                    {
                        // remove trailing zeros from fraction
                        fraction = s_trailingZeros.Replace(fraction, "$1");
                        // if the results is just a single zero, we're going
                        // to ignore the fractional part altogether
                        if (fraction == "0" || fraction.Length == 0)
                        {
                            fraction = null;
                        }
                    }
                    // if we have a fractional part and the units is zero, then
                    // we're going to ignore the units
                    if (fraction != null && units == "0")
                    {
                        units = null;
                    }

                    if (fraction == null)
                    {
                        num = units;
                    }
                    else
                    {
                        num = units + '.' + fraction;
                    }

                    // This is a dumb error message. So what if the minified code is smaller?
                    // isn't that what the tool is supposed to do?
                    // Maybe if we are in Analyze mode or something.
                    /*if (num != rawNum)
                    {
                        ReportError(
                          4,
                          StringEnum.EquivalentNumbers, rawNum, num
                          );
                    }*/
                }
                /*else
                {
                    // just use what we have
                    num = rawNum;
                }*/
            }
            return num;
        }

        private string GetUrl()
        {
            StringBuilder sb = new StringBuilder();
            while (m_currentChar != '\0')
            {
                string escape = GetEscape();
                if (escape != null)
                {
                    sb.Append(escape);
                }
                else if (IsNonAscii(m_currentChar)
                  || (m_currentChar == '!')
                  || (m_currentChar == '#')
                  || (m_currentChar == '$')
                  || (m_currentChar == '%')
                  || (m_currentChar == '&')
                  || ('*' <= m_currentChar && m_currentChar <= '~'))
                {
                    sb.Append(m_currentChar);
                    NextChar();
                }
                else
                {
                    break;
                }
            }
            return sb.ToString();
        }

        private string GetW()
        {
            string w = string.Empty;
            if (IsSpace(m_currentChar))
            {
                w = " ";
                NextChar();
                while (IsSpace(m_currentChar))
                {
                    NextChar();
                }
            }
            return w;
        }

        private string GetNewline()
        {
            string nl = null;
            switch (m_currentChar)
            {
                case '\n':
                    NextChar();
                    nl = "\n";
                    break;

                case '\f':
                    NextChar();
                    nl = "\f";
                    break;

                case '\r':
                    NextChar();
                    if (m_currentChar == '\n')
                    {
                        NextChar();
                        nl = "\r\n";
                    }
                    else
                    {
                        nl = "\r";
                    }
                    break;

                default:
                    break;
            }
            return nl;
        }

        #endregion

        #region NextChar, Peek..., Push...

        private void NextChar()
        {
            if (m_readAhead != null)
            {
                m_currentChar = m_readAhead[0];
                if (m_readAhead.Length == 1)
                {
                    m_readAhead = null;
                }
                else
                {
                    m_readAhead = m_readAhead.Substring(1); ;
                }

                // REVIEW: we don't handle pushing newlines back into buffer
                m_context.End.NextChar();
            }
            else
            {
                int ch = m_reader.Read();
                if (ch < 0)
                {
                    m_currentChar = '\0';
                }
                else
                {
                    m_currentChar = (char)ch;
                    switch (m_currentChar)
                    {
                        case '\n':
                        case '\f':
                            m_context.End.NextLine();
                            break;

                        case '\r':
                            if (PeekChar() != '\n')
                            {
                                m_context.End.NextLine();
                            }
                            break;

                        default:
                            m_context.End.NextChar();
                            break;
                    }
                }
            }
        }

        private char PeekChar()
        {
            if (m_readAhead != null)
            {
                return m_readAhead[0];
            }

            int ch = m_reader.Peek();
            if (ch < 0)
            {
                return '\0';
            }

            return (char)ch;
        }

        // case-INsensitive string at the current location in the input stream
        private bool ReadString(string str)
        {
            // if the first character doesn't match, then we
            // know we're not the string, and we don't have to
            // push anything back on the stack because we haven't
            // gone anywhere yet
            if (char.ToUpperInvariant(m_currentChar) != char.ToUpperInvariant(str[0]))
            {
                return false;
            }

            // we'll start peeking ahead so we have less to push
            // if we fail
            for (int ndx = 1; ndx < str.Length; ++ndx)
            {
                if (char.ToUpperInvariant(PeekChar()) != char.ToUpperInvariant(str[ndx]))
                {
                    // not this string. Push what we've matched
                    if (ndx > 1)
                    {
                        PushString(str.Substring(0, ndx - 1));
                    }
                    return false;
                }
                NextChar();
            }
            NextChar();
            return true;
        }

        private void PushChar(char ch)
        {
            if (m_readAhead == null)
            {
                m_readAhead = char.ToString(m_currentChar);
                m_currentChar = ch;
            }
            else
            {
                m_readAhead = m_currentChar + m_readAhead;
                m_currentChar = ch;
            }
            // REVIEW: doesn't handle pushing a newline back onto the buffer
            m_context.End.PreviousChar();
        }

        private void PushString(string str)
        {
            if (str.Length > 0)
            {
                if (str.Length > 1)
                {
                    m_readAhead = str.Substring(1) + m_currentChar + m_readAhead;
                }
                else
                {
                    m_readAhead = m_currentChar + m_readAhead;
                }
                m_currentChar = str[0];
            }

            // REVIEW: doesn't handle pushing a newline back onto the buffer
            for (int ndx = 0; ndx < str.Length; ++ndx)
            {
                m_context.End.PreviousChar();
            }
        }

        #endregion

        #region Error handling

        private void ReportError(int severity, StringEnum error, params object[] args)
        {
            // guide: 0 == syntax error
            //        1 == the programmer probably did not intend to do this
            //        2 == this can lead to problems in the future.
            //        3 == this can lead to performance problems
            //        4 == this is just not right

            string message = CssStringMgr.GetString(error, args);
            OnScannerError(new CssScannerException(
                (int)error,
                severity,
                m_context.End.Line,
                m_context.End.Char,
                message
                ));
        }

        public event EventHandler<CssScannerErrorEventArgs> ScannerError;

        protected void OnScannerError(CssScannerException exc)
        {
            if (ScannerError != null)
            {
                ScannerError(this, new CssScannerErrorEventArgs(exc));
            }
        }

        #endregion
    }

#if !SILVERLIGHT
    [Serializable]
#endif
    public sealed class CssScannerException : CssException
    {
        private static readonly string s_originator = CssStringMgr.GetString(StringEnum.ScannerSubsystem);

        internal CssScannerException(int error, int severity, int line, int pos, string message)
            : base(error, s_originator, severity, line, pos, message)
        {
        }

        public CssScannerException()
            : base((int)StringEnum.UnknownError, s_originator, 1, 0, 0, CssStringMgr.GetString(StringEnum.UnknownError))
        {
        }

        public CssScannerException(string message)
            : base((int)StringEnum.UnknownError, s_originator, 1, 0, 0, message)
        {
        }

        public CssScannerException(string message, Exception innerException)
            : base((int)StringEnum.UnknownError, s_originator, 1, 0, 0, message, innerException)
        {
        }

#if !SILVERLIGHT
        private CssScannerException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
#endif
    }

    internal class CssScannerErrorEventArgs : EventArgs
    {
        public CssScannerException Exception { get; private set; }

        public CssScannerErrorEventArgs(CssScannerException exc)
        {
            Exception = exc;
        }
    }
}