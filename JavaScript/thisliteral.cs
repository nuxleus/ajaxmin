// thisliteral.cs
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
    public sealed class ThisLiteral : AstNode
    {

        public ThisLiteral(Context context, JSParser parser)
            : base(context, parser)
        {
        }

        public override AstNode Clone()
        {
            return new ThisLiteral((Context == null ? null : Context.Clone()), Parser);
        }

        public override string ToCode(ToCodeFormat format)
        {
            return "this";
        }

        internal override void AnalyzeNode()
        {
            // we're going to look for the first FunctionScope on the stack
            FunctionScope functionScope = null;

            // get the current scope
            ActivationObject activationObject = ScopeStack.Peek();
            do
            {
                functionScope = activationObject as FunctionScope;
                if (functionScope != null)
                {
                    // found it -- break out of the loop
                    break;
                }
                // otherwise go up the chain
                activationObject = activationObject.Parent;
            } while (activationObject != null);

            // if we found one....
            if (functionScope != null)
            {
                // add this object to the list of thisliterals
                functionScope.AddThisLiteral(this);
            }
        }
    }
}