// constantwrapper.cs
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
using System.Text.RegularExpressions;

namespace Microsoft.Ajax.Utilities
{

    public class ConstantWrapper : AstNode
    {
        // this is a regular expression that we'll use to strip a leading "0x" from
        // a string if we are trying to parse it into a number. also removes the leading
        // and trailing spaces, while we're at it.
        // will also capture a sign if it's present. Strictly speaking, that's not allowed
        // here, but some browsers (Firefox, Opera, Chrome) will parse it. IE and Safari
        // will not. So if we match that sign, we are in a cross-browser gray area.
        private static Regex s_hexNumberFormat = new Regex(
          @"^\s*(?<sign>[-+])?0X(?<hex>[0-9a-f]+)\s*$",
          RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
#if !SILVERLIGHT
 | RegexOptions.Compiled
#endif
);
        // this is a regular expression that we'll use to minimize numeric values
        // that don't employ the e-notation
        private static Regex s_decimalFormat = new Regex(
          @"^\s*\+?(?<neg>\-)?0*(?<mag>(?<sig>\d*[1-9])(?<zer>0*))?(\.(?<man>\d*[1-9])?0*)?(?<exp>E\+?(?<eng>\-?)0*(?<pow>[1-9]\d*))?",
          RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
#if !SILVERLIGHT
 | RegexOptions.Compiled
#endif
);


        public Object Value { get; set; }

        public PrimitiveType PrimitiveType
        {
            get;
            set;
        }

        public bool IsNumericLiteral
        {
            get
            {
                return PrimitiveType == PrimitiveType.Number;
            }
        }

        public bool IsFiniteNumericLiteral
        {
            get
            {
                // numeric literal, but not NaN, +Infinity, or -Infinity
                return IsNumericLiteral
                    ? !double.IsNaN((double)Value) && !double.IsInfinity((double)Value)
                    : false;
            }
        }

        public bool IsIntegerLiteral
        {
            get
            {
                try
                {
                    // numeric literal, but not NaN, +Infinity, or -Infinity; and no decimal portion 
                    return IsFiniteNumericLiteral ? (ToInteger() == (double)Value) : false;
                }
                catch (InvalidCastException)
                {
                    // couldn't convert to a number, so we are not an integer literal
                    // (at least, not a *cross-browser* integer literal)
                    return false;
                }
            }
        }

        public bool IsExactInteger
        {
            get
            {
                // first off, it has to BE an integer value.
                if (IsIntegerLiteral)
                {
                    // and then it has to be within the range of -2^53 and +2^53. Every integer in that range can
                    // be EXACTLY represented in a 64-bit IEEE double value. Outside that range and the source characters
                    // may not be exactly what we would get if we turn this value to a string because the gap between
                    // consecutive available numbers is larger than one.
                    return -0x20000000000000 <= (double)Value && (double)Value <= 0x20000000000000;
                }
                return false;
            }
        }

        public bool IsNaN
        {
            get
            {
                return IsNumericLiteral && double.IsNaN((double)Value);
            }
        }

        public bool IsInfinity
        {
            get
            {
                return IsNumericLiteral && double.IsInfinity((double)Value);
            }
        }

        public bool IsZero
        {
            get
            {
                return IsNumericLiteral && ((double)Value == 0);
            }
        }

        public bool IsBooleanLiteral
        {
            get
            {
                return PrimitiveType == PrimitiveType.Boolean;
            }
        }

        public bool IsStringLiteral
        {
            get
            {
                return PrimitiveType == PrimitiveType.String;
            }
        }

        private bool m_isParameterToRegExp;
        public bool IsParameterToRegExp
        {
            get { return m_isParameterToRegExp; }
        }

        public bool IsSpecialNumeric
        {
            get
            {
                bool isSpecialNumeric = false;
                if (IsNumericLiteral)
                {
                    double doubleValue = (double)Value;
                    isSpecialNumeric = (double.IsNaN(doubleValue) || double.IsInfinity(doubleValue));
                }
                return isSpecialNumeric;
            }
        }

        public ConstantWrapper(Object value, PrimitiveType primitiveType, Context context, JSParser parser)
            : base(context, parser)
        {
            PrimitiveType = primitiveType;

            // force numerics to be of type double
            Value = (primitiveType == PrimitiveType.Number ? System.Convert.ToDouble(value) : value);
        }

