﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CEParser
{
    [Serializable]
    public class BinaryFile : File, IDisposable
    {
        MemoryStream stream;
        string token;
        public Ironmelt Decoder { get; private set; }

        public BinaryFile(string path, string gameToken, Encoding encoding)
        {
            if (IsCompressed(path))
            {
                this.stream = new MemoryStream(Extract(path, "*" + Path.GetExtension(path)));
            }
            else
            {
                this.stream = new MemoryStream(Read(path));
            }
            this.Decoder = new Ironmelt(gameToken, Ironmelt.ReadTokensFile(gameToken + "bin.csv"), encoding)
            {
                IncludeHour = gameToken == "hoi4"
            };
        }

        /// <summary>
        /// Creates a new file structure.
        /// </summary>
        public BinaryFile(MemoryStream stream, string gameToken, string[] tokens, Encoding encoding) : base()
        {
            this.token = gameToken ?? throw new NullReferenceException("Null token passed to BinaryFile creator.");
            this.stream = stream;
            this.Decoder = new Ironmelt(gameToken, tokens, encoding)
            {
                IncludeHour = gameToken == "hoi4"
            };
        }

        public BinaryFile(MemoryStream stream, string gameToken, Encoding encoding) : base()
        {
            this.token = gameToken?? throw new NullReferenceException("Null token passed to BinaryFile creator.");
            this.stream = stream;
            this.Decoder = new Ironmelt(gameToken, Ironmelt.ReadTokensFile(gameToken + "bin.csv"), encoding)
            {
                IncludeHour = gameToken == "hoi4"
            };
        }

        public override void Parse()
        {
            ParsePhase phase = ParsePhase.Looking;

            int sl = (int)stream.Length;
            DecodeResult lhs = new DecodeResult();

            try
            {
                while (stream.Position < sl)
                {
                    if (stream.Position % 100000 == 0) OnFileParseProgress(stream.Position / (float)sl);

                    byte b = (byte)stream.ReadByte();
                    Decoder.Decode(b, hierarchy);
                    if (Decoder.Result.Token == null) continue;

                    switch (phase)
                    {
                        case ParsePhase.Looking:
                            switch (Decoder.Result.Token)
                            {
                                case "=": AddError((int)stream.Position, "Parsing error", "Unexpected equals block found, without left-hand side.", 100); break;
                                case "{": AddContainer(); break;
                                case "}": CloseContainer(); break;
                                default: lhs = Decoder.Result; phase = Decoder.Result.Quoted ? ParsePhase.LookingAfterRecordedQuotedLHS : ParsePhase.LookingAfterRecordedQuotelessLHS; break;
                            }
                            break;

                        case ParsePhase.LookingAfterRecordedQuotedLHS:
                            switch (Decoder.Result.Token)
                            {
                                case "=": phase = ParsePhase.LookingForRHS; break;
                                case "{": AddEntry(lhs); lhs = new DecodeResult(); AddContainer(); phase = ParsePhase.Looking; break;
                                case "}": AddEntry(lhs); lhs = new DecodeResult(); CloseContainer(); phase = ParsePhase.Looking; break;
                                default: AddEntry(lhs); lhs = Decoder.Result; phase = Decoder.Result.Quoted ? ParsePhase.LookingAfterRecordedQuotedLHS : ParsePhase.LookingAfterRecordedQuotelessLHS; break;
                            }
                            break;

                        case ParsePhase.LookingAfterRecordedQuotelessLHS:
                            switch (Decoder.Result.Token)
                            {
                                case "=": phase = ParsePhase.LookingForRHS; break;
                                case "{": AddEntry(lhs); lhs = new DecodeResult(); AddContainer(); phase = ParsePhase.Looking; break;
                                case "}": AddEntry(lhs); lhs = new DecodeResult(); CloseContainer(); phase = ParsePhase.Looking; break;
                                default: AddEntry(lhs); lhs = Decoder.Result; phase = Decoder.Result.Quoted ? ParsePhase.LookingAfterRecordedQuotedLHS : ParsePhase.LookingAfterRecordedQuotelessLHS; break;
                            }
                            break;

                        case ParsePhase.LookingForRHS:
                            switch (Decoder.Result.Token)
                            {
                                case "=": AddError((int)stream.Position, "Parsing error", "Duplicated equals block found, while looking for right-hand side.", 100); break;
                                case "{": AddContainer(lhs); lhs = new DecodeResult(); phase = ParsePhase.Looking; break;
                                case "}": AddError((int)stream.Position, "Parsing error", "Closing brace found, while looking for right-hand side.", 100); break;
                                default: AddAttribute(lhs, Decoder.Result); lhs = new DecodeResult(); phase = ParsePhase.Looking; break;
                            }
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                AddError((int)stream.Position, "Unrecognized parsing error", ex.Message + "\n\n" + ex.StackTrace, 1000);
            }

            // Calculate severity
            foreach (var e in Errors)
            {
                e.CalculateSeverityScore();
            }

            OnFileParseProgress(1);
        }

        private void AddAttribute(DecodeResult lhs, DecodeResult rhs)
        {
            Attribute n = new Attribute(hierarchy.Peek(), lhs.Token, rhs.Token, rhs.Quoted);
            if (lhs.Unknown != null)
                AddError((int)stream.Position, "Unknown token", lhs.Unknown, 0, n);
            if (rhs.Unknown != null)
                AddError((int)stream.Position, "Unknown token", rhs.Unknown, 0, n);
        }

        private void AddEntry(DecodeResult value)
        {
            Entry n = new Entry(hierarchy.Peek(), value.Token, value.Quoted);
            if (value.Unknown != null)
                AddError((int)stream.Position, "Unknown token", value.Unknown, 0, n);
        }

        private void AddContainer()
        {
            Node n = new Node(hierarchy.Peek(), "");
            hierarchy.Push(n);
        }

        private void AddContainer(DecodeResult name)
        {
            Node n = new Node(hierarchy.Peek(), name.Token);
            if (name.Unknown != null)
                AddError((int)stream.Position, "Unknown token", name.Unknown, 0, n);
            hierarchy.Push(n);
        }

        private void CloseContainer()
        {
            if (hierarchy.Count > 1)
            {
                var n = hierarchy.Pop();
                n.PrepareCache();
            }
        }

        public void Dispose()
        {
            stream.Dispose();
        }
    }

}
