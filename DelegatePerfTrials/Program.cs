using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
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
            var factory = FactoryMethodHelper<string, string>.GetFactory(f.Method);

            const int iterations = 50000;

            var ctorTimer = new Stopwatch();
            var delegateTimer = new Stopwatch();
            var baselineTimer = new Stopwatch();
            var lcgTimer = new Stopwatch();
            var lcgPreFactoryTimer = new Stopwatch();

            for (int i = 0; i < iterations; i++)
            {
                ctorTimer.Start();
                string result = A1(f, ctor);
                ctorTimer.Stop();

                delegateTimer.Start();
                result = A2(f);
                delegateTimer.Stop();

                baselineTimer.Start();
                result = A3(f);
                baselineTimer.Stop();

                lcgTimer.Start();
                result = A4(f);
                lcgTimer.Stop();

                lcgPreFactoryTimer.Start();
                result = A5(f, factory);
                lcgPreFactoryTimer.Stop();
            }

            Console.WriteLine("Func<>.ctor: {0}", ctorTimer.ElapsedMilliseconds);
            Console.WriteLine("Delegate:    {0}", delegateTimer.ElapsedMilliseconds);
            Console.WriteLine("Baseline:    {0}", baselineTimer.ElapsedMilliseconds);
            Console.WriteLine("LCG:         {0}", lcgTimer.ElapsedMilliseconds);
            Console.WriteLine("LCG (pref):  {0}", lcgPreFactoryTimer.ElapsedMilliseconds);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static string A5(Func<string, string> f, Func<string, Func<string>> factory)
        {
            var fc3 = factory("World!");
            return fc3();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static string A4(Func<string, string> f)
        {
            var fc3 = CreateLCGApproach(f, "World!");
            return fc3();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static string A3(Func<string, string> f)
        {
            Func<string> fc4 = () => f("World!");
            return fc4();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static string A2(Func<string, string> f)
        {
            var fc2 = CreateDelegateApproach(f);
            return fc2();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static string A1(Func<string, string> f, ConstructorInfo ctor)
        {
            var fc1 = CtorApproach(f, ctor);
            return fc1();
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

        private static string SomeAction(string value)
        {
            return string.Format("Hello, {0}!", value);
        }
    }
}
