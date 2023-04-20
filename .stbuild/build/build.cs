using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using Nuke.Common;
using Nuke.Common.CI.Jenkins;
using SprutCAMTech.Logging;
using SprutCAMTech.Logging.Console;
using SprutCAMTech.BuildSystem;
using SprutCAMTech.BuildSystem.Info;
using SprutCAMTech.BuildSystem.SettingsReader;
using SprutCAMTech.BuildSystem.SettingsReader.Object;

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
    public readonly string Variant = "Release_x64";

    /// <inheritdoc/>
    public static ILogger Logger = new LoggerConsole();

    private IBuildSpace? _buildSpace;
    /// <summary> Build Space instance </summary>
    private IBuildSpace BSpace { 
        get
        {
            _buildSpace ??= InitBuildSpace();
            return _buildSpace;
        }
    }

    private IBuildSpace InitBuildSpace() {
        var localJsonFile = Path.Combine(RootDirectory, $"buildspace.{BuildInfo.RunParams[RunInfo.Local]}.json");
        var bsJsonFile = Path.Combine(RootDirectory, "buildspace.json");
        SettingsObject config = new BuildSpaceSettings(new[] {bsJsonFile, localJsonFile});
        return new BuildSpace(Logger, RootDirectory + "//temp", SettingsReaderType.Object, config);
    }

    /// <summary>
    /// Set build constants
    /// </summary>
    private Target SetBuildInfo => _ => _
    .Executes(() => {
        Logger.setMinLevel(SprutCAMTech.Logging.LogLevel.info);

        // params provided to command line
        BuildInfo.RunParams[RunInfo.Variant] = Variant;
        BuildInfo.RunParams[RunInfo.NoRestore] = "false";
        BuildInfo.RunParams[RunInfo.NoCheckRestoredFiles] = "false";
        BuildInfo.RunParams[RunInfo.Local] = "local";

        // jenkins machine params
        if (Jenkins.Instance != null) {
            BuildInfo.RunParams[RunInfo.Local] = "jenkins";
            BuildInfo.JenkinsParams[JenkinsInfo.BranchName] = Jenkins.Instance.BranchName ?? "develop";
            BuildInfo.JenkinsParams[JenkinsInfo.BuildDisplayName] = Jenkins.Instance.BuilDisplayName;
            BuildInfo.JenkinsParams[JenkinsInfo.BuildNumber] = Jenkins.Instance.BuildNumber;
            BuildInfo.JenkinsParams[JenkinsInfo.BuildTag] = Jenkins.Instance.BuildTag;
            BuildInfo.JenkinsParams[JenkinsInfo.ChangeId] = Jenkins.Instance.ChangeId;
            BuildInfo.JenkinsParams[JenkinsInfo.ExecutorNumber] = Jenkins.Instance.ExecutorNumber;
            BuildInfo.JenkinsParams[JenkinsInfo.GitBranch] = Jenkins.Instance.GitBranch;
            BuildInfo.JenkinsParams[JenkinsInfo.GitCommit] = Jenkins.Instance.GitCommit;
            BuildInfo.JenkinsParams[JenkinsInfo.GitPreviousCommit] = Jenkins.Instance.GitPreviousCommit;
            BuildInfo.JenkinsParams[JenkinsInfo.GitPreviousSuccessfulCommit] = Jenkins.Instance.GitPreviousSuccessfulCommit;
            BuildInfo.JenkinsParams[JenkinsInfo.GitUrl] = Jenkins.Instance.GitUrl;
            BuildInfo.JenkinsParams[JenkinsInfo.JenkinsHome] = Jenkins.Instance.JenkinsHome;
            BuildInfo.JenkinsParams[JenkinsInfo.JobBaseName] = Jenkins.Instance.JobBaseName;
            BuildInfo.JenkinsParams[JenkinsInfo.JobDisplayUrl] = Jenkins.Instance.JobDisplayUrl;
            BuildInfo.JenkinsParams[JenkinsInfo.JobName] = Jenkins.Instance.JobName;
            BuildInfo.JenkinsParams[JenkinsInfo.NodeLabels] = Jenkins.Instance.NodeLabels;
            BuildInfo.JenkinsParams[JenkinsInfo.NodeName] = Jenkins.Instance.NodeName;
            BuildInfo.JenkinsParams[JenkinsInfo.RunChangesDisplayUrl] = Jenkins.Instance.RunChangesDisplayUrl;
            BuildInfo.JenkinsParams[JenkinsInfo.RunDisplayUrl] = Jenkins.Instance.RunDisplayUrl;
            BuildInfo.JenkinsParams[JenkinsInfo.Workspace] = Jenkins.Instance.Workspace;
            Logger.info("Current branch: " + BuildInfo.JenkinsParams[JenkinsInfo.BranchName]);
            Logger.info("ChangeId: " + Jenkins.Instance.ChangeId);
            Logger.info("Change target: " + Environment.GetEnvironmentVariable("CHANGE_TARGET"));
        }

        Logger.debug($"{nameof(RunInfo.Variant)}:{Variant}");
        Logger.debug($"{nameof(RunInfo.Local)}:{BuildInfo.RunParam(RunInfo.Local)}");
    });

    /// <summary> Restoring build space </summary>
    private Target Restore => _ => _
        .DependsOn(SetBuildInfo)
        .Executes(() => {
            BSpace.Restore(Variant);
        });

    /// <summary> Parameterized compile </summary>
    private Target Compile => _ => _
        .DependsOn(SetBuildInfo)
        .Executes(() => {
            BSpace.Projects.Compile(Variant, true);
        });

    /// <summary> Publishing packages </summary>
    private Target Deploy => _ => _
        .DependsOn(SetBuildInfo)
        .Executes(() => {
            // !! публикация в master feed !!
            BuildInfo.JenkinsParams[JenkinsInfo.BranchName] = BuildSpaceSettings.MSTBRANCHNAME;
            BSpace.Projects.Compile("Release_x32", true);
            BSpace.Projects.Compile("Release_x64", true);
            BSpace.Projects.Deploy(Variant);
        });

    /// <summary> Cleaning build results </summary>
    private Target Clean => _ => _
        .DependsOn(SetBuildInfo)
        .Executes(() => {
            BSpace.Projects.Clean(Variant);
        });
}