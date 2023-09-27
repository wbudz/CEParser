using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;

namespace CEParser
{
    public class Node : Entity
    {
        string name;
        public override string Name
        {
            get
            {
                return name;
            }
            protected set
            {
                name = value;
            }
        }

        public override string Value
        {
            get
            {
                return "";
            }
            protected set
            {
                children.Clear();
            }
        }

        Node parent;
        public Node Parent
        {
            get
            {
                return parent;
            }
            protected internal set
            {
                parent = value;
            }
        }

        /// <summary>
        /// Specifies how deep in the file structure the current entity lies.
        /// </summary>
        public byte Depth { get; private set; }

        protected internal List<Entity> children = new List<Entity>();
        Dictionary<string, int> subnodeCache;

        /// <summary>
        /// Creates a new entity.
        /// </summary>
        /// <param name="file">Reference to a file that holds the entity contents.</param>
        public Node(Node parent, string name)
        {
            this.name = name;
            this.Parent = parent;
            this.Depth = (byte)(Parent.Depth + 1);
            parent.children.Add(this);
        }

        public Node()
        {
            this.name = "";
            this.Parent = null;
            this.Depth = 0;
        }

        public override void Export(StringBuilder sb, int depth, ref bool endline)
        {
            if (name != "")
            {
                if (!endline)
                {
                    sb.AppendLine();
                }
                CreateDepth(sb, Depth);
                sb.Append(name);
                sb.Append("={");
                sb.AppendLine();
                endline = true;
            }
            else if (Depth > 0)
            {
                if (!endline)
                {
                    sb.AppendLine();
                }
                CreateDepth(sb, Depth);
                sb.Append("{");
                sb.AppendLine();
                endline = true;
            }

            foreach (var n in children)
            {
                n.Export(sb, Depth + 1, ref endline);
            }

            if (Depth > 0)
            {
                sb.AppendLine();
                CreateDepth(sb, Depth);
                sb.Append("}");
                endline = false;
            }
        }

        public override string ToString()
        {
            return name + "={";
        }

        internal void PrepareCache()
        {
            if (children.Count > 256)
            {
                subnodeCache = new Dictionary<string, int>();
                for (int i = 0; i < children.Count; i++)
                {
                    if (!subnodeCache.ContainsKey(children[i].Name.ToLowerInvariant()))
                        subnodeCache.Add(children[i].Name.ToLowerInvariant(), i);
                }
            }
        }

        #region Accessor methods

        public Node GetSubnode(params string[] path)
        {
            if (path.Length == 0) return null;
            if (subnodeCache != null)
            {
                if (!subnodeCache.ContainsKey(path[0])) return null;
                return (children[subnodeCache[path[0]]] as Node).GetSubnode(path.Skip(1).ToArray());
            }
            else
            {
                var n = children.Find(x => String.Compare(x.Name, path[0], StringComparison.OrdinalIgnoreCase) == 0) as Node;
                if (path.Length == 1)
                {
                    return n;
                }
                else
                {
                    return n.GetSubnode(path.Skip(1).ToArray());
                }
            }
        }

        public IEnumerable<Node> GetSubnodes(params string[] path)
        {
            if (path.Length == 0) return children.Where(x => x is Node).Cast<Node>();
            if (path[0] == "*")
            {
                return GetSubnodes();
            }
            if (path.Length == 1)
            {
                return children.Where(x => x is Node && String.Compare(x.Name, path[0], StringComparison.OrdinalIgnoreCase) == 0).Cast<Node>();
            }
            else
            {
                var n = children.Find(x => String.Compare(x.Name, path[0], StringComparison.OrdinalIgnoreCase) == 0) as Node;
                return n.GetSubnodes(path.Skip(1).ToArray());
            }
        }

        public IEnumerable<Node> GetSubnodes(Func<Node, bool> match)
        {
            return children.Where(x => x is Node).Cast<Node>().Where(match);
        }

        public IEnumerable<Node> GetSubnodes(bool recursive)
        {
            if (recursive)
            {
                return RecurseSubnodes(new List<Node>());
            }
            else
            {
                return children.FindAll(x => x is Node).Cast<Node>();
            }
        }

