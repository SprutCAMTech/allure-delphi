using System.Diagnostics.CodeAnalysis;
using Nuke.Common;
using Nuke.Common.CI.Jenkins;
using SprutTechnology.BuildSystem;
using SprutTechnology.BuildSystem.Info;
using SprutTechnology.Logging.Console;

/// <inheritdoc />
[SuppressMessage("ReSharper", "ClassNeverInstantiated.Global")]
public class Build : NukeBuild
{
    /// <summary>
    /// Calling method when running
    /// </summary>
    public static int Main() => Execute<Build>(x => x.Compile);
    
    /// <summary>
    /// Configuration to build - 'Debug' (default) or 'Release'
    /// </summary>
    [Parameter("Settings provided for running build space")]
    public readonly string Variant = "Debug_x64";

    /// <summary>
    /// Projects scope. By default, projects are set to the default value "main". 
    /// Сan be overridden for each project individually.
    /// </summary>
    [Parameter("Projects scope")]
    public readonly string Scope = "main";

    /// <summary>
    /// Projects scope ('main' is default)
    /// </summary>
    [Parameter("Projects scope")]
    public readonly string ProjectName = "";

    /// <summary>
    /// Defines a file name suffix with local settings
    /// </summary>
    [Parameter("Defines a file name suffix with local settings")]
    public readonly string Local = "local";

    /// <summary>
    /// Don't restore any dependencies
    /// </summary>
    [Parameter("Don't restore any dependencies", Name = "no-restore")]
    public readonly string NoRestore = "false";

    /// <summary>
    /// Don't check that project dependencies build results were copied into parent project output folder. If parent
    /// project was compiled, then miss copying build results of dependency projects
    /// </summary>
    [Parameter("Don't restore any dependencies", Name = "no-check-restored-files")]
    public readonly string NoCheckRestoredFiles = "false";

    /// <summary>
    /// Variable through targets
    /// </summary>
    private IBuildSpace? _buildSpace;

    /// <summary>
    /// Set build constants
    /// </summary>
    private Target SetBuildInfo => _ => _
    .Executes(() => {
        // params provided to command line
        BuildInfo.RunParams[RunInfo.Variant] = Variant;
        _buildSpace?.Logger.debug($"{nameof(RunInfo.Variant)}:{Variant}");

        BuildInfo.RunParams[RunInfo.NoRestore] = NoRestore;
        _buildSpace?.Logger.debug($"{nameof(RunInfo.NoRestore)}:{NoRestore}");

        BuildInfo.RunParams[RunInfo.NoCheckRestoredFiles] = NoCheckRestoredFiles;
        _buildSpace?.Logger.debug($"{nameof(RunInfo.NoCheckRestoredFiles)}:{NoCheckRestoredFiles}");

        BuildInfo.RunParams[RunInfo.ProjectMask] = ProjectName;
        _buildSpace?.Logger.debug($"{nameof(RunInfo.ProjectMask)}:{ProjectName}");

        BuildInfo.RunParams[RunInfo.Local] = Local;
        _buildSpace?.Logger.debug($"{nameof(RunInfo.Local)}:{Local}");

        // params from build machine
        BuildInfo.JenkinsParams[JenkinsInfo.BranchName] = Jenkins.Instance?.BranchName ?? "master";
        BuildInfo.JenkinsParams[JenkinsInfo.BuildDisplayName] = Jenkins.Instance?.BuilDisplayName;
        BuildInfo.JenkinsParams[JenkinsInfo.BuildNumber] = Jenkins.Instance?.BuildNumber;
        BuildInfo.JenkinsParams[JenkinsInfo.BuildTag] = Jenkins.Instance?.BuildTag;
        BuildInfo.JenkinsParams[JenkinsInfo.ChangeId] = Jenkins.Instance?.ChangeId;
        BuildInfo.JenkinsParams[JenkinsInfo.ExecutorNumber] = Jenkins.Instance?.ExecutorNumber;
        BuildInfo.JenkinsParams[JenkinsInfo.GitBranch] = Jenkins.Instance?.GitBranch;
        BuildInfo.JenkinsParams[JenkinsInfo.GitCommit] = Jenkins.Instance?.GitCommit;
        BuildInfo.JenkinsParams[JenkinsInfo.GitPreviousCommit] = Jenkins.Instance?.GitPreviousCommit;
        BuildInfo.JenkinsParams[JenkinsInfo.GitPreviousSuccessfulCommit] = Jenkins.Instance?.GitPreviousSuccessfulCommit;
        BuildInfo.JenkinsParams[JenkinsInfo.GitUrl] = Jenkins.Instance?.GitUrl;
        BuildInfo.JenkinsParams[JenkinsInfo.JenkinsHome] = Jenkins.Instance?.JenkinsHome;
        BuildInfo.JenkinsParams[JenkinsInfo.JobBaseName] = Jenkins.Instance?.JobBaseName;
        BuildInfo.JenkinsParams[JenkinsInfo.JobDisplayUrl] = Jenkins.Instance?.JobDisplayUrl;
        BuildInfo.JenkinsParams[JenkinsInfo.JobName] = Jenkins.Instance?.JobName;
        BuildInfo.JenkinsParams[JenkinsInfo.NodeLabels] = Jenkins.Instance?.NodeLabels;
        BuildInfo.JenkinsParams[JenkinsInfo.NodeName] = Jenkins.Instance?.NodeName;
        BuildInfo.JenkinsParams[JenkinsInfo.RunChangesDisplayUrl] = Jenkins.Instance?.RunChangesDisplayUrl;
        BuildInfo.JenkinsParams[JenkinsInfo.RunDisplayUrl] = Jenkins.Instance?.RunDisplayUrl;
        BuildInfo.JenkinsParams[JenkinsInfo.Workspace] = Jenkins.Instance?.Workspace;
    });

