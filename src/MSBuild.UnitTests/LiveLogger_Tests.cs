﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Build.Framework;
using Microsoft.Build.Logging.LiveLogger;

using VerifyTests;
using VerifyXunit;
using Xunit;

using static VerifyXunit.Verifier;

namespace Microsoft.Build.UnitTests
{
    [UsesVerify]
    public class LiveLogger_Tests : IEventSource, IDisposable
    {
        private const int _nodeCount = 8;
        private const string _eventSender = "Test";
        private readonly string _projectFile = NativeMethods.IsUnixLike ? "/src/project.proj" : @"C:\src\project.proj";

        private StringWriter _outputWriter = new();

        private readonly Terminal _mockTerminal;
        private readonly LiveLogger _liveLogger;

        private readonly DateTime _buildStartTime = new DateTime(2023, 3, 30, 16, 30, 0);
        private readonly DateTime _buildFinishTime = new DateTime(2023, 3, 30, 16, 30, 5);

        private VerifySettings _settings = new();

        private static Regex s_elapsedTime = new($@"\(\d+{Regex.Escape(CultureInfo.CurrentUICulture.NumberFormat.NumberDecimalSeparator)}\ds\)", RegexOptions.Compiled);

        public LiveLogger_Tests()
        {
            _mockTerminal = new Terminal(_outputWriter);
            _liveLogger = new LiveLogger(_mockTerminal);

            _liveLogger.Initialize(this, _nodeCount);

            UseProjectRelativeDirectory("Snapshots");

            // Scrub timestamps on intermediate execution lines,
            // which are subject to the vagaries of the test machine
            // and OS scheduler.
            _settings.AddScrubber(static lineBuilder =>
            {
                string line = lineBuilder.ToString();
                lineBuilder.Clear();
                lineBuilder.Append(s_elapsedTime.Replace(line, "(0.0s)"));
            });
        }

        #region IEventSource implementation

#pragma warning disable CS0067
        public event BuildMessageEventHandler? MessageRaised;

        public event BuildErrorEventHandler? ErrorRaised;

        public event BuildWarningEventHandler? WarningRaised;

        public event BuildStartedEventHandler? BuildStarted;

        public event BuildFinishedEventHandler? BuildFinished;

        public event ProjectStartedEventHandler? ProjectStarted;

        public event ProjectFinishedEventHandler? ProjectFinished;

        public event TargetStartedEventHandler? TargetStarted;

        public event TargetFinishedEventHandler? TargetFinished;

        public event TaskStartedEventHandler? TaskStarted;

        public event TaskFinishedEventHandler? TaskFinished;

        public event CustomBuildEventHandler? CustomEventRaised;

        public event BuildStatusEventHandler? StatusEventRaised;

        public event AnyEventHandler? AnyEventRaised;
#pragma warning restore CS0067

        #endregion

        #region IDisposable implementation

        public void Dispose()
        {
            _liveLogger.Shutdown();
        }

        #endregion

        #region Event args helpers

        private BuildEventContext MakeBuildEventContext()
        {
            return new BuildEventContext(1, 1, 1, 1);
        }

        private BuildStartedEventArgs MakeBuildStartedEventArgs()
        {
            return new BuildStartedEventArgs(null, null, _buildStartTime);
        }

        private BuildFinishedEventArgs MakeBuildFinishedEventArgs(bool succeeded)
        {
            return new BuildFinishedEventArgs(null, null, succeeded, _buildFinishTime);
        }

        private ProjectStartedEventArgs MakeProjectStartedEventArgs(string projectFile, string targetNames = "Build")
        {
            return new ProjectStartedEventArgs("", "", projectFile, targetNames, new Dictionary<string, string>(), new List<DictionaryEntry>())
            {
                BuildEventContext = MakeBuildEventContext(),
            };
        }

        private ProjectFinishedEventArgs MakeProjectFinishedEventArgs(string projectFile, bool succeeded)
        {
            return new ProjectFinishedEventArgs(null, null, projectFile, succeeded)
            {
                BuildEventContext = MakeBuildEventContext(),
            };
        }

