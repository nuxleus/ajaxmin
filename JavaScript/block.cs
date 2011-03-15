// block.cs
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
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Ajax.Utilities
{

    public sealed class Block : AstNode
    {
        private List<AstNode> m_list;

        public AstNode this[int index]
        {
            get { return m_list[index]; }
        }

        private BlockScope m_blockScope;
        internal BlockScope BlockScope
        {
            get { return m_blockScope; }
            set { m_blockScope = value; }
        }

        public override ActivationObject EnclosingScope
        {
            get
            {
                return m_blockScope != null ? m_blockScope : base.EnclosingScope;
            }
        }

        public Block(Context context, JSParser parser)
            : base(context, parser)
        {
            m_list = new List<AstNode>();
        }

        public override AstNode Clone()
        {
            /*
            BlockScope newBlockScope = null;
            if (m_blockScope != null)
            {
                newBlockScope = m_blockScope.Clone();
                ScopeStack.Push(newBlockScope);
            }
            try
            {
                Block newBlock  = new Block((Context == null ? null : Context.Clone()), Parser);
                newBlock.BlockScope = newBlockScope;
                for(int ndx = 0; ndx < m_list.Count; ++ndx)
                {
                    if (m_list[ndx] != null)
                    {
                        newBlock.Append(m_list[ndx].Clone());
                    }
                }
                return newBlock;
            }
            finally
            {
                if (newBlockScope != null)
                {
                    ScopeStack.Pop();
                }
            }
            */
            throw new NotImplementedException("Block.Clone not implemented");
        }

        internal override bool RequiresSeparator
        {
            get
            {
                // 0 statements, true (lone semicolon)
                // 1 = ask list[0]
                // > 1, false (enclosed in braces
                // if there are 2 or more statements in the block, then
                // we'll wrap them in braces and they won't need a separator
                return (
                  m_list.Count == 0
                  ? true
                  : (m_list.Count == 1 ? m_list[0].RequiresSeparator : false)
                  );
            }
        }

        // if the block has no statements, it ends in an empty block
        internal override bool EndsWithEmptyBlock
        {
            get
            {
                return (m_list.Count == 0);
            }
        }

        internal override bool EncloseBlock(EncloseBlockType type)
        {
            // if there's more than one item, then return false.
            // otherwise recurse the call
            return (m_list.Count == 1 && m_list[0].EncloseBlock(type));
        }

        internal override bool IsDebuggerStatement
        {
            get
            {
                // a block will pop-positive for being a debugger statement
                // if all the statements within it are debugger statements. 
                // So loop through our list, and if any isn't, return false.
                // otherwise return true.
                // empty blocks do not pop positive for "debugger" statements
                if (m_list.Count == 0)
                {
                    return false;
                }

                foreach (AstNode statement in m_list)
                {
                    if (!statement.IsDebuggerStatement)
                    {
                        return false;
                    }
                }
                return true;
            }
        }

        public int Count
        {
            get { return m_list.Count; }
        }

        private void StripDebugStatements()
        {
            // walk the list backwards
            for (int ndx = m_list.Count - 1; ndx >= 0; --ndx)
            {
                // if this item pops positive...
                if (m_list[ndx].IsDebuggerStatement)
                {
                    // just remove it
                    m_list.RemoveAt(ndx);
                }
            }
        }

        // unnest any child blocks
        private void UnnestBlocks()
        {
            // walk the list of items backwards -- if we come
            // to any blocks, unnest the block recursively. We walk backwards because
            // we could be adding any number of statements and we don't want to have
            // to modify the counter
            for (int ndx = m_list.Count - 1; ndx >= 0; --ndx)
            {
                Block nestedBlock = m_list[ndx] as Block;
                if (nestedBlock != null)
                {
                    // unnest recursively
                    nestedBlock.UnnestBlocks();

                    // remove the nested block
                    m_list.RemoveAt(ndx);

                    // then start adding the statements in the nested block to our own.
                    // go backwards so we can just keep using the same index
                    m_list.InsertRange(ndx, nestedBlock.m_list);

                    // make sure the parents are set properly
                    foreach (AstNode insertedNode in nestedBlock.m_list)
                    {
                        insertedNode.Parent = this;
                    }
                }
            }
        }

        internal override void AnalyzeNode()
        {
            // javascript doesn't have block scope, so there really is no point
            // in nesting blocks. Unnest any now, before we start combining var statements
            UnnestBlocks();

            // if we want to remove debug statements...
            if (Parser.Settings.StripDebugStatements && Parser.Settings.IsModificationAllowed(TreeModifications.StripDebugStatements))
            {
                // do it now before we try doing other things
                StripDebugStatements();
            }

            // these variables are used to check for combining a particular type of
            // for-statement with preceding var-statements.
            ForNode targetForNode = null;
            string targetName = null;

            // check to see if we want to combine adjacent var statements
            bool combineVarStatements = Parser.Settings.IsModificationAllowed(TreeModifications.CombineVarStatements);

            // check to see if we want to combine a preceding var with a for-statement
            bool moveVarIntoFor = Parser.Settings.IsModificationAllowed(TreeModifications.MoveVarIntoFor);

            // look at the statements in the block. 
            // if there are multiple var statements adjacent to each other, combine them.
            // walk BACKWARDS down the list because we'll be removing items when we encounter
            // multiple vars.
            // we also don't need to check the first one, since there is nothing before it.
            for (int ndx = m_list.Count - 1; ndx > 0; --ndx)
            {
                // if the previous node is not a Var, then we don't need to try and combine
                // it withthe current node
                Var previousVar = m_list[ndx - 1] as Var;
                if (previousVar != null)
                {
                    // see if THIS item is also a Var...
                    if (m_list[ndx] is Var && combineVarStatements)
                    {
                        // add the items in this VAR to the end of the previous
                        previousVar.Append(m_list[ndx]);

                        // delete this item from the block
                        m_list.RemoveAt(ndx);

                        // if we have a target for-node waiting for another comparison....
                        if (targetForNode != null)
                        {
                            // check to see if the variable we are looking for is in the new list
                            if (previousVar.Contains(targetName))
                            {
                                // IT DOES! we can combine the var statement with the initializer in the for-statement
                                // we already know it's a binaryop, or it wouldn't be a target for-statement
                                BinaryOperator binaryOp = targetForNode.Initializer as BinaryOperator;

                                // create a vardecl that matches our assignment initializer
                                // ignore duplicates because this scope will already have the variable defined.
                                VariableDeclaration varDecl = new VariableDeclaration(
                                    binaryOp.Context.Clone(),
                                    Parser,
                                    targetName,
                                    binaryOp.Operand1.Context.Clone(),
                                    binaryOp.Operand2,
                                    0,
                                    true
                                    );
                                // append it to the preceding var-statement
                                previousVar.Append(varDecl);

                                // move the previous vardecl to our initializer
                                targetForNode.ReplaceChild(targetForNode.Initializer, previousVar);

                                // and remove the previous var from the list.
                                m_list.RemoveAt(ndx - 1);
                                // this will bump the for node up one position in the list, so the next iteration
                                // will be right back on this node, but the initializer will not be null

                                // but now we no longer need the target mechanism -- the for-statement is
                                // not the current node again
                                targetForNode = null;
                            }
                        }
                    }
                    else if (moveVarIntoFor)
                    {
                        // see if this item is a ForNode
                        ForNode forNode = m_list[ndx] as ForNode;
                        if (forNode != null)
                        {
                            // and see if the forNode's initializer is empty
                            if (forNode.Initializer != null)
                            {
                                // not empty -- see if it is a Var node
                                Var varInitializer = forNode.Initializer as Var;
                                if (varInitializer != null)
                                {
                                    // we want to PREPEND the initializers in the previous var statement
                                    // to our for-statement's initializer list
                                    varInitializer.InsertAt(0, previousVar);

                                    // then remove the previous var statement
                                    m_list.RemoveAt(ndx - 1);
                                    // this will bump the for node up one position in the list, so the next iteration
                                    // will be right back on this node in case there are other var statements we need
                                    // to combine
                                }
                                else
                                {
                                    // see if the initializer is a simple assignment
                                    BinaryOperator binaryOp = forNode.Initializer as BinaryOperator;
                                    if (binaryOp != null && binaryOp.OperatorToken == JSToken.Assign)
                                    {
                                        // it is. See if it's a simple lookup
                                        Lookup lookup = binaryOp.Operand1 as Lookup;
                                        if (lookup != null)
                                        {
                                            // it is. see if that variable is in the previous var statement
                                            if (previousVar.Contains(lookup.Name))
                                            {
                                                // create a vardecl that matches our assignment initializer
                                                // ignore duplicates because this scope will already have the variable defined.
                                                VariableDeclaration varDecl = new VariableDeclaration(
                                                    binaryOp.Context.Clone(),
                                                    Parser,
                                                    lookup.Name,
                                                    lookup.Context.Clone(),
                                                    binaryOp.Operand2,
                                                    0,
                                                    true
                                                    );
                                                // append it to the var statement before us
                                                previousVar.Append(varDecl);

                                                // move the previous vardecl to our initializer
                                                forNode.ReplaceChild(forNode.Initializer, previousVar);

                                                // and remove the previous var from the list.
                                                m_list.RemoveAt(ndx - 1);
                                                // this will bump the for node up one position in the list, so the next iteration
                                                // will be right back on this node, but the initializer will not be null
                                            }
                                            else
                                            {
                                                // it's not in the immediately preceding var-statement, but that doesn't mean it won't be in 
                                                // a var-statement immediately preceding that one -- in which case they'll get combined and 
                                                // then it WILL be in the immediately preceding var-statement. So hold on to this
                                                // for statement and we'll check after we do a combine.
                                                targetForNode = forNode;
                                                targetName = lookup.Name;
                                            }
                                        }
                                    }
                                }
                            }
                            else
                            {
                                // if it's empty, then we're free to add the previous var statement
                                // to this for statement's initializer. remove it from it's current
                                // position and add it as the initializer
                                m_list.RemoveAt(ndx - 1);
                                forNode.ReplaceChild(forNode.Initializer, previousVar);
                                // this will bump the for node up one position in the list, so the next iteration
                                // will be right back on this node, but the initializer will not be null
                            }
                        }
                    }
                }
                else
                {
                    // not a var statement. make sure the target for-node is cleared.
                    targetForNode = null;

                    ConditionalCompilationComment previousComment = m_list[ndx - 1] as ConditionalCompilationComment;
                    if (previousComment != null)
                    {
                        ConditionalCompilationComment thisComment = m_list[ndx] as ConditionalCompilationComment;
                        if (thisComment != null)
                        {
                            // two adjacent conditional comments -- combine them into the first.
                            // this will actually make the second block a nested block within the first block,
                            // but they'll be flattened when the comment's block gets recursed.
                            previousComment.Statements.Append(thisComment.Statements);
                            
                            // and remove the second one (which is now a duplicate)
                            m_list.RemoveAt(ndx);
                        }
                    }
                }
            }

            if (m_blockScope != null)
            {
                ScopeStack.Push(m_blockScope);
            }
            try
            {
                // call the base class to recurse
                base.AnalyzeNode();
            }
            finally
            {
                if (m_blockScope != null)
                {
                    ScopeStack.Pop();
                }
            }

            // NOW that we've recursively analyzed all the child nodes in this block, let's see
            // if we can further reduce the statements by checking for a couple good opportunities
            if (Parser.Settings.RemoveUnneededCode)
            {
                // Transform: {var foo=expression;return foo;} to: {return expression;}
                if (m_list.Count == 2 && Parser.Settings.IsModificationAllowed(TreeModifications.VarInitializeReturnToReturnInitializer))
                {
                    Var varStatement = m_list[0] as Var;
                    ReturnNode returnStatement = m_list[1] as ReturnNode;

                    // see if we have two statements in our block: a var with a single declaration, and a return
                    if (returnStatement != null && varStatement != null
                        && varStatement.Count == 1 && varStatement[0].Initializer != null)
                    {
                        // now see if the return is returning a lookup for the same var we are declaring in the
                        // previous statement
                        Lookup lookup = returnStatement.Operand as Lookup;
                        if (lookup != null
                            && string.Compare(lookup.Name, varStatement[0].Identifier, StringComparison.Ordinal) == 0)
                        {
                            // it's a match!
                            // create a combined context starting with the var and adding in the return
                            Context context = varStatement.Context.Clone();
                            context.UpdateWith(returnStatement.Context);

                            // create a new return statement
                            ReturnNode newReturn = new ReturnNode(context, Parser, varStatement[0].Initializer);

                            // clear out the existing statements
                            m_list.Clear();

                            // and add our new one
                            Append(newReturn);
                        }
                    }
                }

                // we do things differently if these statements are the last in a function
                // because we can assume the implicit return
                bool isFunctionLevel = (Parent is FunctionObject);

                // see if we want to change if-statement that forces a return to a return conditional
                if (Parser.Settings.IsModificationAllowed(TreeModifications.IfElseReturnToReturnConditional))
                {
                    // transform: {...; if(cond1)return;} to {...;cond;}
                    // transform: {...; if(cond1)return exp1;else return exp2;} to {...;return cond1?exp1:exp2;}
                    if (m_list.Count >= 1)
                    {
                        // see if the last statement is an if-statement with a true-block containing only one statement
                        IfNode ifStatement = m_list[m_list.Count - 1] as IfNode;
                        if (ifStatement != null
                            && ifStatement.TrueBlock != null)
                        {
                            // see if this if-statement is structured such that we can convert it to a
                            // Conditional node that is the operand of a return statement
                            Conditional returnOperand = ifStatement.CanBeReturnOperand(null, isFunctionLevel);
                            if (returnOperand != null)
                            {
                                // it can! change it.
                                ReturnNode returnNode = new ReturnNode(
                                    (Context == null ? null : Context.Clone()),
                                    Parser,
                                    returnOperand);

                                // replace the if-statement with the return statement
                                ReplaceChild(ifStatement, returnNode);
                            }
                        }
                        // else last statement is not an if-statement, or true block is not a single statement
                    }

                    // transform: {...; if(cond1)return exp1;return exp2;} to {...; return cond1?exp1:exp2;}
                    // my cascade! changing the two statements to a return may cause us to run this again if the
                    // third statement up becomes the penultimate and is an if-statement
                    while (m_list.Count > 1)
                    {
                        int lastIndex = m_list.Count - 1;
                        // end in a return statement?
                        ReturnNode finalReturn = m_list[lastIndex] as ReturnNode;
                        if (finalReturn != null)
                        {
                            // it does -- see if the penultimate statement is an if-block
                            IfNode ifNode = m_list[lastIndex - 1] as IfNode;
                            if (ifNode != null)
                            {
                                // if followed by return. See if the if statement can be changed to a
                                // return of a conditional, using the operand of the following return
                                // as the ultimate expression
                                Conditional returnConditional = ifNode.CanBeReturnOperand(finalReturn.Operand, isFunctionLevel);
                                if (returnConditional != null)
                                {
                                    // it can! so create the new return statement.
                                    // the context of this new return statement should start with a clone of
                                    // the if-statement and updated with the return statement
                                    Context context = ifNode.Context.Clone();
                                    context.UpdateWith(finalReturn.Context);

                                    // create the new return node
                                    ReturnNode newReturn = new ReturnNode(
                                        context,
                                        Parser,
                                        returnConditional);

                                    // remove the last node (the old return)
                                    m_list.RemoveAt(lastIndex--);

                                    // and replace the if-statement with the new return
                                    m_list[lastIndex] = newReturn;
                                    newReturn.Parent = this;

                                    // we collapsed the last two statements, and we KNOW the last one is a
                                    // return -- go back up to the top of the loop to see if we can keep going.
                                    continue;
                                }
                            }
                        }

                        // if we get here, then something went wrong, we didn't collapse the last
                        // two statements, so break out of the loop
                        break;
                    }

                    // now we may have converted the last functional statement 
                    // from if(cond)return expr to return cond?expr:void 0, which is four
                    // extra bytes. So let's check to see if the last statement in the function
                    // now fits this pattern, and if so, change it back.
                    // We didn't just NOT change it in the first place because changing it could've
                    // enabled even more changes that would save a lot more space. But apparently
                    // those subsequent changes didn't pan out.
                    if (m_list.Count >= 1)
                    {
                        int lastIndex = m_list.Count - 1;
                        ReturnNode returnNode = m_list[lastIndex] as ReturnNode;
                        if (returnNode != null)
                        {
                            Conditional conditional = returnNode.Operand as Conditional;
                            if (conditional != null)
                            {
                                VoidNode falseVoid = conditional.FalseExpression as VoidNode;
                                if (falseVoid != null && falseVoid.Operand is ConstantWrapper)
                                {
                                    // we have the required pattern: "return cond?expr:void 0"
                                    // (well, the object of the void is a constant, at least).
                                    // undo it back to "if(cond)return expr" because that takes fewer bytes.

                                    // by default, the operand of the return operator will be the 
                                    // true branch of the conditional
                                    AstNode returnOperand = conditional.TrueExpression;

                                    VoidNode trueVoid = conditional.TrueExpression as VoidNode;
                                    if (trueVoid != null && trueVoid.Operand is ConstantWrapper)
                                    {
                                        // the true branch of the conditional is a void operator acting
                                        // on a constant! So really, there is no operand to the return statement
                                        returnOperand = null;

                                        if (Parser.Settings.IsModificationAllowed(TreeModifications.IfConditionReturnToCondition))
                                        {
                                            // actually, we have return cond?void 0:void 0,
                                            // which would get changed back to function{...;if(cond)return}
                                            // BUT we can just shorten it to function{...;cond}
                                            m_list[lastIndex] = conditional.Condition;
                                            conditional.Condition.Parent = this;
                                            return;
                                        }
                                    }

                                    IfNode ifNode = new IfNode(
                                        returnNode.Context.Clone(),
                                        Parser,
                                        conditional.Condition,
                                        new ReturnNode(returnNode.Context.Clone(), Parser, returnOperand),
                                        null);
                                    m_list[lastIndex] = ifNode;
                                    ifNode.Parent = this;
                                }
                            }
                        }
                    }
                }
            }
        }

        public override IEnumerable<AstNode> Children
        {
            get
            {
                for (int ndx = 0; ndx < m_list.Count; ++ndx)
                {
                    AstNode ast = m_list[ndx];
                    if (ast != null)
                    {
                        yield return ast;
                    }
                }
            }
        }

        public int StatementIndex(AstNode childNode)
        {
            // find childNode in our collection of statements
            for (var ndx = 0; ndx < m_list.Count; ++ndx)
            {
                if (m_list[ndx] == childNode)
                {
                    return ndx;
                }
            }
            // if we got here, then childNode is not a statement in our collection!
            return -1;
        }

        public override bool ReplaceChild(AstNode oldNode, AstNode newNode)
        {
            for (int ndx = m_list.Count - 1; ndx >= 0; --ndx)
            {
                if (m_list[ndx] == oldNode)
                {
                    if (newNode == null)
                    {
                        // just remove it
                        m_list.RemoveAt(ndx);
                    }
                    else
                    {
                        Block newBlock = newNode as Block;
                        if (newBlock != null)
                        {
                            // the new "statement" is a block. That means we need to insert all
                            // the statements from the new block at the location of the old item.
                            m_list.RemoveAt(ndx);
                            m_list.InsertRange(ndx, newBlock.m_list);
                        }
                        else
                        {
                            // not a block -- slap it in there
                            m_list[ndx] = newNode;
                            newNode.Parent = this;
                        }
                    }
                    return true;
                }
            }
            return false;
        }

        public override string ToCode(ToCodeFormat format)
        {
            if (format == ToCodeFormat.NoBraces && m_list.Count == 0)
            {
                return string.Empty;
            }

            StringBuilder sb = new StringBuilder();
            bool closeBrace = false;
            bool unindent = false;

            // if the format is N, then we never enclose in braces.
            // if the format is B or T, then we always enclose in braces.
            // anything else, then we enclose in braces if there's more than
            // one enclosing line
            if (format != ToCodeFormat.NoBraces && Parent != null)
            {
                if (format == ToCodeFormat.AlwaysBraces
                  || format == ToCodeFormat.NestedTry
                  || m_list.Count > 1)
                {
                    // opening brace on a new line
                    Parser.Settings.NewLine(sb);

                    // up the indent level for the content within
                    Parser.Settings.Indent();

                    sb.Append("{");
                    closeBrace = true;
                }
                else if (m_list.Count == 1 && format != ToCodeFormat.ElseIf)
                {
                    // we're pretty-printing a single-line block.
                    // we still won't enclose in brackets, but we need to indent it
                    Parser.Settings.Indent();
                    unindent = true;
                }
            }

            bool requireSeparator = true;
            bool endsWithEmptyBlock = false;
            bool mightNeedSpace = false;
            for (int ndx = 0; ndx < m_list.Count; ++ndx)
            {
                AstNode item = m_list[ndx];
                if (item != null)
                {
                    // see if we need to add a semi-colon
                    if (ndx > 0 && requireSeparator)
                    {
                        sb.Append(';');
                        if (Parser.Settings.OutputMode == OutputMode.SingleLine && item is ImportantComment)
                        {
                            // if this item is an important comment and we're in single-line mode, 
                            // we'll start on a new line
                            sb.Append('\n');
                        }
                        // we no longer require a separator next time around
                        requireSeparator = false;
                    }

                    string itemText = item.ToCode();
                    if (itemText.Length > 0)
                    {
                        // if this is an else-if construct, we don't want to break to a new line.
                        // but all other formats put the next statement on a newline
                        if (format != ToCodeFormat.ElseIf)
                        {
                            Parser.Settings.NewLine(sb);
                        }

                        if (mightNeedSpace && JSScanner.IsValidIdentifierPart(itemText[0]))
                        {
                            sb.Append(' ');
                        }

                        sb.Append(itemText);
                        requireSeparator = (item.RequiresSeparator && !itemText.EndsWith(";", StringComparison.Ordinal));
                        endsWithEmptyBlock = item.EndsWithEmptyBlock;

                        mightNeedSpace =
                            (item is ConditionalCompilationStatement)
                            && JSScanner.IsValidIdentifierPart(itemText[itemText.Length - 1]);
                    }
                }
            }
            if (endsWithEmptyBlock)
            {
                sb.Append(';');
            }

            if (closeBrace)
            {
                // unindent now that the block is done
                Parser.Settings.Unindent();
                Parser.Settings.NewLine(sb);
                sb.Append("}");
            }
            else if (unindent)
            {
                Parser.Settings.Unindent();
            }
            return sb.ToString();
        }

        public void Append(AstNode element)
        {
            if (element != null)
            {
                element.Parent = this;
                m_list.Add(element);
            }
        }

        /* FxCop error -- unreferenced method
        internal void InsertAfter(AstNode after, AstNode item)
        {
          int index = m_list.IndexOf(after);
          if (index >= 0)
          {
            item.Parent = this;
            m_list.Insert(index + 1, item);
          }
        }
        */

        public void Insert(int position, AstNode item)
        {
            if (item != null)
            {
                item.Parent = this;
                m_list.Insert(position, item);
            }
        }

        internal void RemoveLast()
        {
            m_list.RemoveAt(m_list.Count - 1);
        }
    }
}