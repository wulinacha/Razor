// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.VisualStudio.ProjectSystem;
using Moq;
using Xunit;

namespace Microsoft.CodeAnalysis.Razor.ProjectSystem
{
    public class DefaultRazorProjectHostTest : ForegroundDispatcherTestBase
    {
        public DefaultRazorProjectHostTest()
        {
            Workspace = new AdhocWorkspace();
            ProjectManager = new TestProjectSnapshotManager(Dispatcher, Workspace);
        }

        private TestProjectSnapshotManager ProjectManager { get; }

        private Workspace Workspace { get; }

        [ForegroundFact]
        public async Task DefaultRazorProjectHost_ForegroundThread_CreateAndDispose_Succeeds()
        {
            // Arrange
            var services = new TestProjectSystemServices("c:\\MyProject\\Test.csproj");
            var host = new DefaultRazorProjectHost(services, Workspace, ProjectManager);

            // Act & Assert
            await host.LoadAsync();
            Assert.Empty(ProjectManager.Projects);

            await host.DisposeAsync();
            Assert.Empty(ProjectManager.Projects);
        }

        [ForegroundFact]
        public async Task DefaultRazorProjectHost_BackgroundThread_CreateAndDispose_Succeeds()
        {
            // Arrange
            var services = new TestProjectSystemServices("c:\\MyProject\\Test.csproj");
            var host = new DefaultRazorProjectHost(services, Workspace, ProjectManager);

            // Act & Assert
            await Task.Run(async () => await host.LoadAsync());
            Assert.Empty(ProjectManager.Projects);

            await Task.Run(async () => await host.DisposeAsync());
            Assert.Empty(ProjectManager.Projects);
        }

        [ForegroundFact]
        public async Task OnProjectChanged_ReadsProperties_InitializesProject()
        {
            // Arrange
            var changes = new TestProjectChangeDescription[]
            {
                new TestProjectChangeDescription()
                {
                    RuleName = Rules.RazorGeneral.SchemaName,
                    After = TestProjectRuleSnapshot.CreateProperties(Rules.RazorGeneral.SchemaName, new Dictionary<string, string>()
                    {
                        { Rules.RazorGeneral.RazorLangVersionProperty, "2.1" },
                        { Rules.RazorGeneral.RazorDefaultConfigurationProperty, "MVC-2.1" },
                    }),
                },
                new TestProjectChangeDescription()
                {
                    RuleName = Rules.RazorConfiguration.SchemaName,
                    After = TestProjectRuleSnapshot.CreateItems(Rules.RazorConfiguration.SchemaName, new Dictionary<string, Dictionary<string, string>>()
                    {
                        { "MVC-2.1", new Dictionary<string, string>() { { "Extensions", "MVC-2.1;Another-Thing" }, } },
                    })
                },
                new TestProjectChangeDescription()
                {
                    RuleName = Rules.RazorExtension.SchemaName,
                    After = TestProjectRuleSnapshot.CreateItems(Rules.RazorExtension.SchemaName, new Dictionary<string, Dictionary<string, string>>()
                    {
                        { "MVC-2.1", new Dictionary<string, string>(){ } },
                        { "Another-Thing", new Dictionary<string, string>(){ } },
                    })
                },
                new TestProjectChangeDescription()
                {
                    RuleName = Rules.RazorGenerateWithTargetPath.SchemaName,
                    After = TestProjectRuleSnapshot.CreateItems(Rules.RazorGenerateWithTargetPath.SchemaName, new Dictionary<string, Dictionary<string, string>>()
                    {
                        {
                            "File.cshtml",
                            new Dictionary<string, string>()
                            {
                                { Rules.RazorGenerateWithTargetPath.TargetPathProperty, "File.cshtml" }
                            }
                        },
                    })
                }
            };

            var services = new TestProjectSystemServices("c:\\MyProject\\Test.csproj");

            var host = new DefaultRazorProjectHost(services, Workspace, ProjectManager);

            await Task.Run(async () => await host.LoadAsync());
            Assert.Empty(ProjectManager.Projects);

            // Act
            await Task.Run(async () => await host.OnProjectChanged(services.CreateUpdate(changes)));

            // Assert
            var snapshot = Assert.Single(ProjectManager.Projects);
            Assert.Equal("c:\\MyProject\\Test.csproj", snapshot.FilePath);

            Assert.Equal(RazorLanguageVersion.Version_2_1, snapshot.Configuration.LanguageVersion);
            Assert.Equal("MVC-2.1", snapshot.Configuration.ConfigurationName);
            Assert.Collection(
                snapshot.Configuration.Extensions,
                e => Assert.Equal("MVC-2.1", e.ExtensionName),
                e => Assert.Equal("Another-Thing", e.ExtensionName));

            Assert.Collection(
                snapshot.Documents,
                d =>
                {
                    Assert.Equal("c:\\MyProject\\File.cshtml", d.FilePath);
                    Assert.Equal("File.cshtml", d.TargetPath);
                });

            await Task.Run(async () => await host.DisposeAsync());
            Assert.Empty(ProjectManager.Projects);
        }