    /// <summary> Restoring build space </summary>
    private Target Restore => _ => _
        .DependsOn(SetBuildInfo)
        .Executes(() => {
            var logger = new LoggerConsole();
            _buildSpace ??= new BuildSpace(logger, RootDirectory);
            _buildSpace.Restore(Variant);
        });

    /// <summary> Clone repositories, which are set as VCS dependencies </summary>
    private Target DownloadRepos => _ => _
        .DependsOn(SetBuildInfo)
        .Executes(() => {
            var logger = new LoggerConsole();
            _buildSpace ??= new BuildSpace(logger, RootDirectory);
            _buildSpace.RestoreVcsDependencies();
        });

    /// <summary> Parameterized compile </summary>
    private Target Compile => _ => _
        .DependsOn(SetBuildInfo)
        .Executes(() => {
            var logger = new LoggerConsole();
            // logger.MinLevel = SprutTechnology.Logging.LogLevel.Debug;
            _buildSpace ??= new BuildSpace(logger, RootDirectory);
            _buildSpace.Projects.Compile(Variant, true);
        });

    /// <summary> Compiling projects for all release configurations </summary>
    private Target CompileAllReleasePlatf => _ => _
        .DependsOn(SetBuildInfo)
        .Executes(() => {
            var logger = new LoggerConsole();
            // logger.MinLevel = SprutTechnology.Logging.LogLevel.debug;
            _buildSpace ??= new BuildSpace(logger, RootDirectory);
            _buildSpace.Projects.Compile("Release_x32", true);
            _buildSpace.Projects.Compile("Release_x64", true);
        });

    /// <summary> Publishing packages </summary>
    private Target Deploy => _ => _
        .Before(DownloadRepos)
        .DependsOn(CompileAllReleasePlatf) //include SetBuildInfo
        .Executes(() => {
            var logger = new LoggerConsole();
            _buildSpace ??= new BuildSpace(logger, RootDirectory);
            _buildSpace.Projects.Deploy(Variant);
        });

    /// <summary> Deleting old versions of packages </summary>
    private Target Reclaim => _ => _
        .Executes(() => {
            var logger = new LoggerConsole();
            _buildSpace ??= new BuildSpace(logger, RootDirectory);
            _buildSpace.Projects.Reclaim(Variant);
        });

    /// <summary> Cleaning build results </summary>
    private Target Clean => _ => _
        .Executes(() => {
            var logger = new LoggerConsole();
            _buildSpace ??= new BuildSpace(logger, RootDirectory);
            _buildSpace.Projects.Clean(Variant);
        });
}