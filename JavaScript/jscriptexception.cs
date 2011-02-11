// jscriptexception.cs
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
using System.Runtime.Serialization;
using System.Security;
using System.Security.Permissions;

namespace Microsoft.Ajax.Utilities
{

    //-------------------------------------------------------------------------------------------------------
    // JScriptException
    //
    //  An error in JScript goes to a COM+ host/program in the form of a JScriptException. However a 
    //  JScriptException is not always thrown. In fact a JScriptException is also a IVsaError and thus it can be
    //  passed to the host through IVsaSite.OnCompilerError(IVsaError error).
    //  When a JScriptException is not a wrapper for some other object (usually either a COM+ exception or 
    //  any value thrown in a JScript throw statement) it takes a JSError value.
    //  The JSError enum is defined in JSError.cs. When introducing a new type of error perform
    //  the following four steps:
    //  1- Add the error in the JSError enum (JSError.cs)
    //  2- Update JScript.resx with the US English error message
    //  3- Update Severity.
    //-------------------------------------------------------------------------------------------------------
#if !SILVERLIGHT
    [Serializable]
#endif
    public class JScriptException : Exception
    {
        private Object m_valueObject;
#if !SILVERLIGHT
        [NonSerialized]
#endif
        private Context m_context;
        private bool m_isError;
        private bool m_canRecover = true;
        private int m_code; // This is same as base.HResult. We have this so that the debugger can get the
        // error code without doing a func-eval ( to evaluate the HResult property )
        private string m_fileContext;

        public JScriptException() : this(JSError.UncaughtException, null) { }
        public JScriptException(JSError errorNumber) : this(errorNumber, null) { }
        public JScriptException(string message) : this(message, null) { }

        internal JScriptException(JSError errorNumber, Context context)
        {
            m_valueObject = Missing.Value;
            m_context = (context == null ? null : context.Clone());
            m_fileContext = (context == null ? null : context.FileContext);
            m_code = HResult = unchecked((int)(0x800A0000 + (int)errorNumber));
        }

        public JScriptException(string message, Exception innerException)
            : base(message, innerException)
        {
            m_valueObject = Missing.Value;
            m_code = HResult = unchecked((int)(0x800A0000 + (int)JSError.UncaughtException));
        }

#if !SILVERLIGHT
        protected JScriptException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            if (info == null)
            {
                throw new ArgumentException(StringMgr.GetString("InternalCompilerError"));
            }
            m_code = HResult = info.GetInt32("Code");
            m_valueObject = info.GetValue("Value", typeof(System.Object));
            if (m_valueObject == null)
                m_valueObject = Missing.Value;
            m_isError = info.GetBoolean("IsError");
        }
#endif

        public string FileContext
        {
            get
            {
                return m_fileContext;
            }
        }

        public bool CanRecover
        {
            get { return m_canRecover; }
            set { m_canRecover = value; }
        }

        public bool IsError
        {
            get { return m_isError; }
            set { m_isError = value; }
        }

        public object Value
        {
            get { return m_valueObject; }
            set { m_valueObject = value; }
        }

        public int StartColumn
        {
            get
            {
                return Column;
            }
        }

        public int Line
        {
            get
            {
                if (m_context != null)
                {
                    return m_context.StartLineNumber;
                }
                else
                {
                    return 0;
                }
            }
        }

        public int Column
        {
            get
            {
                if (m_context != null)
                {
                    // one-based column number
                    return m_context.StartColumn + 1;
                }
                else
                {
                    return 0;
                }
            }
        }

        public int EndLine
        {
            get
            {
                if (m_context != null)
                {
                    return m_context.EndLineNumber;
                }
                else
                {
                    return 0;
                }
            }
        }