        [ForegroundFact]
        public async Task OnProjectChanged_NoVersionFound_DoesNotIniatializeProject()
        {
            // Arrange
            var changes = new TestProjectChangeDescription[]
            {
                new TestProjectChangeDescription()
                {
                    RuleName = Rules.RazorGeneral.SchemaName,
                    After = TestProjectRuleSnapshot.CreateProperties(Rules.RazorGeneral.SchemaName, new Dictionary<string, string>()
                    {
                        { Rules.RazorGeneral.RazorLangVersionProperty, "" },
                        { Rules.RazorGeneral.RazorDefaultConfigurationProperty, "" },
                    }),
                },

                // Everything else is ignored if there is no default configuration
                new TestProjectChangeDescription()
                {
                    RuleName = Rules.RazorConfiguration.SchemaName,
                    After = TestProjectRuleSnapshot.CreateItems(Rules.RazorConfiguration.SchemaName, new Dictionary<string, Dictionary<string, string>>()
                    {
                        { "TestConfiguration", new Dictionary<string, string>() },
                    })
                },
                new TestProjectChangeDescription()
                {
                    RuleName = Rules.RazorExtension.SchemaName,
                    After = TestProjectRuleSnapshot.CreateItems(Rules.RazorExtension.SchemaName, new Dictionary<string, Dictionary<string, string>>()
                    {
                        { "TestExtension", new Dictionary<string, string>() },
                    })
                },
                new TestProjectChangeDescription()
                {
                    RuleName = Rules.RazorGenerateWithTargetPath.SchemaName,
                    After = TestProjectRuleSnapshot.CreateItems(Rules.RazorGenerateWithTargetPath.SchemaName, new Dictionary<string, Dictionary<string, string>>()
                    {
                        { "File.cshtml", new Dictionary<string, string>(){ } },
                    })
                }
            };

            var services = new TestProjectSystemServices("c:\\MyProject\\Test.csproj");

            var host = new DefaultRazorProjectHost(services, Workspace, ProjectManager);

            await Task.Run(async () => await host.LoadAsync());
            Assert.Empty(ProjectManager.Projects);

            // Act
            await Task.Run(async () => await host.OnProjectChanged(services.CreateUpdate(changes)));

            // Assert
            Assert.Empty(ProjectManager.Projects);

            await Task.Run(async () => await host.DisposeAsync());
            Assert.Empty(ProjectManager.Projects);
        }

