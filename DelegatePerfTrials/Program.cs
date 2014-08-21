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
    public class Program
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
                Func<string> fc4 = () => f("World!");
                result = fc4();
                baselineTimer.Stop();

                lcgTimer.Start();
                var fc3 = CreateLCGApproach(f, "World!");
                result = fc3();
                lcgTimer.Stop();
            }

            Console.WriteLine("Func<>.ctor: {0}", ctorTimer.ElapsedMilliseconds);
            Console.WriteLine("Delegate:    {0}", delegateTimer.ElapsedMilliseconds);
            Console.WriteLine("Baseline:    {0}", baselineTimer.ElapsedMilliseconds);
            Console.WriteLine("LCG:         {0}", lcgTimer.ElapsedMilliseconds);
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
            if (f.Target != null)
            {
                throw new ArgumentException();
            }

            var factory = FactoryMethodHelper<TArg, TReturn>.GetFactory(f.Method);
            Func<TReturn> simpleWrapper = factory(arg);
            return simpleWrapper;
        }

        private static class FuncCtorHelper<TReturn>
        {
            internal static readonly ConstructorInfo FuncOfTCtor = typeof(Func<TReturn>).GetConstructors().Single();
        }

        private static class FactoryMethodHelper<TArg, TReturn>
        {
            private static readonly Dictionary<MethodInfo, Func<TArg, Func<TReturn>>> helperMethods = new Dictionary<MethodInfo, Func<TArg, Func<TReturn>>>();

            internal static Func<TArg, Func<TReturn>> GetFactory(MethodInfo method)
            {
                Func<TArg, Func<TReturn>> factory;
                lock (helperMethods)
                {
                    helperMethods.TryGetValue(method, out factory);
                }

                if (factory == null)
                {
                    factory = CreateFactory(method);

                    lock (helperMethods)
                    {
                        helperMethods[method] = factory;
                    }
                }

                return factory;
            }

            private static Func<TArg, Func<TReturn>> CreateFactory(MethodInfo m)
            {
                var method = new DynamicMethod("test", typeof(Func<TReturn>), new Type[] { typeof(TArg) }, restrictedSkipVisibility: true);
                var il = method.GetILGenerator();

                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Ldftn, m);
                il.Emit(OpCodes.Newobj, FuncCtorHelper<TReturn>.FuncOfTCtor);
                il.Emit(OpCodes.Ret);

                var fastMethod = (Func<TArg, Func<TReturn>>)method.CreateDelegate(typeof(Func<TArg, Func<TReturn>>));
                return fastMethod;
            }
        }

        public static string SomeAction(string value)
        {
            return string.Format("Hello, {0}!", value);
        }
    }
}
