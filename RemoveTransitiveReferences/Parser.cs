using Mono.Cecil;
using NuGet;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace RemoveTransitiveReferences
{
    public class Parser
    {
        private string _userProfile = Environment.GetEnvironmentVariable("UserProfile");
        private string _targetFramework;

        public XDocument Parse(XDocument xmlDocument, string file)
        {
            if (!IsProjectSdk(xmlDocument))
            {
                Console.WriteLine($"This is not a VS2017 project file.");
                return null;
            }

            _targetFramework = GetTargetFramework(xmlDocument);
            var outputType = GetOutputType(xmlDocument);

            var baseDir = Path.GetDirectoryName(file);
            var assemblyName = Path.GetFileNameWithoutExtension(file);

            string assembly;
            if (outputType == "Exe")
            {
                assembly = Path.Combine(baseDir, "bin", "debug", _targetFramework, $"{assemblyName}.exe");
            }
            else
            {
                assembly = Path.Combine(baseDir, "bin", "debug", _targetFramework, $"{assemblyName}.dll");
            }

            if (!File.Exists(assembly))
            {
                Console.WriteLine($"Was not able to find '{assembly}'. Please build project first");
                return null;
            }

            var assemblies = GetAssemblies(assembly).ToList();

            var highLevelDependencies = ReadHighLevelDependencies(xmlDocument);
            var highLevelDependenciesToRemove = new HashSet<HighLevelDependency>();

            foreach (var highLevelDependency in highLevelDependencies)
            {
                var dependencies = ReadDependenciesRecursive(highLevelDependency.Name, new SemanticVersion(highLevelDependency.Version), 0);
                if (dependencies.Any())
                {
                    foreach (var dependency in dependencies)
                    {
                        var remove = highLevelDependencies.Where(x => x.Name == dependency.Item1.Id).ToList();
                        if (remove.Any())
                        {
                            highLevelDependenciesToRemove.AddRange(remove);
                        }
                    }
                }
            }

            foreach (var remove in highLevelDependenciesToRemove.OrderBy(x => x.Name))
            {
                if (assemblies.Any(x => x == remove.Name))
                {
                    continue;
                }
                Console.WriteLine($"Removing {remove.Name}");
                xmlDocument.Element("Project").Elements("ItemGroup").Elements("PackageReference").Where(x => x.Attribute("Include").Value == remove.Name).Remove();
            }

            return xmlDocument;
        }

        private bool IsProjectSdk(XDocument xmlDocument)
        {
            var sdk = xmlDocument.Element("Project")?.Attribute("Sdk");
            if (sdk == null)
            {
                return false;
            }
            return sdk.Value == "Microsoft.NET.Sdk";
        }

        private string GetTargetFramework(XDocument xmlDocument)
        {
            return xmlDocument.Element("Project").Elements("PropertyGroup").Elements("TargetFramework").Single().Value;
        }

        private string GetOutputType(XDocument xmlDocument)
        {
            var outputType = xmlDocument.Element("Project").Elements("PropertyGroup").Elements("OutputType").SingleOrDefault();
            if (outputType == null)
            {
                return "";
            }
            return outputType.Value;
        }

        private IList<HighLevelDependency> ReadHighLevelDependencies(XDocument xmlDocument)
        {
            var packageReferences = xmlDocument.Element("Project").Elements("ItemGroup").Elements("PackageReference");

            var highLevelDependencies = new List<HighLevelDependency>();
            foreach (var reference in packageReferences)
            {
                var name = reference.Attribute("Include").Value;
                var version = reference.Attribute("Version").Value;
                highLevelDependencies.Add(new HighLevelDependency
                {
                    Name = name,
                    Version = version,
                });
            }
            return highLevelDependencies;
        }

        private IList<Tuple<PackageDependency, int>> ReadDependenciesRecursive(string package, SemanticVersion version, int level)
        {
            var packagePath = Path.Combine(_userProfile, ".nuget", "packages", package);

            if (!Directory.Exists(packagePath))
            {
                throw new InvalidOperationException($"Package directory {packagePath} was not found. Please restore packages");
            }

            var versions = Directory.GetDirectories(packagePath)
                .Select(x => x.Substring(x.LastIndexOf(@"\") + 1))
                .Select(x => new SemanticVersion(x));

            var versionDirectory = versions.Where(x => x == version).SingleOrDefault();
            if (versionDirectory == null)
            {
                // Get closest match
                versionDirectory = versions.First(x => x >= version);
            }
            var path = Path.Combine(packagePath, versionDirectory.ToString(), $"{package}.{versionDirectory}.nupkg");

            var zipPackage = new ZipPackage(path);
            var dependencySet = GetDependencySet(zipPackage);

            if (dependencySet == null)
            {
                return new List<Tuple<PackageDependency, int>>();
            }

            var dependenciesAll = dependencySet.Dependencies.Select(x => new Tuple<PackageDependency, int>(x, level)).ToList();
            var dependencies = dependencySet.Dependencies.ToList();
            level++;
            foreach (var dependency in dependencies)
            {
                dependenciesAll.AddRange(ReadDependenciesRecursive(dependency.Id, dependency.VersionSpec.MinVersion, level));
            }
            return dependenciesAll;
        }

        private PackageDependencySet GetDependencySet(ZipPackage zipPackage)
        {
            PackageDependencySet dependencySet;
            if (zipPackage.DependencySets.Count() == 1)
            {
                dependencySet = zipPackage.DependencySets.Single();
            }
            else
            {
                if (_targetFramework.Contains("core"))
                {
                    dependencySet = zipPackage.DependencySets.FirstOrDefault(x => x.SupportedFrameworks.Any(y => y.Identifier.Contains("standard")));
                }
                else if (_targetFramework.Contains("standard"))
                {
                    dependencySet = zipPackage.DependencySets.FirstOrDefault(x => x.SupportedFrameworks.Any(y => y.Identifier.Contains("core")));
                }
                else
                {
                    dependencySet = zipPackage.DependencySets.FirstOrDefault(x => x.TargetFramework == null || x.SupportedFrameworks.Any(y => y.Identifier.Contains("Framework")));
                }
            }
            return dependencySet;
        }

        private IEnumerable<string> GetAssemblies(string fileName)
        {
            ModuleDefinition module = ModuleDefinition.ReadModule(fileName);
            foreach (var assembly in module.AssemblyReferences)
            {
                yield return assembly.Name;
            }
        }
    }
}