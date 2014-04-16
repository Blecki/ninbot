﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace EtcScriptLib
{
	public class LambdaBlock
	{
		internal VirtualMachine.InstructionList Instructions;
		internal int EntryPoint;
		internal Ast.Node Body;
		private VirtualMachine.InvokeableFunction CachedLambda;
		internal List<int> CallPoints = new List<int>();
		internal int CleanupPoint;
		internal int CleanupCall = -1;

		public LambdaBlock(Ast.Node Body)
		{
			this.Body = Body;
		}

		public void Transform(ParseScope DeclarationScope)
		{
			Body = Body.Transform(DeclarationScope);
		}

		public virtual void EmitInstructions(ParseScope DeclarationScope, VirtualMachine.InstructionList Into)
		{
			if (Instructions != null) throw new InvalidOperationException("Instructions should not be emitted twice");

			CleanupPoint = Into.Count;

			if (CleanupCall >= 0) 
				Into[CleanupCall] = CleanupPoint;

			Into.AddInstructions("CLEANUP NEXT", DeclarationScope.Owner.ActualParameterCount, "CONTINUE POP");

			Instructions = Into;
			EntryPoint = Into.Count;
			Instructions.AddInstructions("MOVE F PUSH", "MARK_STACK F");
			Body.Emit(Instructions, Ast.OperationDestination.Discard);
			Instructions.AddInstructions("MOVE NEXT R", 0); //If a function has no return statement, it returns 0.
			var returnJumpPoint = Instructions.Count;
			Instructions.AddInstructions(
				"RESTORE_STACK F",
				"MOVE POP F",
				"CONTINUE POP");

			System.Diagnostics.Debug.Assert(DeclarationScope.Type == ScopeType.Function);
			System.Diagnostics.Debug.Assert(DeclarationScope.ReturnJumpSources != null);

			foreach (var point in DeclarationScope.ReturnJumpSources)
				Instructions[point] = returnJumpPoint;
		}

		public void ResolveCallPoints()
		{
			foreach (var point in CallPoints)
				Instructions[point] = EntryPoint;
		}

		public void CacheSystemImplementation(int ArgumentCount, Func<VirtualMachine.ExecutionContext, List<Object>, Object> Implementation)
		{
			CachedLambda = new VirtualMachine.NativeFunction("SYS-LAMBDA", ArgumentCount, Implementation);
		}

		public VirtualMachine.InvokeableFunction GetBasicInvokable(int ArgumentCount)
		{
			if (CachedLambda == null)
				CachedLambda = VirtualMachine.LambdaFunction.CreateLambda(GetEntryPoint(),	ArgumentCount);
		
			return CachedLambda;
		}

		public VirtualMachine.CodeContext GetEntryPoint()
		{
			return new VirtualMachine.CodeContext(Instructions, EntryPoint);
		}		
	}
}
