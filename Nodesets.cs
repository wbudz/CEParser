using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CEParser
{
    [Serializable]
    public class Nodesets
    {
        Dictionary<string, Dictionary<ushort, Node>> nodesets = new Dictionary<string, Dictionary<ushort, Node>>();

        public void Add(string name, Dictionary<ushort, Node> nodeset)
        {
            if (nodesets.ContainsKey(name))
                nodesets.Remove(name);

            nodesets.Add(name, nodeset);
        }

        public Dictionary<ushort, Node> Get(string name)
        {
            nodesets.TryGetValue(name, out Dictionary<ushort, Node> output);
            return output ?? new Dictionary<ushort, Node>();
        }

        public void Remove(string name)
        {
            if (nodesets.ContainsKey(name))
                nodesets.Remove(name);
        }
    }
}
