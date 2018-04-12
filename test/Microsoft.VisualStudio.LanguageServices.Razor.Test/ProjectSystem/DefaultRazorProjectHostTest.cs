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
using ItemCollection = Microsoft.VisualStudio.ProjectSystem.ItemCollection;

namespace Microsoft.CodeAnalysis.Razor.ProjectSystem
{
    public class DefaultRazorProjectHostTest : ForegroundDispatcherTestBase
    {
        public DefaultRazorProjectHostTest()
        {
            Workspace = new AdhocWorkspace();
            ProjectManager = new TestProjectSnapshotManager(Dispatcher, Workspace);

            ConfigurationItems = new ItemCollection(Rules.RazorConfiguration.SchemaName);
            ExtensionItems = new ItemCollection(Rules.RazorExtension.SchemaName);
            DocumentItems = new ItemCollection(Rules.RazorGenerateWithTargetPath.SchemaName);
            RazorGeneralProperties = new PropertyCollection(Rules.RazorGeneral.SchemaName);
        }

        private ItemCollection ConfigurationItems { get; }

        private ItemCollection ExtensionItems { get; }

        private ItemCollection DocumentItems { get; }

        private PropertyCollection RazorGeneralProperties { get; }

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

        [ForegroundFact] // This can happen if the .xaml files aren't included correctly.
        public async Task DefaultRazorProjectHost_OnProjectChanged_NoRulesDefined()
        {
            // Arrange
            var changes = new TestProjectChangeDescription[]
            {
            };

            var services = new TestProjectSystemServices("Test.csproj");
            var host = new DefaultRazorProjectHost(services, Workspace, ProjectManager);

            // Act & Assert
            await Task.Run(async () => await host.LoadAsync());
            Assert.Empty(ProjectManager.Projects);

            await Task.Run(async () => await host.OnProjectChanged(services.CreateUpdate(changes)));
            Assert.Empty(ProjectManager.Projects);
        }

        [ForegroundFact]
        public async Task OnProjectChanged_ReadsProperties_InitializesProject()
        {
            // Arrange
            RazorGeneralProperties.Property(Rules.RazorGeneral.RazorLangVersionProperty, "2.1");
            RazorGeneralProperties.Property(Rules.RazorGeneral.RazorDefaultConfigurationProperty, "MVC-2.1");

            ConfigurationItems.Item("MVC-2.1");
            ConfigurationItems.Property("MVC-2.1", Rules.RazorConfiguration.ExtensionsProperty, "MVC-2.1;Another-Thing");

            ExtensionItems.Item("MVC-2.1");
            ExtensionItems.Item("Another-Thing");

            DocumentItems.Item("File.cshtml");
            DocumentItems.Property("File.cshtml", Rules.RazorGenerateWithTargetPath.TargetPathProperty, "File.cshtml");

            var changes = new TestProjectChangeDescription[]
            {
                RazorGeneralProperties.ToChange(),
                ConfigurationItems.ToChange(),
                ExtensionItems.ToChange(),
                DocumentItems.ToChange(),
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
                snapshot.DocumentFilePaths.OrderBy(d => d),
                d =>
                {
                    var document = snapshot.GetDocument(d);
                    Assert.Equal("c:\\MyProject\\File.cshtml", document.FilePath);
                    Assert.Equal("File.cshtml", document.TargetPath);
                });

            await Task.Run(async () => await host.DisposeAsync());
            Assert.Empty(ProjectManager.Projects);
        }

