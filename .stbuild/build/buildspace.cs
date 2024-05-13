using System;
using System.IO;
using System.Collections.Generic;
using SprutCAMTech.BuildSystem.Info;
using SprutCAMTech.BuildSystem.Logging;
using SprutCAMTech.BuildSystem.SettingsReader;
using SprutCAMTech.BuildSystem.SettingsReader.Object;
using SprutCAMTech.BuildSystem.Variants;
using SprutCAMTech.BuildSystem.ProjectList.Common;
using SprutCAMTech.BuildSystem.Package;
using SprutCAMTech.BuildSystem.Builder.MsDelphi;
using SprutCAMTech.BuildSystem.VersionManager.Common;
using SprutCAMTech.BuildSystem.ProjectCache.Common;
using SprutCAMTech.BuildSystem.ProjectCache.NuGet;
using SprutCAMTech.BuildSystem.HashGenerator.Common;
using SprutCAMTech.BuildSystem.Restorer.Nuget;
using SprutCAMTech.BuildSystem.Cleaner.Common;
using SprutCAMTech.BuildSystem.PackageManager.Nuget;
using SprutCAMTech.BuildSystem.ProjectList.Helpers;
using SprutCAMTech.BuildSystem.ManagerObject.Interfaces;

/// <inheritdoc />
class BuildSpaceSettings : SettingsObject
{
    const StringComparison IGNCASE = StringComparison.CurrentCultureIgnoreCase;

    const string DEVFEED = "https://nexus.office.sprut.ru/repository/dev-feed/index.json";
    const string MSTFEED = "https://nexus.office.sprut.ru/repository/master-feed/index.json";

    ILogger _logger;
    ReaderJson _readerJson;
    private string GitBranch => BuildInfo.JenkinsParam(JenkinsInfo.BranchName) + "";

    /// <inheritdoc />
    /// <param name="configFiles"> Json configuration file paths </param>
    /// <param name="wdir"> Working directory </param>
    /// <param name="logger"> Build space logger </param>
    public BuildSpaceSettings(string[] configFiles, string wdir, ILogger logger) : base() {
        _logger = logger;
        _readerJson = new ReaderJson(_logger);
        _readerJson.ReadRules(configFiles);
        ReaderLocalVars = _readerJson.LocalVars;
        ReaderDefines = _readerJson.Defines;

        Projects = new HashSet<string>() {
            Path.Combine(wdir, "..\\AllureDUnitXPackage\\main\\.stbuild\\AllureDUnitXProject.json"),
            Path.Combine(wdir, "..\\allure-delphi\\main\\.stbuild\\AllureDelphiProject.json")
        };

        ProjectListProps = new ProjectListCommonProps(_logger) {
            BuildInfoSaverProps = new BuildInfoSaverCommonProps(),
            AnalyzerProps = new AnalyzerCommonProps(),
            SourceHashCalculatorProps = new SourceHashCalculatorCommonProps(),
            CompilerProps = new CompilerCommonProps(),
            CopierBuildResultsProps = new CopierBuildResultsCommonProps(),
            DeployerProps = new DeployerCommonProps(),
            ProjectRestorerProps = new ProjectRestorerCommonProps
            {
                RestoreInsteadOfBuild = (info) => false
            },
            GetNextVersion = GetNextVersion.FromRemotePackages
        };

        RegisterBSObjects();
    }

    /// <summary>
    /// Register Build System control objects
    /// </summary>
    private void RegisterBSObjects() {
        Variants = new() {
            new() {
                Name = "Debug_x64",
                Configurations = new() { [Variant.NodeConfig] = "Debug" },
                Platforms =      new() { [Variant.NodePlatform] = "Win64" }
            },
            new() {
                Name = "Release_x64",
                Configurations = new() { [Variant.NodeConfig] = "Release" },
                Platforms =      new() { [Variant.NodePlatform] = "Win64" }
            },
            new() {
                Name = "Debug_x32",
                Configurations = new() { [Variant.NodeConfig] = "Debug" },
                Platforms =      new() { [Variant.NodePlatform] = "Win32" }
            },
            new() {
                Name = "Release_x32",
                Configurations = new() { [Variant.NodeConfig] = "Release" },
                Platforms =      new() { [Variant.NodePlatform] = "Win32" }
            }
        };

        AddManagerProp("builder_delphi", new() {"Release_x64", "Release_x32"}, builderDelphiRelease);
        AddManagerProp("builder_delphi", new() {"Debug_x64", "Debug_x32"}, builderDelphiDebug);
        AddManagerProp("package_manager", null, packageManagerNuget);
        AddManagerProp("version_manager", null, versionManagerCommon);
        AddManagerProp("hash_generator", null, hashGeneratorCommon);
        AddManagerProp("restorer", null, restorerNuget);
        AddManagerProp("cleaner", null, cleanerCommon);
        AddManagerProp("cleaner_delphi", null, cleanerCommonDelphi);
        AddManagerProp("project_cache", null, projectCacheNuGet); // projectCacheCommon
    }

