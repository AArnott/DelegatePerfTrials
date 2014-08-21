using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleApplication11
{
    class Program
    {
        static void Main(string[] args)
        {
            var f = new Func<string, string>(SomeAction);
            var ctor = typeof(Func<string>).GetConstructors().Single();

            const int iterations = 50000;

            var ctorTimer = new Stopwatch();
            var delegateTimer = new Stopwatch();
            var baselineTimer = new Stopwatch();
            var lcgTimer = new Stopwatch();

            for (int i = 0; i < iterations; i++)
            {
                ctorTimer.Start();
                var fc1 = CtorApproach(f, ctor);
                string result = fc1();
                ctorTimer.Stop();

                delegateTimer.Start();
                var fc2 = CreateDelegateApproach(f);
                result = fc2();
                delegateTimer.Stop();

                baselineTimer.Start();
                result = f("World!");
                baselineTimer.Stop();

                lcgTimer.Start();
                var fc3 = CreateLCGApproach(f, "World!");
                result = fc3();
                lcgTimer.Stop();
            }

            Console.WriteLine("Func<>.ctor: {0}", ctorTimer.ElapsedMilliseconds);
            Console.WriteLine("Delegate:    {0}", delegateTimer.ElapsedMilliseconds);
            Console.WriteLine("Baseline:    {0}", baselineTimer.ElapsedMilliseconds);
        }

        private static Func<string> CreateDelegateApproach(Func<string, string> f)
        {
            return (Func<string>)Delegate.CreateDelegate(typeof(Func<string>), "World!", f.Method);
        }

        private static Func<string> CtorApproach(Func<string, string> f, ConstructorInfo ctor)
        {
            return (Func<string>)ctor.Invoke(new object[] { "World!", f.Method.MethodHandle.GetFunctionPointer() });
        }

        private static Func<TReturn> CreateLCGApproach<TArg, TReturn>(Func<TArg, TReturn> f, TArg arg)
        {
            var an = new AssemblyName("test_" + Guid.NewGuid());
            var ab = AssemblyBuilder.DefineDynamicAssembly(an, AssemblyBuilderAccess.Run);
            var dm = ab.DefineDynamicModule("Main");
            var tb = dm.DefineType("SomeType");
            var mb = tb.DefineMethod("HelperMethod", MethodAttributes.Static | MethodAttributes.Public, typeof(Func<TReturn>), new Type[] { typeof(TArg) });
            var il = mb.GetILGenerator();

            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldftn, f.Method);
            il.Emit(OpCodes.Newobj, typeof(Func<TReturn>).GetConstructors().Single());
            il.Emit(OpCodes.Ret);

            var type = tb.CreateType();
            var method = type.GetMethod(mb.Name, BindingFlags.Static | BindingFlags.Public);
            var fastMethod = (Func<TArg, Func<TReturn>>)method.CreateDelegate(typeof(Func<TArg, Func<TReturn>>));
            Func<TReturn> simpleWrapper = fastMethod(arg);
            return simpleWrapper;
        }

        static string SomeAction(string value)
        {
            return string.Format("Hello, {0}!", value);
        }
    }
}
