using System;
using System.Collections.Generic;
using System.IO;

namespace UnityEditor.iOS.Xcode
{
    using GUIDBuildFile = KeyValuePair<CommentedGUID, PBXBuildFile>;
    using GUIDFileRef   = KeyValuePair<CommentedGUID, PBXFileReference>;
    using GUIDGroup     = KeyValuePair<CommentedGUID, PBXGroup>;

    using PBXBuildFileSection             = KnownSectionBase<PBXBuildFile>;
    using PBXFileReferenceSection         = KnownSectionBase<PBXFileReference>;
    using PBXGroupSection                 = KnownSectionBase<PBXGroup>;
    using PBXContainerItemProxySection    = KnownSectionBase<PBXContainerItemProxy>;
    using PBXReferenceProxySection        = KnownSectionBase<PBXReferenceProxy>;
    using PBXSourcesBuildPhaseSection     = KnownSectionBase<PBXSourcesBuildPhase>;
    using PBXFrameworksBuildPhaseSection  = KnownSectionBase<PBXFrameworksBuildPhase>;
    using PBXResourcesBuildPhaseSection   = KnownSectionBase<PBXResourcesBuildPhase>;
    using PBXCopyFilesBuildPhaseSection   = KnownSectionBase<PBXCopyFilesBuildPhase>;
    using PBXShellScriptBuildPhaseSection = KnownSectionBase<PBXShellScriptBuildPhase>;
    using PBXVariantGroupSection          = KnownSectionBase<PBXVariantGroup>;
    using PBXNativeTargetSection          = KnownSectionBase<PBXNativeTarget>;
    using PBXTargetDependencySection      = KnownSectionBase<PBXTargetDependency>;
    using XCBuildConfigurationSection     = KnownSectionBase<XCBuildConfiguration>;
    using XCConfigurationListSection      = KnownSectionBase<XCConfigurationList>;

    public class PBXProject
    {
        readonly Dictionary<string, SectionBase> m_Section = new Dictionary<string, SectionBase>
        {
            { "PBXBuildFile",             new PBXBuildFileSection("PBXBuildFile") },
            { "PBXFileReference",         new PBXFileReferenceSection("PBXFileReference") },
            { "PBXGroup",                 new PBXGroupSection("PBXGroup") },
            { "PBXContainerItemProxy",    new PBXContainerItemProxySection("PBXContainerItemProxy") },
            { "PBXReferenceProxy",        new PBXReferenceProxySection("PBXReferenceProxy") },
            { "PBXSourcesBuildPhase",     new PBXSourcesBuildPhaseSection("PBXSourcesBuildPhase") },
            { "PBXFrameworksBuildPhase",  new PBXFrameworksBuildPhaseSection("PBXFrameworksBuildPhase") },
            { "PBXResourcesBuildPhase",   new PBXResourcesBuildPhaseSection("PBXResourcesBuildPhase") },
            { "PBXCopyFilesBuildPhase",   new PBXCopyFilesBuildPhaseSection("PBXCopyFilesBuildPhase") },
            { "PBXShellScriptBuildPhase", new PBXShellScriptBuildPhaseSection("PBXShellScriptBuildPhase") },
            { "PBXNativeTarget",          new PBXNativeTargetSection("PBXNativeTarget") },
            { "PBXTargetDependency",      new PBXTargetDependencySection("PBXTargetDependency") },
            { "PBXVariantGroup",          new PBXVariantGroupSection("PBXVariantGroup") },
            { "XCBuildConfiguration",     new XCBuildConfigurationSection("XCBuildConfiguration") },
            { "XCConfigurationList",      new XCConfigurationListSection("XCConfigurationList") },

            { "PBXProject",               new PBXProjectSection() },
        };

        readonly List<string> m_Header       = new List<string>();
        readonly List<string> m_Footer       = new List<string>();
        readonly List<string> m_SectionOrder = new List<string>
        {
            "PBXBuildFile", "PBXContainerItemProxy", "PBXCopyFilesBuildPhase", "PBXFileReference",
            "PBXFrameworksBuildPhase", "PBXGroup", "PBXNativeTarget", "PBXProject", "PBXReferenceProxy",
            "PBXResourcesBuildPhase", "PBXShellScriptBuildPhase", "PBXSourcesBuildPhase", "PBXTargetDependency",
            "PBXVariantGroup", "XCBuildConfiguration", "XCConfigurationList"
        };