        [ForegroundFact]
        public async Task OnProjectChanged_NoVersionFound_DoesNotIniatializeProject()
        {
            // Arrange
            RazorGeneralProperties.Property(Rules.RazorGeneral.RazorLangVersionProperty, "");
            RazorGeneralProperties.Property(Rules.RazorGeneral.RazorDefaultConfigurationProperty, "");

            ConfigurationItems.Item("TestConfiguration");

            ExtensionItems.Item("TestExtension");

            DocumentItems.Item("File.cshtml");

            var changes = new TestProjectChangeDescription[]
            {
                RazorGeneralProperties.ToChange(),
                ConfigurationItems.ToChange(),
                ExtensionItems.ToChange(),
                DocumentItems.ToChange(),
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
            RazorGeneralProperties.Property(Rules.RazorGeneral.RazorLangVersionProperty, "2.1");
            RazorGeneralProperties.Property(Rules.RazorGeneral.RazorDefaultConfigurationProperty, "MVC-2.1");

            ConfigurationItems.Item("MVC-2.1");
            ConfigurationItems.Property("MVC-2.1", Rules.RazorConfiguration.ExtensionsProperty, "MVC-2.1;Another-Thing");

            ExtensionItems.Item("MVC-2.1");
            ExtensionItems.Item("Another-Thing");

            DocumentItems.Item("File.cshtml");
            DocumentItems.Property("File.cshtml", Rules.RazorGenerateWithTargetPath.TargetPathProperty, "File.cshtml");

            var changes = new TestProjectChangeDescription[]
            {
                RazorGeneralProperties.ToChange(),
                ConfigurationItems.ToChange(),
                ExtensionItems.ToChange(),
                DocumentItems.ToChange(),
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
                snapshot.DocumentFilePaths.OrderBy(d => d),
                d => 
                {
                    var document = snapshot.GetDocument(d);
                    Assert.Equal("c:\\MyProject\\File.cshtml", document.FilePath);
                    Assert.Equal("File.cshtml", document.TargetPath);
                });

            // Act - 2
            RazorGeneralProperties.Property(Rules.RazorGeneral.RazorLangVersionProperty, "2.0");
            RazorGeneralProperties.Property(Rules.RazorGeneral.RazorDefaultConfigurationProperty, "MVC-2.0");
            ConfigurationItems.RemoveItem("MVC-2.1");
            ConfigurationItems.Item("MVC-2.0", new Dictionary<string, string>() { { "Extensions", "MVC-2.0;Another-Thing" }, });
            ExtensionItems.Item("MVC-2.0");
            DocumentItems.Item("c:\\AnotherProject\\AnotherFile.cshtml", new Dictionary<string, string>()
            {
                { Rules.RazorGenerateWithTargetPath.TargetPathProperty, "Pages\\AnotherFile.cshtml" },
            });

            changes = new TestProjectChangeDescription[]
            {
                RazorGeneralProperties.ToChange(changes[0].After),
                ConfigurationItems.ToChange(changes[1].After),
                ExtensionItems.ToChange(changes[2].After),
                DocumentItems.ToChange(changes[3].After),
            };

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
                snapshot.DocumentFilePaths.OrderBy(d => d),
                d =>
                {
                    var document = snapshot.GetDocument(d);
                    Assert.Equal("c:\\AnotherProject\\AnotherFile.cshtml", document.FilePath);
                    Assert.Equal("Pages\\AnotherFile.cshtml", document.TargetPath);
                },
                d =>
                {
                    var document = snapshot.GetDocument(d);
                    Assert.Equal("c:\\MyProject\\File.cshtml", document.FilePath);
                    Assert.Equal("File.cshtml", document.TargetPath);
                });

            await Task.Run(async () => await host.DisposeAsync());
            Assert.Empty(ProjectManager.Projects);
        }

