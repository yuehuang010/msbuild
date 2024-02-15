
# Question the build (Is Up-To-Date?)

MSBuild can skip Target or Task from running again by implementing some checks. Targets uses the Inputs and Outputs parameters to compare the timestamp of input and output files. Tasks have different behavior and thus have different rules. See below for details of each task shipped with MSBuild. Custom tasks can implement `IIncrementalTask` interface.

Question switch ask if the next build is up-to-date. It will start a build but will error out as soon as a target or task is not up-to-date. This error stops the build and allows investigation at the point of failure. Recommend to use binlog logging to gather all the information. Targets or task that don't have an up-to-date check will build normally. 

Fast Up-To-Date is a system that is implemented by a IDE that decides if it needs to run MSBuild.  MSBuild takes a non-trival amount of time to load, evaluate, and run through each target and task.  Fast Up-To-Date is faster, but can be less accurate, suitable for an IDE and a human interface.  It is not accurate enough for CI.

## Usage
Question is designed to be used on the command line.  Run your normal build, then run again with /question.
```
msbuild /p:Configuration=Debug Project1.csproj /bl:build.binlog
msbuild /p:Configuration=Debug Project1.csproj /bl:incremental.binlog /question
```
If no errors, then your build is up-to-date.  
If there are errors, then investigate the error.  See common errors below.  Keep both logs to help with your investigation.

## Custom Tasks
Task author can implement the optional `IIncrementalTask` interface that will explose `FailIfNotIncremental`. `FailIfNotIncremental` is true when /question switch is used. The custom task will need to decide how it want to handle their behavior.  For example.  If there is already a message describing why the task cannot be skipped, then simply convert the message to a error. Remember to return false to stop the build.  For the best reproducibility, do not modify any files on disk.

```C#
if (FailIfNotIncremental)
{
  TaskLoggingHelper.LogErrorWithCodeFromResources("ToolTask.NotUpToDate");
  return false;
}
else
{
  TaskLoggingHelper.LogMessageWithCodeFromResources("ToolTask.NotUpToDate");
}
```

## Shipping Tasks
When question switch is used, it will modify the shipping task with these behavior.  Note: this is still experimental and can change.

`Exec`
Doesn't have a up-to-date check.  It will always run.

`ToolTask`
Error when inherited ToolTask overrides `SkipTaskExecution()` and returns false.

`Touch`
Warn when a file touched.  It is unclear if the file touched will participate in the build as it is a common practice to touch a file to signal external tool to run.  Use Target Inputs and Outputs to skip this task.

`Copy`
Error when any copy action.

`WriteLinesToFile`
Error when WriteOnlyWhenDifferent is true.  This task could be used to append to a log file that isn't participating in the build itself.

`Delete`
Warn that a file still exists and is to be deleted.

`Move`
No warning or errors.  This Task doesn't move any files as the file could not exist anymore.

`DownloadFile`
Error when SkipUnchangedFiles is true.

`GenerateResource` 
Error when any files needs to be generated.

`MakeDir`
Error if folder doesn't exist.

`RemoveDir`
Error if folder still exist.

`Unzip`
Error when SkipUnchangedFiles is true.

`ZipDirectory`
Error if the destination zip file doesn't exists.


## Common Error
- **Typographical error**. Spelling, casing, or incorrect path.  Check if the target inputs and outputs real files.
- Inputs and Outputs are sometimes used for Cross Product. Try to move all to Outputs. If not possible, use Returns instead of Inputs.
- **Double Checks**.  Since target and task could be incremental, if both are implemented, then it can lead task skipping but not the task.  For example, a Target has inputs A and outputs B.  If A is newer, than B, then the target will start.  If the task compares the content of A and B and deems nothing has changed, then B is not updated.  If such case, this leads to target rerunning.
- **Exec Task** are not Skipable, thus they should be wrapped with Target Inputs and Outputs or other systems.  For backwards compatibility, Question will not issue an error.
- **FileWritten**.  The common clean system will remove files that aren't in the FileWritten itemgroup.  Sometimes task output won't be add to FileWritten itemgroup.
- **Build, then Build**.  Sometimes, a 2nd build will break up to date.  Question after the 2nd build. 
