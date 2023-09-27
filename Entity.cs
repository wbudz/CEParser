using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CEParser
{
    [Serializable]
    public abstract class Entity: MarshalByRefObject
    {
        public abstract string Name { get; protected set; }

        public abstract string Value { get; protected set; }

        public abstract void Export(StringBuilder sb, int depth, ref bool endline);
        
        protected static void CreateDepth(StringBuilder sb, int depth)
        {
            for (int i = 0; i < depth - 1 && i < 20; i++) sb.Append("\t");
        }

        internal int GetChildrenCount()
        {
            int count = 0;
            if (this is Node)
            {
                foreach (var n in (this as Node).children)
                {
                    count += n.GetChildrenCount();
                }
            }
            return count;
        }
    }
}