        public void ReadFromString(string src)
        {
            TextReader sr = new StringReader(src);

            PBXStream.ReadLinesWithConditionForLastLine(sr, m_Header, s => s.Trim() == "objects = {");

            string line = PBXStream.ReadSkippingEmptyLines(sr);

            string prevSectionName = null;
            while (PBXRegex.BeginSection.IsMatch(line))
            {
                string sectName = PBXRegex.BeginSection.Match(line).Groups[1].Value;

                // Duplicate sections (which should never appear) will be simply read again.
                if (m_Section.ContainsKey(sectName))
                    m_Section[sectName].ReadSection(line, sr);
                else
                {
                    SectionBase sect = new TextSection();
                    sect.ReadSection(line, sr);
                    m_Section.Add(sectName, sect);
                }

                // if the section is unknown, save its position relative to other sections
                if (!m_SectionOrder.Contains(sectName))
                {
                    int pos = 0;
                    if (prevSectionName != null) // TODO: static analysis: expression is always false
                    {
                        pos = m_SectionOrder.FindIndex(x => x == prevSectionName); // this never fails
                        pos += 1;
                    }

                    m_SectionOrder.Insert(pos, sectName);
                }
                line = PBXStream.ReadSkippingEmptyLines(sr);
            }

            m_Footer.Add(line);
            PBXStream.ReadLinesFromFile(sr, m_Footer);
        }

        public string WriteToString()
        {
            TextWriter sw = new StringWriter();

            var commentMap = BuildCommentMap();

            foreach (string s in m_Header)
                sw.WriteLine(s);

            foreach (string sectionName in m_SectionOrder)
                if (m_Section.ContainsKey(sectionName))
                    m_Section[sectionName].WriteSection(sw, commentMap);

            foreach (string s in m_Footer)
                sw.WriteLine(s);

            return sw.ToString();
        }

        PBXBuildFileSection             buildFiles          { get { return m_Section["PBXBuildFile"] as PBXBuildFileSection; } }
        PBXFileReferenceSection         fileRefs            { get { return m_Section["PBXFileReference"] as PBXFileReferenceSection; } }
        PBXGroupSection                 groups              { get { return m_Section["PBXGroup"] as PBXGroupSection; } }
        PBXContainerItemProxySection    containerItems      { get { return m_Section["PBXContainerItemProxy"] as PBXContainerItemProxySection; } }
        PBXReferenceProxySection        references          { get { return m_Section["PBXReferenceProxy"] as PBXReferenceProxySection; } }
        PBXSourcesBuildPhaseSection     sources             { get { return m_Section["PBXSourcesBuildPhase"] as PBXSourcesBuildPhaseSection; } }
        PBXFrameworksBuildPhaseSection  frameworks          { get { return m_Section["PBXFrameworksBuildPhase"] as PBXFrameworksBuildPhaseSection; } }
        PBXResourcesBuildPhaseSection   resources           { get { return m_Section["PBXResourcesBuildPhase"] as PBXResourcesBuildPhaseSection; } }
        PBXCopyFilesBuildPhaseSection   copyFiles           { get { return m_Section["PBXCopyFilesBuildPhase"] as PBXCopyFilesBuildPhaseSection; } }
        PBXShellScriptBuildPhaseSection shellScripts        { get { return m_Section["PBXShellScriptBuildPhase"] as PBXShellScriptBuildPhaseSection; } }
        PBXNativeTargetSection          nativeTargets       { get { return m_Section["PBXNativeTarget"] as PBXNativeTargetSection; } }
        PBXTargetDependencySection      targetDependencies  { get { return m_Section["PBXTargetDependency"] as PBXTargetDependencySection; } }
        PBXVariantGroupSection          variantGroups       { get { return m_Section["PBXVariantGroup"] as PBXVariantGroupSection; } }
        XCBuildConfigurationSection     buildConfigs        { get { return m_Section["XCBuildConfiguration"] as XCBuildConfigurationSection; } }
        XCConfigurationListSection      configs             { get { return m_Section["XCConfigurationList"] as XCConfigurationListSection; } }
        PBXProjectSection               project             { get { return m_Section["PBXProject"] as PBXProjectSection; } }

        /// Returns a guid identifying native target with name @a name
        public string TargetGuidByName(string name)
        {
            foreach (var entry in nativeTargets.entry)
                if (entry.Value.name == name)
                    return entry.Key;
            return null;
        }