        public Node GetSubnode(int index)
        {
            int idx = 0;
            for (int i = 0; i < children.Count; i++)
            {
                if (children[i] is Node)
                {
                    if (idx++ == index) return children[i] as Node;
                }
            }
            return null;
        }

        #endregion


        internal List<Node> GetSubnodes(string name)
        {
            return children.FindAll(x => x is Node && String.Compare(x.Name, name, StringComparison.OrdinalIgnoreCase) == 0).Cast<Node>().ToList();
        }

        public bool HasAParent()
        {
            return parent != null;
        }

        /// <summary>
        /// Returns true if the entity has a parent and this parent has a given name, otherwise false.
        /// </summary>
        /// <returns>True if the entity has a parent of a given name, otherwise false</returns>
        public bool HasAParent(string name)
        {
            return Depth > 1 && String.Compare(Parent.Name, name, true) == 0;
        }

        /// <summary>
        /// Returns true if the entity has a parent and this parent has a given name, counting in the
        /// hierarchy above by the given number of levels, otherwise false.
        /// </summary>
        /// <returns>True if the entity has a parent of a given name up in the hierarchy, otherwise false</returns>
        public bool HasAParent(string name, int levels)
        {
            if (levels == 1)
                return HasAParent(name);
            else if (Depth > 1)
                return Parent.HasAParent(name, levels - 1);
            else
                return false;
        }

        /// <summary>
        /// Returns true if the entity has a parent, counting in the hierarchy above by the given number of levels, otherwise false.
        /// </summary>
        /// <returns>True if the entity has a parent up in the hierarchy, otherwise false</returns>
        public bool HasAParent(int levels)
        {
            return Depth - 1 >= levels;
        }

        // A top entity in the hierarchical tree.
        public Node GetTopParent()
        {
            Node n = this;
            while (n.HasAParent())
            {
                n = n.Parent;
            }
            return n;
        }

        public string GetAttributeValue(string name)
        {
            if (name == null || name == "") return "";
            return (children.Find(x => x is Attribute && String.Compare(x.Name, name, true) == 0)?.Value) ?? "";
        }

        public string GetAttributeValue(int index)
        {
            int idx = 0;
            for (int i = 0; i < children.Count; i++)
            {
                if (children[i] is Attribute)
                {
                    if (idx++ == index) return children[i].Value;
                }
            }
            return "";
        }

        public IEnumerable<string> GetAttributeValues(string name)
        {
            for (int i = 0; i < children.Count; i++)
            {
                if (children[i] is Attribute && String.Compare(children[i].Name, name, true) == 0)
                {
                    yield return children[i].Value;
                }
            }
        }

        public string GetAttributeName(string value)
        {
            if (value == null || value == "") return "";
            return (children.Find(x => x is Attribute && String.Compare(x.Value, value, true) == 0)?.Name) ?? "";
        }

        public string GetAttributeName(int index)
        {
            try
            {
                return children.Where(x => x is Attribute).ElementAt(index).Name;
            }
            catch (ArgumentOutOfRangeException)
            {
                return "";
            }
        }

        internal List<Node> RecurseSubnodes(List<Node> output)
        {
            var subnodes = GetSubnodes();
            output.AddRange(subnodes);
            foreach (var n in subnodes)
            {
                output.AddRange(n.RecurseSubnodes(output));
            }
            return output;
        }

        public IEnumerable<string> GetAttributeNames(string value)
        {
            for (int i = 0; i < children.Count; i++)
            {
                if (children[i] is Attribute && String.Compare(children[i].Value, value, true) == 0)
                {
                    yield return children[i].Value;
                }
            }
        }

        public IEnumerable<string> GetAttributeNames()
        {
            return children.Where(x => x is Attribute).Select(x => x.Name);
        }

        public IEnumerable<string> GetAttributeValues()
        {
            List<string> output = new List<string>();
            for (int i = 0; i < children.Count; i++)
            {
                if (children[i] is Attribute)
                {
                    yield return children[i].Value;
                }
            }
        }

        public IEnumerable<KeyValuePair<string, string>> GetAttributes()
        {
            for (int i = 0; i < children.Count; i++)
            {
                if (children[i] is Attribute)
                {
                    yield return new KeyValuePair<string, string>(children[i].Name, children[i].Value);
                }
            }
        }

