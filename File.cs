using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Text.RegularExpressions;

namespace CEParser
{
    [Serializable]
    public abstract class File
    {
        public Node Root
        {
            get; protected set;
        }

        protected Nodesets nodesets = new Nodesets();
        protected Stack<Node> hierarchy = new Stack<Node>();

        public List<ParseError> Errors
        {
            get; private set;
        }

        public File()
        {
            Bytes.Initialize();
            Errors = new List<ParseError>();

            Root = new Node();
            hierarchy.Push(Root);
        }

        public static File Create(string path, string gameToken, Encoding encoding)
        {
            if (IsBinary(path))
            {
                return new BinaryFile(path, gameToken, encoding);
            }
            else
            {
                return new TextFile(path);
            }
        }

        public abstract void Parse();

        public async Task ParseAsync()
        {
            await Task.Run(() =>
            {
                Parse();
            });
        }

        public string Export()
        {
            StringBuilder sb = new StringBuilder(" ");
            bool endline = false;
            Root.Export(sb, 0, ref endline);
            sb.Remove(0, 2);
            return sb.ToString();
        }

        protected static StreamReader GetStreamReader(string path)
        {
            return new StreamReader(System.IO.File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite), Encoding.GetEncoding(1250), true, 1024, true);
        }

        public Dictionary<ushort, Node> this[string name]
        {
            get { return nodesets.Get(name); }
        }

        #region Nodeset methods

        public void AddNodeset(string name, Dictionary<ushort, Node> nodes)
        {
            nodesets.Add(name, nodes);
        }

        public Dictionary<ushort, Node> GetNodeset(string name)
        {
            return nodesets.Get(name);
        }

        public void RemoveNodeset(string name)
        {
            nodesets.Remove(name);
        }

        #endregion

        public static bool IsCompressed(string path)
        {
            using (BinaryReader br = new BinaryReader(System.IO.File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)))
            {
                byte[] code = br.ReadBytes(2);
                return (code[0] == 80 && code[1] == 75);
            }
        }

        public static bool IsBinary(string path)
        {
            if (IsCompressed(path))
            {
                byte[] code = Extract(path, "meta", 6);
                if (code == null) // no meta file found
                {
                    code = Extract(path, "*" + Path.GetExtension(path), 6);
                    if (code == null) throw new Exception("Cannot extract file.");

                }
                return (code[3] == 98 && code[4] == 105 && code[5] == 110);
            }
            else
            {
                using (BinaryReader br = new BinaryReader(System.IO.File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)))
                {
                    byte[] code = br.ReadBytes(7);
                    return (code.Length >= 7 && (code[3] == 98 && code[4] == 105 && code[5] == 110) || (code[4] == 98 && code[5] == 105 && code[6] == 110));
                }
            }
        }

        public static byte[] Extract(string path, string entry, int length)
        {
            using (System.IO.Compression.ZipArchive zip = new System.IO.Compression.ZipArchive(System.IO.File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)))
            {
                var e = zip.Entries.FirstOrDefault(x => Like(x.Name, entry));
                if (e == null) return null;
                Stream s = e.Open();

                MemoryStream ms = new MemoryStream();
                if (length < 0)
                {
                    s.CopyTo(ms);
                    return ms.ToArray();
                }
                else
                {
                    byte[] output = new byte[Math.Min(length, s.Length)];
                    s.Read(output, 0, output.Length);
                    return output;
                }
            }
        }

        public static byte[] Extract(string path, string entry)
        {
            return Extract(path, entry, -1);
        }

        static bool Like(string expression, string mask)
        {
            var rx = new Regex("^" + Regex.Escape(mask).Replace(@"\*", ".*").Replace(@"\?", "."));
            return rx.IsMatch(expression);
        }

        public static byte[] Read(string path, int count)
        {
            using (BinaryReader br = new BinaryReader(System.IO.File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)))
            {
                return count < 0 ? br.ReadBytes((int)br.BaseStream.Length) : br.ReadBytes(count);
            }
        }

        public static byte[] Read(string path)
        {
            return Read(path, -1);
        }

        protected void AddError(int position, string error, string details, int score)
        {
            Errors.Add(new ParseError(position, error, details, score));
        }

        protected void AddError(int position, string error, string details, int score, Entity entity)
        {
            Errors.Add(new ParseError(position, error, details, score, entity));
        }

        public void CleanUp(string mask, bool caseSensitive)
        {
            Root.CleanUp(mask, caseSensitive);
        }

        #region Event handling

        // File parse progress

        public delegate void FileParseProgressHandler(object sender, FileParseEventArgs e);

        public event FileParseProgressHandler FileParseProgress;

        internal void OnFileParseProgress(double progress)
        {
            FileParseProgress?.Invoke(this, new FileParseEventArgs(progress));
        }
        #endregion
    }

    public class FileParseEventArgs : EventArgs
    {
        public FileParseEventArgs(double progress)
        {
            Progress = progress;
        }
        public double Progress { get; private set; }
    }


    enum ParsePhase
    {
        Looking,
        RecordingQuotedLHS,
        RecordingQuotelessLHS,
        SkippingComments,
        LookingAfterRecordedQuotelessLHS,
        LookingAfterRecordedQuotedLHS,
        LookingForRHS,
        RecordingQuotedRHS,
        RecordingQuotelessRHS,
    };

    public class ParseError
    {
        public int Position { get; set; }
        public string Error { get; set; }
        public string Details { get; set; }
        public int SeverityScore { get; set; }
        public Entity Entity { get; set; }

        public ParseError(int position, string error, string details, int score) : this(position, error, details, score, null)
        {
        }

        public ParseError(int position, string error, string details, int score, Entity entity)
        {
            Position = position;
            Error = error;
            Details = details;
            SeverityScore = score;
            Entity = entity;
        }

        public void CalculateSeverityScore()
        {
            if (SeverityScore > 0 || Entity == null)
                return;

            SeverityScore = Entity.GetChildrenCount() + 1;
        }
    }
}
