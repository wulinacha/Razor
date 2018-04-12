// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.VisualStudio.LanguageServices;
using Microsoft.VisualStudio.ProjectSystem;

namespace Microsoft.CodeAnalysis.Razor.ProjectSystem
{
    // Somewhat similar to https://github.com/dotnet/project-system/blob/fa074d228dcff6dae9e48ce43dd4a3a5aa22e8f0/src/Microsoft.VisualStudio.ProjectSystem.Managed/ProjectSystem/LanguageServices/LanguageServiceHost.cs
    //
    // This class is responsible for intializing the Razor ProjectSnapshotManager for cases where
    // MSBuild provides configuration support (>= 2.1).
    [AppliesTo("DotNetCoreRazor & DotNetCoreRazorConfiguration")]
    [Export(ExportContractNames.Scopes.UnconfiguredProject, typeof(IProjectDynamicLoadComponent))]
    internal class DefaultRazorProjectHost : RazorProjectHostBase
    {
        private IDisposable _subscription;

        [ImportingConstructor]
        public DefaultRazorProjectHost(
            IUnconfiguredProjectCommonServices commonServices,
            [Import(typeof(VisualStudioWorkspace))] Workspace workspace)
            : base(commonServices, workspace)
        {
        }

        // Internal for testing
        internal DefaultRazorProjectHost(
            IUnconfiguredProjectCommonServices commonServices,
             Workspace workspace,
             ProjectSnapshotManagerBase projectManager)
            : base(commonServices, workspace, projectManager)
        {
        }

        protected override async Task InitializeCoreAsync(CancellationToken cancellationToken)
        {
            await base.InitializeCoreAsync(cancellationToken).ConfigureAwait(false);

            // Don't try to evaluate any properties here since the project is still loading and we require access
            // to the UI thread to push our updates.
            //
            // Just subscribe and handle the notification later.
            // Don't try to evaluate any properties here since the project is still loading and we require access
            // to the UI thread to push our updates.
            //
            // Just subscribe and handle the notification later.
            var receiver = new ActionBlock<IProjectVersionedValue<IProjectSubscriptionUpdate>>(OnProjectChanged);
            _subscription = CommonServices.ActiveConfiguredProjectSubscription.JointRuleSource.SourceBlock.LinkTo(
                receiver,
                initialDataAsNew: true,
                suppressVersionOnlyUpdates: true,
                ruleNames: new string[] 
                {
                    Rules.RazorGeneral.SchemaName,
                    Rules.RazorConfiguration.SchemaName,
                    Rules.RazorExtension.SchemaName,
                    Rules.RazorGenerateWithTargetPath.SchemaName,
                });
        }

        protected override async Task DisposeCoreAsync(bool initialized)
        {
            await base.DisposeCoreAsync(initialized).ConfigureAwait(false);

            if (initialized)
            {
                _subscription.Dispose();
            }
        }

        // Internal for testing
        internal async Task OnProjectChanged(IProjectVersionedValue<IProjectSubscriptionUpdate> update)
        {
            if (IsDisposing || IsDisposed)
            {
                return;
            }

            await CommonServices.TasksService.LoadedProjectAsync(async () =>
            {
                await ExecuteWithLock(async () =>
                {
                    RazorConfiguration configuration = null;
                    if (TryGetLangaugeVersion(update.Value, out var languageVersion) &&
                        TryGetDefaultConfigurationName(update.Value, out var defaultConfiguration))
                    {
                        var configurations = GetConfigurations(update.Value, languageVersion);
                        configuration = configurations.Where(c => c.ConfigurationName == defaultConfiguration).FirstOrDefault();
                    }

                    if (configuration == null)
                    {
                        // Ok we can't find a language version. Let's assume this project isn't using Razor then.
                        await UpdateProjectUnsafeAsync(null).ConfigureAwait(false);
                        return;
                    }

                    var documents = GetDocuments(update.Value);
                    var hostProject = new HostProject(CommonServices.UnconfiguredProject.FullPath, configuration, documents);
                    await UpdateProjectUnsafeAsync(hostProject).ConfigureAwait(false);
                });
            }, registerFaultHandler: true);
        }

        private static bool TryGetLangaugeVersion(IProjectSubscriptionUpdate update, out RazorLanguageVersion version)
        {
            if (update.CurrentState.TryGetValue(Rules.RazorGeneral.SchemaName, out var rule) &&
                rule.Properties.TryGetValue(Rules.RazorGeneral.RazorLangVersionProperty, out var text) &&
                !string.IsNullOrWhiteSpace(text))
            {
                if (!RazorLanguageVersion.TryParse(text, out version))
                {
                    version = RazorLanguageVersion.Latest;
                }

                return true;
            }

            version = null;
            return false;
        }

        private static bool TryGetDefaultConfigurationName(IProjectSubscriptionUpdate update, out string configurationName)
        {
            if (update.CurrentState.TryGetValue(Rules.RazorGeneral.SchemaName, out var rule) &&
                rule.Properties.TryGetValue(Rules.RazorGeneral.RazorDefaultConfigurationProperty, out var text) &&
                !string.IsNullOrWhiteSpace(text))
            {
                configurationName = text;
                return true;
            }

            configurationName = null;
            return false;
        }

        private static RazorConfiguration[] GetConfigurations(IProjectSubscriptionUpdate update, RazorLanguageVersion languageVersion)
        {
            if (!update.CurrentState.TryGetValue(Rules.RazorExtension.SchemaName, out var extensionRule) ||
                !update.CurrentState.TryGetValue(Rules.RazorConfiguration.SchemaName, out var configurationRule))
            {
                return Array.Empty<RazorConfiguration>();
            }
            
            var extensions = new List<RazorExtension>();
            foreach (var kvp in extensionRule.Items)
            {
                if (!string.IsNullOrEmpty(kvp.Key))
                {
                    extensions.Add(new ProjectSystemRazorExtension(kvp.Key));
                }
            }

            var configurations = new List<RazorConfiguration>();
            foreach (var kvp in configurationRule.Items)
            {
                var configurationName = kvp.Key;
                if (string.IsNullOrWhiteSpace(configurationName))
                {
                    continue;
                }

                var includedExtensions = Array.Empty<RazorExtension>();
                if (kvp.Value.TryGetValue(Rules.RazorConfiguration.ExtensionsProperty, out var text))
                {
                    includedExtensions = text
                        .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(name => extensions.Where(e => e.ExtensionName == name).FirstOrDefault())
                        .Where(e => e != null)
                        .ToArray();
                }

                configurations.Add(new ProjectSystemRazorConfiguration(languageVersion, configurationName, includedExtensions));
            }

            return configurations.ToArray();
        }

        private RazorDocument[] GetDocuments(IProjectSubscriptionUpdate update)
        {
            if (!update.CurrentState.TryGetValue(Rules.RazorGenerateWithTargetPath.SchemaName, out var rule))
            {
                return Array.Empty<RazorDocument>();
            }

            var documents = new List<RazorDocument>();
            foreach (var kvp in rule.Items)
            {
                if ( kvp.Value.TryGetValue(Rules.RazorGenerateWithTargetPath.TargetPathProperty, out var targetPath) &&
                    !string.IsNullOrWhiteSpace(kvp.Key) &&
                    !string.IsNullOrWhiteSpace(targetPath))
                {
                    var filePath = CommonServices.UnconfiguredProject.MakeRooted(kvp.Key);
                    documents.Add(new ProjectSystemRazorDocument(filePath, targetPath));
                }
            }

            return documents.ToArray();
        }
    }
}