        FileGUIDListBase BuildSection(PBXNativeTarget target, string path)
        {
            string ext  = Path.GetExtension(path);
            var phase = FileTypeUtils.GetFileType(ext);
            switch (phase)
            {
                case PBXFileType.Framework:
                    foreach (var guid in target.phase)
                        if (frameworks.entry.ContainsKey(guid))
                            return frameworks.entry[guid];
                    break;
                case PBXFileType.Resource:
                    foreach (var guid in target.phase)
                        if (resources.entry.ContainsKey(guid))
                            return resources.entry[guid];
                    break;
                case PBXFileType.Source:
                    foreach (var guid in target.phase)
                        if (sources.entry.ContainsKey(guid))
                            return sources.entry[guid];
                    break;
                case PBXFileType.CopyFile:
                    foreach (var guid in target.phase)
                        if (copyFiles.entry.ContainsKey(guid))
                            return copyFiles.entry[guid];
                    break;
            }
            return null;
        }

        public static bool IsKnownExtension(string ext)
        {
            return FileTypeUtils.IsKnownExtension(ext);
        }

        public static bool IsBuildable(string ext)
        {
            return FileTypeUtils.IsBuildable(ext);
        }

        // The same file can be referred to by more than one project path.
        string AddFileImpl(string path, string projectPath, PBXSourceTree tree)
        {
            path = FixSlashesInPath(path);
            projectPath = FixSlashesInPath(projectPath);

            string ext = Path.GetExtension(path);
            if (ext != Path.GetExtension(projectPath))
                throw new Exception("Project and real path extensions do not match");

            string guid = FindFileGuidByProjectPath(projectPath) ?? FindFileGuidByRealPath(path);
            if (guid == null)
            {
                PBXFileReference fileRef = PBXFileReference.CreateFromFile(path, GetFilenameFromPath(projectPath), tree);
                fileRefs.AddEntry(fileRef);
                CreateSourceGroup(GetDirectoryFromPath(projectPath)).AddGUID(fileRef.guid);
                guid = fileRef.guid;
            }
            return guid;
        }

        // The extension of the files identified by path and projectPath must be the same
        // FIXME: check if PBXSourceTree.Group is the best option. Maybe we can parse the path a bit.
        string AddFile(string path, string projectPath)
        {
            return AddFileImpl(path, projectPath, PBXSourceTree.Group);
        }

        public string AddFile(string path, string projectPath, PBXSourceTree sourceTree)
        {
            return AddFileImpl(path, projectPath, sourceTree);
        }

        void AddBuildFileImpl(string targetGuid, string fileGuid, bool weak, string compileFlags)
        {
            PBXNativeTarget target = nativeTargets[targetGuid];
            string ext = Path.GetExtension(fileRefs[fileGuid].path);
            if (FileTypeUtils.IsBuildable(ext) &&
                GetBuildFileForFileRef(target, fileGuid, ext) == null)
            {
                PBXBuildFile buildFile = PBXBuildFile.CreateFromFile(fileGuid, weak, compileFlags);
                buildFiles.AddEntry(buildFile);
                BuildSection(target, ext).AddGUID(buildFile.guid);
            }
        }

        public void AddFileToBuild(string targetGuid, string fileGuid)
        {
            AddBuildFileImpl(targetGuid, fileGuid, false, null);
        }

        public void AddFileToBuildWithFlags(string targetGuid, string fileGuid, string compileFlags)
        {
            AddBuildFileImpl(targetGuid, fileGuid, false, compileFlags);
        }

        string GetBuildFileForFileRef(PBXNativeTarget target, string fileRefGuid, string ext)
        {
            foreach (var guid in BuildSection(target, ext).file)
            {
                var buildFile = buildFiles[guid];
                if (buildFile != null && buildFile.fileRef == fileRefGuid)
                    return guid;
            }
            return null;
        }

        public bool ContainsFileByRealPath(string path)
        {
            return FindFileGuidByRealPath(path) != null;
        }

        public bool ContainsFileByRealPath(string path, PBXSourceTree sourceTree)
        {
            return FindFileGuidByRealPath(path, sourceTree) != null;
        }

        public bool ContainsFileByProjectPath(string path)
        {
            return FindFileGuidByProjectPath(path) != null;
        }

        public bool HasFramework(string framework)
        {
            return ContainsFileByRealPath("System/Library/Frameworks/" + framework);
        }

        /// The framework must be specified with the '.framework' extension
        public void AddFrameworkToProject(string targetGuid, string framework, bool weak)
        {
            string fileGuid = AddFile("System/Library/Frameworks/" + framework, "Frameworks/" + framework, PBXSourceTree.Sdk);
            AddBuildFileImpl(targetGuid, fileGuid, weak, null);
        }

        string GetDirectoryFromPath(string path)
        {
            int pos = path.LastIndexOf('/');
            if (pos == -1)
                return "";
            return path.Substring(0, pos);
        }

