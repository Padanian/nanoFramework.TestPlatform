//
// Copyright (c) 2018 The nanoFramework project contributors
// Portions Copyright (c) Microsoft Corporation.  All rights reserved.
// See LICENSE file in the project root for full license information.
//

namespace nanoFramework.TestPlatform.MSTest.TestAdapter.PlatformServices
{
    using nanoFramework.TestPlatform.MSTest.TestAdapter.Interface;
    using nanoFramework.TestPlatform.MSTest.TestAdapter.ObjectModel;
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Data.Common;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Linq;

    //using UTF = nanoFramework.TestPlatform.MSTest.TestAdapter;

    /// <summary>
    /// Internal implementation of TestContext exposed to the user.
    /// The virtual string properties of the TestContext are retreived from the property dictionary
    /// like GetProperty&lt;string&gt;("TestName") or GetProperty&lt;string&gt;("FullyQualifiedTestClassName");
    /// </summary>
    public class TestContextImplementation : TestContext, ITestContext
    {
        /// <summary>
        /// List of result files associated with the test
        /// </summary>
        private IList<string> testResultFiles;

        /// <summary>
        /// Properties
        /// </summary>
        private IDictionary<string, object> properties;

        /// <summary>
        /// Unit test outcome
        /// </summary>
        private UnitTestOutcome outcome;

        /// <summary>
        /// Writer on which the messages given by the user should be written
        /// </summary>
        private StringWriter stringWriter;

        /// <summary>
        /// Specifies whether the writer is disposed or not
        /// </summary>
        private bool stringWriterDisposed = false;

        /// <summary>
        /// Test Method
        /// </summary>
        private ITestMethod testMethod;

        /// <summary>
        /// DB connection for test context
        /// </summary>
        private DbConnection dbConnection;

        /// <summary>
        /// Data row for TestContext
        /// </summary>
        private DataRow dataRow;

        /// <summary>
        /// Initializes a new instance of the <see cref="TestContextImplementation"/> class.
        /// </summary>
        /// <param name="testMethod">The test method.</param>
        /// <param name="stringWriter">The writer where diagnostic messages are written to.</param>
        /// <param name="properties">Properties/configuration passed in.</param>
        public TestContextImplementation(ITestMethod testMethod, StringWriter stringWriter, IDictionary<string, object> properties)
        {
            Debug.Assert(testMethod != null, "TestMethod is not null");
            Debug.Assert(stringWriter != null, "StringWriter is not null");
            Debug.Assert(properties != null, "properties is not null");

            this.testMethod = testMethod;
            this.stringWriter = stringWriter;
            this.properties = new Dictionary<string, object>(properties);

            this.InitializeProperties();

            this.testResultFiles = new List<string>();
        }

        #region TestContext impl

        /// <inheritdoc/>
        public override UnitTestOutcome CurrentTestOutcome
        {
            get
            {
                return this.outcome;
            }
        }

        /// <inheritdoc/>
        //public override DbConnection DataConnection
        //{
        //    get
        //    {
        //        return this.dbConnection;
        //    }
        //}

        ///// <inheritdoc/>
        //public override DataRow DataRow
        //{
        //    get
        //    {
        //        return this.dataRow;
        //    }
        //}

        ///// <inheritdoc/>
        //public override IDictionary Properties
        //{
        //    get
        //    {
        //        return this.properties as IDictionary;
        //    }
        //}

        /// <inheritdoc/>
        //public override string TestRunDirectory
        //{
        //    get
        //    {
        //        return this.GetStringPropertyValue(TestContextPropertyStrings.TestRunDirectory);
        //    }
        //}

        ///// <inheritdoc/>
        //public override string DeploymentDirectory
        //{
        //    get
        //    {
        //        return this.GetStringPropertyValue(TestContextPropertyStrings.DeploymentDirectory);
        //    }
        //}

        ///// <inheritdoc/>
        //public override string ResultsDirectory
        //{
        //    get
        //    {
        //        return this.GetStringPropertyValue(TestContextPropertyStrings.ResultsDirectory);
        //    }
        //}

        ///// <inheritdoc/>
        //public override string TestRunResultsDirectory
        //{
        //    get
        //    {
        //        return this.GetStringPropertyValue(TestContextPropertyStrings.TestRunResultsDirectory);
        //    }
        //}

