// voidop.cs
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
    public sealed class VoidNode : UnaryOperator
    {
        public VoidNode(Context context, JSParser parser, AstNode operand)
            : base(context, parser, operand, JSToken.Void)
        {
        }

        public override AstNode Clone()
        {
            return new VoidNode(
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
                // need parens
                return "void(" + operandString + ')';
            }
            else if (JSScanner.StartsWithIdentifierPart(operandString))
            {
                // need the space
                return "void " + operandString;
            }
            else
            {
                // no separator needed
                return "void" + operandString;
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
                    // the void operator evaluates its operand and returns undefined. Since evaluating a literal
                    // does nothing, then it doesn't matter what the heck it is. Replace it with a zero -- a one-
                    // character literal.
                    ReplaceChild(literalOperand, new ConstantWrapper(0, PrimitiveType.Number, Context, Parser));
                }
            }
        }
    }
}