        [ForegroundFact]
        public async Task OnProjectChanged_UpdateProject_Succeeds()
        {
            // Arrange
            var changes = new TestProjectChangeDescription[]
            {
                new TestProjectChangeDescription()
                {
                    RuleName = Rules.RazorGeneral.SchemaName,
                    After = TestProjectRuleSnapshot.CreateProperties(Rules.RazorGeneral.SchemaName, new Dictionary<string, string>()
                    {
                        { Rules.RazorGeneral.RazorLangVersionProperty, "2.1" },
                        { Rules.RazorGeneral.RazorDefaultConfigurationProperty, "MVC-2.1" },
                    }),
                },
                new TestProjectChangeDescription()
                {
                    RuleName = Rules.RazorConfiguration.SchemaName,
                    After = TestProjectRuleSnapshot.CreateItems(Rules.RazorConfiguration.SchemaName, new Dictionary<string, Dictionary<string, string>>()
                    {
                        { "MVC-2.1", new Dictionary<string, string>() { { "Extensions", "MVC-2.1;Another-Thing" }, } },
                    })
                },
                new TestProjectChangeDescription()
                {
                    RuleName = Rules.RazorExtension.SchemaName,
                    After = TestProjectRuleSnapshot.CreateItems(Rules.RazorExtension.SchemaName, new Dictionary<string, Dictionary<string, string>>()
                    {
                        { "MVC-2.1", new Dictionary<string, string>(){ } },
                        { "Another-Thing", new Dictionary<string, string>(){ } },
                    })
                },
                new TestProjectChangeDescription()
                {
                    RuleName = Rules.RazorGenerateWithTargetPath.SchemaName,
                    After = TestProjectRuleSnapshot.CreateItems(Rules.RazorGenerateWithTargetPath.SchemaName, new Dictionary<string, Dictionary<string, string>>()
                    {
                        {
                            "File.cshtml",
                            new Dictionary<string, string>()
                            {
                                { Rules.RazorGenerateWithTargetPath.TargetPathProperty, "File.cshtml" }
                            }
                        },
                    })
                }
            };

            var services = new TestProjectSystemServices("c:\\MyProject\\Test.csproj");

            var host = new DefaultRazorProjectHost(services, Workspace, ProjectManager);

            await Task.Run(async () => await host.LoadAsync());
            Assert.Empty(ProjectManager.Projects);

            // Act - 1
            await Task.Run(async () => await host.OnProjectChanged(services.CreateUpdate(changes)));

            // Assert - 1
            var snapshot = Assert.Single(ProjectManager.Projects);
            Assert.Equal("c:\\MyProject\\Test.csproj", snapshot.FilePath);

            Assert.Equal(RazorLanguageVersion.Version_2_1, snapshot.Configuration.LanguageVersion);
            Assert.Equal("MVC-2.1", snapshot.Configuration.ConfigurationName);
            Assert.Collection(
                snapshot.Configuration.Extensions,
                e => Assert.Equal("MVC-2.1", e.ExtensionName),
                e => Assert.Equal("Another-Thing", e.ExtensionName));

            Assert.Collection(
                snapshot.Documents,
                d => 
                {
                    Assert.Equal("c:\\MyProject\\File.cshtml", d.FilePath);
                    Assert.Equal("File.cshtml", d.TargetPath);
                });

            // Act - 2
            changes[0].After.SetProperty(Rules.RazorGeneral.RazorLangVersionProperty, "2.0");
            changes[0].After.SetProperty(Rules.RazorGeneral.RazorDefaultConfigurationProperty, "MVC-2.0");
            changes[1].After.SetItem("MVC-2.0", new Dictionary<string, string>() { { "Extensions", "MVC-2.0;Another-Thing" }, });
            changes[2].After.SetItem("MVC-2.0", new Dictionary<string, string>());
            changes[3].After.Items = changes[3].After.Items.Add("c:\\AnotherProject\\AnotherFile.cshtml", new Dictionary<string, string>()
            {
                { Rules.RazorGenerateWithTargetPath.TargetPathProperty, "Pages\\AnotherFile.cshtml" },
            }.ToImmutableDictionary());

            await Task.Run(async () => await host.OnProjectChanged(services.CreateUpdate(changes)));

            // Assert - 2
            snapshot = Assert.Single(ProjectManager.Projects);
            Assert.Equal("c:\\MyProject\\Test.csproj", snapshot.FilePath);

            Assert.Equal(RazorLanguageVersion.Version_2_0, snapshot.Configuration.LanguageVersion);
            Assert.Equal("MVC-2.0", snapshot.Configuration.ConfigurationName);
            Assert.Collection(
                snapshot.Configuration.Extensions,
                e => Assert.Equal("MVC-2.0", e.ExtensionName),
                e => Assert.Equal("Another-Thing", e.ExtensionName));

            Assert.Collection(
                snapshot.Documents.OrderBy(d => d.FilePath),
                d =>
                {
                    Assert.Equal("c:\\AnotherProject\\AnotherFile.cshtml", d.FilePath);
                    Assert.Equal("Pages\\AnotherFile.cshtml", d.TargetPath);
                },
                d =>
                {
                    Assert.Equal("c:\\MyProject\\File.cshtml", d.FilePath);
                    Assert.Equal("File.cshtml", d.TargetPath);
                });

            await Task.Run(async () => await host.DisposeAsync());
            Assert.Empty(ProjectManager.Projects);
        }