        public override AstNode Clone()
        {
            return new ConstantWrapper(
                Value,
                PrimitiveType,
                (Context == null ? null : Context.Clone()),
                Parser
                );
        }

        internal override void AnalyzeNode()
        {
            // check to see if this node is an argument to a RegExp constructor.
            // if it is, we'll want to not use certain string escapes
            AstNode previousNode = null;
            AstNode parentNode = Parent;
            while (parentNode != null)
            {
                // is this a call node and he previous node was one of the parameters?
                CallNode callNode = parentNode as CallNode;
                if (callNode != null && previousNode == callNode.Arguments)
                {
                    // are we calling a simple lookup for "RegExp"?
                    Lookup lookup = callNode.Function as Lookup;
                    if (lookup != null && lookup.Name == "RegExp")
                    {
                        // we are -- so all string literals passed within this constructor should not use
                        // standard string escape sequences
                        m_isParameterToRegExp = true;
                        // we can stop looking
                        break;
                    }
                }

                // next up the chain, keeping track of this current node as next iteration's "previous" node
                previousNode = parentNode;
                parentNode = parentNode.Parent;
            }

            // we only need to process the literals IF we are actually going to do
            // anything with them (combine duplicates). So if we aren't, don't call
            // AddLiteral because it hugely inflates the processing time of the application.
            if (Parser.Settings.CombineDuplicateLiterals)
            {
                // add this literal to the scope's literal collection
                ActivationObject thisScope = Parser.ScopeStack.Peek();
                thisScope.AddLiteral(this, thisScope);
            }

            // this node has no children, so don't bother calling the base
        }

