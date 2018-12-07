using System.Collections.Generic;

namespace JoinPDF
{
    public class XrefItem
    {
        public int id;
        public int newId;
        public uint pos;

        public uint iteration;

        public bool used;

        public byte[] content;

        public bool hasAStream = false; 
        public List<string> text = new List<string>();
        public List<string> newText = new List<string>();
        public byte[] fullContent;

        public byte[] streamContent;
    }
}