        [ForegroundFact]
        public async Task OnProjectChanged_VersionRemoved_DeinitializesProject()
        {
            // Arrange
            var changes = new TestProjectChangeDescription[]
            {
                new TestProjectChangeDescription()
                {
                    RuleName = Rules.RazorGeneral.SchemaName,
                    After = TestProjectRuleSnapshot.CreateProperties(Rules.RazorGeneral.SchemaName, new Dictionary<string, string>()
                    {
                        { Rules.RazorGeneral.RazorLangVersionProperty, "2.1" },
                        { Rules.RazorGeneral.RazorDefaultConfigurationProperty, "MVC-2.1" },
                    }),
                },
                new TestProjectChangeDescription()
                {
                    RuleName = Rules.RazorConfiguration.SchemaName,
                    After = TestProjectRuleSnapshot.CreateItems(Rules.RazorConfiguration.SchemaName, new Dictionary<string, Dictionary<string, string>>()
                    {
                        { "MVC-2.1", new Dictionary<string, string>() { { "Extensions", "MVC-2.1;Another-Thing" }, } },
                    })
                },
                new TestProjectChangeDescription()
                {
                    RuleName = Rules.RazorExtension.SchemaName,
                    After = TestProjectRuleSnapshot.CreateItems(Rules.RazorExtension.SchemaName, new Dictionary<string, Dictionary<string, string>>()
                    {
                        { "MVC-2.1", new Dictionary<string, string>(){ } },
                        { "Another-Thing", new Dictionary<string, string>(){ } },
                    })
                }
            };

            var services = new TestProjectSystemServices("c:\\MyProject\\Test.csproj");

            var host = new DefaultRazorProjectHost(services, Workspace, ProjectManager);

            await Task.Run(async () => await host.LoadAsync());
            Assert.Empty(ProjectManager.Projects);

            // Act - 1
            await Task.Run(async () => await host.OnProjectChanged(services.CreateUpdate(changes)));

            // Assert - 1
            var snapshot = Assert.Single(ProjectManager.Projects);
            Assert.Equal("c:\\MyProject\\Test.csproj", snapshot.FilePath);

            Assert.Equal(RazorLanguageVersion.Version_2_1, snapshot.Configuration.LanguageVersion);
            Assert.Equal("MVC-2.1", snapshot.Configuration.ConfigurationName);
            Assert.Collection(
                snapshot.Configuration.Extensions,
                e => Assert.Equal("MVC-2.1", e.ExtensionName),
                e => Assert.Equal("Another-Thing", e.ExtensionName));

            // Act - 2
            changes[0].After.SetProperty(Rules.RazorGeneral.RazorLangVersionProperty, "");
            changes[0].After.SetProperty(Rules.RazorGeneral.RazorDefaultConfigurationProperty, "");

            await Task.Run(async () => await host.OnProjectChanged(services.CreateUpdate(changes)));

            // Assert - 2
            Assert.Empty(ProjectManager.Projects);

            await Task.Run(async () => await host.DisposeAsync());
            Assert.Empty(ProjectManager.Projects);
        }

        [ForegroundFact]
        public async Task OnProjectChanged_AfterDispose_IgnoresUpdate()
        {
            // Arrange
            var changes = new TestProjectChangeDescription[]
            {
                new TestProjectChangeDescription()
                {
                    RuleName = Rules.RazorGeneral.SchemaName,
                    After = TestProjectRuleSnapshot.CreateProperties(Rules.RazorGeneral.SchemaName, new Dictionary<string, string>()
                    {
                        { Rules.RazorGeneral.RazorLangVersionProperty, "2.1" },
                        { Rules.RazorGeneral.RazorDefaultConfigurationProperty, "MVC-2.1" },
                    }),
                },
                new TestProjectChangeDescription()
                {
                    RuleName = Rules.RazorConfiguration.SchemaName,
                    After = TestProjectRuleSnapshot.CreateItems(Rules.RazorConfiguration.SchemaName, new Dictionary<string, Dictionary<string, string>>()
                    {
                        { "MVC-2.1", new Dictionary<string, string>() { { "Extensions", "MVC-2.1;Another-Thing" }, } },
                    })
                },
                new TestProjectChangeDescription()
                {
                    RuleName = Rules.RazorExtension.SchemaName,
                    After = TestProjectRuleSnapshot.CreateItems(Rules.RazorExtension.SchemaName, new Dictionary<string, Dictionary<string, string>>()
                    {
                        { "MVC-2.1", new Dictionary<string, string>(){ } },
                        { "Another-Thing", new Dictionary<string, string>(){ } },
                    })
                }
            };

            var services = new TestProjectSystemServices("c:\\MyProject\\Test.csproj");

            var host = new DefaultRazorProjectHost(services, Workspace, ProjectManager);

            await Task.Run(async () => await host.LoadAsync());
            Assert.Empty(ProjectManager.Projects);

            // Act - 1
            await Task.Run(async () => await host.OnProjectChanged(services.CreateUpdate(changes)));

            // Assert - 1
            var snapshot = Assert.Single(ProjectManager.Projects);
            Assert.Equal("c:\\MyProject\\Test.csproj", snapshot.FilePath);

            Assert.Equal(RazorLanguageVersion.Version_2_1, snapshot.Configuration.LanguageVersion);
            Assert.Equal("MVC-2.1", snapshot.Configuration.ConfigurationName);
            Assert.Collection(
                snapshot.Configuration.Extensions,
                e => Assert.Equal("MVC-2.1", e.ExtensionName),
                e => Assert.Equal("Another-Thing", e.ExtensionName));

            // Act - 2
            await Task.Run(async () => await host.DisposeAsync());

            // Assert - 2
            Assert.Empty(ProjectManager.Projects);

            // Act - 3
            changes[0].After.SetProperty(Rules.RazorGeneral.RazorLangVersionProperty, "2.0");
            changes[0].After.SetProperty(Rules.RazorGeneral.RazorDefaultConfigurationProperty, "MVC-2.0");
            changes[1].After.SetItem("MVC-2.0", new Dictionary<string, string>() { { "Extensions", "MVC-2.0;Another-Thing" }, });

            await Task.Run(async () => await host.OnProjectChanged(services.CreateUpdate(changes)));

            // Assert - 3
            Assert.Empty(ProjectManager.Projects);
        }

