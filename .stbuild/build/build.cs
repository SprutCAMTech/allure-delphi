using System.Diagnostics.CodeAnalysis;
using Nuke.Common;
using SprutTechnology.BuildSystem;
using SprutTechnology.Logging.Console;
using LogLevel = SprutTechnology.Logging.LogLevel;

/// <inheritdoc />
[SuppressMessage("ReSharper", "ClassNeverInstantiated.Global")]
public class Build : NukeBuild
{
    /// <summary>
    /// Calling method when running
    /// </summary>
    // ReSharper disable once InconsistentNaming
    public static int Main() => Execute<Build>(x => x.Compile);
    
    /// <summary>
    /// Configuration to build - 'Debug' (default) or 'Release'
    /// </summary>
    [Parameter("Settings provided for running build space")]
    public readonly string RunSettings = "Release_x64";

    /// <summary>
    /// Variable through targets
    /// </summary>
    private IBuildSpace? _buildSpace;

    /// <summary>
    /// Download dependencies, which set in settings, and build them into package (according to settings)
    /// </summary>
    // ReSharper disable once UnusedMember.Local
    private Target Restore => _ => _
        .Executes(() =>
        {
            var logger = new LoggerConsole();
            _buildSpace ??= new BuildSpace(logger, RootDirectory);
            _buildSpace.restorePackageDependencies(RunSettings);
        });

    /// <summary>
    /// Clone repositories, which are set as VCS dependencies
    /// </summary>
    // ReSharper disable once UnusedMember.Local
    private Target DownloadRepos => _ => _
        .Executes(() =>
        {
            var logger = new LoggerConsole();
            _buildSpace ??= new BuildSpace(logger, RootDirectory);
            _buildSpace.RestoreVcsDependencies();
        });

    /// <summary>
    /// Prepare binary files for copying into packages
    /// </summary>
    // ReSharper disable once UnusedMember.Local
    private Target Compile => _ => _
        .Executes(() =>
        {
            var logger = new LoggerConsole();
            logger.MinLevel = SprutTechnology.Logging.LogLevel.Debug;
            _buildSpace ??= new BuildSpace(logger, RootDirectory);
            var projectList = _buildSpace.getOrderedProjects(ProjectQueryType.Dependency);
            projectList.compile(RunSettings);
        });

    private Target CompileAllReleasePlatf => _ => _
        .Executes(() =>
        {
            var logger = new LoggerConsole();
            logger.MinLevel = SprutTechnology.Logging.LogLevel.Debug;
            _buildSpace ??= new BuildSpace(logger, RootDirectory);
            var projectList = _buildSpace.getOrderedProjects(ProjectQueryType.Dependency);
            projectList.compile("Release_x32");
            projectList.compile("Release_x64");
        });

    /// <summary>
    /// Publishing packages into storage
    /// </summary>
    // ReSharper disable once UnusedMember.Local
    private Target Deploy => _ => _
        .DependsOn(DownloadRepos)
        .DependsOn(CompileAllReleasePlatf)
        .Executes(() =>
        {
            var logger = new LoggerConsole();
            _buildSpace ??= new BuildSpace(logger, RootDirectory);
            var projectList = _buildSpace.getOrderedProjects(ProjectQueryType.Dependency);
            projectList.deploy(RunSettings);
        });

    /// <summary>
    /// Deleting old versions of packages
    /// </summary>
    // ReSharper disable once UnusedMember.Local
    private Target Reclaim => _ => _
        .Executes(() =>
        {
            var logger = new LoggerConsole();
            _buildSpace ??= new BuildSpace(logger, RootDirectory);
            var projectList = _buildSpace.getOrderedProjects(ProjectQueryType.Dependency);
            //projectList.Reverse();
            projectList.reclaim(RunSettings);
        });

    /// <summary>
    /// Clean build results for each project
    /// </summary>
    // ReSharper disable once UnusedMember.Local
    private Target Clean => _ => _
        .Executes(() =>
        {
            var logger = new LoggerConsole();
            logger.MinLevel = LogLevel.Debug;
            _buildSpace ??= new BuildSpace(logger, RootDirectory);
            _buildSpace.Projects.Clean(RunSettings);
        });
}