        private TargetStartedEventArgs MakeTargetStartedEventArgs(string projectFile, string targetName)
        {
            return new TargetStartedEventArgs("", "", targetName, projectFile, targetFile: projectFile)
            {
                BuildEventContext = MakeBuildEventContext(),
            };
        }

        private TargetFinishedEventArgs MakeTargetFinishedEventArgs(string projectFile, string targetName, bool succeeded)
        {
            return new TargetFinishedEventArgs("", "", targetName, projectFile, targetFile: projectFile, succeeded)
            {
                BuildEventContext = MakeBuildEventContext(),
            };
        }

        private TaskStartedEventArgs MakeTaskStartedEventArgs(string projectFile, string taskName)
        {
            return new TaskStartedEventArgs("", "", projectFile, taskFile: projectFile, taskName)
            {
                BuildEventContext = MakeBuildEventContext(),
            };
        }

        private TaskFinishedEventArgs MakeTaskFinishedEventArgs(string projectFile, string taskName, bool succeeded)
        {
            return new TaskFinishedEventArgs("", "", projectFile, taskFile: projectFile, taskName, succeeded)
            {
                BuildEventContext = MakeBuildEventContext(),
            };
        }

        private BuildWarningEventArgs MakeWarningEventArgs(string warning)
        {
            return new BuildWarningEventArgs("", "", "", 0, 0, 0, 0, warning, null, null)
            {
                BuildEventContext = MakeBuildEventContext(),
            };
        }

        private BuildErrorEventArgs MakeErrorEventArgs(string error)
        {
            return new BuildErrorEventArgs("", "", "", 0, 0, 0, 0, error, null, null)
            {
                BuildEventContext = MakeBuildEventContext(),
            };
        }

        #endregion

        #region Build summary tests

        private void InvokeLoggerCallbacksForSimpleProject(bool succeeded, Action additionalCallbacks)
        {
            BuildStarted?.Invoke(_eventSender, MakeBuildStartedEventArgs());
            ProjectStarted?.Invoke(_eventSender, MakeProjectStartedEventArgs(_projectFile));

            TargetStarted?.Invoke(_eventSender, MakeTargetStartedEventArgs(_projectFile, "Build"));
            TaskStarted?.Invoke(_eventSender, MakeTaskStartedEventArgs(_projectFile, "Task"));

            additionalCallbacks();

            Thread.Sleep(1_000);

            TaskFinished?.Invoke(_eventSender, MakeTaskFinishedEventArgs(_projectFile, "Task", succeeded));
            TargetFinished?.Invoke(_eventSender, MakeTargetFinishedEventArgs(_projectFile, "Build", succeeded));

            ProjectFinished?.Invoke(_eventSender, MakeProjectFinishedEventArgs(_projectFile, succeeded));
            BuildFinished?.Invoke(_eventSender, MakeBuildFinishedEventArgs(succeeded));
        }

        [Fact]
        public Task PrintsBuildSummary_Succeeded()
        {
            InvokeLoggerCallbacksForSimpleProject(succeeded: true, () => { });

            return Verify(_outputWriter.ToString(), _settings);
        }

        [Fact]
        public Task PrintBuildSummary_SucceededWithWarnings()
        {
            InvokeLoggerCallbacksForSimpleProject(succeeded: true, () =>
            {
                WarningRaised?.Invoke(_eventSender, MakeWarningEventArgs("Warning!"));
            });

            return Verify(_outputWriter.ToString(), _settings);
        }

        [Fact]
        public Task PrintBuildSummary_Failed()
        {
            InvokeLoggerCallbacksForSimpleProject(succeeded: false, () => { });
            return Verify(_outputWriter.ToString(), _settings);
        }

        [Fact]
        public Task PrintBuildSummary_FailedWithErrors()
        {
           InvokeLoggerCallbacksForSimpleProject(succeeded: false, () =>
           {
               ErrorRaised?.Invoke(_eventSender, MakeErrorEventArgs("Error!"));
           });

           return Verify(_outputWriter.ToString(), _settings);
        }

        #endregion
    }
}
