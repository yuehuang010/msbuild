// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Build.Execution
{
    /// <summary>
    /// The state enabled by the [Q]uestion switch.
    /// </summary>
    [Flags]
    public enum QuestionMode
    {
        /// <summary>
        /// Default normal build.
        /// </summary>
        None = 0,

        /// <summary>
        /// Error out only when a target report it is unable to skip.
        /// </summary>
        Targets = 1,

        /// <summary>
        /// Error out only when a task with <see cref="Microsoft.Build.Framework.IIncrementalTask"/> report it is unable to skip.
        /// </summary>
        Tasks = 2,

        /// <summary>
        /// Error out when either Target or Task is unable to skip.
        /// </summary>
        Enable = 3,
    }
}
