// This file is part of JoinPDF.
// 
// JoinPDF is free software: you can redistribute it and/or modify
// it under the terms of the GNU Lesser General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// JoinPDF is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU Lesser General Public License for more details.
// 
// You should have received a copy of the GNU Lesser General Public License
// along with JoinPDF.  If not, see <http://www.gnu.org/licenses/>.

using System.Collections.Generic;

namespace JoinPDF
{
    public class XrefItem
    {
        public int id;
        public int newId;
        public uint pos;
        public bool IsUsed;
        public byte[] content;        
        public List<string> text = new List<string>();
        public List<string> newText = new List<string>();
        public byte[] streamContent;
    }
}