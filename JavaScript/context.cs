// context.cs
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

namespace Microsoft.Ajax.Utilities
{
    public class Context
    {
        public DocumentContext Document { get; private set; }

        public int StartLineNumber { get; internal set; }
        public int StartLinePosition { get; internal set; }
        public int StartPosition { get; internal set; }
        public int EndLineNumber { get; internal set; }
        public int EndLinePosition { get; internal set; }
        public int EndPosition { get; internal set; }
        public JSToken Token { get; internal set; }

        private int m_errorReported;

        public Context(JSParser parser)
            : this(new DocumentContext(parser, "[generated code]"))
        {
        }

        public Context(DocumentContext document)
        {
            if (document == null)
            {
                throw new ArgumentNullException("document");
            }

            Document = document;
            StartLineNumber = 1;
            EndLineNumber = 1;
            EndPosition = (Document.Source == null) ? -1 : Document.Source.Length;
            Token = JSToken.None;
            m_errorReported = 1000000;
        }

        public Context(DocumentContext document, int lineNumber, int startLinePos, int startPos, int endLineNumber,
                         int endLinePos, int endPos, JSToken token)
        {
            Document = document;
            StartLineNumber = lineNumber;
            StartLinePosition = startLinePos;
            StartPosition = startPos;
            EndLineNumber = endLineNumber;
            EndLinePosition = endLinePos;
            EndPosition = endPos;
            Token = token;
            m_errorReported = 1000000;
        }

        public Context Clone()
        {
            Context context = new Context(Document, StartLineNumber, StartLinePosition, StartPosition,
                               EndLineNumber, EndLinePosition, EndPosition, Token);
            context.m_errorReported = m_errorReported;
            return context;
        }

        public Context CombineWith(Context other)
        {
            return (other == null
              ? this.Clone()
              : new Context(
                Document,
                StartLineNumber,
                StartLinePosition,
                StartPosition,
                other.EndLineNumber,
                other.EndLinePosition,
                other.EndPosition,
                Token
                )
              );
        }

        public int StartColumn
        {
            get
            {
                return StartPosition - StartLinePosition;
            }
        }

        public int EndColumn
        {
            get
            {
                return EndPosition - EndLinePosition;
            }
        }

        public String Code
        {
            get
            {
                return (EndPosition > StartPosition && EndPosition <= Document.Source.Length)
                  ? Document.Source.Substring(StartPosition, EndPosition - StartPosition)
                  : null;
            }
        }

        internal void ReportUndefined(Lookup lookup)
        {
            UndefinedReferenceException ex = new UndefinedReferenceException(lookup, this);
            Document.ReportUndefined(ex);
        }

        internal void HandleError(JSError errorId)
        {
            HandleError(errorId, null, false);
        }

        internal void HandleError(JSError errorId, bool treatAsError)
        {
            HandleError(errorId, null, treatAsError);
        }

        internal void HandleError(JSError errorId, String message, bool treatAsError)
        {
            if ((errorId == JSError.UndeclaredVariable || errorId == JSError.UndeclaredFunction) && Document.HasAlreadySeenErrorFor(Code))
                return;
            JScriptException error = new JScriptException(errorId, this);
            if (message != null)
                error.Value = message;
            if (treatAsError)
                error.IsError = treatAsError;
            int sev = error.Severity;
            if (sev < m_errorReported)
            {
                Document.HandleError(error);
                m_errorReported = sev;
            }
        }

        public void UpdateWith(Context other)
        {
            if (other != null)
            {
                StartPosition = Math.Min(StartPosition, other.StartPosition);
                StartLineNumber = Math.Min(StartLineNumber, other.StartLineNumber);
                StartLinePosition = Math.Min(StartLinePosition, other.StartLinePosition);
                EndPosition = Math.Max(EndPosition, other.EndPosition);
                EndLineNumber = Math.Max(EndLineNumber, other.EndLineNumber);
                EndLinePosition = Math.Max(EndLinePosition, other.EndLinePosition);
            }
        }

        public bool IsBefore(Context other)
        {
            // this context is BEFORE the other context if it starts on an earlier line,
            // OR if it starts on the same line but at an earlier column
            // (or if the other context is null)
            return other == null
                || StartLineNumber < other.StartLineNumber
                || (StartLineNumber == other.StartLineNumber && StartColumn < other.StartColumn);
        }

        public override string ToString()
        {
            return Code;
        }
    }
}