        /// <inheritdoc/>
        //[SuppressMessage("Microsoft.Naming", "CA1702:CompoundWordsShouldBeCasedCorrectly", Justification = "TestResultsDirectory is what we need.")]
        //public override string TestResultsDirectory
        //{
        //    get
        //    {
        //        // In MSTest, it is actually "In\697105f7-004f-42e8-bccf-eb024870d3e9\User1", but
        //        // we are setting it to "In" only because MSTest does not create this directory.
        //        return this.GetStringPropertyValue(TestContextPropertyStrings.TestResultsDirectory);
        //    }
        //}

        /// <inheritdoc/>
        //public override string TestDir
        //{
        //    get
        //    {
        //        return this.GetStringPropertyValue(TestContextPropertyStrings.TestDir);
        //    }
        //}

        ///// <inheritdoc/>
        //public override string TestDeploymentDir
        //{
        //    get
        //    {
        //        return this.GetStringPropertyValue(TestContextPropertyStrings.TestDeploymentDir);
        //    }
        //}

        ///// <inheritdoc/>
        //public override string TestLogsDir
        //{
        //    get
        //    {
        //        return this.GetStringPropertyValue(TestContextPropertyStrings.TestLogsDir);
        //    }
        //}

        /// <inheritdoc/>
        public override string FullyQualifiedTestClassName
        {
            get
            {
                return this.GetStringPropertyValue(TestContextPropertyStrings.FullyQualifiedTestClassName);
            }
        }

        /// <inheritdoc/>
        public override string TestName
        {
            get
            {
                return this.GetStringPropertyValue(TestContextPropertyStrings.TestName);
            }
        }

        public TestContext Context
        {
            get
            {
                return this as TestContext;
            }
        }

        TestContext ITestContext.Context => throw new NotImplementedException();

        public override IDictionary<string, object> Properties => throw new NotImplementedException();

        /// <inheritdoc/>
        //public override void AddResultFile(string fileName)
        //{
        //    if (string.IsNullOrEmpty(fileName))
        //    {
        //        throw new ArgumentException(Resource.Common_CannotBeNullOrEmpty, "fileName");
        //    }

        //    this.testResultFiles.Add(Path.GetFullPath(fileName));
        //}

        ///// <inheritdoc/>
        //public override void BeginTimer(string timerName)
        //{
        //    throw new NotSupportedException();
        //}

        ///// <inheritdoc/>
        //public override void EndTimer(string timerName)
        //{
        //    throw new NotSupportedException();
        //}

        /// <summary>
        /// When overridden in a derived class, used to write trace messages while the
        ///     test is running.
        /// </summary>
        /// <param name="message">The formatted string that contains the trace message.</param>
        public override void WriteLine(string message)
        {
            if (this.stringWriterDisposed)
            {
                return;
            }

            try
            {
                var msg = message?.Replace("\0", "\\0");
                this.stringWriter.WriteLine(msg);
            }
            catch (ObjectDisposedException)
            {
                this.stringWriterDisposed = true;
            }
        }

        /// <summary>
        /// When overridden in a derived class, used to write trace messages while the
        ///     test is running.
        /// </summary>
        /// <param name="format">The string that contains the trace message.</param>
        /// <param name="args">Arguments to add to the trace message.</param>
        public override void WriteLine(string format, params object[] args)
        {
            if (this.stringWriterDisposed)
            {
                return;
            }

            try
            {
                string message = string.Format(CultureInfo.CurrentCulture, format?.Replace("\0", "\\0"), args);
                this.stringWriter.WriteLine(message);
            }
            catch (ObjectDisposedException)
            {
                this.stringWriterDisposed = true;
            }
        }

        /// <summary>
        /// Set the unit-test outcome
        /// </summary>
        /// <param name="outcome">The test outcome.</param>
        public void SetOutcome(UnitTestOutcome outcome)
        {
            this.outcome = ToUTF(outcome);
        }

        /// <summary>
        /// Set data row for particular run of TestMethod.
        /// </summary>
        /// <param name="dataRow">data row.</param>
        public void SetDataRow(object dataRow)
        {
            this.dataRow = dataRow as DataRow;
        }

        /// <summary>
        /// Set connection for TestContext
        /// </summary>
        /// <param name="dbConnection">db Connection.</param>
        public void SetDataConnection(object dbConnection)
        {
            this.dbConnection = dbConnection as DbConnection;
        }

