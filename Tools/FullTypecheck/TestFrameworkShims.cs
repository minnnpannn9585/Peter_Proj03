// Shims for NUnit and Unity's TestTools, so the Edit Mode test suite can be type-checked here too.
//
// The real assemblies live inside the Unity installation and its packages, neither of which exists
// in this sandbox. As with FullUnityShims.cs, nothing here implements behaviour: the purpose is
// purely that a signature error or a typo in a test fails a build rather than surfacing the first
// time someone opens the Test Runner.
using System;
using System.Collections;
using System.Text.RegularExpressions;

namespace NUnit.Framework
{
    [AttributeUsage(AttributeTargets.Class)]
    public class TestFixtureAttribute : Attribute
    {
    }

    [AttributeUsage(AttributeTargets.Method)]
    public class TestAttribute : Attribute
    {
    }

    [AttributeUsage(AttributeTargets.Method)]
    public class SetUpAttribute : Attribute
    {
    }

    [AttributeUsage(AttributeTargets.Method)]
    public class TearDownAttribute : Attribute
    {
    }

    [AttributeUsage(AttributeTargets.Method)]
    public class OneTimeSetUpAttribute : Attribute
    {
    }

    [AttributeUsage(AttributeTargets.Method)]
    public class OneTimeTearDownAttribute : Attribute
    {
    }

    public class AssertionException : Exception
    {
        public AssertionException(string message)
            : base(message)
        {
        }
    }

    public static class Assert
    {
        public static void Fail(string message = "") => throw new AssertionException(message);

        public static void Pass(string message = "")
        {
        }

        public static void IsTrue(bool condition, string message = "")
        {
        }

        public static void IsFalse(bool condition, string message = "")
        {
        }

        public static void IsNull(object value, string message = "")
        {
        }

        public static void IsNotNull(object value, string message = "")
        {
        }

        public static void IsEmpty(IEnumerable collection, string message = "")
        {
        }

        public static void AreEqual(object expected, object actual, string message = "")
        {
        }

        public static void AreEqual(int expected, int actual, string message = "")
        {
        }

        public static void AreEqual(bool expected, bool actual, string message = "")
        {
        }

        public static void AreEqual(string expected, string actual, string message = "")
        {
        }

        public static void AreEqual(float expected, float actual, float delta, string message = "")
        {
        }

        public static void AreEqual(double expected, double actual, double delta, string message = "")
        {
        }

        public static void AreNotEqual(object expected, object actual, string message = "")
        {
        }

        public static void Greater(int a, int b, string message = "")
        {
        }

        public static void Greater(float a, float b, string message = "")
        {
        }

        public static void Greater(double a, double b, string message = "")
        {
        }

        public static void GreaterOrEqual(int a, int b, string message = "")
        {
        }

        public static void GreaterOrEqual(float a, float b, string message = "")
        {
        }

        public static void Less(int a, int b, string message = "")
        {
        }

        public static void Less(float a, float b, string message = "")
        {
        }

        public static void LessOrEqual(int a, int b, string message = "")
        {
        }

        public static void LessOrEqual(float a, float b, string message = "")
        {
        }

        public static void LessOrEqual(double a, double b, string message = "")
        {
        }

        public static void Contains(object expected, ICollection collection, string message = "")
        {
        }

        public static T Throws<T>(Action code) where T : Exception => null;
    }
}

namespace UnityEngine.TestTools
{
    public static class LogAssert
    {
        public static bool ignoreFailingMessages { get; set; }

        public static void Expect(LogType type, string message)
        {
        }

        public static void Expect(LogType type, Regex message)
        {
        }

        public static void NoUnexpectedReceived()
        {
        }
    }
}

namespace UnityEngine
{
    public enum LogType
    {
        Error = 0,
        Assert = 1,
        Warning = 2,
        Log = 3,
        Exception = 4
    }
}