        public int EndColumn
        {
            get
            {
                if (m_context != null)
                {
                    if (m_context.EndColumn > m_context.StartColumn)
                    {
                        // normal condition - one-based
                        return m_context.EndColumn + 1;
                    }
                    else
                    {
                        // end column before start column -- just set end to be the end of the line
                        return LineText.Length;
                    }
                }
                else
                    return 0;
            }
        }

#if !SILVERLIGHT
        [SecurityCritical] 
        [SecurityPermissionAttribute(SecurityAction.Demand, SerializationFormatter = true)]
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            if (info == null) throw new ArgumentNullException("info");
            base.GetObjectData(info, context);
            info.AddValue("IsError", m_isError);
            info.AddValue("Code", m_code);
            info.AddValue("Value", m_valueObject as Exception);
        }
#endif

        public string FullSource
        {
            get
            {
                return (m_context == null ? string.Empty : m_context.SourceString);
            }
        }

        public String LineText
        {
            get
            {
                string lineText = string.Empty;
                if (m_context != null)
                {
                    int lineStart = m_context.StartLinePosition;
                    string source = m_context.SourceString;

                    if (lineStart < source.Length)
                    {
                        int ndxLF = source.IndexOf('\n', lineStart);
                        if (ndxLF < lineStart)
                        {
                            // no line endings for the rest of the source
                            lineText = source.Substring(lineStart);
                        }
                        else if (ndxLF == lineStart || (ndxLF == lineStart + 1 && source[lineStart] == '\r'))
                        {
                            // blank line
                        }
                        else if (source[ndxLF - 1] == '\r')
                        {
                            // up to CRLF
                            lineText = source.Substring(lineStart, ndxLF - lineStart - 1);
                        }
                        else
                        {
                            // up to LF
                            lineText = source.Substring(lineStart, ndxLF - lineStart);
                        }
                    }
                }
                return lineText;
            }
        }

        public string ErrorSegment
        {
            get
            {
                // just pull out the string that's between start position and end position
                if (m_context.StartPosition >= m_context.SourceString.Length)
                {
                    return string.Empty;
                }
                else
                {
                    int length = m_context.EndPosition - m_context.StartPosition;
                    if (m_context.StartPosition + length <= m_context.SourceString.Length)
                    {
                        return m_context.SourceString.Substring(m_context.StartPosition, length).Trim();
                    }
                    else
                    {
                        return m_context.SourceString.Substring(m_context.StartPosition).Trim();
                    }
                }
            }
        }

        public override String Message
        {
            get
            {
                if (m_valueObject is Exception)
                {
                    Exception e = (Exception)m_valueObject;
                    String result = e.Message;
                    if (result != null && result.Length > 0)
                        return result;
                    else
                    {
                        return e.ToString();
                    }
                }
                String code = (HResult & 0xFFFF).ToString(CultureInfo.InvariantCulture);
                if (m_valueObject is String)
                {
                    switch (((JSError)(HResult & 0xFFFF)))
                    {
                        case JSError.DuplicateName:
                            return StringMgr.GetString(code, (String)m_valueObject);
                        default: return (String)m_valueObject;
                    }
                }
                // special case some errors with contextual information
                if (m_context != null)
                {
                    switch (((JSError)(HResult & 0xFFFF)))
                    {
                        case JSError.AmbiguousCatchVar:
                        case JSError.ArgumentNotReferenced:
                        case JSError.DuplicateName:
                        case JSError.FunctionNotReferenced:
                        case JSError.KeywordUsedAsIdentifier:
                        case JSError.UndeclaredFunction:
                        case JSError.UndeclaredVariable:
                        case JSError.VariableDefinedNotReferenced:
                        case JSError.VariableLeftUninitialized:
                            return StringMgr.GetString(code, m_context.Code);
                    }
                }
                return StringMgr.GetString(((int)(HResult & 0xFFFF)).ToString(CultureInfo.InvariantCulture));
            }
        }

        public int Error
        {
            get { return m_code; }
        }

        public int Severity
        {
            get
            {
                //guide: 0 == there will be a run-time error if this code executes
                //       1 == the programmer probably did not intend to do this
                //       2 == this can lead to problems in the future.
                //       3 == this can lead to performance problems
                //       4 == this is just not right
                int ec = m_code;
                if ((ec & 0xFFFF0000) != 0x800A0000) return 0;
                if (!m_isError)
                {
                    switch ((JSError)(ec & 0xFFFF))
                    {
                        case JSError.AmbiguousCatchVar: return 1;
                        case JSError.AmbiguousNamedFunctionExpression: return 1;
                        case JSError.ArgumentNotReferenced: return 3;
                        case JSError.DuplicateName: return 1;
                        case JSError.FunctionNotReferenced: return 3;
                        case JSError.KeywordUsedAsIdentifier: return 2;
                        case JSError.StatementBlockExpected: return 4;
                        case JSError.SuspectAssignment: return 1;
                        case JSError.SuspectSemicolon: return 2;
                        case JSError.UndeclaredFunction: return 3;
                        case JSError.UndeclaredVariable: return 3;
                        case JSError.VariableLeftUninitialized: return 3;
                        case JSError.VariableDefinedNotReferenced: return 3;
                        case JSError.WithNotRecommended: return 4;
                        case JSError.ObjectConstructorTakesNoArguments: return 4;
                        case JSError.NumericOverflow: return 1;
                        case JSError.NumericMaximum: return 4;
                        case JSError.NumericMinimum: return 4;
                        case JSError.MisplacedFunctionDeclaration: return 2;
                        case JSError.OctalLiteralsDeprecated: return 4;
                    }
                }
                return 0;
            }
        }
    }

    public class JScriptExceptionEventArgs : EventArgs
    {
        private JScriptException m_exception;
        public JScriptException Exception { get { return m_exception; } }

        public JScriptExceptionEventArgs(JScriptException exception)
        {
            m_exception = exception;
        }
    }

