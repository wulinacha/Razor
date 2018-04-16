// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Composition;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.CodeAnalysis.Razor
{
    [Export(typeof(ProjectSnapshotChangeTrigger))]
    internal class DocumentGenerator : ProjectSnapshotChangeTrigger
    {
        private readonly Dictionary<string, FileEntry> _files;
        private readonly Dictionary<string, ProjectEntry> _projects;

        private readonly object _lock;
        private readonly Dictionary<string, DocumentEntry> _dirty;
        private Timer _timer;

        public DocumentGenerator()
        {
            _files = new Dictionary<string, FileEntry>(FilePathComparer.Instance);
            _projects = new Dictionary<string, ProjectEntry>(FilePathComparer.Instance);

            _lock = new object();
            _dirty = new Dictionary<string, DocumentEntry>(FilePathComparer.Instance);
        }

        // Used in unit tests to control the timer delay.
        public TimeSpan Delay { get; set; } = TimeSpan.FromSeconds(2);

        public override void Initialize(ProjectSnapshotManagerBase projectManager)
        {
            if (projectManager == null)
            {
                throw new ArgumentNullException(nameof(projectManager));
            }

            projectManager.Changed += ProjectManager_Changed;
        }

        private void AddProject(ProjectSnapshot project)
        {
            _projects.Add(project.FilePath, new ProjectEntry(project.FilePath)
            {
                ProjectSnapshot = project,
            });
        }

        private void RemoveProject(ProjectSnapshot project)
        {
            _projects.Remove(project.FilePath);
        }

        private void AddDocument(ProjectSnapshot project, RazorDocument document)
        {
            var documentEntry = new DocumentEntry(project.FilePath, document.FilePath)
            {
                Document = document,
            };

            var projectEntry = _projects[project.FilePath];
            projectEntry.Documents.Add(document.FilePath, documentEntry);

            if (!_files.TryGetValue(document.FilePath, out var fileEntry))
            {
                fileEntry = new FileEntry(document.FilePath);
                _files.Add(document.FilePath, fileEntry);
            }

            fileEntry.Documents.Add(project.FilePath, documentEntry);
            Enqueue(documentEntry);
        }

        private void RemoveDocument(ProjectSnapshot project, RazorDocument document)
        {
            var projectEntry = _projects[project.FilePath];
            var fileEntry = _files[document.FilePath];

            // Suppress any future work
            var documentEntry = projectEntry.Documents[document.FilePath];
            documentEntry.Detached = true;

            projectEntry.Documents.Remove(document.FilePath);
            fileEntry.Documents.Remove(project.FilePath);

            if (fileEntry.Documents.Count == 0)
            {
                _files.Remove(document.FilePath);
            }
        }

        private void SetDirty(ProjectSnapshot project, RazorDocument document)
        {
            var documentEntry = _projects[project.FilePath].Documents[document.FilePath];
            documentEntry.KnownVersion = documentEntry.KnownVersion.GetNewerVersion();

            Enqueue(documentEntry);
        }

        private void Enqueue(DocumentEntry documentEntry)
        {
            lock (_lock)
            {
                _dirty[documentEntry.FilePath] = documentEntry;

                StartWorker();
            }
        }

        private void StartWorker()
        {
            if (_timer == null)
            {
                _timer = new Timer(Timer_Tick, null, Delay, Timeout.InfiniteTimeSpan);
            }
        }

        private async void Timer_Tick(object state) // Yeah, I know.
        {
            lock (_lock)
            {
                _timer.Change(Timeout.Infinite, Timeout.Infinite);

                var work = _dirty.Values.ToArray();
                _dirty.Clear();
            }

            new SourceTextContainer.


            lock (_lock)
            {
                // Resetting the timer allows another batch of work to start.
                _timer.Dispose();
                _timer = null;

                // If more work came in while we were running start the worker again.
                if (_dirty.Count > 0)
                {
                    StartWorker();
                }
            }
        }

        private void ProjectManager_Changed(object sender, ProjectChangeEventArgs e)
        {
            switch (e.Kind)
            {
                case ProjectChangeKind.ProjectAdded:
                    {
                        AddProject(e.Project);

                        for (var i = 0; i < e.Project.Documents.Count; i++)
                        {
                            AddDocument(e.Project, e.Project.Documents[i]);
                        }

                        break;
                    }

                case ProjectChangeKind.ProjectRemoved:
                    {
                        for (var i = 0; i < e.Project.Documents.Count; i++)
                        {
                            RemoveDocument(e.Project, e.Project.Documents[i]);
                        }

                        RemoveProject(e.Project);

                        break;
                    }

                case ProjectChangeKind.ProjectChanged:
                case ProjectChangeKind.TagHelpersChanged:
                case ProjectChangeKind.DocumentsChanged:
                    {
                        var projectEntry = _projects[e.Project.FilePath];

                        // Capture newest snapshot
                        projectEntry.ProjectSnapshot = e.Project;

                        var documentsByFilePath = e.Project.Documents.ToDictionary(d => d.FilePath, d => d);

                        var removals = new List<RazorDocument>();
                        foreach (var kvp in projectEntry.Documents)
                        {
                            // Just process removals during this part, we will do additions + changes next.
                            if (!documentsByFilePath.ContainsKey(kvp.Key))
                            {
                                // Missing from new version.
                                removals.Add(kvp.Value.Document);
                            }
                        }

                        for (var i = 0; i < removals.Count; i++)
                        {
                            RemoveDocument(projectEntry.ProjectSnapshot, removals[i]);
                        }

                        foreach (var kvp in documentsByFilePath)
                        {
                            if (!projectEntry.Documents.TryGetValue(kvp.Key, out var documentEntry))
                            {
                                AddDocument(projectEntry.ProjectSnapshot, kvp.Value);
                            }
                            else if (!object.Equals(kvp.Value, documentEntry.Document))
                            {
                                // A change in the document properties, treat this as a Remove+Add.
                                RemoveDocument(projectEntry.ProjectSnapshot, documentEntry.Document);
                                AddDocument(projectEntry.ProjectSnapshot, kvp.Value);
                            }
                            else if (e.Kind != ProjectChangeKind.DocumentsChanged)
                            {
                                // Mark dirty if this is a project change (not just a document change).
                                SetDirty(e.Project, kvp.Value);
                            }
                        }
                        break;
                    }

                default:
                    {
                        throw new InvalidOperationException($"Unknown ProjectChangeKind: {e.Kind}");
                    }
            }
        }

        private class FileEntry
        {
            public FileEntry(string filePath)
            {
                FilePath = filePath;

                Documents = new Dictionary<string, DocumentEntry>(FilePathComparer.Instance);
            }

            public string FilePath { get; }

            public Dictionary<string, DocumentEntry> Documents { get; }
        }

        private class ProjectEntry
        {
            public ProjectEntry(string filePath)
            {
                FilePath = filePath;

                Documents = new Dictionary<string, DocumentEntry>();
            }

            public string FilePath { get; }

            public ProjectSnapshot ProjectSnapshot { get; set; }

            public Dictionary<string, DocumentEntry> Documents { get; }
        }

        private class DocumentEntry
        {
            public DocumentEntry(string projectFilePath, string filePath)
            {
                ProjectFilePath = projectFilePath;
                FilePath = filePath;

                ComputedVersion = VersionStamp.Default;
                KnownVersion = VersionStamp.Create();
            }

            public string FilePath { get; }

            public string ProjectFilePath { get; }

            public bool Detached { get; set; }

            public RazorDocument Document { get; set; }

            public VersionStamp ComputedVersion { get; set; }

            public VersionStamp KnownVersion { get; set; }
        }
    }
}