        string GetFilenameFromPath(string path)
        {
            int pos = path.LastIndexOf('/');
            if (pos == -1)
                return path;
            return path.Substring(pos + 1);
        }

        // FIXME: ignores sourceTree at the moment
        public string FindFileGuidByRealPath(string path, PBXSourceTree sourceTree)
        {
            return FindFileGuidByRealPath(path);
        }

        public string FindFileGuidByRealPath(string path)
        {
            path = FixSlashesInPath(path);

            string guid = null;
            foreach (var entry in fileRefs.entry)
            {
                if (entry.Value.path == path)
                {
                    guid = entry.Key;
                    break;
                }
            }
            return guid;
        }

        public string FindFileGuidByProjectPath(string path)
        {
            PBXGroup group = GetSourceGroup(GetDirectoryFromPath(path));
            if (group == null)
                return null;

            foreach (var guid in group.children)
            {
                var fr = fileRefs[guid];
                if (fr != null && fr.name == GetFilenameFromPath(path))
                    return guid;
            }
            return null;
        }

        PBXGroup FindSourceGroupContainingGuid(string guid)
        {
            foreach (var item in groups.entry)
                foreach (var childGuid in item.Value.children)
                    if (childGuid == guid)
                        return item.Value;
            return null;
        }

        // FIXME: removes the file from all targets
        public void RemoveFileFromBuild(string targetGuid, string fileGuid)
        {
            // PBXNativeTarget target = nativeTargets[targetGuid];

            string buildGuid = null;
            foreach (var entry in buildFiles.entry)
            {
                if (entry.Value.fileRef == fileGuid)
                {
                    buildGuid = entry.Key;
                    buildFiles.RemoveEntry(buildGuid);
                    break;
                }
            }
            if (buildGuid != null)
            {
                foreach (var section in sources.entry)
                    section.Value.RemoveGUID(buildGuid);
                foreach (var section in resources.entry)
                    section.Value.RemoveGUID(buildGuid);
                foreach (var section in copyFiles.entry)
                    section.Value.RemoveGUID(buildGuid);
                foreach (var section in frameworks.entry)
                    section.Value.RemoveGUID(buildGuid);
            }
        }

        public void RemoveFile(string fileGuid)
        {
            if (fileGuid == null)
                return;

            foreach (var target in nativeTargets.entry)
                RemoveFileFromBuild(target.Value.guid, fileGuid);
            fileRefs.RemoveEntry(fileGuid);
            FindSourceGroupContainingGuid(fileGuid).RemoveGUID(fileGuid);
        }

        PBXGroup GetPBXGroupChildByName(PBXGroup group, string path)
        {
            foreach (string guid in group.children)
            {
                var gr = groups[guid];
                if (gr != null && gr.name == path)
                    return gr;
            }
            return null;
        }

        /// Returns the source group identified by sourceGroup. If sourceGroup is empty or null,
        /// root group is returned. If no group is found, null is returned.
        PBXGroup GetSourceGroup(string sourceGroup)
        {
            sourceGroup = FixSlashesInPath(sourceGroup);

            PBXGroup gr = groups[project.project.mainGroup];

            if (string.IsNullOrEmpty(sourceGroup))
                return gr;

            var elements = sourceGroup.Trim('/').Split('/');
            foreach (string el in elements)
            {
                gr = GetPBXGroupChildByName(gr, el);
                if (gr == null)
                    return null;
            }
            return gr;
        }

        /// Creates source group identified by sourceGroup, if needed, and returns it.
        /// If sourceGroup is empty or null, root group is returned
        PBXGroup CreateSourceGroup(string sourceGroup)
        {
            sourceGroup = FixSlashesInPath(sourceGroup);

            PBXGroup gr = groups[project.project.mainGroup];

            if (string.IsNullOrEmpty(sourceGroup))
                return gr;

            var elements = sourceGroup.Trim('/').Split('/');
            foreach (string el in elements)
            {
                PBXGroup child = GetPBXGroupChildByName(gr, el);
                if (child != null)
                    gr = child;
                else
                {
                    PBXGroup newGroup = PBXGroup.Create(el);
                    gr.AddGUID(newGroup.guid);
                    groups.AddEntry(newGroup);
                    gr = newGroup;
                }
            }
            return gr;
        }

