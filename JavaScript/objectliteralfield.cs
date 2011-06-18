// objectliteralfield.cs
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
    public class ObjectLiteralField : ConstantWrapper
    {
        public ObjectLiteralField(Object value, PrimitiveType primitiveType, Context context, JSParser parser)
            : base(value, primitiveType, context, parser)
        {
        }

        public override void Accept(IVisitor visitor)
        {
            if (visitor != null)
            {
                visitor.Visit(this);
            }
        }

        public override string ToCode(ToCodeFormat format)
        {
            if (PrimitiveType == PrimitiveType.String
                && Parser.Settings.IsModificationAllowed(TreeModifications.RemoveQuotesFromObjectLiteralNames))
            {
                string rawValue = Value.ToString();

                // if the raw value is safe to be an identifier, then go ahead and ditch the quotes and just output
                // the raw value. Otherwise call ToCode to wrap the string in quotes.
                return JSScanner.IsSafeIdentifier(rawValue) && !JSScanner.IsKeyword(rawValue, EnclosingScope.UseStrict) ? rawValue : base.ToCode(format);
            }
            else
            {
                // call the base to format the value
                return base.ToCode(format);
            }
        }
    }
}