    BuilderMsDelphiProps builderDelphiRelease => new() {
        Name = "builder_delphi_release",
        BuilderVersion = "12.1-23.0",
        MsBuilderPath = _readerJson.LocalVars["msbuilder_path"],
        EnvBdsPath = _readerJson.LocalVars["env_bds"],
        RsVarsPath = _readerJson.LocalVars["rsvars_path"],
        AutoClean = true,
        BuildParams = new Dictionary<string, string?>
        {
            ["-verbosity"] = "normal",
            ["-consoleloggerparameters"] = "ErrorsOnly",
            ["-nologo"] = "true",
            ["/p:DCC_Warnings"] = "false",
            ["/p:DCC_Hints"] = "false",
            ["/p:DCC_MapFile"] = "3",
            ["/p:DCC_AssertionsAtRuntime"] = "true",
            ["/p:DCC_DebugInformation"] = "2",
            ["/p:DCC_DebugDCUs"] = "false",
            ["/p:DCC_IOChecking"] = "true",
            ["/p:DCC_WriteableConstants"] = "true",
            ["/t:build"] = "true",
            ["/p:DCC_Optimize"] = "false",
            ["/p:DCC_GenerateStackFrames"] = "false",
            ["/p:DCC_LocalDebugSymbols"] = "false",
            ["/p:DCC_SymbolReferenceInfo"] = "0",
            ["/p:DCC_IntegerOverflowCheck"] = "false",
            ["/p:DCC_RangeChecking"] = "false"
        }
    };

    BuilderMsDelphiProps builderDelphiDebug {
        get {
            var bdelphi = new BuilderMsDelphiProps(builderDelphiRelease);
            bdelphi.Name = "builder_delphi_debug";
            bdelphi.BuildParams["/p:DCC_GenerateStackFrames"] = "true";
            bdelphi.BuildParams["/p:DCC_LocalDebugSymbols"] = "true";
            bdelphi.BuildParams["/p:DCC_SymbolReferenceInfo"] = "2";
            bdelphi.BuildParams["/p:DCC_IntegerOverflowCheck"] = "true";
            bdelphi.BuildParams["/p:DCC_RangeChecking"] = "true";
            return bdelphi;
        }
    }

    VersionManagerCommonProps versionManagerCommon => new() {
        Name = "version_manager_common",
        DepthSearch = 2,
        DevelopBranchName = GitBranch.EndsWith("develop", IGNCASE) ? GitBranch : "develop",
        MasterBranchName =  GitBranch.EndsWith("master", IGNCASE)  ? GitBranch : "master",
        ReleaseBranchName = GitBranch.EndsWith("release", IGNCASE) ? GitBranch : "release"
    };

    ProjectCacheCommonProps projectCacheCommon => new() {
        Name = "project_cache_main",
        TempDir = "./hash"
    };

    ProjectCacheNuGetProps projectCacheNuGet => new() {
        Name = "project_cache_nuget",
        VersionManagerProps = versionManagerCommon,
        PackageManagerProps = packageManagerNuget,
        TempDir = "./hash"
    };

    HashGeneratorCommonProps hashGeneratorCommon => new() {
        Name = "hash_generator_main",
        HashAlgorithmType = HashAlgorithmType.Sha256
    };

    RestorerNugetProps restorerNuget => new() { 
        Name = "restorer_main" 
    };

    CleanerCommonProps cleanerCommon => new() {
        Name = "cleaner_default_main",
        AllBuildResults = true
    };

    CleanerCommonProps cleanerCommonDelphi => new() {
        Name = "cleaner_delphi_main",
        AllBuildResults = true,
        Paths = new Dictionary<string, List<string>>
        {
            ["$project:output_dcu$"] = new() { "*.dcu" }
        }
    };

    PackageManagerNugetProps packageManagerNuget => new() {
        Name = "package_manager_nuget_rc",
        SetStorageInfo = SetStorageInfoFunc,
        GitOptions = new()
    };

    private StorageInfo SetStorageInfoFunc(PackageAction packageAction, string packageId, VersionProp? packageVersion) {
       var isMaster = GitBranch.EndsWith("master", IGNCASE) 
            && packageAction != PackageAction.Reclaim && packageAction != PackageAction.Delete;
        var si = new StorageInfo() {
            Url = isMaster ? MSTFEED : DEVFEED,
            ApiKey = Environment.GetEnvironmentVariable("ST_NUGET_API_KEY")
        };

        _logger.debug($"SetStorageInfoFunc: url={si.Url} - apiKey has " + !string.IsNullOrEmpty(si.ApiKey));

        return si;
    }
}