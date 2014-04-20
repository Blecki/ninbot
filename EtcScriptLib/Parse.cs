﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace EtcScriptLib
{
    public class Parse
    {
		private static bool IsEndOfStatement(Iterator<Token> Stream)
		{
			if (Stream.AtEnd()) return true;
			return Stream.Next().Type == TokenType.Semicolon;
		}

		private static Ast.Let ParseLetStatement(
			Iterator<Token> Stream,
			ParseContext Context)
		{
			var start = Stream.Next();

			if (Stream.AtEnd() || Stream.Next().Type != TokenType.Identifier || Stream.Next().Value.ToUpper() != "LET")
				throw new CompileError("[000] Impossible error: Let parse entered, no let found.", Stream);

			Stream.Advance();
			
			var LHS = ParseTerm(Stream, Context);

			if (Stream.Next().Type != TokenType.Operator || Stream.Next().Value != "=")
				throw new CompileError("[001] Expected '='", Stream);
			Stream.Advance();

			var RHS = ParseExpression(Stream, Context, TokenType.Semicolon);

			if (!IsEndOfStatement(Stream)) throw new CompileError("[002] Expected ;", Stream);
			Stream.Advance();
			return new Ast.Let(start, LHS, RHS);
		}

		private static Ast.LocalDeclaration ParseLocalDeclaration(
			Iterator<Token> Stream,
			ParseContext Context)
		{
			var start = Stream.Next();

			Stream.Advance();

			if (Stream.Next().Type != TokenType.Identifier)
				throw new CompileError("[003] Expected identifier", Stream);

			var r = new Ast.LocalDeclaration(start);
			r.Name = Stream.Next().Value;
			//r.Typename = "GENERIC";
			Stream.Advance();

			if (Stream.Next().Type == TokenType.Colon)
			{
				Stream.Advance();
				if (Stream.Next().Type != TokenType.Identifier)
					throw new CompileError("[004] Expected identifier", Stream);
				r.Typename = Stream.Next().Value.ToUpper();
				Stream.Advance();
			}

			if (Stream.Next().Type == TokenType.Semicolon)
			{
				Stream.Advance();
				return r;
			}
			else if (Stream.Next().Type == TokenType.Operator && Stream.Next().Value == "=")
			{
				Stream.Advance();
				r.Value = ParseExpression(Stream, Context, TokenType.Semicolon);
				if (!IsEndOfStatement(Stream)) throw new CompileError("[005] Expected ;", Stream);
				Stream.Advance();
				return r;
			}
			throw new CompileError("[006] Expected ; or =", Stream);
		}

		private static Ast.Return ParseReturnStatement(
			Iterator<Token> Stream,
			ParseContext Context)
		{
			var start = Stream.Next();

			if (Stream.AtEnd() || Stream.Next().Type != TokenType.Identifier || Stream.Next().Value.ToUpper() != "RETURN")
				throw new CompileError("[007] Impossible error: Return parse entered, no return found.", Stream);

			Stream.Advance();

			var r = new Ast.Return(start);
			if (Stream.Next().Type != TokenType.Semicolon)
				r.Value = ParseExpression(Stream, Context, TokenType.Semicolon);

			if (Stream.Next().Type != TokenType.Semicolon) throw new CompileError("[008] Expected ;", Stream);
			Stream.Advance();
			return r;
		}

		private static Ast.If ParseIfStatement(
			Iterator<Token> Stream,
			ParseContext Context)
		{
			if (Stream.AtEnd() || Stream.Next().Type != TokenType.Identifier || Stream.Next().Value.ToUpper() != "IF")
				throw new CompileError("[009] Impossible error: If parse entered, no if found.", Stream);
			var r = new Ast.If(Stream.Next());

			Stream.Advance();  //Skip 'if'
			r.Header = ParseExpression(Stream, Context, TokenType.OpenBrace);

			if (Stream.Next().Type != TokenType.OpenBrace) throw new CompileError("[00A] Expected {", Stream);
			
			r.ThenBlock = ParseBlock(Stream, Context);

			if (Stream.Next().Type == TokenType.Identifier && Stream.Next().Value.ToUpper() == "ELSE")
			{
				Stream.Advance();
				if (Stream.Next().Type == TokenType.OpenBrace)
					r.ElseBlock = ParseBlock(Stream, Context);
				else if (Stream.Next().Type == TokenType.Identifier && Stream.Next().Value.ToUpper() == "IF")
					r.ElseBlock = ParseIfStatement(Stream, Context);
			}

			return r;
		}

		private static Ast.Statement ParseStatement(
			Iterator<Token> Stream,
			ParseContext Context)
		{
			Ast.Statement r = null;

			var firstToken = Stream.Next().Value.ToUpper();

			if (firstToken == "LET")
			{
				r = ParseLetStatement(Stream, Context);
			}
			else if (firstToken == "IF")
			{
				r = ParseIfStatement(Stream, Context);
			}
			else if (firstToken == "RETURN")
			{
				r = ParseReturnStatement(Stream, Context);
			}
			else if (firstToken == "VAR" || firstToken == "VARIABLE")
			{
				r = ParseLocalDeclaration(Stream, Context);
			}
			else if (Stream.Next().Type == TokenType.Colon)
			{
				Stream.Advance();
				var parameters = ParseDynamicInvokation(Stream, Context);
				r = new Ast.CompatibleCall(parameters[0].Source, new Ast.AssembleList(new Token(), parameters));
				if (Stream.Next().Type != TokenType.Semicolon) throw new CompileError("[00B] Expected ;", Stream);
				Stream.Advance();
			}
			else
			{
				//If it's not any special statement, it must be a function call.
				var parameters = ParseStaticInvokationStatement(Stream, Context);
				var control = Context.FindControl(parameters);
				if (control != null)
				{
					Ast.Node childBlock = null;
					if (control.BlockType == ControlBlockType.RequiredBlock)
					{
						if (Stream.Next().Type == TokenType.OpenBrace)
							childBlock = ParseBlock(Stream, Context);
						else
							throw new CompileError("[00C] Expected {", Stream);
					}
					else
					{
						if (Stream.Next().Type != TokenType.Semicolon) throw new CompileError("[00D] Expected ;", Stream);
						Stream.Advance();
					}
					r = new Ast.ControlInvokation(parameters[0].Source, control, parameters, childBlock);
				}
				else
				{
					r = new Ast.StaticInvokation(parameters[0].Source, parameters);
					if (Stream.Next().Type != TokenType.Semicolon) throw new CompileError("[00E] Expected ;", Stream);
					Stream.Advance();
				}
			}
			
			return r;
		}

		private static Ast.Node ParseOptionalDot(
			Iterator<Token> Stream,
			Ast.Node LHS,
			ParseContext Context)
		{
			if (Stream.AtEnd()) return LHS;

			if (Stream.Next().Type == TokenType.Dot)
			{
				Stream.Advance();
				if (Stream.Next().Type != TokenType.Identifier)
					throw new CompileError("[00F] Dot operator must be followed by identifier", Stream);
				var RHS = Stream.Next().Value;
				var MA = new Ast.MemberAccess(LHS.Source);
				MA.Object = LHS;
				MA.Name = RHS.ToUpper();
				Stream.Advance();

				return ParseOptionalDot(Stream, MA, Context);
			}
			else
				return LHS;
		}		

		private static Ast.Node ParseTerm(
			Iterator<Token> Stream,
			ParseContext Context)
		{
			if (IsEndOfStatement(Stream)) throw new CompileError("[012] Expected argument", Stream);
			Ast.Node r = null;
			if (Stream.Next().Type == TokenType.OpenParen)
			{
				Stream.Advance();
				r = ParseExpression(Stream, Context, TokenType.CloseParen);
				Stream.Advance();
				if (!Stream.AtEnd() && Stream.Next().Type == TokenType.Colon)
				{
					Stream.Advance();
					if (Stream.Next().Type != TokenType.Identifier)
						throw new CompileError("[013] Expected identifier", Stream);
					r = new Ast.Cast(Stream.Next(), r, Stream.Next().Value.ToUpper());
					Stream.Advance();
				}
			}
			else if (Stream.Next().Type == TokenType.OpenBracket)
			{
				var parameters = ParseStaticInvokation(Stream, Context);
				r = new Ast.StaticInvokation(parameters[0].Source, parameters);
			}
			else if (Stream.Next().Type == TokenType.Colon)
			{
				Stream.Advance();
				var parameters = ParseDynamicInvokation(Stream, Context);
				r = new Ast.CompatibleCall(parameters[0].Source, new Ast.AssembleList(new Token(), parameters));
			}
			else if (Stream.Next().Type == TokenType.Identifier ||
				Stream.Next().Type == TokenType.Number ||
				Stream.Next().Type == TokenType.String)
			{
				r = new Ast.Identifier(Stream.Next());
				Stream.Advance();
			}
			else if (Stream.Next().Type == TokenType.Operator)
			{
				r = new Ast.Identifier(Stream.Next());
				Stream.Advance();
			}
			else if (Stream.Next().Type == TokenType.At)
			{
				return ParseComplexString(Stream, Context);
			}
			else
				throw new CompileError("[014] Illegal token in argument list", Stream.Next());

			r = ParseOptionalDot(Stream, r, Context);

			return r;
		}

		public static Ast.ComplexString ParseComplexString(Iterator<Token> Stream, ParseContext Context)
		{
			System.Diagnostics.Debug.Assert(Stream.Next().Type == TokenType.At);
			var start = Stream.Next();

			var tokenStream = Stream as TokenStream;
			tokenStream.PushState(TokenStreamState.ComplexString); //Enter complex string parsing mode

			Stream.Advance();
			if (Stream.Next().Type != TokenType.ComplexStringQuote)
				throw new CompileError("[015] Expected \"", Stream);
			Stream.Advance();

			var stringPieces = new List<Ast.Node>();

			while (Stream.Next().Type != TokenType.ComplexStringQuote)
			{
				if (Stream.Next().Type == TokenType.ComplexStringPart)
				{
					stringPieces.Add(new Ast.StringLiteral(Stream.Next(), Stream.Next().Value));
					Stream.Advance();
				}
				else if (Stream.Next().Type == TokenType.OpenBracket)
				{
					tokenStream.PushState(TokenStreamState.Normal); //Make sure we are parsing normally for the
					Stream.Advance(); //Skip the [						//embedded expression
					stringPieces.Add(ParseExpression(Stream, Context, TokenType.CloseBracket));
					if (Stream.Next().Type != TokenType.CloseBracket)	//Shouldn't be possible
						throw new CompileError("[016] Expected ]", Stream);
					tokenStream.PopState();	//Return to complex string parsing mode
					Stream.Advance();
				}
				else
					throw new InvalidProgramException();
			}

			tokenStream.PopState(); //Return to normal parsing mode
			Stream.Advance();

			return new Ast.ComplexString(start, stringPieces);
		}

		public static List<Ast.Node> ParseStaticInvokation(
			Iterator<Token> Stream,
			ParseContext Context)
		{
			if (Stream.Next().Type != TokenType.OpenBracket) throw new CompileError("[017] Expected [", Stream);
			Stream.Advance();

			 var parameters = new List<Ast.Node>();
			 while (true)
			 {
				 if (IsEndOfStatement(Stream)) throw new CompileError("[018] Expected ]", Stream);
				 if (Stream.Next().Type == TokenType.CloseBracket)
				 {
					 Stream.Advance();
					 return parameters;
				 }
				 parameters.Add(ParseTerm(Stream, Context));
			 }
		}

		public static List<Ast.Node> ParseStaticInvokationStatement(
			Iterator<Token> Stream,
			ParseContext Context)
		{
			var parameters = new List<Ast.Node>();
			while (true)
			{
				if (IsEndOfStatement(Stream) || Stream.Next().Type == TokenType.OpenBrace)
				{
					//Why don't we skip the semi-colon? This is called when parsing control macros where it might be optional.
					return parameters;
				}
				parameters.Add(ParseTerm(Stream, Context));
			}
		}

		private static List<Ast.Node> ParseDynamicInvokation(
			Iterator<Token> Stream,
			ParseContext Context)
		{
			if (Stream.Next().Type != TokenType.OpenBracket) throw new CompileError("[019] Expected [", Stream);
			Stream.Advance();
			var parameters = new List<Ast.Node>();
			while (true)
			{
				if (IsEndOfStatement(Stream)) throw new CompileError("[01A] Unexpected end of statement", Stream);
				if (Stream.Next().Type == TokenType.CloseBracket)
				{
					Stream.Advance();
					return parameters;
				}
				else parameters.Add(ParseTerm(Stream, Context));
			}
		}


		//Implements http://en.wikipedia.org/wiki/Operator-precedence_parser
		private static Ast.Node ParseExpression(
			Ast.Node lhs,
			Iterator<Token> state, 
			ParseContext operators,
			int minimum_precedence,
			TokenType terminal)
		{
			while (true)
			{
				if (state.AtEnd() && terminal != TokenType.NewLine)
					throw new CompileError("[01B] Unexpected end of line in expression", lhs.Source);
				if (state.AtEnd() && terminal == TokenType.NewLine) return lhs;
				if (state.Next().Type == terminal) return lhs;
				if (state.Next().Type != TokenType.Operator) throw new CompileError("[01C] Expected operator", state.Next());

				var precedence = operators.FindPrecedence(state.Next().Value);
				if (precedence < minimum_precedence) return lhs;

				var op = state.Next();
				state.Advance();
				var rhs = ParseTerm(state, operators);

				while (true)
				{
					if (state.AtEnd()) break;
					if (state.Next().Type == TokenType.Operator)
					{
						var next_precedence = operators.FindPrecedence(state.Next().Value);
						if (next_precedence > precedence)
							rhs = ParseExpression(rhs, state, operators, next_precedence, terminal);
						else
							break;
					}
					else
						break;
				}

				lhs = new Ast.BinaryOperator(lhs.Source, operators.FindOperator(op.Value).Value.instruction, lhs, rhs);
			}
		}

		private static Ast.Node ParseExpression(
			Iterator<Token> Stream,
			ParseContext Context,
			TokenType terminal)
		{
			if (Stream.Next().Type == TokenType.Identifier && Stream.Next().Value.ToUpper() == "LAMBDA")
			{
				var declaration = ParseMacroDeclaration(Stream, Context);
				declaration.Type = DeclarationType.Lambda;
				return new Ast.Lambda(declaration.Body.Body.Source, declaration, "GENERIC");
			}
			else if (Stream.Next().Type == TokenType.Identifier && Stream.Next().Value.ToUpper() == "NEW")
				return ParseNew(Stream, Context);
			else
				return ParseExpression(ParseTerm(Stream, Context), Stream, Context, 0, terminal);
		}

		private static Ast.New ParseNew(
			Iterator<Token> Stream,
			ParseContext Context)
		{
			Stream.Advance();
			if (Stream.Next().Type != TokenType.Identifier)
				throw new CompileError("[01D] Expected identifier", Stream);
			var r = new Ast.New(Stream.Next());
			r.Typename = Stream.Next().Value.ToUpper();
			Stream.Advance();
			if (Stream.Next().Type == TokenType.OpenBrace)
				r.Initializers = ParseInitializers(Stream, Context);
			return r;
		}

		private static List<Ast.Initializer> ParseInitializers(
			Iterator<Token> Stream,
			ParseContext Context)
		{
			Stream.Advance();
			var r = new List<Ast.Initializer>();
			while (Stream.Next().Type != TokenType.CloseBrace)
			{
				var initializerStart = Stream.Next();
				if (Stream.Next().Type != TokenType.Identifier || Stream.Next().Value.ToUpper() != "LET") 
					throw new CompileError("[01E] Expected let", Stream);
				Stream.Advance();
				if (Stream.Next().Type != TokenType.Identifier) throw new CompileError("[01E] Expected identifier", Stream);
				var memberName = Stream.Next().Value.ToUpper();
				Stream.Advance();
				if (Stream.Next().Type != TokenType.Operator && Stream.Next().Value != "=")
					throw new CompileError("[01F] Expected =", Stream);
				Stream.Advance();
				var value = ParseExpression(Stream, Context, TokenType.Semicolon);
				if (Stream.Next().Type != TokenType.Semicolon) throw new CompileError("[020] Expected ;", Stream);
				Stream.Advance();
				r.Add(new Ast.Initializer(initializerStart, memberName, value));
			}
			Stream.Advance();
			return r;
		}

		private static Ast.BlockStatement ParseBlock(
			Iterator<Token> Stream,
			ParseContext Context)
		{
			var r = new Ast.BlockStatement(Stream.Next());
			Stream.Advance(); //Skip the opening brace.

			while (Stream.Next().Type != TokenType.CloseBrace)
				r.Statements.Add(ParseStatement(Stream, Context));

			Stream.Advance(); //Skip the closing brace.
			return r;
		}
		
		internal static DeclarationTerm ParseDeclarationTerm(Iterator<Token> Stream)
		{
			DeclarationTerm r = null;
			var start = Stream.Next();

			if (Stream.Next().Type == TokenType.Identifier)
			{
				r = new DeclarationTerm
				{
					Name = Stream.Next().Value.ToUpper(),
					Type = DeclarationTermType.Keyword,
					RepetitionType = DeclarationTermRepetitionType.Once
				};
				Stream.Advance();
			}
			else if (Stream.Next().Type == TokenType.OpenParen)
			{
				Stream.Advance();
				if (Stream.Next().Type != TokenType.Identifier)
					throw new CompileError("[021] Expected identifier", start);
				r = new DeclarationTerm
				{
					Name = Stream.Next().Value.ToUpper(),
					Type = DeclarationTermType.Term,
					RepetitionType = DeclarationTermRepetitionType.Once,
					DeclaredTypeName = "GENERIC"
				};
				Stream.Advance();
				if (Stream.Next().Type == TokenType.Colon)
				{
					Stream.Advance();
					if (Stream.Next().Type != TokenType.Identifier)
						throw new CompileError("[022] Expected identifier", start);
					var declaredType = Stream.Next().Value;
					r.DeclaredTypeName = declaredType.ToUpper();
					Stream.Advance();
				}
				if (Stream.Next().Type != TokenType.CloseParen)
					throw new CompileError("[023] Expected )", start);
				Stream.Advance();
			}
			else if (Stream.Next().Type == TokenType.String)
			{
				r = new DeclarationTerm
				{
					Name = Stream.Next().Value.ToUpper(),
					Type = DeclarationTermType.Keyword,
					RepetitionType = DeclarationTermRepetitionType.Once
				};
				Stream.Advance();
			}
			else
				throw new CompileError("[025] Illegal token in declaration header", start);

			if (!Stream.AtEnd())
			{
				if (Stream.Next().Type == TokenType.Operator || Stream.Next().Type == TokenType.QuestionMark)
				{
					var marker = Stream.Next();
					var repetitionMarker = Stream.Next().Value;
					Stream.Advance();
					if (repetitionMarker == "?")
					{
						if (r.Type == DeclarationTermType.Term)
							throw new CompileError("[026] Only keywords can be optional in a declaration header", Stream);
						r.RepetitionType = DeclarationTermRepetitionType.Optional;
					}
					//else if (repetitionMarker == "+")
					//    r.RepetitionType = DeclarationTermRepetitionType.OneOrMany;
					//else if (repetitionMarker == "*")
					//    r.RepetitionType = DeclarationTermRepetitionType.NoneOrMany;
					else
						throw new CompileError("[027] Unrecognized repetition marker on declaration term", marker);
				}
			}

			return r;
		}

		internal enum DeclarationHeaderTerminatorType
		{
			StreamEnd,
			OpenBrace,
			OpenBraceOrWhen
		}

		internal static List<DeclarationTerm> ParseMacroDeclarationHeader(Iterator<Token> Stream, 
			DeclarationHeaderTerminatorType TerminatorType)
		{
			var r = new List<DeclarationTerm>();
			while (true)
			{
				if (TerminatorType == DeclarationHeaderTerminatorType.StreamEnd && Stream.AtEnd()) return r;
				else if (Stream.Next().Type == TokenType.Colon) return r;
				else if (TerminatorType == DeclarationHeaderTerminatorType.OpenBrace && Stream.Next().Type == TokenType.OpenBrace) return r;
				else if (TerminatorType == DeclarationHeaderTerminatorType.OpenBraceOrWhen)
				{
					if (Stream.Next().Type == TokenType.OpenBrace) return r;
					else if (Stream.Next().Value.ToUpper() == "WHEN") return r;
				}
				r.Add(ParseDeclarationTerm(Stream));
			}
		}

		internal static Declaration ParseMacroDeclaration(Iterator<Token> Stream, ParseContext Context)
		{
			if (Stream.AtEnd()) throw new CompileError("[028] Impossible error: ParseDeclaration entered at end of stream.", Stream);

			try
			{
				var r = new Declaration();
				r.ReturnTypeName = "VOID";

				if (Stream.Next().Type != TokenType.Identifier) throw new CompileError("[029] Expected identifier", Stream.Next());

				r.Type = DeclarationType.Macro;
				Stream.Advance();

				r.Terms = ParseMacroDeclarationHeader(Stream, DeclarationHeaderTerminatorType.OpenBrace);

				if (!Stream.AtEnd() && Stream.Next().Type == TokenType.Colon)
				{
					Stream.Advance();
					if (Stream.Next().Type != TokenType.Identifier) throw new CompileError("[02A] Expected identifier", Stream);
					r.ReturnTypeName = Stream.Next().Value.ToUpper();
					Stream.Advance();
				}

				if (!Stream.AtEnd() && Stream.Next().Type == TokenType.OpenBrace)
				{
					r.Body = new LambdaBlock(ParseBlock(Stream, Context));
				}
				else
					throw new CompileError("[02B] Expected block", Stream);
				
				return r;
			}
			catch (CompileError ce)
			{
				throw ce;
			}
			catch (Exception e)
			{
				throw new CompileError(e.Message + e.StackTrace, Stream);
			}
		}

		internal static Type ParseTypeDeclaration(Iterator<Token> Stream, ParseContext Context)
		{
			Stream.Advance();
			var r = new Type();

			if (Stream.Next().Type != TokenType.Identifier) throw new CompileError("[02C] Expected identifier", Stream);
			r.Name = Stream.Next().Value.ToUpper();

			Stream.Advance();
			if (Stream.Next().Type != TokenType.OpenBrace) throw new CompileError("[02D] Expected {", Stream);
			Stream.Advance();

			while (Stream.Next().Type != TokenType.CloseBrace)
			{
				if (Stream.Next().Value.ToUpper() == "VAR" || Stream.Next().Value.ToUpper() == "VARIABLE")
					r.Members.Add(ParseMemberDeclaration(Stream, Context));
				else
					throw new CompileError("[02E] Expected var", Stream);
			}

			Stream.Advance();

			r.AssignMemberOffsets();
			return r;
		}

		internal static Declaration ParseRuleDeclaration(Iterator<Token> Stream, ParseContext Context)
		{
			if (Stream.AtEnd()) throw new CompileError("[02F] Impossible error: ParseRuleDeclaration entered at end of stream.", Stream);

			try
			{
				var r = new Declaration();
				r.ReturnTypeName = "NUMBER";

				if (Stream.Next().Type != TokenType.Identifier) throw new CompileError("[030] Expected identifier", Stream.Next());

				r.Type = DeclarationType.Rule;
				Stream.Advance();

				r.Terms = ParseMacroDeclarationHeader(Stream, DeclarationHeaderTerminatorType.OpenBraceOrWhen);

				if (Stream.Next().Value.ToUpper() == "WHEN")
				{
					Stream.Advance();
					r.WhenClause = new WhenClause(ParseExpression(Stream, Context, TokenType.OpenBrace));
				}

				if (Stream.Next().Type == TokenType.OpenBrace)
				{
					r.Body = new LambdaBlock(ParseBlock(Stream, Context));
					
				}
				else
				{	throw new CompileError("[031] Expected block", Stream);
				}

				return r;
			}
			catch (CompileError ce)
			{
				throw ce;
			}
			catch (Exception e)
			{
				throw new CompileError(e.Message + e.StackTrace, Stream);
			}
		}

		internal static Variable ParseMemberDeclaration(Iterator<Token> Stream, ParseContext Context)
		{
			if (Stream.Next().Type != TokenType.Identifier) throw new CompileError("[032] Expected identifier", Stream.Next());
			Stream.Advance();

			var r = new Variable();
			r.StorageMethod = VariableStorageMethod.Member;

			if (Stream.Next().Type != TokenType.Identifier) throw new CompileError("[033] Expected identifier", Stream.Next());
			r.Name = Stream.Next().Value.ToUpper();

			Stream.Advance();

			if (Stream.Next().Type == TokenType.Colon)
			{
				Stream.Advance();
				if (Stream.Next().Type != TokenType.Identifier) throw new CompileError("[034] Expected identifier", Stream.Next());
				r.DeclaredTypeName = Stream.Next().Value.ToUpper();
				Stream.Advance();
			}
			
			if (Stream.Next().Type != TokenType.Semicolon) throw new CompileError("[035] Expected ;", Stream.Next());
			Stream.Advance();
			return r;
		}

		public static List<Declaration> Build(
			Iterator<Token> Stream, 
			ParseContext Context,
			Func<String,ErrorStrategy> OnError)
        {
			var r = new List<Declaration>();
			while (!Stream.AtEnd())
			{
				try
				{
					if (Stream.Next().Type != TokenType.Identifier) throw new CompileError("[036] Expected identifier", Stream);
					if (Stream.Next().Value.ToUpper() == "MACRO")
					{
						var declaration = ParseMacroDeclaration(Stream, Context);
						declaration.OwnerContextID = Context.ID;
						//Context.ActiveScope.Macros.Add(declaration);
						Context.PendingEmission.Add(declaration);
					}
					else if (Stream.Next().Value.ToUpper() == "TEST")
					{
						var declaration = ParseMacroDeclaration(Stream, Context);
						declaration.OwnerContextID = Context.ID;
						r.Add(declaration);
						Context.PendingEmission.Add(declaration);
					}
					else if (Stream.Next().Value.ToUpper() == "RULE")
					{
						var declaration = ParseRuleDeclaration(Stream, Context);
						declaration.OwnerContextID = Context.ID;
						var rulebook = Context.Rules.FindMatchingRulebook(declaration.Terms);
						if (rulebook == null)
						{
							rulebook = new Rulebook { DeclarationTerms = declaration.Terms };
							Context.Rules.Rulebooks.Add(rulebook);
						}
						if (Declaration.AreTermTypesCompatible(rulebook.DeclarationTerms, declaration.Terms) == false)
							throw new CompileError("[037] Term types are not compatible with existing rulebook", Stream);
						rulebook.Rules.Add(declaration);
						Context.PendingEmission.Add(declaration);
					}
					else if (Stream.Next().Value.ToUpper() == "TYPE")
					{
						var type = ParseTypeDeclaration(Stream, Context);
						if (Context.ActiveScope.FindType(type.Name) != null) throw new CompileError("[038] Type already defined", Stream);
						Context.ActiveScope.Types.Add(type);
					}
					else if (Stream.Next().Value.ToUpper() == "GLOBAL")
					{
						var variable = ParseMemberDeclaration(Stream, Context);
						variable.StorageMethod = VariableStorageMethod.Static;
						Context.ActiveScope.Variables.Add(variable);
					}
					else
						throw new CompileError("[039] Unknown declaration type", Stream);
				}
				catch (Exception e)
				{
					if (OnError(e.Message) == ErrorStrategy.Abort) return r;
					Stream.Advance(); //Prevent an error from causing an infinite loop
				}
			}
			return r;
		}
    }
}