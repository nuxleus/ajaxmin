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

		public override AstNode Clone()
		{
			return new AspNetBlockNode((Context == null ? null : Context.Clone()), Parser,
				aspNetBlockText, blockTerminatedByExplicitSemicolon);
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
