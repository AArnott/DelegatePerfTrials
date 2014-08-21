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

        static string SomeAction(string value)
        {
            return string.Format("Hello, {0}!", value);
        }
    }
}
