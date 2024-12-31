﻿using DotnetSortAndSyncRefs.Common;
using DotnetSortAndSyncRefs.Xml;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.XPath;
using McMaster.Extensions.CommandLineUtils;

namespace DotnetSortAndSyncRefs.Commands
{
    [HelpOption]
    internal abstract class CommandBase : ICommandBase
    {
        private readonly string _commandStartMessage;
        protected readonly IServiceProvider ServiceProvider;
        protected readonly IFileSystem FileSystem;
        protected readonly Common.IReporter Reporter;
        public List<string> AllExtensions { get; private set; }
        public List<string> FileProps { get; private set; }
        public List<string> FileProjects { get; private set; }
        public List<string> AllFiles { get; private set; }
        public List<string> ProjFilesWithNonSortedReferences { get; private set; }

        public HashSet<string> ProjectFilePostfix = new(StringComparer.OrdinalIgnoreCase) {
            ".csproj",
            ".vbproj",
            ".fsproj",
        };

        public HashSet<string> AdditionalFilePostfix = new(StringComparer.OrdinalIgnoreCase) {
            ".props"
        };

        protected readonly string CentralPackageManagementFile = "Directory.Packages.props";

        public virtual string Path { get; set; }

        [Option(CommandOptionType.NoValue,
            Description =
                "Specifies whether to do a dry run. It shows the effected actions, but do not change the files.",
            ShortName = "dr", LongName = "dry-run")]
        public bool IsDryRun { get; set; } = false;

        public bool IsNoDryRun => !IsDryRun;

        protected CommandBase(IServiceProvider serviceProvider, string commandStartMessage)
        {
            ServiceProvider = serviceProvider;
            _commandStartMessage = commandStartMessage;
            FileSystem = serviceProvider.GetRequiredService<IFileSystem>();
            Reporter = serviceProvider.GetRequiredService<Common.IReporter>();

          
        }

        public virtual Task<int> OnExecute()
        {
            Reporter.Output($"start command: {_commandStartMessage}");

            if (string.IsNullOrEmpty(Path))
            {
                Path = FileSystem
                    .Directory
                    .GetCurrentDirectory();
            }

            if (!(FileSystem.File.Exists(Path) ||
                  FileSystem.Directory.Exists(Path)))
            {
                Reporter.Error("Directory or file does not exist.");
                return Task.FromResult(ErrorCodes.DirectoryDoNotExists);
            }

            AllExtensions = new List<string> { };
            AllExtensions.AddRange(ProjectFilePostfix);
            AllExtensions.AddRange(AdditionalFilePostfix);

            FileProjects = LoadFilesFromExtension(ProjectFilePostfix);
            FileProps = LoadFilesFromExtension(AdditionalFilePostfix);

            AllFiles = new List<string> { };
            AllFiles.AddRange(FileProjects);
            AllFiles.AddRange(FileProps);

            if (AllFiles.Count == 0)
            {
                Reporter.Error($"no '{string.Join(", ", AllExtensions)}'' files found.");
                return Task.FromResult(ErrorCodes.FileDoNotExists);
            }

            Reporter.Output("Running analysis ...");
            ProjFilesWithNonSortedReferences = Inspect();

            if (ProjFilesWithNonSortedReferences == null)
            {
                Reporter.Do("Please solve the issue of the Project file(s).");
                return Task.FromResult(ErrorCodes.ProjectFileHasNotAValidXmlFormat);
            }

            return Task.FromResult(ErrorCodes.Ok);
        }

        public async Task<List<string>> InspectAsync()
        {
            var projFilesWithNonSortedReferences = new List<string>();

            foreach (var projFile in AllFiles)
            {
                try
                {
                    var xmlFile = ServiceProvider.GetRequiredService<XmlAllElementFile>();
                    await xmlFile
                        .LoadFileReadOnlyAsync(projFile)
                        .ConfigureAwait(false);

                    foreach (var itemGroup in xmlFile.ItemGroups)
                    {
                        var references = itemGroup
                            .XPathSelectElements(ConstConfig.AllElementTypesQuery)
                            .Select(x => x.Attribute("Include")?.Value.ToLowerInvariant())
                            .ToList();

                        if (references.Count <= 1) continue;

                        var sortedReferences = references
                            .OrderBy(x => x)
                            .ToList();

                        var result = references.SequenceEqual(sortedReferences);

                        if (!result && !projFilesWithNonSortedReferences.Contains(projFile))
                        {
                            Reporter.NotOk($"» {projFile}");
                            projFilesWithNonSortedReferences.Add(projFile);
                        }
                        else if (!projFilesWithNonSortedReferences.Contains(projFile))
                        {
                            Reporter.Ok($"» {projFile}");
                            projFilesWithNonSortedReferences.Add(projFile);
                        }
                    }
                }
                catch (Exception e)
                {
                    Reporter.Error($"» {projFile}");
                    Reporter.Error(e.Message);
                    return null;
                }
            }

            return projFilesWithNonSortedReferences;
        }

        private List<string> Inspect()
        {
            return InspectAsync()
                .ConfigureAwait(true).GetAwaiter().GetResult();
        }

        private List<string> LoadFilesFromExtension(IEnumerable<string> extensions)
        {
            var projFiles = new List<string>();
            if (FileSystem.File.Exists(Path))
            {
                projFiles.Add(Path);
            }
            else
            {
                projFiles = extensions
                    .SelectMany(ext => FileSystem
                        .Directory
                        .GetFiles(Path, $"*{ext}", SearchOption.AllDirectories))
                    .ToList();
            }

            return projFiles;
        }
    }
}
