//
// Copyright (c) 2018 The nanoFramework project contributors
// Portions Copyright (c) Microsoft Corporation.  All rights reserved.
// See LICENSE file in the project root for full license information.
//

namespace nanoFramework.TestPlatform.MSTest.TestAdapter.Execution
{
    using nanoFramework.TestPlatform.MSTest.TestAdapter.Extensions;
    using nanoFramework.TestPlatform.MSTest.TestAdapter.Helpers;
    using nanoFramework.TestPlatform.MSTest.TestAdapter.ObjectModel;
    using nanoFramework.TestPlatform.MSTest.TestAdapter.Resources;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Diagnostics.CodeAnalysis;
    using System.Globalization;
    using System.Reflection;
    using System.Text;

    /// <summary>
    /// Defines the TestMethod Info object
    /// </summary>
    public class TestMethodInfo : TestAdapter.ITestMethod
    {
        /// <summary>
        /// Specifies the timeout when it is not set in a test case
        /// </summary>
        public const int TimeoutWhenNotSet = 0;

        private object[] arguments;

        internal TestMethodInfo(
            MethodInfo testMethod,
            TestClassInfo parent,
            TestMethodOptions testmethodOptions)
        {
            Debug.Assert(testMethod != null, "TestMethod should not be null");
            Debug.Assert(parent != null, "Parent should not be null");

            this.TestMethod = testMethod;
            this.Parent = parent;
            this.TestMethodOptions = testmethodOptions;
        }

        /// <summary>
        /// Gets a value indicating whether timeout is set.
        /// </summary>
        public bool IsTimeoutSet => this.TestMethodOptions.Timeout != TimeoutWhenNotSet;

        /// <summary>
        /// Gets the reason why the test is not runnable
        /// </summary>
        public string NotRunnableReason { get; internal set; }

        /// <summary>
        /// Gets a value indicating whether test is runnnable
        /// </summary>
        public bool IsRunnable => string.IsNullOrEmpty(this.NotRunnableReason);

        public ParameterInfo[] ParameterTypes => this.TestMethod.GetParameters();

        public Type ReturnType => this.TestMethod.ReturnType;

        public string TestClassName => this.Parent.ClassType.FullName;

        public string TestMethodName => this.TestMethod.Name;

        public MethodInfo MethodInfo => this.TestMethod;

        public object[] Arguments => this.arguments;

        /// <summary>
        /// Gets testMethod referred by this object
        /// </summary>
        internal MethodInfo TestMethod { get; private set; }

        /// <summary>
        /// Gets the parent class Info object
        /// </summary>
        internal TestClassInfo Parent { get; private set; }

        /// <summary>
        /// Gets the options for the test method in this environment.
        /// </summary>
        internal TestMethodOptions TestMethodOptions { get; private set; }

        public Attribute[] GetAllAttributes(bool inherit)
        {
            return ReflectHelper.GetCustomAttributes(this.TestMethod, inherit) as Attribute[];
        }

        public TAttributeType[] GetAttributes<TAttributeType>(bool inherit)
            where TAttributeType : Attribute
        {
            Attribute[] attributeArray = ReflectHelper.GetCustomAttributes(this.TestMethod, typeof(TAttributeType), inherit);

            TAttributeType[] tAttributeArray = attributeArray as TAttributeType[];
            if (tAttributeArray != null)
            {
                return tAttributeArray;
            }

            List<TAttributeType> tAttributeList = new List<TAttributeType>();
            if (attributeArray != null)
            {
                foreach (Attribute attribute in attributeArray)
                {
                    TAttributeType tAttribute = attribute as TAttributeType;
                    if (tAttribute != null)
                    {
                        tAttributeList.Add(tAttribute);
                    }
                }
            }

            return tAttributeList.ToArray();
        }

