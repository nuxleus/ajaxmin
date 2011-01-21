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
        // this is a regular expression that we'll use to fix up the regular exponent-syntax results
        // to be what we want. For instance, they will use a capital-E -- but we want a lower-case e.
        // and they will use a plus-sign after the E that can be left off. And they might have leading
        // zeros for the exponent value. So for instance, 1.23456E+05 can be changed to 1.23456e5. 
        private static Regex s_exponentReplacement = new Regex(
          "E[+]?(?<s>-?)0*",
          RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
#if !SILVERLIGHT
 | RegexOptions.Compiled
#endif
);
        // this is a regular expression that we'll use to strip a leading "0x" from
        // a string if we are trying to parse it into a number. also removes the leading
        // and trailing spaces, while we're at it.
        private static Regex s_hexNumberFormat = new Regex(
          @"^\s*0X(?<hex>[0-9a-f]+)\s*$",
          RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
#if !SILVERLIGHT
 | RegexOptions.Compiled
#endif
);

        private Object m_value;
        public Object Value
        {
            get { return m_value; }
            set { m_value = value; }
        }

        private bool m_isNumericLiteral;
        public bool IsNumericLiteral
        {
            get { return m_isNumericLiteral; }
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
                    double doubleValue = (double)m_value;
                    isSpecialNumeric = (double.IsNaN(doubleValue) || double.IsInfinity(doubleValue));
                }
                return isSpecialNumeric;
            }
        }

        public ConstantWrapper(Object value, bool isNumericLiteral, Context context, JSParser parser)
            : base(context, parser)
        {
            m_isNumericLiteral = isNumericLiteral;
            // force numerics to be of type double
            m_value = (isNumericLiteral ? (double)value : value);
        }

        public override AstNode Clone()
        {
            return new ConstantWrapper(
                m_value,
                m_isNumericLiteral,
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
            if (m_value == null)
            {
                str = "null";
            }
            else if (m_isNumericLiteral)
            {
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
            }
            else
            {
                Type type = m_value.GetType();
                // not a number -- check a few other types
                if (type == typeof(string))
                {
                    if (Context == null || Parser.Settings.IsModificationAllowed(TreeModifications.MinifyStringLiterals))
                    {
                        // escape the string
                        str = EscapeString(
                            m_value.ToString(),
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
                }
                else if (type == typeof(bool))
                {
                    str = (bool)m_value
                      ? "true"
                      : "false";
                }
                else
                {
                    str = m_value.ToString();
                }
            }
            return str;
        }

        private string NumericToString()
        {
            // numerics are doubles in JavaScript, so force it now as a shortcut
            double doubleValue = (double)m_value;
            if (double.IsNaN(doubleValue) || double.IsInfinity(doubleValue))
            {
                // weird number -- just return the uncrunched source code as-is. 
                // we've should have already thrown an error alerting the developer 
                // to the overflow mistake, and it might alter the code to change the value
                if (Context != null && !string.IsNullOrEmpty(Context.Code))
                {
                    return Context.Code;
                }

                // Hmmm... don't have a context source. 
                // Must be generated. Just generate the proper JS literal.
                //
                // DANGER! If we just output NaN and Infinity and -Infinity, that assumes
                // that there aren't any local variables in this scope chain with that
                // name and we're pulling the global values. Might want to use properties
                // on the Number object!
                if (double.IsPositiveInfinity(doubleValue))
                {
                    return "Infinity";
                }
                if (double.IsNegativeInfinity(doubleValue))
                {
                    return "-Infinity";
                }
                return "NaN";
            }
            else
            {
                // normal string representations
                string normal = StripLeadingZero(doubleValue.ToString("R", CultureInfo.InvariantCulture));
                if (normal.IndexOf("E", StringComparison.OrdinalIgnoreCase) > 0)
                {
                    // already an exponent. If the sign is positive, then we can strip it out.
                    // we want the "E" to be lower-case "e", and we want to leave off the plus in "e+"
                    normal = s_exponentReplacement.Replace(normal, "e$1");
                }
                else
                {
                    // the normal is not an exponent-syntax value already.
                    // check to see if exponent-syntax is shorter
                    string exp = doubleValue.ToString("#.########################e-0", CultureInfo.InvariantCulture);
                    if (exp.Length < normal.Length)
                    {
                        normal = exp;
                    }

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
                }
                return normal;
            }
        }

        private static string StripLeadingZero(string stringValue)
        {
            if (stringValue.StartsWith("0.", StringComparison.Ordinal))
            {
                return stringValue.Substring(1);
            }
            else if (stringValue.StartsWith("-0.", StringComparison.Ordinal))
            {
                return "-" + stringValue.Substring(2);
            }
            return stringValue;
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

            string escapedString;
            if (sb == null)
            {
                // didn't escape any characters -- can use the string unchanged
                escapedString = text;
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
            if (m_isNumericLiteral)
            {
                // pass-through the double as-is
                return (double)m_value;
            }
            if (m_value == null)
            {
                // converting null to a number returns +0
                return 0;
            }
            if (m_value.GetType() == typeof(bool))
            {
                // converting boolean to number: true is 1, false is +0
                return (bool)m_value ? 1 : 0;
            }
            
            // otherwise this must be a string
            try
            {
                string stringValue = m_value.ToString();

                // see if this is a hex number representation
                Match match = s_hexNumberFormat.Match(stringValue);
                if (match.Success)
                {
                    // parse the hexadecimal digits portion
                    return double.Parse(match.Result("${hex}"), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
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
                return (negativeSign.IsMatch(m_value.ToString())) 
                    ? double.NegativeInfinity 
                    : double.PositiveInfinity;
            }
        }

        public bool IsInteger()
        {
            return m_isNumericLiteral ? ToInteger() == (double)m_value : false;
        }

        public double ToInteger()
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
            return Math.Floor(Math.Abs(value)) * Math.Sign(value);
        }

        public double ToInt32()
        {
            double value = ToNumber();
            if (value == 0 || double.IsNaN(value) || double.IsInfinity(value))
            {
                // +0, -0, NaN, +Infinity and -Infinity all return +0
                return 0;
            }

            // get the integer value, then MOD it with 2^32 to restrict to an unsigned 32-bit range.
            // and then check that top bit to see if the value should be negative or not;
            // if so, subtract 2^32 to get the negative value.
            double posInt = Math.Floor(Math.Abs(value)) * Math.Sign(value);
            double int32bit = posInt % 0x100000000;
            return int32bit >= 0x80000000 ? int32bit - 0x100000000 : int32bit;
        }

        public double ToUInt32()
        {
            double value = ToNumber();
            if (value == 0 || double.IsNaN(value) || double.IsInfinity(value))
            {
                // +0, -0, NaN, +Infinity and -Infinity all return +0
                return 0;
            }

            // get the integer value, then MOD it with 2^32 to restrict to an unsigned 32-bit range.
            double posInt = Math.Floor(Math.Abs(value)) * Math.Sign(value);
            return posInt % 0x100000000;
        }

        public double ToUInt16()
        {
            double value = ToNumber();
            if (value == 0 || double.IsNaN(value) || double.IsInfinity(value))
            {
                // +0, -0, NaN, +Infinity and -Infinity all return +0
                return 0;
            }

            // get the integer value, then MOD it with 2^16 to restrict to an unsigned 16-bit range.
            double posInt = Math.Floor(Math.Abs(value)) * Math.Sign(value);
            return posInt % 0x10000;
        }

        public override string ToString()
        {
            // this function returns the STRING representation
            // of this primitive value -- NOT the same as the CODE representation
            // of this AST node.
            if (m_value == null)
            {
                // null is just "null"
                return "null";
            }
            if (m_value.GetType() == typeof(bool))
            {
                // boolean is "true" or "false"
                return (bool)m_value ? "true" : "false";
            }
            if (m_isNumericLiteral)
            {
                // handle some special values, otherwise just fall through
                // to the default ToString implementation
                double doubleValue = (double)m_value;
                if (doubleValue == 0)
                {
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

            // otherwise this must be a string or a regular double
            return m_value.ToString();
        }
    }
}