        public void AddExternalProjectDependency(string path, string projectPath, PBXSourceTree sourceTree)
        {
            path = FixSlashesInPath(path);
            projectPath = FixSlashesInPath(projectPath);

            PBXGroup productGroup = CreateSourceGroup("Products");

            PBXFileReference fileRef = PBXFileReference.CreateFromFile(path, Path.GetFileName(projectPath),
                                                                       sourceTree);
            fileRefs.AddEntry(fileRef);
            CreateSourceGroup(GetDirectoryFromPath(projectPath)).AddGUID(fileRef.guid);

            project.project.AddReference(productGroup.guid, fileRef.guid);
        }

        /** This function must be called only after the project the library is in has
            been added as a dependency via AddExternalProjectDependency. projectPath must be
            the same as the 'path' parameter passed to the AddExternalProjectDependency.
            remoteFileGuid must be the guid of the referenced file as specified in
            PBXFileReference section of the external project

            TODO: wtf. is remoteInfo entry in PBXContainerItemProxy? Is in referenced project name or
            referenced library name without extension?
        */
        public void AddExternalLibraryDependency(string targetGuid, string filename, string remoteFileGuid, string projectPath,
                                                 string remoteInfo)
        {
            PBXNativeTarget target = nativeTargets[targetGuid];
            filename = FixSlashesInPath(filename);
            projectPath = FixSlashesInPath(projectPath);

            // find the products group to put the new library in
            string projectGuid = FindFileGuidByRealPath(projectPath);
            if (projectGuid == null)
                throw new Exception("No such project");

            string productsGroupGuid = null;
            foreach (var proj in project.project.projectReferences)
            {
                if (proj.projectRef == projectGuid)
                {
                    productsGroupGuid = proj.group;
                    break;
                }
            }

            if (productsGroupGuid == null)
                throw new Exception("Malformed project: no project in project references");

            PBXGroup productGroup = groups[productsGroupGuid];

            // verify file extension
            string ext = Path.GetExtension(filename);
            if (!FileTypeUtils.IsBuildable(ext))
                throw new Exception("Wrong file extension");

            // create ContainerItemProxy object
            var container = PBXContainerItemProxy.Create(projectGuid, "2", remoteFileGuid, remoteInfo);
            containerItems.AddEntry(container);

            // create a reference and build file for the library
            string typeName = FileTypeUtils.GetTypeName(ext);

            var libRef = PBXReferenceProxy.Create(filename, typeName, container.guid, "BUILT_PRODUCTS_DIR");
            references.AddEntry(libRef);
            PBXBuildFile libBuildFile = PBXBuildFile.CreateFromFile(libRef.guid, false, null);
            buildFiles.AddEntry(libBuildFile);
            BuildSection(target, ext).AddGUID(libBuildFile.guid);

            // add to products folder
            productGroup.AddGUID(libRef.guid);
        }

        void SetDefaultAppExtensionReleaseBuildFlags(XCBuildConfiguration config, string infoPlistPath)
        {
            config.AddProperty("ALWAYS_SEARCH_USER_PATHS", "NO");
            config.AddProperty("CLANG_CXX_LANGUAGE_STANDARD", "gnu++0x");
            config.AddProperty("CLANG_CXX_LIBRARY", "libc++");
            config.AddProperty("CLANG_ENABLE_MODULES", "YES");
            config.AddProperty("CLANG_ENABLE_OBJC_ARC", "YES");
            config.AddProperty("CLANG_WARN_BOOL_CONVERSION", "YES");
            config.AddProperty("CLANG_WARN_CONSTANT_CONVERSION", "YES");
            config.AddProperty("CLANG_WARN_DIRECT_OBJC_ISA_USAGE", "YES_ERROR");
            config.AddProperty("CLANG_WARN_EMPTY_BODY", "YES");
            config.AddProperty("CLANG_WARN_ENUM_CONVERSION", "YES");
            config.AddProperty("CLANG_WARN_INT_CONVERSION", "YES");
            config.AddProperty("CLANG_WARN_OBJC_ROOT_CLASS", "YES_ERROR");
            config.AddProperty("CLANG_WARN_UNREACHABLE_CODE", "YES");
            config.AddProperty("CLANG_WARN__DUPLICATE_METHOD_MATCH", "YES");
            config.AddProperty("COPY_PHASE_STRIP", "YES");
            config.AddProperty("ENABLE_NS_ASSERTIONS", "NO");
            config.AddProperty("ENABLE_STRICT_OBJC_MSGSEND", "YES");
            config.AddProperty("GCC_C_LANGUAGE_STANDARD", "gnu99");
            config.AddProperty("GCC_WARN_64_TO_32_BIT_CONVERSION", "YES");
            config.AddProperty("GCC_WARN_ABOUT_RETURN_TYPE", "YES_ERROR");
            config.AddProperty("GCC_WARN_UNDECLARED_SELECTOR", "YES");
            config.AddProperty("GCC_WARN_UNINITIALIZED_AUTOS", "YES_AGGRESSIVE");
            config.AddProperty("GCC_WARN_UNUSED_FUNCTION", "YES");
            config.AddProperty("INFOPLIST_FILE", infoPlistPath);
            config.AddProperty("IPHONEOS_DEPLOYMENT_TARGET", "8.0");
            config.AddProperty("LD_RUNPATH_SEARCH_PATHS", "$(inherited) @executable_path/Frameworks @executable_path/../../Frameworks");
            config.AddProperty("MTL_ENABLE_DEBUG_INFO", "NO");
            config.AddProperty("PRODUCT_NAME", "$(TARGET_NAME)");
            config.AddProperty("SKIP_INSTALL", "YES");
            config.AddProperty("VALIDATE_PRODUCT", "YES");
        }