#if !SILVERLIGHT
    [Serializable]
#endif
    public class UndefinedReferenceException : Exception
    {
#if !SILVERLIGHT
        [NonSerialized]
#endif
        private Context m_context;

#if !SILVERLIGHT
        [NonSerialized]
#endif
        private Lookup m_lookup;
        public AstNode LookupNode
        {
            get { return m_lookup; }
        }

        private string m_name;
        private ReferenceType m_type;

        public string Name
        {
            get { return m_name; }
        }

        public ReferenceType ReferenceType
        {
            get { return m_type; }
        }

        public int Column
        {
            get
            {
                if (m_context != null)
                {
                    // one-based
                    return m_context.StartColumn + 1;
                }
                else
                {
                    return 0;
                }
            }
        }

        public int Line
        {
            get
            {
                if (m_context != null)
                {
                    return m_context.StartLineNumber;
                }
                else
                {
                    return 0;
                }
            }
        }

        internal UndefinedReferenceException(Lookup lookup, Context context)
        {
            m_lookup = lookup;
            m_name = lookup.Name;
            m_type = lookup.RefType;
            m_context = context;
        }

        public UndefinedReferenceException() : base() { }
        public UndefinedReferenceException(string message) : base(message) { }
        public UndefinedReferenceException(string message, Exception innerException) : base(message, innerException) { }

#if !SILVERLIGHT
        protected UndefinedReferenceException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            if (info == null)
            {
                throw new ArgumentNullException("info");
            }
            m_name = info.GetString("name");
            m_type = (ReferenceType)Enum.Parse(typeof(ReferenceType), info.GetString("type"));
        }

        [SecurityCritical] 
        [SecurityPermission(SecurityAction.Demand, SerializationFormatter = true)]
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            if (info == null)
            {
                throw new ArgumentNullException("info");
            }

            base.GetObjectData(info, context);
            info.AddValue("name", m_name);
            info.AddValue("type", m_type.ToString());
        }
#endif

        public override string ToString()
        {
            return m_name;
        }
    }

    public class UndefinedReferenceEventArgs : EventArgs
    {
        private UndefinedReferenceException m_exception;
        public UndefinedReferenceException Exception { get { return m_exception; } }

        public UndefinedReferenceEventArgs(UndefinedReferenceException exception)
        {
            m_exception = exception;
        }
    }
}
