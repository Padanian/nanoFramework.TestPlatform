//
// Copyright (c) 2018 The nanoFramework project contributors
// Portions Copyright (c) Microsoft Corporation.  All rights reserved.
// See LICENSE file in the project root for full license information.
//

namespace nanoFramework.TestPlatform.MSTest.TestAdapter
{
    using nanoFramework.TestPlatform.MSTest.TestAdapter.ObjectModel;
    using nanoFramework.TestPlatform.MSTest.TestAdapter.Resources;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;

    /// <summary>
    /// TestContext class. This class should be fully abstract and not contain any
    /// members. The adapter will implement the members. Users in the framework should
    /// only access this via a well-defined interface.
    /// </summary>
    public abstract class TestContext
    {
        /// <summary>
        /// Gets test properties for a test.
        /// </summary>
        public abstract IDictionary<string, object> Properties { get; }

        /// <summary>
        /// Gets Fully-qualified name of the class containing the test method currently being executed
        /// </summary>
        /// <remarks>
        /// This property can be useful in attributes derived from ExpectedExceptionBaseAttribute.
        /// Those attributes have access to the test context, and provide messages that are included
        /// in the test results. Users can benefit from messages that include the fully-qualified
        /// class name in addition to the name of the test method currently being executed.
        /// </remarks>
        public virtual string FullyQualifiedTestClassName
        {
            get
            {
                return this.GetProperty<string>("FullyQualifiedTestClassName");
            }
        }

        /// <summary>
        /// Gets the Name of the test method currently being executed
        /// </summary>
        public virtual string TestName
        {
            get
            {
                return this.GetProperty<string>("TestName");
            }
        }

        /// <summary>
        /// Gets the current test outcome.
        /// </summary>
        public virtual UnitTestOutcome CurrentTestOutcome => UnitTestOutcome.Unknown;

        /// <summary>
        /// Used to write trace messages while the test is running
        /// </summary>
        /// <param name="message">formatted message string</param>
        public abstract void WriteLine(string message);

        /// <summary>
        /// Used to write trace messages while the test is running
        /// </summary>
        /// <param name="format">format string</param>
        /// <param name="args">the arguments</param>
        public abstract void WriteLine(string format, params object[] args);

        private T GetProperty<T>(string name)
            where T : class
        {
            object o;

            if (!this.Properties.TryGetValue(name, out o))
            {
                return null;
            }

            if (o != null && !(o is T))
            {
                // If o has a value, but it's not the right type
                Debug.Assert(false, "How did an invalid value get in here?");
                throw new InvalidCastException(string.Format(CultureInfo.CurrentCulture, Resource.InvalidPropertyType, name, o.GetType(), typeof(T)));
            }

            return (T)o;
        }
    }
}