        void SetDefaultAppExtensionDebugBuildFlags(XCBuildConfiguration config, string infoPlistPath)
        {
            config.AddProperty("ALWAYS_SEARCH_USER_PATHS", "NO");
            config.AddProperty("CLANG_CXX_LANGUAGE_STANDARD", "gnu++0x");
            config.AddProperty("CLANG_CXX_LIBRARY", "libc++");
            config.AddProperty("CLANG_ENABLE_MODULES", "YES");
            config.AddProperty("CLANG_ENABLE_OBJC_ARC", "YES");
            config.AddProperty("CLANG_WARN_BOOL_CONVERSION", "YES");
            config.AddProperty("CLANG_WARN_CONSTANT_CONVERSION", "YES");
            config.AddProperty("CLANG_WARN_DIRECT_OBJC_ISA_USAGE", "YES_ERROR");
            config.AddProperty("CLANG_WARN_EMPTY_BODY", "YES");
            config.AddProperty("CLANG_WARN_ENUM_CONVERSION", "YES");
            config.AddProperty("CLANG_WARN_INT_CONVERSION", "YES");
            config.AddProperty("CLANG_WARN_OBJC_ROOT_CLASS", "YES_ERROR");
            config.AddProperty("CLANG_WARN_UNREACHABLE_CODE", "YES");
            config.AddProperty("CLANG_WARN__DUPLICATE_METHOD_MATCH", "YES");
            config.AddProperty("COPY_PHASE_STRIP", "NO");
            config.AddProperty("ENABLE_STRICT_OBJC_MSGSEND", "YES");
            config.AddProperty("GCC_C_LANGUAGE_STANDARD", "gnu99");
            config.AddProperty("GCC_DYNAMIC_NO_PIC", "NO");
            config.AddProperty("GCC_OPTIMIZATION_LEVEL", "0");
            config.AddProperty("GCC_PREPROCESSOR_DEFINITIONS", "DEBUG=1");
            config.AddProperty("GCC_PREPROCESSOR_DEFINITIONS", "$(inherited)");
            config.AddProperty("GCC_SYMBOLS_PRIVATE_EXTERN", "NO");
            config.AddProperty("GCC_WARN_64_TO_32_BIT_CONVERSION", "YES");
            config.AddProperty("GCC_WARN_ABOUT_RETURN_TYPE", "YES_ERROR");
            config.AddProperty("GCC_WARN_UNDECLARED_SELECTOR", "YES");
            config.AddProperty("GCC_WARN_UNINITIALIZED_AUTOS", "YES_AGGRESSIVE");
            config.AddProperty("GCC_WARN_UNUSED_FUNCTION", "YES");
            config.AddProperty("INFOPLIST_FILE", infoPlistPath);
            config.AddProperty("IPHONEOS_DEPLOYMENT_TARGET", "8.0");
            config.AddProperty("LD_RUNPATH_SEARCH_PATHS", "$(inherited) @executable_path/Frameworks @executable_path/../../Frameworks");
            config.AddProperty("MTL_ENABLE_DEBUG_INFO", "YES");
            config.AddProperty("ONLY_ACTIVE_ARCH", "YES");
            config.AddProperty("PRODUCT_NAME", "$(TARGET_NAME)");
            config.AddProperty("SKIP_INSTALL", "YES");
        }