        /// <summary>
        /// Execute test method. Capture failures, handle async and return result.
        /// </summary>
        /// <param name="arguments">
        ///  Arguments to pass to test method. (E.g. For data driven)
        /// </param>
        /// <returns>Result of test method invocation.</returns>
        public virtual TestResult Invoke(object[] arguments)
        {
            Stopwatch watch = new Stopwatch();
            TestResult result = null;

            // check if arguments are set for data driven tests
            if (arguments == null)
            {
                arguments = this.Arguments;
            }

            using (LogMessageListener listener = new LogMessageListener(this.TestMethodOptions.CaptureDebugTraces))
            {
                watch.Start();
                try
                {
                    //if (this.IsTimeoutSet)
                    //{
                    //    result = this.ExecuteInternalWithTimeout(arguments);
                    //}
                    //else
                    {
                        result = this.ExecuteInternal(arguments);
                    }
                }
                finally
                {
                    // Handle logs & debug traces.
                    watch.Stop();

                    if (result != null)
                    {
                        result.Duration = watch.Elapsed;
                        result.DebugTrace = listener.DebugTrace;
                        result.LogOutput = listener.StandardOutput;
                        result.LogError = listener.StandardError;
                        result.TestContextMessages = this.TestMethodOptions.TestContext.GetAndClearDiagnosticMessages();
                        result.ResultFiles = this.TestMethodOptions.TestContext.GetResultFiles();
                    }
                }
            }

            return result;
        }

        internal void SetArguments(object[] arguments)
        {
            this.arguments = arguments;
        }

        /// <summary>
        /// Execute test without timeout.
        /// </summary>
        /// <param name="arguments">Arguments to be passed to the method.</param>
        /// <returns>The result of the execution.</returns>
        [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "Requirement is to handle all kinds of user exceptions and message appropriately.")]
        private TestResult ExecuteInternal(object[] arguments)
        {
            Debug.Assert(this.TestMethod != null, "UnitTestExecuter.DefaultTestMethodInvoke: testMethod = null.");

            var result = new TestResult();

            // TODO remove dry violation with TestMethodRunner
            var classInstance = this.CreateTestClassInstance(result);
            var testContextSetup = false;
            bool isExceptionThrown = false;
            bool hasTestInitializePassed = false;
            Exception testRunnerException = null;

            try
            {
                try
                {
                    if (classInstance != null && this.SetTestContext(classInstance, result))
                    {
                        // For any failure after this point, we must run TestCleanup
                        testContextSetup = true;

                        if (this.RunTestInitializeMethod(classInstance, result))
                        {
                            hasTestInitializePassed = true;
                            // TODO
                            //PlatformServiceProvider.Instance.ThreadOperations.ExecuteWithAbortSafety(
                            //    () => this.TestMethod.InvokeAsSynchronousTask(classInstance, arguments));
                            result.Outcome = UnitTestOutcome.Passed;
                        }
                    }
                }
                catch (Exception ex)
                {
                    isExceptionThrown = true;

                    if (this.IsExpectedException(ex, result))
                    {
                        // Expected Exception was thrown, so Pass the test
                        result.Outcome = UnitTestOutcome.Passed;
                    }
                    else if (result.TestFailureException == null)
                    {
                        // This block should not throw. If it needs to throw, then handling of
                        // ThreadAbortException will need to be revisited. See comment in RunTestMethod.
                        result.TestFailureException = this.HandleMethodException(
                            ex,
                            this.TestClassName,
                            this.TestMethodName);
                    }

                    if (result.Outcome != UnitTestOutcome.Passed)
                    {
                        if (ex is AssertInconclusiveException || ex.InnerException is AssertInconclusiveException)
                        {
                            result.Outcome = UnitTestOutcome.Inconclusive;
                        }
                        else
                        {
                            result.Outcome = UnitTestOutcome.Failed;
                        }
                    }
                }

                // if we get here, the test method did not throw the exception
                // if the user specified that the test was going to throw an exception, and
                // it did not, we should fail the test
                // We only perform this check if the test initialize passes and the test method is actually run.
                if (hasTestInitializePassed && !isExceptionThrown && this.TestMethodOptions.ExpectedException != null)
                {
                    result.TestFailureException = new TestFailedException(
                        UnitTestOutcome.Failed,
                        this.TestMethodOptions.ExpectedException.NoExceptionMessage);
                    result.Outcome = UnitTestOutcome.Failed;
                }
            }
            catch (Exception exception)
            {
                testRunnerException = exception;
            }

            // Set the current tests outcome before cleanup so it can be used in the cleanup logic.
            this.TestMethodOptions.TestContext.SetOutcome(result.Outcome);

            // TestCleanup can potentially be a long running operation which should'nt ideally be in a finally block.
            // Pulling it out so extension writers can abort custom cleanups if need be. Having this in a finally block
            // does not allow a threadabort exception to be raised within the block but throws one after finally is executed
            // crashing the process. This was blocking writing an extension for Dynamic Timeout in VSO.
            if (classInstance != null && testContextSetup)
            {
                this.RunTestCleanupMethod(classInstance, result);
            }

            if (testRunnerException != null)
            {
                throw testRunnerException;
            }

            return result;
        }

