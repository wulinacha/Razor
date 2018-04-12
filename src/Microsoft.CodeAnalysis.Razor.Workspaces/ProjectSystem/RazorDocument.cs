// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.Extensions.Internal;

namespace Microsoft.CodeAnalysis.Razor.ProjectSystem
{
    internal abstract class RazorDocument : IEquatable<RazorDocument>
    {
        public abstract string FilePath { get; }

        public abstract string TargetPath { get; }

        public override bool Equals(object obj)
        {
            return base.Equals(obj as RazorDocument);
        }

        public bool Equals(RazorDocument other)
        {
            if (ReferenceEquals(null, other))
            {
                return false;
            }

            return
                FilePathComparer.Instance.Equals(FilePath, other.FilePath) &&
                FilePathComparer.Instance.Equals(TargetPath, other.TargetPath);
        }

        public override int GetHashCode()
        {
            var hash = new HashCodeCombiner();
            hash.Add(FilePath, FilePathComparer.Instance);
            hash.Add(TargetPath, FilePathComparer.Instance);
            return hash;
        }
    }
}