        // Returns the guid of the new target
        internal string AddAppExtension(string mainTarget, string name, string infoPlistPath)
        {
            const string ext = ".appex";
            string fullName = name + ext;
            var productFileRef = PBXFileReference.CreateFromFile("Products/" + fullName, "Products/" + fullName,
                                                                 PBXSourceTree.Group);
            var releaseBuildConfig = XCBuildConfiguration.Create("Release");
            buildConfigs.AddEntry(releaseBuildConfig);
            SetDefaultAppExtensionReleaseBuildFlags(releaseBuildConfig, infoPlistPath);

            var debugBuildConfig = XCBuildConfiguration.Create("Debug");
            buildConfigs.AddEntry(debugBuildConfig);
            SetDefaultAppExtensionDebugBuildFlags(debugBuildConfig, infoPlistPath);

            var buildConfigList = XCConfigurationList.Create();
            configs.AddEntry(buildConfigList);
            buildConfigList.buildConfig.Add(releaseBuildConfig.guid);
            buildConfigList.buildConfig.Add(debugBuildConfig.guid);

            var newTarget = PBXNativeTarget.Create(name, productFileRef.guid, "com.apple.product-type.app-extension", buildConfigList.guid);
            nativeTargets.AddEntry(newTarget);
            project.project.targets.Add(newTarget.guid);

            var sourcesBuildPhase = PBXSourcesBuildPhase.Create();
            sources.AddEntry(sourcesBuildPhase);
            newTarget.phase.Add(sourcesBuildPhase.guid);

            var resourcesBuildPhase = PBXResourcesBuildPhase.Create();
            resources.AddEntry(resourcesBuildPhase);
            newTarget.phase.Add(resourcesBuildPhase.guid);

            var frameworksBuildPhase = PBXFrameworksBuildPhase.Create();
            frameworks.AddEntry(frameworksBuildPhase);
            newTarget.phase.Add(frameworksBuildPhase.guid);

            var copyFilesBuildPhase = PBXCopyFilesBuildPhase.Create("Embed App Extensions", "13");
            copyFiles.AddEntry(copyFilesBuildPhase);
            nativeTargets[mainTarget].phase.Add(copyFilesBuildPhase.guid);

            var containerProxy = PBXContainerItemProxy.Create(project.project.guid, "1", newTarget.guid, name);
            containerItems.AddEntry(containerProxy);

            var targetDependency = PBXTargetDependency.Create(newTarget.guid, containerProxy.guid);
            targetDependencies.AddEntry(targetDependency);

            nativeTargets[mainTarget].dependencies.Add(targetDependency.guid);

            AddFile(fullName, "Products/" + fullName, PBXSourceTree.Build);
            var buildAppCopy = PBXBuildFile.CreateFromFile(FindFileGuidByProjectPath("Products/" + fullName), false, "");
            buildFiles.AddEntry(buildAppCopy);
            copyFilesBuildPhase.file.Add(buildAppCopy.guid);

            AddFile(infoPlistPath, name + "/Supporting Files/Info.plist", PBXSourceTree.Group);

            return newTarget.guid;
        }

        public string BuildConfigByName(string targetGuid, string name)
        {
            PBXNativeTarget target = nativeTargets[targetGuid];
            foreach (string guid in configs[target.buildConfigList].buildConfig)
            {
                var buildConfig = buildConfigs[guid];
                if (buildConfig != null && buildConfig.name == name)
                    return buildConfig.guid;
            }
            return null;
        }

        // Adds an item to a build property that contains a value list
        public void AddBuildProperty(string targetGuid, string name, string value)
        {
            PBXNativeTarget target = nativeTargets[targetGuid];
            foreach (string guid in configs[target.buildConfigList].buildConfig)
                buildConfigs[guid].AddProperty(name, value);
        }

        public void AddBuildProperty(string[] targetGuids, string name, string value)
        {
            foreach (string t in targetGuids)
                AddBuildProperty(t, name, value);
        }

        public void AddBuildPropertyForConfig(string configGuid, string name, string value)
        {
            buildConfigs[configGuid].AddProperty(name, value);
        }

        public void AddBuildPropertyForConfig(string[] configGuids, string name, string value)
        {
            foreach (string guid in configGuids)
                AddBuildPropertyForConfig(guid, name, value);
        }

        public void SetBuildProperty(string targetGuid, string name, string value)
        {
            PBXNativeTarget target = nativeTargets[targetGuid];
            foreach (string guid in configs[target.buildConfigList].buildConfig)
                buildConfigs[guid].SetProperty(name, value);
        }

        public void SetBuildProperty(string[] targetGuids, string name, string value)
        {
            foreach (string t in targetGuids)
                SetBuildProperty(t, name, value);
        }

        public void SetBuildPropertyForConfig(string configGuid, string name, string value)
        {
            buildConfigs[configGuid].SetProperty(name, value);
        }