        [ForegroundFact]
        public async Task OnProjectRenamed_RemovesHostProject_CopiesConfiguration()
        {
            // Arrange
            var changes = new TestProjectChangeDescription[]
            {
                new TestProjectChangeDescription()
                {
                    RuleName = Rules.RazorGeneral.SchemaName,
                    After = TestProjectRuleSnapshot.CreateProperties(Rules.RazorGeneral.SchemaName, new Dictionary<string, string>()
                    {
                        { Rules.RazorGeneral.RazorLangVersionProperty, "2.1" },
                        { Rules.RazorGeneral.RazorDefaultConfigurationProperty, "MVC-2.1" },
                    }),
                },
                new TestProjectChangeDescription()
                {
                    RuleName = Rules.RazorConfiguration.SchemaName,
                    After = TestProjectRuleSnapshot.CreateItems(Rules.RazorConfiguration.SchemaName, new Dictionary<string, Dictionary<string, string>>()
                    {
                        { "MVC-2.1", new Dictionary<string, string>() { { "Extensions", "MVC-2.1;Another-Thing" }, } },
                    })
                },
                new TestProjectChangeDescription()
                {
                    RuleName = Rules.RazorExtension.SchemaName,
                    After = TestProjectRuleSnapshot.CreateItems(Rules.RazorExtension.SchemaName, new Dictionary<string, Dictionary<string, string>>()
                    {
                        { "MVC-2.1", new Dictionary<string, string>(){ } },
                        { "Another-Thing", new Dictionary<string, string>(){ } },
                    })
                }
            };

            var services = new TestProjectSystemServices("c:\\MyProject\\Test.csproj");

            var host = new DefaultRazorProjectHost(services, Workspace, ProjectManager);

            await Task.Run(async () => await host.LoadAsync());
            Assert.Empty(ProjectManager.Projects);

            // Act - 1
            await Task.Run(async () => await host.OnProjectChanged(services.CreateUpdate(changes)));

            // Assert - 1
            var snapshot = Assert.Single(ProjectManager.Projects);
            Assert.Equal("c:\\MyProject\\Test.csproj", snapshot.FilePath);
            Assert.Same("MVC-2.1", snapshot.Configuration.ConfigurationName);

            // Act - 2
            services.UnconfiguredProject.FullPath = "c:\\AnotherProject\\Test2.csproj";
            await Task.Run(async () => await host.OnProjectRenamingAsync());

            // Assert - 1
            snapshot = Assert.Single(ProjectManager.Projects);
            Assert.Equal("c:\\AnotherProject\\Test2.csproj", snapshot.FilePath);
            Assert.Same("MVC-2.1", snapshot.Configuration.ConfigurationName);

            await Task.Run(async () => await host.DisposeAsync());
            Assert.Empty(ProjectManager.Projects);
        }

        private class TestProjectSnapshotManager : DefaultProjectSnapshotManager
        {
            public TestProjectSnapshotManager(ForegroundDispatcher dispatcher, Workspace workspace) 
                : base(dispatcher, Mock.Of<ErrorReporter>(), Mock.Of<ProjectSnapshotWorker>(), Array.Empty<ProjectSnapshotChangeTrigger>(), workspace)
            {
            }

            protected override void NotifyBackgroundWorker(ProjectSnapshotUpdateContext context)
            {
            }
        }
    }
}
