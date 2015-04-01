// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Reflection;
using Microsoft.Framework.Runtime.Compilation;
using System.IO;

namespace Microsoft.Framework.Runtime.Loader
{
    internal class ProjectAssemblyLoader : IAssemblyLoader
    {
        private readonly IProjectResolver _projectResolver;
        private readonly ILibraryManager _libraryManager;
        private readonly IAssemblyLoadContextAccessor _loadContextAccessor;

        public ProjectAssemblyLoader(IProjectResolver projectResovler,
                                     IAssemblyLoadContextAccessor loadContextAccessor,
                                     ILibraryManager libraryManager)
        {
            _projectResolver = projectResovler;
            _loadContextAccessor = loadContextAccessor;
            _libraryManager = libraryManager;
        }

        public Assembly Load(string name)
        {
            return Load(name, _loadContextAccessor.Default);
        }

        public Assembly Load(AssemblyName assemblyName)
        {
            return Load(assemblyName, _loadContextAccessor.Default);
        }

        public Assembly Load(string name, IAssemblyLoadContext loadContext)
        {
            return Load(new AssemblyName(name), loadContext);
        }

        public Assembly Load(AssemblyName assemblyName, IAssemblyLoadContext loadContext)
        {
            // An assembly name like "MyLibrary!alternate!more-text"
            // is parsed into:
            // name == "MyLibrary"
            // aspect == "alternate"
            // and the more-text may be used to force a recompilation of an aspect that would
            // otherwise have been cached by some layer within Assembly.Load

            string name = assemblyName.Name;
            string aspect = null;

            var parts = name.Split(new[] { '!' }, 3);
            if (parts.Length != 1)
            {
                name = parts[0];
                aspect = parts[1];
            }

            if (!string.IsNullOrEmpty(assemblyName.CultureName) &&
                Path.GetExtension(name).Equals(".resources", StringComparison.OrdinalIgnoreCase))
            {
                name = Path.GetFileNameWithoutExtension(name);
            }

            Project project;
            if (!_projectResolver.TryResolveProject(name, out project))
            {
                return null;
            }

            var export = _libraryManager.GetLibraryExport(name, aspect);

            if (export == null)
            {
                return null;
            }

            foreach (var projectReference in export.MetadataReferences.OfType<IMetadataProjectReference>())
            {
                if (string.Equals(projectReference.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    return projectReference.Load(assemblyName, loadContext);
                }
            }

            return null;
       }
    }
}
