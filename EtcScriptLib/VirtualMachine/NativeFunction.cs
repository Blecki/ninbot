﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace EtcScriptLib.VirtualMachine
{
    public class NativeFunction : InvokeableFunction
    {
        public String Name = null;
        public Func<ExecutionContext, List<Object>, Object> NativeImplementation { get; private set; }

        public NativeFunction(
            String Name,
            Func<ExecutionContext, List<Object>, Object> NativeImplementation)
        {
            this.Name = Name;
            this.NativeImplementation = NativeImplementation;
        }

        public NativeFunction(Func<ExecutionContext, List<Object>, Object> NativeImplementation)
        {
            this.NativeImplementation = NativeImplementation;
        }

        public override InvokationResult Invoke(ExecutionContext context, List<Object> arguments)
        {
            try
            {
                var result = NativeImplementation.Invoke(context, arguments.GetRange(1, arguments.Count - 1));
                VirtualMachine.SetOperand(Operand.R, result, context);
                return InvokationResult.Success;
            }
            catch (Exception e)
            {
                return InvokationResult.Failure(e.Message + e.StackTrace);
            }
        }
    }
}
