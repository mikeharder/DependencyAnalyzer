using System.Collections.Generic;

namespace DependencyAnalyzer
{
    class Project
    {
        public Project(string name)
        {
            Name = name;
        }

        public string Name { get; }

        public IEnumerable<string> PackageRefs { get; set; }

        public IEnumerable<string> ProjectRefs { get; set; }

        public int? Rank { get; set; }
    }
}
