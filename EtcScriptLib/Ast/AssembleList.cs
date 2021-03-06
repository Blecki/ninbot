﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace EtcScriptLib.Ast
{
	public class AssembleList : Node
	{
		public List<Node> Members = new List<Node>();
		public AssembleList(Token Source, List<Node> Members)
			: base(Source)
		{
			this.Members = Members;
		}

		public override Node Transform(ParseScope Scope)
		{
			ResultType = Scope.FindType("LIST");
			Members = new List<Node>(Members.Select(s => s.Transform(Scope)));
			return this;
		}

		public override void Emit(VirtualMachine.InstructionList Instructions, OperationDestination Destination)
		{
			Instructions.AddInstructions("EMPTY_LIST PUSH");

			foreach (var member in Members)
			{
				member.Emit(Instructions, OperationDestination.R);
				Instructions.AddInstructions("APPEND R PEEK PEEK");
			}

			if (Destination == OperationDestination.Discard)
				Instructions.AddInstructions("MOVE POP");
			else if (Destination != OperationDestination.Top)
				Instructions.AddInstructions("MOVE POP " + WriteOperand(Destination));			
		}

		public override string ToString()
		{
			return "{ " + String.Join(" ", Members.Select(m => m.ToString())) + " }";
		}
	}
}
