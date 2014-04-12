﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace EtcScriptLib.Ast
{
	public class StackCall : Statement
	{
		public List<Node> Arguments;
		public VirtualMachine.InvokeableFunction Function;

		public StackCall(Token Source, VirtualMachine.InvokeableFunction Function, List<Node> Arguments) : base(Source) 
		{
			this.Function = Function;
			this.Arguments = Arguments;
		}

		public override Node Transform(ParseScope Scope)
		{
			ResultType = Function.ReturnType;
			Arguments = new List<Node>(Arguments.Select(n => n.Transform(Scope)));
			return this;
		}

		public override void Emit(VirtualMachine.InstructionList into, OperationDestination Destination)
		{
			foreach (var arg in Arguments)
				arg.Emit(into, OperationDestination.Stack);
			into.AddInstructions("STACK_INVOKE NEXT", Function);
			if (Arguments.Count > 0) into.AddInstructions("CLEANUP NEXT", Arguments.Count);
			if (Destination != OperationDestination.R && Destination != OperationDestination.Discard)
				into.AddInstructions("MOVE R " + Node.WriteOperand(Destination));
		}

		public override void Debug(int depth)
		{
			Console.Write(new String(' ', depth * 3));
			Console.WriteLine("Stack Call");
			foreach (var arg in Arguments)
				arg.Debug(depth + 1);
		}
	}
}
