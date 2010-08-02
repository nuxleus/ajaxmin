// gettersetter.cs
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
using System.Text;

namespace Microsoft.Ajax.Utilities
{

    public sealed class GetterSetter : ObjectLiteralField
    {
        private bool m_isGetter;

        public GetterSetter(String identifier, bool isGetter, Context context, JSParser parser)
            : base(identifier, false, context, parser)
        {
            m_isGetter = isGetter;
        }

        /*
        public override AstNode Clone()
        {
          return new GetterSetter(m_identifier, m_isGetter, Context.Clone(), Parser);
        }
        */

        public override string ToCode(ToCodeFormat format)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(m_isGetter ? "get " : "set ");
            sb.Append(Value);
            return sb.ToString();
        }

        public override String ToString()
        {
            return Value.ToString();
        }
    }
}

