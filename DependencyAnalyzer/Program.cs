using CommandLine;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace DependencyAnalyzer
{
    class Options
    {
        [Option('v', "verbose")]
        public bool Verbose { get; set; }

        [Option('p', "path", Required = true)]
        public string Path { get; set; }
    }

    class Program
    {
        private static bool _verbose;

        static int Main(string[] args)
        {
            return Parser.Default.ParseArguments<Options>(args).MapResult(
                options => Run(options),
                _ => 1
            );
        }

        private static int Run(Options options)
        {
            _verbose = options.Verbose;
            Analyze(options.Path);
            return 0;
        }

        private static int PrintHelp()
        {
            Console.WriteLine($"{typeof(Program).Assembly.GetName().Name} [path]");
            return -1;
        }

        private static void Analyze(string path)
        {
            var projects = FindProjects(path);

            Console.WriteLine($"Total Projects: {projects.Count()}");

            var graph = new Dictionary<string, (IEnumerable<string> ProjectRefs, IEnumerable<string> PackageRefs)>();
            foreach (var project in projects)
            {
                graph.Add(Path.GetFileName(project), GetRefs(project));
            }

            var allProjectRefs = graph.Values.Select(v => v.ProjectRefs).Aggregate((a, b) => Enumerable.Concat(a, b));
            Console.WriteLine($"Total ProjectRefs: {allProjectRefs.Count()}");

            var allPackageRefs = graph.Values.Select(v => v.PackageRefs).Aggregate((a, b) => Enumerable.Concat(a, b));
            Console.WriteLine($"Total PackageRefs: {allPackageRefs.Count()}");
            Console.WriteLine($"Unique PackageRefs: {allPackageRefs.Distinct().Count()}");

            if (_verbose)
            {
                foreach (var kvp in graph)
                {
                    Console.WriteLine(kvp.Key);

                    Console.WriteLine("  ProjectRefs");
                    foreach (var r in kvp.Value.ProjectRefs)
                    {
                        Console.WriteLine($"    {r}");
                    }

                    Console.WriteLine("  PackageRefs");
                    foreach (var r in kvp.Value.PackageRefs)
                    {
                        Console.WriteLine($"    {r}");
                    }
                }

                Console.WriteLine(Environment.NewLine + "Unique Package Refs");
                foreach (var r in allPackageRefs.Distinct())
                {
                    Console.WriteLine(r);
                }
            }
        }

        private static IEnumerable<string> FindProjects(string path)
        {
            foreach (var f in Directory.GetFiles(path, "*.csproj"))
            {
                yield return f;
            }

            foreach (var d in Directory.GetDirectories(path))
            {
                foreach (var p in FindProjects(d))
                {
                    yield return p;
                }
            }
        }

        private static (IEnumerable<string> ProjectRefs, IEnumerable<string> PackageRefs) GetRefs(string project)
        {
            var projectRefs = new List<string>();
            var packageRefs = new List<string>();

            var root = XElement.Load(project);

            foreach (var r in root.Descendants("ProjectReference"))
            {
                projectRefs.Add(Path.GetFileName(r.Attribute("Include").Value));
            }

            foreach (var r in root.Descendants("PackageReference"))
            {
                packageRefs.Add(r.Attribute("Include").Value);
            }

            return (projectRefs, packageRefs);
        }
    }
}