        [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "Requirement is to handle all kinds of user exceptions and message appropriately.")]
        private bool IsExpectedException(Exception ex, TestResult result)
        {
            Exception realException = this.GetRealException(ex);

            // if the user specified an expected exception, we need to check if this
            // exception was thrown. If it was thrown, we should pass the test. In
            // case a different exception was thrown, the test is seen as failure
            if (this.TestMethodOptions.ExpectedException != null)
            {
                Exception exceptionFromVerify;
                try
                {
                    // If the expected exception attribute's Verify method returns, then it
                    // considers this exception as expected, so the test passed
                    this.TestMethodOptions.ExpectedException.Verify(realException);
                    return true;
                }
                catch (Exception verifyEx)
                {
                    var isTargetInvocationError = verifyEx is TargetInvocationException;
                    if (isTargetInvocationError && verifyEx.InnerException != null)
                    {
                        exceptionFromVerify = verifyEx.InnerException;
                    }
                    else
                    {
                        // Verify threw an exception, so the expected exception attribute does not
                        // consider this exception to be expected. Include the exception message in
                        // the test result.
                        exceptionFromVerify = verifyEx;
                    }
                }

                // See if the verification exception (thrown by the expected exception
                // attribute's Verify method) is an AssertInconclusiveException. If so, set
                // the test outcome to Inconclusive.
                result.TestFailureException = new TestFailedException(
                    exceptionFromVerify is AssertInconclusiveException ? UnitTestOutcome.Inconclusive : UnitTestOutcome.Failed,
                                              exceptionFromVerify.TryGetMessage(),
                                              realException.TryGetStackTraceInformation());
                return false;
            }
            else
            {
                return false;
            }
        }

        private Exception GetRealException(Exception ex)
        {
            if (ex is TargetInvocationException)
            {
                Debug.Assert(ex.InnerException != null, "Inner exception of TargetInvocationException is null. This should occur because we should have caught this case above.");

                // Our reflected call will typically always get back a TargetInvocationException
                // containing the real exception thrown by the test method as its inner exception
                return ex.InnerException;
            }
            else
            {
                return ex;
            }
        }

        /// <summary>
        /// Handles the exception that is thrown by a test method. The exception can either
        /// be expected or not expected
        /// </summary>
        /// <param name="ex">Exception that was thrown</param>
        /// <param name="className">The class name.</param>
        /// <param name="methodName">The method name.</param>
        /// <returns>Test framework exception with details.</returns>
        private Exception HandleMethodException(Exception ex, string className, string methodName)
        {
            Debug.Assert(ex != null, "exception should not be null.");

            var isTargetInvocationException = ex is TargetInvocationException;
            if (isTargetInvocationException && ex.InnerException == null)
            {
                var errorMessage = string.Format(CultureInfo.CurrentCulture, Resource.UTA_FailedToGetTestMethodException, className, methodName);
                return new TestFailedException(UnitTestOutcome.Error, errorMessage);
            }

            // Get the real exception thrown by the test method
            Exception realException = this.GetRealException(ex);
            string exceptionMessage = null;
            StackTraceInformation exceptionStackTraceInfo = null;
            var outcome = UnitTestOutcome.Failed;

            if (realException.TryGetUnitTestAssertException(out outcome, out exceptionMessage, out exceptionStackTraceInfo))
            {
                return new TestFailedException(outcome.ToUnitTestOutcome(), exceptionMessage, exceptionStackTraceInfo, realException);
            }
            else
            {
                string errorMessage;

                // Handle special case of UI objects in TestMethod to suggest UITestMethod
                if (realException.HResult == -2147417842)
                {
                    errorMessage = string.Format(
                        CultureInfo.CurrentCulture,
                        Resource.UTA_WrongThread,
                        string.Format(CultureInfo.CurrentCulture, Resource.UTA_TestMethodThrows, className, methodName, StackTraceHelper.GetExceptionMessage(realException)));
                }
                else
                {
                    errorMessage = string.Format(
                        CultureInfo.CurrentCulture,
                        Resource.UTA_TestMethodThrows,
                        className,
                        methodName,
                        StackTraceHelper.GetExceptionMessage(realException));
                }

                StackTraceInformation stackTrace = null;

                // For ThreadAbortException (that can be thrown only by aborting a thread as there's no public constructor)
                // there's no inner exception and exception itself contains reflection-related stack trace
                // (_RuntimeMethodHandle.InvokeMethodFast <- _RuntimeMethodHandle.Invoke <- UnitTestExecuter.RunTestMethod)
                // which has no meaningful info for the user. Thus, we do not show call stack for ThreadAbortException.
                if (realException.GetType().Name != "ThreadAbortException")
                {
                    stackTrace = StackTraceHelper.GetStackTraceInformation(realException);
                }

                return new TestFailedException(UnitTestOutcome.Failed, errorMessage, stackTrace, realException);
            }
        }

        /// <summary>
        /// Runs TestCleanup methods of parent TestClass and base classes.
        /// </summary>
        /// <param name="classInstance">Instance of TestClass.</param>
        /// <param name="result">Instance of TestResult.</param>
        [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "Requirement is to handle all kinds of user exceptions and message appropriately.")]
        private void RunTestCleanupMethod(object classInstance, TestResult result)
        {
            Debug.Assert(classInstance != null, "classInstance != null");
            Debug.Assert(result != null, "result != null");

            var testCleanupMethod = this.Parent.TestCleanupMethod;
            try
            {
                try
                {
                    // Test cleanups are called in the order of discovery
                    // Current TestClass -> Parent -> Grandparent
                    testCleanupMethod?.InvokeAsSynchronousTask(classInstance, null);
                    var baseTestCleanupQueue = new Queue<MethodInfo>(this.Parent.BaseTestCleanupMethodsQueue);
                    while (baseTestCleanupQueue.Count > 0)
                    {
                        testCleanupMethod = baseTestCleanupQueue.Dequeue();
                        testCleanupMethod?.InvokeAsSynchronousTask(classInstance, null);
                    }
                }
                finally
                {
                    (classInstance as IDisposable)?.Dispose();
                }
            }
            catch (Exception ex)
            {
                var cleanupOutcome = UnitTestOutcome.Failed;
                var cleanupError = new StringBuilder();
                var cleanupStackTrace = new StringBuilder();
                StackTraceInformation cleanupStackTraceInfo = null;

                TestFailedException testFailureException = result.TestFailureException as TestFailedException;
                testFailureException.TryGetTestFailureExceptionMessageAndStackTrace(cleanupError, cleanupStackTrace);

                if (cleanupStackTrace.Length > 0)
                {
                        cleanupStackTrace.Append(Resource.UTA_CleanupStackTrace);
                        cleanupStackTrace.Append(Environment.NewLine);
                }

                Exception realException = ex.GetInnerExceptionOrDefault();
                string exceptionMessage = null;
                StackTraceInformation realExceptionStackTraceInfo = null;

                // special case UnitTestAssertException to trim off part of the stack trace
                if (!realException.TryGetUnitTestAssertException(out cleanupOutcome, out exceptionMessage, out realExceptionStackTraceInfo))
                {
                    cleanupOutcome = UnitTestOutcome.Failed;
                    exceptionMessage = this.GetTestCleanUpExceptionMessage(testCleanupMethod, realException);
                    realExceptionStackTraceInfo = realException.TryGetStackTraceInformation();
                }

                cleanupError.Append(exceptionMessage);
                if (realExceptionStackTraceInfo != null)
                {
                    cleanupStackTrace.Append(realExceptionStackTraceInfo.ErrorStackTrace);
                    cleanupStackTraceInfo = cleanupStackTraceInfo ?? realExceptionStackTraceInfo;
                }

                UnitTestOutcome outcome = testFailureException == null ? cleanupOutcome : cleanupOutcome.GetMoreImportantOutcome(result.Outcome);
                StackTraceInformation finalStackTraceInfo = cleanupStackTraceInfo != null ?
                                new StackTraceInformation(
                                    cleanupStackTrace.ToString(),
                                    cleanupStackTraceInfo.ErrorFilePath,
                                    cleanupStackTraceInfo.ErrorLineNumber,
                                    cleanupStackTraceInfo.ErrorColumnNumber) :
                                new StackTraceInformation(cleanupStackTrace.ToString());

                result.Outcome = outcome;
                result.TestFailureException = new TestFailedException(outcome.ToUnitTestOutcome(), cleanupError.ToString(), finalStackTraceInfo);
            }
        }

        private string GetTestCleanUpExceptionMessage(MethodInfo testCleanupMethod, Exception exception)
        {
            if (testCleanupMethod != null)
            {
                return string.Format(
                            CultureInfo.CurrentCulture,
                            Resource.UTA_CleanupMethodThrows,
                            this.TestClassName,
                            testCleanupMethod?.Name,
                            exception.GetType().ToString(),
                            StackTraceHelper.GetExceptionMessage(exception));
            }
            else
            {
                return string.Format(
                            CultureInfo.CurrentCulture,
                            Resource.UTA_CleanupMethodThrowsGeneralError,
                            this.TestClassName,
                            StackTraceHelper.GetExceptionMessage(exception));
            }
        }

        /// <summary>
        /// Runs TestInitialize methods of parent TestClass and the base classes.
        /// </summary>
        /// <param name="classInstance">Instance of TestClass.</param>
        /// <param name="result">Instance of TestResult.</param>
        /// <returns>True if the TestInitialize method(s) did not throw an exception.</returns>
        [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "Requirement is to handle all kinds of user exceptions and message appropriately.")]
        private bool RunTestInitializeMethod(object classInstance, TestResult result)
        {
            Debug.Assert(classInstance != null, "classInstance != null");
            Debug.Assert(result != null, "result != null");

            MethodInfo testInitializeMethod = null;
            try
            {
                // TestInitialize methods for base classes are called in reverse order of discovery
                // Grandparent -> Parent -> Child TestClass
                var baseTestInitializeStack = new Stack<MethodInfo>(this.Parent.BaseTestInitializeMethodsQueue);
                while (baseTestInitializeStack.Count > 0)
                {
                    testInitializeMethod = baseTestInitializeStack.Pop();
                    testInitializeMethod?.InvokeAsSynchronousTask(classInstance, null);
                }

                testInitializeMethod = this.Parent.TestInitializeMethod;
                testInitializeMethod?.InvokeAsSynchronousTask(classInstance, null);

                return true;
            }
            catch (Exception ex)
            {
                var innerException = ex.GetInnerExceptionOrDefault();
                string exceptionMessage = null;
                StackTraceInformation exceptionStackTraceInfo = null;
                var outcome = UnitTestOutcome.Failed;

                if (innerException.TryGetUnitTestAssertException(out outcome, out exceptionMessage, out exceptionStackTraceInfo))
                {
                    result.Outcome = outcome;
                    result.TestFailureException = new TestFailedException(
                        UnitTestOutcome.Failed,
                        exceptionMessage,
                        exceptionStackTraceInfo);
                }
                else
                {
                    var stackTrace = StackTraceHelper.GetStackTraceInformation(innerException);
                    var errorMessage = string.Format(
                        CultureInfo.CurrentCulture,
                        Resource.UTA_InitMethodThrows,
                        this.TestClassName,
                        testInitializeMethod?.Name,
                        StackTraceHelper.GetExceptionMessage(innerException));

                    result.Outcome = UnitTestOutcome.Failed;
                    result.TestFailureException = new TestFailedException(UnitTestOutcome.Failed, errorMessage, stackTrace);
                }
            }

            return false;
        }

        /// <summary>
        /// Sets the <see cref="TestContext"/> on <see cref="classInstance"/>.
        /// </summary>
        /// <param name="classInstance">
        /// Reference to instance of TestClass.
        /// </param>
        /// <param name="result">
        /// Reference to instance of <see cref="TestResult"/>.
        /// </param>
        /// <returns>
        /// True if there no exceptions during set context operation.
        /// </returns>
        [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "Requirement is to handle all kinds of user exceptions and message appropriately.")]
        private bool SetTestContext(object classInstance, TestResult result)
        {
            Debug.Assert(classInstance != null, "classInstance != null");
            Debug.Assert(result != null, "result != null");

            try
            {
                if (this.Parent.TestContextProperty != null && this.Parent.TestContextProperty.CanWrite)
                {
                    this.Parent.TestContextProperty.SetValue(classInstance, this.TestMethodOptions.TestContext);
                }

                return true;
            }
            catch (Exception ex)
            {
                var stackTraceInfo = StackTraceHelper.GetStackTraceInformation(ex.GetInnerExceptionOrDefault());
                var errorMessage = string.Format(
                    CultureInfo.CurrentCulture,
                    Resource.UTA_TestContextSetError,
                    this.TestClassName,
                    StackTraceHelper.GetExceptionMessage(ex.GetInnerExceptionOrDefault()));

                result.Outcome = UnitTestOutcome.Failed;
                result.TestFailureException = new TestFailedException(UnitTestOutcome.Failed, errorMessage, stackTraceInfo);
            }

            return false;
        }

        /// <summary>
        /// Creates an instance of TestClass. The TestMethod is invoked on this instance.
        /// </summary>
        /// <param name="result">
        /// Reference to the <see cref="TestResult"/> for this TestMethod.
        /// Outcome and TestFailureException are updated based on instance creation.
        /// </param>
        /// <returns>
        /// An instance of the TestClass. Returns null if there are errors during class instantiation.
        /// </returns>
        [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "Requirement is to handle all kinds of user exceptions and message appropriately.")]
        private object CreateTestClassInstance(TestResult result)
        {
            object classInstance = null;
            try
            {
                classInstance = this.Parent.Constructor.Invoke(null);
            }
            catch (Exception ex)
            {
                // In most cases, exception will be TargetInvocationException with real exception wrapped
                // in the InnerException; or user code throws an exception
                var actualException = ex.InnerException ?? ex;
                var exceptionMessage = StackTraceHelper.GetExceptionMessage(actualException);
                var stackTraceInfo = StackTraceHelper.GetStackTraceInformation(actualException);
                var errorMessage = string.Format(
                    CultureInfo.CurrentCulture,
                    Resource.UTA_InstanceCreationError,
                    this.TestClassName,
                    exceptionMessage);

                result.Outcome = UnitTestOutcome.Failed;
                result.TestFailureException = new TestFailedException(UnitTestOutcome.Failed, errorMessage, stackTraceInfo);
            }

            return classInstance;
        }

        ///// <summary>
        ///// Execute test with a timeout
        ///// </summary>
        ///// <param name="arguments">The arguments to be passed.</param>
        ///// <returns>The result of execution.</returns>
        //[SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "Requirement is to handle all kinds of user exceptions and message appropriately.")]
        //private TestResult ExecuteInternalWithTimeout(object[] arguments)
        //{
        //    Debug.Assert(this.IsTimeoutSet, "Timeout should be set");

        //    TestResult result = null;
        //    Exception failure = null;

        //    Action executeAsyncAction = () =>
        //        {
        //            try
        //            {
        //                result = this.ExecuteInternal(arguments);
        //            }
        //            catch (Exception ex)
        //            {
        //                failure = ex;
        //            }
        //        };

        //    if (PlatformServiceProvider.Instance.ThreadOperations.Execute(executeAsyncAction, this.TestMethodOptions.Timeout))
        //    {
        //        if (failure != null)
        //        {
        //            throw failure;
        //        }

        //        Debug.Assert(result != null, "no timeout, no failure result should not be null");
        //        return result;
        //    }
        //    else
        //    {
        //        // Timed out

        //        // If the method times out, then
        //        //
        //        // 1. If the test is stuck, then we can get CannotUnloadAppDomain exception.
        //        //
        //        // Which are handled as follows: -
        //        //
        //        // For #1, we are now restarting the execution process if adapter fails to unload app-domain.
        //        string errorMessage = string.Format(CultureInfo.CurrentCulture, Resource.Execution_Test_Timeout, this.TestMethodName);
        //        MSTest.TestResult timeoutResult = new nanoFramework.TestPlatform.MSTest.TestResult() { Outcome = nanoFramework.TestPlatform.MSTest.TestAdapter.ObjectModel.UnitTestOutcome.Timeout, TestFailureException = new TestFailedException(UnitTestOutcome.Timeout, errorMessage) };
        //        return timeoutResult;
        //    }
        //}
    }
}