        [ForegroundFact]
        public async Task OnProjectChanged_VersionRemoved_DeinitializesProject()
        {
            // Arrange
            RazorGeneralProperties.Property(Rules.RazorGeneral.RazorLangVersionProperty, "2.1");
            RazorGeneralProperties.Property(Rules.RazorGeneral.RazorDefaultConfigurationProperty, "MVC-2.1");

            ConfigurationItems.Item("MVC-2.1");
            ConfigurationItems.Property("MVC-2.1", Rules.RazorConfiguration.ExtensionsProperty, "MVC-2.1;Another-Thing");

            ExtensionItems.Item("MVC-2.1");
            ExtensionItems.Item("Another-Thing");

            DocumentItems.Item("File.cshtml");
            DocumentItems.Property("File.cshtml", Rules.RazorGenerateWithTargetPath.TargetPathProperty, "File.cshtml");

            var changes = new TestProjectChangeDescription[]
            {
                RazorGeneralProperties.ToChange(),
                ConfigurationItems.ToChange(),
                ExtensionItems.ToChange(),
                DocumentItems.ToChange(),
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
            RazorGeneralProperties.Property(Rules.RazorGeneral.RazorLangVersionProperty, "");
            RazorGeneralProperties.Property(Rules.RazorGeneral.RazorDefaultConfigurationProperty, "");

            changes = new TestProjectChangeDescription[]
            {
                RazorGeneralProperties.ToChange(changes[0].After),
                ConfigurationItems.ToChange(changes[1].After),
                ExtensionItems.ToChange(changes[2].After),
                DocumentItems.ToChange(changes[3].After),
            };

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
            RazorGeneralProperties.Property(Rules.RazorGeneral.RazorLangVersionProperty, "2.1");
            RazorGeneralProperties.Property(Rules.RazorGeneral.RazorDefaultConfigurationProperty, "MVC-2.1");

            ConfigurationItems.Item("MVC-2.1");
            ConfigurationItems.Property("MVC-2.1", Rules.RazorConfiguration.ExtensionsProperty, "MVC-2.1;Another-Thing");

            ExtensionItems.Item("MVC-2.1");
            ExtensionItems.Item("Another-Thing");

            DocumentItems.Item("File.cshtml");
            DocumentItems.Property("File.cshtml", Rules.RazorGenerateWithTargetPath.TargetPathProperty, "File.cshtml");

            var changes = new TestProjectChangeDescription[]
            {
                RazorGeneralProperties.ToChange(),
                ConfigurationItems.ToChange(),
                ExtensionItems.ToChange(),
                DocumentItems.ToChange(),
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
            RazorGeneralProperties.Property(Rules.RazorGeneral.RazorLangVersionProperty, "2.0");
            RazorGeneralProperties.Property(Rules.RazorGeneral.RazorDefaultConfigurationProperty, "MVC-2.0");
            ConfigurationItems.Item("MVC-2.0", new Dictionary<string, string>() { { "Extensions", "MVC-2.0;Another-Thing" }, });

            changes = new TestProjectChangeDescription[]
            {
                RazorGeneralProperties.ToChange(changes[0].After),
                ConfigurationItems.ToChange(changes[1].After),
                ExtensionItems.ToChange(changes[2].After),
                DocumentItems.ToChange(changes[3].After),
            };

            await Task.Run(async () => await host.OnProjectChanged(services.CreateUpdate(changes)));

            // Assert - 3
            Assert.Empty(ProjectManager.Projects);
        }

        [ForegroundFact]
        public async Task OnProjectRenamed_RemovesHostProject_CopiesConfiguration()
        {
            // Arrange
            RazorGeneralProperties.Property(Rules.RazorGeneral.RazorLangVersionProperty, "2.1");
            RazorGeneralProperties.Property(Rules.RazorGeneral.RazorDefaultConfigurationProperty, "MVC-2.1");

            ConfigurationItems.Item("MVC-2.1");
            ConfigurationItems.Property("MVC-2.1", Rules.RazorConfiguration.ExtensionsProperty, "MVC-2.1;Another-Thing");

            ExtensionItems.Item("MVC-2.1");
            ExtensionItems.Item("Another-Thing");

            DocumentItems.Item("File.cshtml");
            DocumentItems.Property("File.cshtml", Rules.RazorGenerateWithTargetPath.TargetPathProperty, "File.cshtml");

            var changes = new TestProjectChangeDescription[]
            {
                RazorGeneralProperties.ToChange(),
                ConfigurationItems.ToChange(),
                ExtensionItems.ToChange(),
                DocumentItems.ToChange(),
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
                : base(dispatcher, Mock.Of<ErrorReporter>(), Array.Empty<ProjectSnapshotChangeTrigger>(), workspace)
            {
            }
        }
    }
}