        public override string ToCode(ToCodeFormat format)
        {
            string str;
            switch(PrimitiveType)
            {
                case PrimitiveType.Null:
                    str = "null";
                    break;

                case PrimitiveType.Number:
                    if (Context == null || Parser.Settings.IsModificationAllowed(TreeModifications.MinifyNumericLiterals))
                    {
                        // apply minification to the literal to get it as small as possible
                        str = NumericToString();
                    }
                    else
                    {
                        // just use the original literal from the context
                        str = Context.Code;
                    }
                    break;

                case PrimitiveType.Boolean:
                    str = Convert.ToBoolean(Value)
                      ? "true"
                      : "false";
                    break;

                case PrimitiveType.String:
                    if (Context == null || Parser.Settings.IsModificationAllowed(TreeModifications.MinifyStringLiterals))
                    {
                        // escape the string
                        str = EscapeString(
                            Value.ToString(),
                            m_isParameterToRegExp, 
                            false);
                    }
                    else
                    {
                        // just use the original literal from the context
                        str = Context.Code;
                    }

                    if (Parser.Settings.InlineSafeStrings)
                    {
                        // if there are ANY closing script tags...
                        if (str.IndexOf("</script>", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            // replace all of them with an escaped version so a text-compare won't match
                            str = str.Replace("</script>", @"<\/script>");
                        }

                        // if there are ANY closing CDATA strings...
                        if (str.IndexOf("]]>", StringComparison.Ordinal) >= 0)
                        {
                            // replace all of them with an escaped version so a text-compare won't match
                            str = str.Replace("]]>", @"]\]>");
                        }
                    }
                    break;

                default:
                    // other....
                    str = Value.ToString();
                    break;
            }

            return str;
        }

        private string NumericToString()
        {
            // numerics are doubles in JavaScript, so force it now as a shortcut
            double doubleValue = (double)Value;
            if (double.IsNaN(doubleValue) || double.IsInfinity(doubleValue))
            {
                // weird number -- just return the uncrunched source code as-is. 
                // we've should have already thrown an error alerting the developer 
                // to the overflow mistake, and it might alter the code to change the value
                if (this.Context != null && !string.IsNullOrEmpty(Context.Code)
                    && string.CompareOrdinal(Context.Code, "[generated code]") != 0)
                {
                    return Context.Code;
                }

                // Hmmm... don't have a context source. 
                // Must be generated. Just generate the proper JS literal.
                //
                // DANGER! If we just output NaN and Infinity and -Infinity blindly, that assumes
                // that there aren't any local variables in this scope chain with that
                // name, and we're pulling the GLOBAL properties. Might want to use properties
                // on the Number object -- which, of course, assumes that Number doesn't
                // resolve to a local variable...
                string objectName = double.IsNaN(doubleValue) ? "NaN" : "Infinity";

                ActivationObject enclosingScope = EnclosingScope;
                if (!(enclosingScope is GlobalScope))
                {
                    JSPredefinedField globalReference = enclosingScope.FindReference(objectName) as JSPredefinedField;
                    if (globalReference == null)
                    {
                        // the name doesn't resolve to the global object properties! Must be a local field in the way!
                        // we can't assume the local field of this name contains the proper numeric value or we
                        // could get into big trouble.
                        // try the Number object
                        globalReference = enclosingScope.FindReference("Number") as JSPredefinedField;
                        if (globalReference != null)
                        {
                            // use the properties off this object. Not very compact, but accurate.
                            // I don't think there will be any precedence problems with these constructs --
                            // the member-dot operator is pretty high on the precedence scale.
                            if (double.IsPositiveInfinity(doubleValue))
                            {
                                return "Number.POSITIVE_INFINITY";
                            }
                            if (double.IsNegativeInfinity(doubleValue))
                            {
                                return "Number.NEGATIVE_INFINITY";
                            }
                            return "Number.NaN";
                        }
                        else
                        {
                            // that doesn't resolve to the global Number object, either!
                            // well, extreme circumstances. Let's use literals to generate those values.
                            if (double.IsPositiveInfinity(doubleValue))
                            {
                                // 1 divided by zero is +Infinity
                                return "(1/0)";
                            }
                            if (double.IsNegativeInfinity(doubleValue))
                            {
                                // 1 divided by negative zero is -Infinity
                                return "(1/-0)";
                            }
                            // the unary plus converts to a number, and "x" will generate NaN
                            return "(+'x')";
                        }
                    }
                }

                // we're good to go -- just return the name because it will resolve to the
                // global properties (make a special case for negative infinity)
                return double.IsNegativeInfinity(doubleValue) ? "-Infinity" : objectName;
            }
            else if (doubleValue == 0)
            {
                // special case zero because we don't need to go through all those
                // gyrations to get a "0" -- and because negative zero is different
                // than a positive zero
                return IsNegativeZero ? "-0" : "0";
            }
            else
            {
                // normal string representations
                string normal = GetSmallestRep(doubleValue.ToString("R", CultureInfo.InvariantCulture));

                // if this is an integer (no decimal portion)....
                if (Math.Floor(doubleValue) == doubleValue)
                {
                    // then convert to hex and see if it's smaller.
                    // only really big numbers might be smaller in hex.
                    string hex = NormalOrHexIfSmaller(doubleValue, normal);
                    if (hex.Length < normal.Length)
                    {
                        normal = hex;
                    }
                }
                return normal;
            }
        }

        private static string GetSmallestRep(string number)
        {
            Match match = s_decimalFormat.Match(number);
            if (match.Success)
            {
                string mantissa = match.Result("${man}");
                if (string.IsNullOrEmpty(match.Result("${exp}")))
                {
                    if (string.IsNullOrEmpty(mantissa))
                    {
                        // no decimal portion
                        if (string.IsNullOrEmpty(match.Result("${sig}")))
                        {
                            // no non-zero digits in the magnitude either -- must be a zero
                            number = match.Result("${neg}") + "0";
                        }
                        else
                        {
                            // see if there are trailing zeros
                            // that we can use e-notation to make smaller
                            int numZeros = match.Result("${zer}").Length;
                            if (numZeros > 2)
                            {
                                number = match.Result("${neg}") + match.Result("${sig}")
                                    + 'e' + numZeros.ToString(CultureInfo.InvariantCulture);
                            }
                        }
                    }
                    else
                    {
                        // there is a decimal portion. Put it back together
                        // with the bare-minimum stuff -- no plus-sign, no leading magnitude zeros,
                        // no trailing mantissa zeros. A zero magnitude won't show up, either.
                        number = match.Result("${neg}") + match.Result("${mag}") + '.' + mantissa;
                    }
                }
                else if (string.IsNullOrEmpty(mantissa))
                {
                    // there is an exponent, but no significant mantissa
                    number = match.Result("${neg}") + match.Result("${mag}")
                        + "e" + match.Result("${eng}") + match.Result("${pow}");
                }
                else
                {
                    // there is an exponent and a significant mantissa
                    // we want to see if we can eliminate it and save some bytes

                    // get the integer value of the exponent
                    int exponent;
                    if (int.TryParse(match.Result("${eng}") + match.Result("${pow}"), NumberStyles.Integer, CultureInfo.InvariantCulture, out exponent))
                    {
                        // slap the mantissa directly to the magnitude without a decimal point.
                        // we'll subtract the number of characters we just added to the magnitude from
                        // the exponent
                        number = match.Result("${neg}") + match.Result("${mag}") + mantissa
                            + 'e' + (exponent - mantissa.Length).ToString(CultureInfo.InvariantCulture);
                    }
                    else
                    {
                        // should n't get here, but it we do, go with what we have
                        number = match.Result("${neg}") + match.Result("${mag}") + '.' + mantissa
                            + 'e' + match.Result("${eng}") + match.Result("${pow}");
                    }
                }
            }
            return number;
        }

        private static string NormalOrHexIfSmaller(double doubleValue, string normal)
        {
            // keep track of the maximum number of characters we can have in our
            // hexadecimal number before it'd be longer than the normal version.
            // subtract two characters for the 0x
            int maxValue = normal.Length - 2;

            int sign = Math.Sign(doubleValue);
            if (sign < 0)
            {
                // negate the value so it's positive
                doubleValue = -doubleValue;
                // subtract another character for the minus sign
                --maxValue;
            }

            // we don't want to get larger -- or even the same size, so we know
            // the maximum length is the length of the normal string less one
            char[] charArray = new char[normal.Length - 1];
            // point PAST the last character in the array because we will decrement
            // the position before we add a character. that way position will always
            // point to the first valid character in the array.
            int position = charArray.Length;

            while (maxValue > 0 && doubleValue > 0)
            {
                // get the right-most hex character
                int digit = (int)(doubleValue % 16);

                // if the digit is less than ten, then we want to add it to '0' to get the decimal character.
                // otherwise we want to add (digit - 10) to 'a' to get the alphabetic hex digit
                charArray[--position] = (char)((digit < 10 ? '0' : 'a' - 10) + digit);

                // next character
                doubleValue = Math.Floor(doubleValue / 16);
                --maxValue;
            }

            // if the max value is still greater than zero, then the hex value
            // will be shorter than the normal value and we want to go with it
            if (maxValue > 0)
            {
                // add the 0x prefix
                charArray[--position] = 'x';
                charArray[--position] = '0';

                // add the sign if negative
                if (sign < 0)
                {
                    charArray[--position] = '-';
                }

                // create a new string starting at the current position
                normal = new string(charArray, position, charArray.Length - position);
            }
            return normal;
        }

        private static void AddEscape(string unescapedRun, string escapedText, ref StringBuilder sb)
        {
            // if we haven't yet created the string builder, do it now
            if (sb == null)
            {
                sb = new StringBuilder();
            }

            // add the run of unescaped text (if any), followed by the escaped text
            sb.Append(unescapedRun);
            sb.Append(escapedText);
        }

        public static string EscapeString(string text, bool isRegExp, bool useW3Strict)
        {
            // see which kind of delimiter we need.
            // if it's okay to use double-quotes, use them. Otherwise use single-quotes
            char delimiter = (OkayToDoubleQuote(text) ? '"' : '\'');

            // don't create the string builder until we actually need it
            StringBuilder sb = null;

            int startOfStretch = 0;
            if (!string.IsNullOrEmpty(text))
            {
                for (int ndx = 0; ndx < text.Length; ++ndx)
                {
                    char c = text[ndx];
                    switch (c)
                    {
                        // explicit escape sequences
                        // if this is for a string parameter to a RegExp object, then we want to use
                        // explicit hex-values, not the escape sequences
                        case '\b':
                            AddEscape(text.Substring(startOfStretch, ndx - startOfStretch), isRegExp ? @"\x08" : @"\b", ref sb);
                            startOfStretch = ndx + 1;
                            break;

                        case '\t':
                            AddEscape(text.Substring(startOfStretch, ndx - startOfStretch), isRegExp ? @"\x09" : @"\t", ref sb);
                            startOfStretch = ndx + 1;
                            break;

                        case '\n':
                            AddEscape(text.Substring(startOfStretch, ndx - startOfStretch), isRegExp ? @"\x0a" : @"\n", ref sb);
                            startOfStretch = ndx + 1;
                            break;

                        case '\v':
                            if (!useW3Strict)
                            {
                                goto default;
                            }

                            AddEscape(text.Substring(startOfStretch, ndx - startOfStretch), isRegExp ? @"\x0b" : @"\v", ref sb);
                            startOfStretch = ndx + 1;
                            break;

                        case '\f':
                            AddEscape(text.Substring(startOfStretch, ndx - startOfStretch), isRegExp ? @"\x0c" : @"\f", ref sb);
                            startOfStretch = ndx + 1;
                            break;

                        case '\r':
                            AddEscape(text.Substring(startOfStretch, ndx - startOfStretch), isRegExp ? @"\x0d" : @"\r", ref sb);
                            startOfStretch = ndx + 1;
                            break;

                        case '\\':
                            AddEscape(text.Substring(startOfStretch, ndx - startOfStretch), @"\\", ref sb);
                            startOfStretch = ndx + 1;
                            break;

                        case '\'':
                        case '"':
                            // whichever character we're using as the delimiter, we need
                            // to escape inside the string
                            if (delimiter == c)
                            {
                                AddEscape(text.Substring(startOfStretch, ndx - startOfStretch), "\\", ref sb);
                                sb.Append(c);
                                startOfStretch = ndx + 1;
                            }

                            // otherwise, we're going to output the character as-is, so just keep going
                            break;

                        case '\x2028':
                        case '\x2029':
                            // issue #14398 - unescaped, these characters (Unicode LineSeparator and ParagraphSeparator)
                            // would introduce a line-break in the string.  they ALWAYS need to be escaped, 
                            // no matter what output encoding we may use.
                            AddEscape(text.Substring(startOfStretch, ndx - startOfStretch), @"\u", ref sb);
                            sb.AppendFormat(CultureInfo.InvariantCulture, "{0:x}", (int)c);
                            startOfStretch = ndx + 1;
                            break;

                        default:
                            if (' ' <= c && c <= 0x7e)
                            {
                                // regular ascii character
                                break;
                            }

                            if (c < ' ')
                            {
                                if (isRegExp)
                                {
                                    // for regular expression strings, \1 through \9 are always backreferences, 
                                    // and \10 through \40 are backreferences if they correspond to existing 
                                    // backreference groups. So we can't use octal for the characters with values
                                    // between 0 and 31. encode with a hexadecimal escape sequence
                                    AddEscape(text.Substring(startOfStretch, ndx - startOfStretch), string.Format(CultureInfo.InvariantCulture, "\\x{0:x2}", (int)c), ref sb);
                                    startOfStretch = ndx + 1;
                                }
                                else
                                {
                                    // we're not a regular expression string. And character with a value between 
                                    // 0 and 31 can be represented in octal with two to three characters (\0 - \37),
                                    // whereas it would always take four characters to do it in hex: \x00 - \x1f.
                                    // so let's go with octal
                                    AddEscape(text.Substring(startOfStretch, ndx - startOfStretch), "\\", ref sb);
                                    int intValue = (int)c;
                                    if (intValue < 8)
                                    {
                                        // single octal digit
                                        sb.Append(intValue.ToString(CultureInfo.InvariantCulture));
                                    }
                                    else
                                    {
                                        // two octal digits
                                        sb.Append((intValue / 8).ToString(CultureInfo.InvariantCulture));
                                        sb.Append((intValue % 8).ToString(CultureInfo.InvariantCulture));
                                    }

                                    startOfStretch = ndx + 1;
                                }
                            }

                            break;
                    }
                }
            }

            string escapedString;
            if (sb == null || string.IsNullOrEmpty(text))
            {
                // didn't escape any characters -- can use the string unchanged
                escapedString = text ?? string.Empty;
            }
            else
            {
                // escaped characters. If there are still unescaped characters left at the
                // end of the string, add them to the builder now
                if (startOfStretch < text.Length)
                {
                    sb.Append(text.Substring(startOfStretch));
                }

                // get the escaped string
                escapedString = sb.ToString();
            }

            // close the delimiter and return the fully-escaped string
            return delimiter + escapedString + delimiter;
        }

        private static bool OkayToDoubleQuote(string text)
        {
            int numberOfQuotes = 0;
            int numberOfApostrophes = 0;
            for (int ndx = 0; ndx < text.Length; ++ndx)
            {
                switch (text[ndx])
                {
                    case '"': 
                        ++numberOfQuotes; 
                        break;
                    case '\'': 
                        ++numberOfApostrophes; 
                        break;
                }
            }

            return numberOfQuotes <= numberOfApostrophes;
        }

        public double ToNumber()
        {
            switch(PrimitiveType)
            {
                case PrimitiveType.Number:
                    // pass-through the double as-is
                    return (double)Value;

                case PrimitiveType.Null:
                    // converting null to a number returns +0
                    return 0;

                case PrimitiveType.Boolean:
                    // converting boolean to number: true is 1, false is +0
                    return (bool)Value ? 1 : 0;

                case PrimitiveType.Other:
                    // don't convert others to numbers
                    throw new InvalidCastException("Cannot convert 'other' primitives to number");

                default:
                    // otherwise this must be a string
                    try
                    {
                        string stringValue = Value.ToString();
                        if (stringValue == null || string.IsNullOrEmpty(stringValue.Trim()))
                        {
                            // empty string or string of nothing but whitespace returns +0
                            return 0;
                        }

                        // see if this is a hex number representation
                        Match match = s_hexNumberFormat.Match(stringValue);
                        if (match.Success)
                        {
                            // if we matched a sign, then we are in a cross-browser gray area.
                            // the ECMA spec says that isn't allowed. IE and Safari correctly return NaN.
                            // But Firefox, Opera, and Chrome will apply the sign to the parsed hex value.
                            if (!string.IsNullOrEmpty(match.Result("${sign}")))
                            {
                                throw new InvalidCastException("Cross-browser error converting signed hex string to number");
                            }

                            // parse the hexadecimal digits portion
                            // can't use NumberStyles.HexNumber in double.Parse, so we need to do the conversion manually
                            //return double.Parse(match.Result("${hex}"), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                            double doubleValue = 0;
                            string hexRep = match.Result("${hex}");

                            // loop from the start of the string to the end, converting the hex digits to a binary
                            // value. As soon as we hit an overflow condition, we can bail.
                            for (int ndx = 0; ndx < hexRep.Length && !double.IsInfinity(doubleValue); ++ndx)
                            {
                                // we already know from the regular expression match that the hex rep is ONLY
                                // 0-9, a-f or A-F, so we don't need to test for outside those ranges.
                                char ch = hexRep[ndx];
                                doubleValue = (doubleValue * 16) + (ch <= '9' ? ch & 0xf : (ch & 0xf) + 9);
                            }
                            return doubleValue;
                        }
                        else
                        {
                            // not a hex number -- try doing a regular decimal float conversion
                            return double.Parse(stringValue, NumberStyles.Float, CultureInfo.InvariantCulture);
                        }
                    }
                    catch (FormatException)
                    {
                        // string isn't a number, return NaN
                        return double.NaN;
                    }
                    catch (OverflowException)
                    {
                        // if the string starts with optional white-space followed by a minus sign,
                        // then it's a negative-infinity overflow. Otherwise it's a positive infinity overflow.
                        Regex negativeSign = new Regex(@"^\s*-");
                        return (negativeSign.IsMatch(Value.ToString()))
                            ? double.NegativeInfinity
                            : double.PositiveInfinity;
                    }
            }
        }

        public bool IsOkayToCombine
        {
            get
            {
                // basically, if it's a real number or an integer not in the range
                // where all integers can be exactly represented by a double,
                // then we don't want to combine them.
                return !IsNumericLiteral || NumberIsOkayToCombine((double)Value);
            }
        }

        static public bool NumberIsOkayToCombine(double doubleValue)
        {
            return (double.IsNaN(doubleValue) || double.IsInfinity(doubleValue)) ||
                (-0x20000000000000 <= doubleValue && doubleValue <= 0x20000000000000
                && Math.Floor(doubleValue) == doubleValue);
        }

        public bool IsNotOneOrPositiveZero
        {
            get
            {
                // 1 or +0 must be a numeric value
                if (IsNumericLiteral)
                {
                    // get the value as a double
                    double numericValue = (double)Value;

                    // if it's one, or if we are equal to zero but NOT -0,
                    // the we ARE 1 or +0, and we return false
                    if (numericValue == 1
                        || (numericValue == 0 && !IsNegativeZero))
                    {
                        return false;
                    }
                }
                // if we get here, we're NOT 1 or +0
                return true;
            }
        }

        public bool IsNegativeZero
        {
            get
            {
                // must be a numeric value, and +0 and -0 are both equal to zero.
                if (IsNumericLiteral && (double)Value == 0)
                {
                    // division by zero produces positive infinity if +0 and negative inifinity if -0
                    return 1 / ((double)Value) < 0;
                }

                // either not a number, or not zero
                return false;
            }
        }

        internal double ToInteger()
        {
            double value = ToNumber();
            if (double.IsNaN(value))
            {
                // NaN returns +0
                return 0;
            }
            if (value == 0 || double.IsInfinity(value))
            {
                // +0, -0, +Infinity and -Infinity return themselves unchanged
                return value;
            }
            return Math.Sign(value) * Math.Floor(Math.Abs(value));
        }

        internal Int32 ToInt32()
        {
            double value = ToNumber();

            if (Math.Floor(value) != value
                || value < Int32.MinValue || Int32.MaxValue < value)
            {
                // some versions of JavaScript return NaN if the value is not an
                // integer in the signed 32-bit range. Therefore if we aren't an
                // integer value in the proper range, we bail
                throw new InvalidCastException("Not an integer in the appropriate range; cross-browser issue");
            }

            if (value == 0 || double.IsNaN(value) || double.IsInfinity(value))
            {
                // +0, -0, NaN, +Infinity and -Infinity all return +0
                return 0;
            }

            // get the integer value, then MOD it with 2^32 to restrict to an unsigned 32-bit range.
            // and then check that top bit to see if the value should be negative or not;
            // if so, subtract 2^32 to get the negative value.
            long int64bit = (Convert.ToInt64(value) % 0x100000000);
            return Convert.ToInt32(int64bit >= 0x80000000 ? int64bit - 0x100000000 : int64bit);
        }

        internal UInt32 ToUInt32()
        {
            double value = ToNumber();

            if (Math.Floor(value) != value
                || value < UInt32.MinValue || UInt32.MaxValue < value)
            {
                // some versions of JavaScript return NaN if the value is not an
                // integer in the unsigned 32-bit range. Therefore if we aren't an
                // integer value in the proper range, we bail
                throw new InvalidCastException("Not an integer in the appropriate range; cross-browser issue");
            }

            if (value == 0 || double.IsNaN(value) || double.IsInfinity(value))
            {
                // +0, -0, NaN, +Infinity and -Infinity all return +0
                return 0;
            }

            // get the integer value, then MOD it with 2^32 to restrict to an unsigned 32-bit range.
            long int64bit = Convert.ToInt64(value);
            return (UInt32)(int64bit & 0xffffffff);
        }

        public bool ToBoolean()
        {
            switch (PrimitiveType)
            {
                case PrimitiveType.Null:
                    // null converts to false
                    return false;

                case PrimitiveType.Boolean:
                    // boolean is just whatever the value is (cast to bool)
                    return (bool)Value;

                case PrimitiveType.Number:
                    {
                        // numeric: false if zero or NaN; otherwise true
                        double doubleValue = (double)Value;
                        return !(doubleValue == 0 || double.IsNaN(doubleValue));
                    }

                case PrimitiveType.Other:
                    throw new InvalidCastException("Cannot convert 'other' primitive types to boolean");

                default:
                    // string or other: false if empty; otherwise true
                    // (we already know the value is not null)
                    return !string.IsNullOrEmpty(Value.ToString());
            }
        }

        public override string ToString()
        {
            // this function returns the STRING representation
            // of this primitive value -- NOT the same as the CODE representation
            // of this AST node.
            switch (PrimitiveType)
            {
                case PrimitiveType.Null:
                    // null is just "null"
                    return "null";

                case PrimitiveType.Boolean:
                    // boolean is "true" or "false"
                    return (bool)Value ? "true" : "false";

                case PrimitiveType.Number:
                    {
                        // handle some special values, otherwise just fall through
                        // to the default ToString implementation
                        double doubleValue = (double)Value;
                        if (doubleValue == 0)
                        {
                            // both -0 and 0 return "0". Go figure.
                            return "0";
                        }
                        if (double.IsNaN(doubleValue))
                        {
                            return "NaN";
                        }
                        if (double.IsPositiveInfinity(doubleValue))
                        {
                            return "Infinity";
                        }
                        if (double.IsNegativeInfinity(doubleValue))
                        {
                            return "-Infinity";
                        }
                    }
                    break;
            }

            // otherwise this must be a string or a regular double
            return Value.ToString();
        }
    }

    public enum PrimitiveType
    {
        Null = 0,
        Boolean,
        Number,
        String,
        Other
    }
}
