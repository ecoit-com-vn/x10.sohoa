using Elastic.Clients.Elasticsearch;
using System.Reflection;
using System;
using System.Linq;

class Program {
    static void Main() {
        var t = typeof(Elastic.Clients.Elasticsearch.IndexManagement.IndexSettingsAnalysisDescriptor);
        foreach(var m in t.GetMethods()) {
            Console.WriteLine(m.Name);
        }
    }
}