        /// <summary>
        /// Returns whether property with parameter name is present or not
        /// </summary>
        /// <param name="propertyName">The property name.</param>
        /// <param name="propertyValue">The property value.</param>
        /// <returns>True if found.</returns>
        public bool TryGetPropertyValue(string propertyName, out object propertyValue)
        {
            if (this.properties == null)
            {
                propertyValue = null;
                return false;
            }

            return this.properties.TryGetValue(propertyName, out propertyValue);
        }

        /// <summary>
        /// Adds the parameter name/value pair to property bag
        /// </summary>
        /// <param name="propertyName">The property name.</param>
        /// <param name="propertyValue">The property value.</param>
        public void AddProperty(string propertyName, string propertyValue)
        {
            if (this.properties == null)
            {
                this.properties = new Dictionary<string, object>();
            }

            this.properties.Add(propertyName, propertyValue);
        }

        /// <summary>
        /// Result files attached
        /// </summary>
        /// <returns>Results files generated in run.</returns>
        public IList<string> GetResultFiles()
        {
            if (this.testResultFiles.Count() == 0)
            {
                return null;
            }

            List<string> results = this.testResultFiles.ToList();

            // clear the result files to handle data driven tests
            this.testResultFiles.Clear();

            return results;
        }

        /// <summary>
        /// Gets messages from the testContext writeLines
        /// </summary>
        /// <returns>The test context messages added so far.</returns>
        public string GetDiagnosticMessages()
        {
            return this.stringWriter.ToString();
        }

        /// <summary>
        /// Clears the previous testContext writeline messages.
        /// </summary>
        public void ClearDiagnosticMessages()
        {
            var sb = this.stringWriter.GetStringBuilder();
            sb.Remove(0, sb.Length);
        }

        #endregion

        /// <summary>
        /// Converts the parameter outcome to UTF outcome
        /// </summary>
        /// <param name="outcome">The UTF outcome.</param>
        /// <returns>test outcome</returns>
        private static UnitTestOutcome ToUTF(
            UnitTestOutcome outcome
            )
        {
            switch (outcome)
            {
                case UnitTestOutcome.Error:
                    {
                        return UnitTestOutcome.Error;
                    }

                case UnitTestOutcome.Failed:
                    {
                        return UnitTestOutcome.Failed;
                    }

                case UnitTestOutcome.Inconclusive:
                    {
                        return UnitTestOutcome.Inconclusive;
                    }

                case UnitTestOutcome.Passed:
                    {
                        return UnitTestOutcome.Passed;
                    }

                case UnitTestOutcome.Timeout:
                    {
                        return UnitTestOutcome.Timeout;
                    }

                case UnitTestOutcome.InProgress:
                    {
                        return UnitTestOutcome.InProgress;
                    }

                default:
                    {
                        Debug.Fail("Unknown outcome " + outcome);
                        return UnitTestOutcome.Unknown;
                    }
            }
        }

        /// <summary>
        /// Helper to safely fetch a property value.
        /// </summary>
        /// <param name="propertyName">Property Name</param>
        /// <returns>Property value</returns>
        private string GetStringPropertyValue(
            string propertyName
            )
        {
            object propertyValue = null;
            this.properties.TryGetValue(propertyName, out propertyValue);
            return propertyValue as string;
        }

        /// <summary>
        /// Helper to initialize the properties.
        /// </summary>
        private void InitializeProperties()
        {
            this.properties[TestContextPropertyStrings.FullyQualifiedTestClassName] = this.testMethod.FullClassName;
            this.properties[TestContextPropertyStrings.TestName] = this.testMethod.Name;
        }
    }

#pragma warning disable SA1402 // File may only contain a single class

    /// <summary>
    /// Test Context Property Names.
    /// </summary>
    internal static class TestContextPropertyStrings
#pragma warning restore SA1402 // File may only contain a single class
    {
        public static readonly string TestRunDirectory = "TestRunDirectory";
        public static readonly string DeploymentDirectory = "DeploymentDirectory";
        public static readonly string ResultsDirectory = "ResultsDirectory";
        public static readonly string TestRunResultsDirectory = "TestRunResultsDirectory";
        public static readonly string TestResultsDirectory = "TestResultsDirectory";
        public static readonly string TestDir = "TestDir";
        public static readonly string TestDeploymentDir = "TestDeploymentDir";
        public static readonly string TestLogsDir = "TestLogsDir";

        public static readonly string FullyQualifiedTestClassName = "FullyQualifiedTestClassName";
        public static readonly string TestName = "TestName";
    }
}
