using CommandLine;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace DependencyAnalyzer
{
    class Options
    {
        [Option('i', "dictionary")]
        public bool Dictionary { get; set; }

        [Option('d', "dot")]
        public bool Dot { get; set; }

        [Option('k', "packageRefs")]
        public bool PackageRefs { get; set; }

        [Option('p', "path", Required = true)]
        public string Path { get; set; }

        [Option('j', "projectRefs")]
        public bool ProjectRefs { get; set; }

        [Option('x', "exclude")]
        public IEnumerable<string> Exclude { get; set; }
    }

    class Program
    {
        private static Options _options;

        static int Main(string[] args)
        {
            return Parser.Default.ParseArguments<Options>(args).MapResult(
                options => Run(options),
                _ => 1
            );
        }

        private static int Run(Options options)
        {
            _options = options;
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
            var allProjects = FindProjects(path);
            Console.WriteLine($"Total Projects: {allProjects.Count()}");

            var projects = allProjects.Where(p => !p.ContainsAny(_options.Exclude, StringComparison.OrdinalIgnoreCase));
            Console.WriteLine($"Selected Projects: {projects.Count()}");

            var graph = new Dictionary<string, Project>(projects.Count());

            foreach (var project in projects)
            {
                var name = Path.GetFileNameWithoutExtension(project);
                var (projectRefs, packageRefs) = GetRefs(project);
                graph.Add(name, new Project(name) { ProjectRefs = projectRefs, PackageRefs = packageRefs });
            }

            AssignRanks(graph);

            Print(graph);
        }

        private static void AssignRanks(Dictionary<string, Project> graph)
        {
            while (graph.Values.Any(p => !p.Rank.HasValue))
            {
                foreach (var project in graph.Values.Where(p => !p.Rank.HasValue))
                {
                    if (!project.ProjectRefs.Any())
                    {
                        project.Rank = 0;
                    }
                    else if (project.ProjectRefs.All(p => graph[p].Rank.HasValue))
                    {
                        var maxRefRank = project.ProjectRefs.Max(p => graph[p].Rank);
                        project.Rank = maxRefRank + 1;
                    }
                }
            }
        }

        private static void Print(Dictionary<string, Project> graph)
        {
            var allProjectRefs = graph.Values.Select(v => v.ProjectRefs).Aggregate((a, b) => Enumerable.Concat(a, b));
            Console.WriteLine();
            Console.WriteLine($"Total ProjectRefs: {allProjectRefs.Count()}");

            var allPackageRefs = graph.Values.Select(v => v.PackageRefs).Aggregate((a, b) => Enumerable.Concat(a, b));
            Console.WriteLine();
            Console.WriteLine($"Total PackageRefs: {allPackageRefs.Count()}");
            Console.WriteLine($"Unique PackageRefs: {allPackageRefs.Distinct().Count()}");

            if (_options.ProjectRefs)
            {
                PrintProjectRefs(graph);
            }

            if (_options.PackageRefs)
            {
                PrintPackageRefs(graph, allPackageRefs);
            }

            if (_options.Dot)
            {
                PrintDot(graph);
            }

            if (_options.Dictionary)
            {
                PrintDictionary(graph);
            }
        }

        private static void PrintDictionary(Dictionary<string, Project> graph)
        {
            Console.WriteLine();

            var count = 1;
            foreach (var group in graph.Values.OrderBy(p => p.Name).GroupBy(p => p.Rank).OrderBy(g => g.Key))
            {
                Console.WriteLine($"// Rank {group.Key}");

                foreach (var p in group)
                {
                    Console.Write($"{{ \"{p.Name}\", ");
                    if (p.ProjectRefs.Any())
                    {
                        Console.Write("new string[] { ");
                        foreach (var r in p.ProjectRefs.OrderBy(s => s))
                        {
                            Console.Write($"\"{r}\", ");
                        }
                        Console.Write("} ");
                    }
                    else
                    {
                        Console.Write("Enumerable.Empty<string>() ");
                    }
                    Console.WriteLine("},");

                    count++;
                }

                Console.WriteLine();
            }
        }

        private static void PrintDot(Dictionary<string, Project> graph)
        {
            var sb = new StringBuilder();

            sb.AppendLine("digraph G {");

            foreach (var group in graph.Values.OrderBy(p => p.Name).GroupBy(p => p.Rank).OrderByDescending(g => g.Key))
            {
                var rankBuilder = new StringBuilder();
                rankBuilder.Append("    { rank = same; ");

                foreach (var p in group)
                {
                    sb.Append($"    {p.Name.Replace('.', '_')} -> {{ ");

                    foreach (var r in p.ProjectRefs)
                    {
                        sb.Append($"{r.Replace('.', '_')} ");
                    }

                    sb.AppendLine("}");

                    rankBuilder.Append(p.Name.Replace('.', '_'));
                    rankBuilder.Append("; ");
                }

                rankBuilder.Append("}");
                sb.AppendLine(rankBuilder.ToString());
            }

            sb.AppendLine("}");

            File.WriteAllText("ProjectRefs.gv", sb.ToString());
            Util.RunProcess("dot", "-Tpdf ProjectRefs.gv -o ProjectRefs.pdf", Environment.CurrentDirectory);
        }

        private static void PrintPackageRefs(Dictionary<string, Project> graph, IEnumerable<string> allPackageRefs)
        {
            Console.WriteLine();

            foreach (var kvp in graph)
            {
                Console.WriteLine(kvp.Key);

                Console.WriteLine("  PackageRefs");
                foreach (var r in kvp.Value.PackageRefs)
                {
                    Console.WriteLine($"    {r}");
                }

                Console.WriteLine(Environment.NewLine + "Unique Package Refs");
                foreach (var r in allPackageRefs.Distinct())
                {
                    Console.WriteLine(r);
                }
            }
        }

        private static void PrintProjectRefs(Dictionary<string, Project> graph)
        {
            Console.WriteLine();

            foreach (var group in graph.Values.OrderBy(p => p.Name).GroupBy(p => p.Rank).OrderByDescending(g => g.Key))
            {
                foreach (var p in group)
                {
                    Console.WriteLine(p.Name);

                    Console.WriteLine("  ProjectRefs");
                    if (p.ProjectRefs.Any())
                    {
                        foreach (var r in p.ProjectRefs)
                        {
                            Console.WriteLine($"    {r}");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"    [None]");
                    }
                    Console.WriteLine();
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
                var name = Path.GetFileNameWithoutExtension(r.Attribute("Include").Value);
                if (!name.ContainsAny(_options.Exclude, StringComparison.OrdinalIgnoreCase))
                {
                    projectRefs.Add(name);
                }
            }

            foreach (var r in root.Descendants("PackageReference"))
            {
                packageRefs.Add(r.Attribute("Include").Value);
            }

            return (projectRefs, packageRefs);
        }
    }
}
