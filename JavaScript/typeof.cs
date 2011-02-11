// typeof.cs
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
    public sealed class TypeOfNode : UnaryOperator
    {
        public TypeOfNode(Context context, JSParser parser, AstNode operand)
            : base(context, parser, operand, JSToken.TypeOf)
        {
        }

        public override AstNode Clone()
        {
            return new TypeOfNode(
                (Context == null ? null : Context.Clone()),
                Parser,
                (Operand == null ? null : Operand.Clone())
                );
        }

        public override string ToCode(ToCodeFormat format)
        {
            string operandString = Operand.ToCode(format);
            if (NeedsParentheses)
            {
                // need parentheses
                return "typeof(" + operandString + ')';
            }
            else if (JSScanner.StartsWithIdentifierPart(operandString))
            {
                // need a space separating them
                return "typeof " + operandString;
            }
            else
            {
                // don't need the space
                return "typeof" + operandString;
            }
        }

        public override void CleanupNodes()
        {
            base.CleanupNodes();

            if (Parser.Settings.EvalLiteralExpressions
                && Parser.Settings.IsModificationAllowed(TreeModifications.EvaluateNumericExpressions))
            {
                // see if our operand is a ConstantWrapper
                ConstantWrapper literalOperand = Operand as ConstantWrapper;
                if (literalOperand != null)
                {
                    // either number, string, boolean, or null.
                    // the operand is a literal. Therefore we already know what the typeof
                    // operator will return. Just short-circuit that behavior now and replace the operator
                    // with a string literal of the proper value
                    string typeName = null;
                    if (literalOperand.IsStringLiteral)
                    {
                        // "string"
                        typeName = "string";
                    }
                    else if (literalOperand.IsNumericLiteral)
                    {
                        // "number"
                        typeName = "number";
                    }
                    else if (literalOperand.IsBooleanLiteral)
                    {
                        // "boolean"
                        typeName = "boolean";
                    }
                    else if (literalOperand.Value == null)
                    {
                        // "object"
                        typeName = "object";
                    }

                    if (!string.IsNullOrEmpty(typeName))
                    {
                        Parent.ReplaceChild(this, new ConstantWrapper(typeName, PrimitiveType.String, Context, Parser));
                    }
                }
            }
        }
    }
}
