﻿// This file is part of JoinPDF.
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

using System;
using System.Collections.Generic;
using System.IO;

namespace JoinPDF
{
    class Program
    {
        static void Main(string[] args)
        {
            // join two pdf's in one (using byte[])
            JoinPDF joinPdf = new JoinPDF();            
            byte[] joined = joinPdf.Join(File.ReadAllBytes("pdfJoin/p1.pdf"), File.ReadAllBytes("pdfJoin/p2.pdf"));

            byte[] joined2 = joinPdf.Join(joinPdf.Join(joined, File.ReadAllBytes("pdfJoin/p2.pdf")), File.ReadAllBytes("pdfJoin/p2.pdf"));

            // and write it out!
            File.WriteAllBytes("pdfJoin/p1_p2_final.pdf", joined);
            File.WriteAllBytes("pdfJoin/p1_p2_final2.pdf", joined2);
        }
    }
}
