using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Ajax.Utilities
{
    public abstract class Expression : AstNode
    {
        protected Expression(Context context, JSParser parser)
            : base(context, parser)
        {
        }

        public override bool IsExpression
        {
            get
            {
                // we're an expression UNLESS we are a directive prologue
                return !IsDirectivePrologue;
            }
        }
    }
}
