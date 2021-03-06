﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace EtcScriptLib
{
	public partial class StandardLibrary
	{
		public static void EachXInListWhere(Environment Environment)
		{
			Environment.AddControl(Control.Create(
				Declaration.Parse("each (x) in (list) where (condition)"),
				ControlBlockType.NoBlock,
				(parameters, body) =>
				{
					if (parameters[0] is Ast.Identifier)
					{
						return new EachXInListWhereNode(parameters[0].Source,
							(parameters[0] as Ast.Identifier).Name.Value,
							parameters[1],
							parameters[2]);
					}
					else
						throw new CompileError("Expected identifier", parameters[0].Source);
				}));
		}

		private class EachXInListWhereNode : Ast.Statement
		{
			public String VariableName;
			public Ast.Node List;
			public Ast.Node Indexer;
			public Ast.Node LengthFunc;
			public Ast.Node Condition;

			Variable TotalVariable;
			Variable CounterVariable;
			Variable ListVariable;
			Variable ValueVariable;
			Variable ResultVariable;

			public EachXInListWhereNode(
				Token Source, 
				String VariableName, 
				Ast.Node List, 
				Ast.Node Condition)
				: base(Source)
			{
				this.VariableName = VariableName;
				this.List = List;
				this.Condition = Condition;
			}

			public override Ast.Node Transform(ParseScope Scope)
			{
				List = List.Transform(Scope);
				ResultType = List.ResultType;

				//Try to find an access macro for this type.
				var getterArguments = DummyArguments(Keyword("GET"), Keyword("AT"), Term(Scope.FindType("NUMBER")),
					Keyword("FROM"), Term(List.ResultType));
				var indexerMacro = Scope.FindAllPossibleMacroMatches(getterArguments).Where(d =>
					ExactDummyMatch(d.Terms, getterArguments)).FirstOrDefault();
				if (indexerMacro == null)
					throw new CompileError("No macro of the form GET AT NUMBER FROM " +
						List.ResultType.Name + " found.", Source);

				var lengthArguments = DummyArguments(Keyword("LENGTH"), Keyword("OF"), Term(List.ResultType));
				var lengthMacro = Scope.FindAllPossibleMacroMatches(lengthArguments).Where(d =>
					ExactDummyMatch(d.Terms, lengthArguments)).FirstOrDefault();
				if (lengthMacro == null)
					throw new CompileError("No macro of the form LENGTH OF " + List.ResultType.Name + " found.", Source);

				var nestedScope = Scope.Push(ScopeType.Block);

				ResultVariable = nestedScope.NewLocal("__result@" + VariableName, Scope.FindType("LIST"));
				ListVariable = nestedScope.NewLocal("__list@" + VariableName, Scope.FindType("LIST"));
				TotalVariable = nestedScope.NewLocal("__total@" + VariableName, Scope.FindType("NUMBER"));
				CounterVariable = nestedScope.NewLocal("__counter@" + VariableName, Scope.FindType("NUMBER"));
				ValueVariable = nestedScope.NewLocal(VariableName, indexerMacro.ReturnType);

				Indexer = Ast.StaticInvokation.CreateCorrectInvokationNode(Source, nestedScope, indexerMacro,
					new List<Ast.Node>(new Ast.Node[] { 
						new Ast.Identifier(new Token { Type = TokenType.Identifier, Value = "__counter@"+VariableName }),
						new Ast.Identifier(new Token { Type = TokenType.Identifier, Value = "__list@"+VariableName })
					})).Transform(nestedScope);

				LengthFunc = Ast.StaticInvokation.CreateCorrectInvokationNode(Source, nestedScope, lengthMacro,
					new List<Ast.Node>(new Ast.Node[] {
						new Ast.Identifier(new Token { Type = TokenType.Identifier, Value = "__list@"+VariableName })
					})).Transform(nestedScope);

				Condition = Condition.Transform(nestedScope);
				if (Condition.ResultType.Name != "BOOLEAN")
					throw new CompileError("Condition to where clause must return boolean", Source);
				return this;
			}

			public override void Emit(VirtualMachine.InstructionList into, Ast.OperationDestination Destination)
			{
				//Prepare loop control variables
				into.AddInstructions("EMPTY_LIST PUSH"); //__result@
				List.Emit(into, Ast.OperationDestination.Stack);	//__list@
				LengthFunc.Emit(into, Ast.OperationDestination.Stack); //__total@
				into.AddInstructions("MOVE NEXT PUSH # SET __COUNTER@", 0);			//__counter@

				var LoopStart = into.Count;

				into.AddInstructions(
					"LOAD_PARAMETER NEXT R #" + TotalVariable.Name, TotalVariable.Offset,
					"GREATER_EQUAL PEEK R R #PEEK = __COUNTER@",
					"IF_TRUE R",
					"JUMP NEXT", 0);

				var BreakPoint = into.Count - 1;

				Indexer.Emit(into, Ast.OperationDestination.Stack);
				Condition.Emit(into, Ast.OperationDestination.R);
				into.AddInstructions("IF_FALSE R", "JUMP NEXT", 0);
				var skipAppend = into.Count - 1;
				into.AddInstructions("LOAD_PARAMETER NEXT R", ResultVariable.Offset);
				into.AddInstructions("APPEND PEEK R R"); //Since lists are mutable, we can just forget about it.
				into[skipAppend] = into.Count;
				into.AddInstructions(
					"MOVE POP #REMOVE __VALUE@",
					"INCREMENT PEEK PEEK",
					"JUMP NEXT", LoopStart);

				into[BreakPoint] = into.Count;

				into.AddInstructions("CLEANUP NEXT #REMOVE __COUNTER@, __TOTAL@, __LIST@", 3);

				if (Destination == Ast.OperationDestination.Discard)
					into.AddInstructions("MOVE POP"); //Remove __result@... that was rather pointless, no?
				if (Destination != Ast.OperationDestination.Stack)
					into.AddInstructions("MOVE POP " + WriteOperand(Destination));
			}
		}
	}
}