        public KeyValuePair<string, string> GetAttribute(int index)
        {
            int idx = 0;
            for (int i = 0; i < children.Count; i++)
            {
                if (children[i] is Attribute)
                {
                    if (idx++ == index) return new KeyValuePair<string, string>(children[i].Name, children[i].Value);
                }
            }
            return new KeyValuePair<string, string>();
        }

        public string GetEntry(int index)
        {
            int idx = 0;
            for (int i = 0; i < children.Count; i++)
            {
                if (children[i] is Entry)
                {
                    if (idx++ == index) return children[i].Value;
                }
            }
            return "";
        }

        public IEnumerable<string> GetEntries()
        {
            return children.Where(x => x is Entry).Select(x => x.Value);
        }

        public bool AttributeExists(string name, string value)
        {
            return children.Exists(x => x is Attribute && String.Compare(x.Name, name, true) == 0 && String.Compare(x.Value, value, true) == 0);
        }

        public bool AttributeWithNameExists(string name)
        {
            return children.Exists(x => x is Attribute && String.Compare(x.Name, name, true) == 0);
        }

        public bool AttributeWithValueExists(string value)
        {
            return children.Exists(x => x is Attribute && String.Compare(x.Value, value, true) == 0);
        }

        public bool EntryExists(string value)
        {
            return children.Exists(x => x is Entry && String.Compare(x.Value, value, true) == 0);
        }

        public bool PathExists(bool lastNodeIsAttribute, params string[] subnodes)
        {
            Node n = this;
            for (int i = 0; i < subnodes.Length; i++)
            {
                if (!n.NodeExists(subnodes[i]) && i == subnodes.Length - 1 && lastNodeIsAttribute)
                {
                    return n.AttributeWithNameExists(subnodes[i]);
                }
                n = n.GetSubnode(subnodes[i]);
            }
            return true;
        }

        /// <summary>
        /// Returns true if a given entity has any child (any other node beneath in hierarchy)
        /// </summary>
        /// <returns>True if the entity has a child, otherwise false</returns>
        public bool NodeExists(string name)
        {
            return children.Exists(x => x is Node && String.Compare(x.Name, name, true) == 0);
        }

        public void CleanUp(string mask, bool caseSensitive)
        {
            string m = caseSensitive ? mask : mask.ToLowerInvariant();
            if (m.StartsWith("*") && !m.EndsWith("*"))
            {
                m = mask.Replace("*", "");
                for (int i = children.Count - 1; i >= 0; i--)
                {
                    string name = caseSensitive ? children[i].Name : children[i].Name.ToLowerInvariant();
                    if (name.EndsWith(m))
                    {
                        children.RemoveAt(i);
                    }
                    else
                    {
                        string value = caseSensitive ? children[i].Value : children[i].Value.ToLowerInvariant();
                        if (value.EndsWith(m)) children.RemoveAt(i);
                    }
                }
            }
            else if (m.EndsWith("*") && !m.StartsWith("*"))
            {
                m = mask.Replace("*", "");
                for (int i = children.Count - 1; i >= 0; i--)
                {
                    string name = caseSensitive ? children[i].Name : children[i].Name.ToLowerInvariant();
                    if (name.StartsWith(m))
                    {
                        children.RemoveAt(i);
                    }
                    else
                    {
                        string value = caseSensitive ? children[i].Value : children[i].Value.ToLowerInvariant();
                        if (value.StartsWith(m)) children.RemoveAt(i);
                    }
                }
            }
            else
            {
                m = mask.Replace("*", "");
                for (int i = children.Count - 1; i >= 0; i--)
                {
                    string name = caseSensitive ? children[i].Name : children[i].Name.ToLowerInvariant();
                    if (name.Contains(m))
                    {
                        children.RemoveAt(i);
                    }
                    else
                    {
                        string value = caseSensitive ? children[i].Value : children[i].Value.ToLowerInvariant();
                        if (value.Contains(m)) children.RemoveAt(i);
                    }
                }
            }

            for (int i = 0; i < children.Count; i++)
            {
                if (children[i] is Node) (children[i] as Node).CleanUp(mask, caseSensitive);
            }
        }
    }

}
