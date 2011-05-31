using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Ajax.Utilities
{
	public sealed class AspNetBlockNode : AstNode
	{
		private bool blockTerminatedByExplicitSemicolon;
		private string aspNetBlockText;

		public AspNetBlockNode(Context context, JSParser parser, string aspNetBlockText,
			bool blockTerminatedByExplicitSemicolon)
			: base(context, parser)
		{
			this.aspNetBlockText = aspNetBlockText;
			this.blockTerminatedByExplicitSemicolon = blockTerminatedByExplicitSemicolon;
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
			return aspNetBlockText;
		}

		internal override bool RequiresSeparator
		{
			get
			{
				return this.blockTerminatedByExplicitSemicolon;
			}
		}
	}
}