        public void SetBuildPropertyForConfig(string[] configGuids, string name, string value)
        {
            foreach (string guid in configGuids)
                SetBuildPropertyForConfig(guid, name, value);
        }

        /// Interprets the value of the given property as a set of space-delimited strings, then
        /// removes strings equal to items to removeValues and adds strings in addValues.
        public void UpdateBuildProperty(string targetGuid, string name, string[] addValues, string[] removeValues)
        {
            PBXNativeTarget target = nativeTargets[targetGuid];
            foreach (string guid in configs[target.buildConfigList].buildConfig)
                buildConfigs[guid].UpdateProperties(name, addValues, removeValues);
        }

        public void UpdateBuildProperty(string[] targetGuids, string name, string[] addValues, string[] removeValues)
        {
            foreach (string t in targetGuids)
                UpdateBuildProperty(t, name, addValues, removeValues);
        }

        public void UpdateBuildPropertyForConfig(string configGuid, string name, string[] addValues, string[] removeValues)
        {
            buildConfigs[configGuid].UpdateProperties(name, addValues, removeValues);
        }

        public void UpdateBuildPropertyForConfig(string[] configGuids, string name, string[] addValues, string[] removeValues)
        {
            foreach (string guid in configGuids)
                UpdateBuildProperty(guid, name, addValues, removeValues);
        }

        /// Replaces '\' with '/'. We need to apply this function to all paths that come from the user
        /// of the API because we store paths to pbxproj and on windows we may get path with '\' slashes
        /// instead of '/' slashes
        static string FixSlashesInPath(string path)
        {
            if (path == null)
                return null;
            return path.Replace('\\', '/');
        }

        void BuildCommentMapForBuildFiles(GUIDToCommentMap comments, IEnumerable<string> guids, string sectName)
        {
            foreach (var guid in guids)
            {
                var buildFile = buildFiles[guid];
                if (buildFile != null)
                {
                    var fileRef = fileRefs[buildFile.fileRef];
                    if (fileRef != null)
                        comments.Add(guid, String.Format("{0} in {1}", fileRef.name, sectName));
                    else
                    {
                        var reference = references[buildFile.fileRef];
                        if (reference != null)
                            comments.Add(guid, String.Format("{0} in {1}", reference.path, sectName));
                    }
                }
            }
        }

        GUIDToCommentMap BuildCommentMap()
        {
            var comments = new GUIDToCommentMap();

            // buildFiles are handled below
            // filerefs are handled below
            foreach (var e in groups.entry.Values)
                comments.Add(e.guid, e.name);
            foreach (var e in containerItems.entry.Values)
                comments.Add(e.guid, "PBXContainerItemProxy");
            foreach (var e in references.entry.Values)
                comments.Add(e.guid, e.path);
            foreach (var e in sources.entry.Values)
            {
                comments.Add(e.guid, "Sources");
                BuildCommentMapForBuildFiles(comments, e.file, "Sources");
            }
            foreach (var e in resources.entry.Values)
            {
                comments.Add(e.guid, "Resources");
                BuildCommentMapForBuildFiles(comments, e.file, "Resources");
            }
            foreach (var e in frameworks.entry.Values)
            {
                comments.Add(e.guid, "Frameworks");
                BuildCommentMapForBuildFiles(comments, e.file, "Frameworks");
            }
            foreach (var e in copyFiles.entry.Values)
            {
                string sectName = e.name ?? "CopyFiles";
                comments.Add(e.guid, sectName);
                BuildCommentMapForBuildFiles(comments, e.file, sectName);
            }
            foreach (var e in shellScripts.entry.Values)
                comments.Add(e.guid, "ShellScript");
            foreach (var e in targetDependencies.entry.Values)
                comments.Add(e.guid, "PBXTargetDependency");
            foreach (var e in nativeTargets.entry.Values)
            {
                comments.Add(e.guid, e.name);
                comments.Add(e.buildConfigList, String.Format("Build configuration list for PBXNativeTarget \"{0}\"", e.name));
            }
            foreach (var e in variantGroups.entry.Values)
                comments.Add(e.guid, e.name);
            foreach (var e in buildConfigs.entry.Values)
                comments.Add(e.guid, e.name);
            foreach (var e in project.entry.Values)
            {
                comments.Add(e.guid, "Project object");
                comments.Add(e.buildConfigList, "Build configuration list for PBXProject \"Unity-iPhone\""); // FIXME: project name is hardcoded
            }
            foreach (var e in fileRefs.entry.Values)
                comments.Add(e.guid, e.name);
            return comments;
        }
    }
} // namespace UnityEditor.iOS.Xcode
