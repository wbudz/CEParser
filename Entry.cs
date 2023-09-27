using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CEParser
{
    public class Entry : Entity
    {
        string value;
        bool quoted;
        
        public override string Name
        {
            get
            {
                return "";
            }
            protected set
            {
            }
        }

        public override string Value
        {
            get
            {
                return value;
            }
            protected set
            {
                this.value = value;
            }
        }

        public Entry(Node parent, string value, bool quoted)
        {
            this.value = value;
            this.quoted = quoted;
            parent.children.Add(this);
        }

        public override void Export(StringBuilder sb, int depth, ref bool endline)
        {
            if (endline)
                CreateDepth(sb, depth);
            else
                sb.Append(" ");
            if (quoted) sb.Append("\"");
            sb.Append(value);
            if (quoted) sb.Append("\"");
            endline = false;
        }

        public override string ToString()
        {
            return value;
        }
    }
}
