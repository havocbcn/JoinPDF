﻿// This file is part of SharpReport.
// 
// SharpReport is free software: you can redistribute it and/or modify
// it under the terms of the GNU Lesser General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// SharpReport is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU Lesser General Public License for more details.
// 
// You should have received a copy of the GNU Lesser General Public License
// along with SharpReport.  If not, see <http://www.gnu.org/licenses/>.

using System;
using System.Collections.Generic;
using System.IO;

namespace JoinPDF
{
    class Program
    {
        static void Main(string[] args)
        {
            File.WriteAllBytes("destinationPath", JoinPDF(File.ReadAllBytes("source1.pdf"), File.ReadAllBytes("source2.pdf")));

        }

        private static byte[] JoinPDF(byte[] pdf1, byte[] pdf2)
        {
            string trailer1;
            Dictionary<int, XrefItem> dctXref1 = new Dictionary<int, XrefItem>();
            trailer1 = ReadXRef(pdf1, dctXref1);

            ReadAllContent(pdf1, dctXref1);

            string trailer2;
            Dictionary<int, XrefItem> dctXref2 = new Dictionary<int, XrefItem>();
            trailer2 = ReadXRef(pdf2, dctXref2);

            ReadAllContent(pdf2, dctXref2);

            AssignNewIndex(dctXref1, 1);

            int pages1 = LookUpPages(dctXref1);
            int pages2 = LookUpPages(dctXref2);
            dctXref2[pages2].newId = dctXref1[pages1].newId;
            dctXref2[pages2].used = false;


            int lastIndex = dctXref1.Count + 1;
            AssignNewIndex(dctXref2, lastIndex);
            

            // second pages are eliminated
            
            

            RetouchAllContent(dctXref1);
            RetouchAllContent(dctXref2);
            
            // sum pages from pdf2 to pdf1
            JoinPages(dctXref1, dctXref2);

          


            MemoryStream output = new MemoryStream();

            output.Write(pdf1, 0, 8);   // %PDF-1.X
            uint pos = 8;
            int XRefElements = 0;
            foreach (KeyValuePair<int, XrefItem> kvp in dctXref1)
            {
                if (!kvp.Value.used)
                    continue;

                XRefElements++;
                kvp.Value.pos = pos + 1;
                pos += (uint)(kvp.Value.content.Length + 1);

                output.WriteByte(10);   // \n
                output.Write(kvp.Value.content, 0, kvp.Value.content.Length);
            }

            foreach (KeyValuePair<int, XrefItem> kvp in dctXref2)
            {
                if (!kvp.Value.used)
                    continue;

                XRefElements++;
                kvp.Value.pos = pos + 1;
                pos += (uint)(kvp.Value.content.Length + 1);

                output.WriteByte(10);   // \n
                output.Write(kvp.Value.content, 0, kvp.Value.content.Length);
            }

            // xref
            output.WriteByte(10);   // \n
            uint xrefPos = pos + 1;
            byte[] textByte = System.Text.ASCIIEncoding.ASCII.GetBytes("xref");
            output.Write(textByte, 0, textByte.Length);
            output.WriteByte(10);   // \n

            textByte = System.Text.ASCIIEncoding.ASCII.GetBytes("0 " + XRefElements);
            output.Write(textByte, 0, textByte.Length);
            output.WriteByte(10);   // \n
            textByte = System.Text.ASCIIEncoding.ASCII.GetBytes("0000000000 65535 f");
            output.Write(textByte, 0, textByte.Length);
            output.WriteByte(10);   // \n

            foreach (KeyValuePair<int, XrefItem> kvp in dctXref1)
            {
                 if (!kvp.Value.used)
                    continue;

                textByte = System.Text.ASCIIEncoding.ASCII.GetBytes(kvp.Value.pos.ToString("D10") + " 00000 n");
                output.Write(textByte, 0, textByte.Length);
                output.WriteByte(10);   // \n
            }

            foreach (KeyValuePair<int, XrefItem> kvp in dctXref2)
            {
                 if (!kvp.Value.used)
                    continue;

                textByte = System.Text.ASCIIEncoding.ASCII.GetBytes(kvp.Value.pos.ToString("D10") + " 00000 n");
                output.Write(textByte, 0, textByte.Length);
                output.WriteByte(10);   // \n
            }

            string newTrailer = trailer1;
            string[] parts = trailer1.Split(separator, StringSplitOptions.RemoveEmptyEntries);

            // nuevo root?
            int i = 0;
            while (i < parts.Length)
            {
                if (parts[i].ToLower() == "size")
                    newTrailer = newTrailer.Replace(parts[i + 1], XRefElements.ToString());
                //parts[i+1] = XRefElements.ToString();
                if (parts[i].ToLower() == "startxref")
                    newTrailer = newTrailer.Replace(parts[i + 1], xrefPos.ToString());
                //parts[i+1] = xrefPos.ToString();
                i++;
            }
            //textByte = System.Text.ASCIIEncoding.ASCII.GetBytes(string.Join('\n', parts));
            textByte = System.Text.ASCIIEncoding.ASCII.GetBytes(newTrailer);
            output.Write(textByte, 0, textByte.Length);

            return output.ToArray();
        }

        private static void JoinPages(Dictionary<int, XrefItem> dctXref1, Dictionary<int, XrefItem> dctXref2)
        {
            int pages1 = LookUpPages(dctXref1);
            if (pages1 < 0)
                throw new Exception("No pages found");
            int pages2 = LookUpPages(dctXref2);
            if (pages2 < 0)
                throw new Exception("No pages found");

            if (dctXref2[pages2].text.Count != 1)
                throw new Exception("page list with stream?");

            string pages1Content = dctXref1[pages1].newText[0];
            int startPages1Content = pages1Content.IndexOf("[") + 1;
            int endPages1Content = pages1Content.IndexOf("]", startPages1Content);
            string content1 = pages1Content.Substring(startPages1Content, endPages1Content - startPages1Content);
            int numPages1 = content1.Split('R', StringSplitOptions.RemoveEmptyEntries).Length;

            string pages2Content = dctXref2[pages2].newText[0];
            int startPages2Content = pages2Content.IndexOf("[") + 1;
            int endPages2Content = pages2Content.IndexOf("]", startPages2Content);
            string content2 = pages2Content.Substring(startPages2Content, endPages2Content - startPages2Content);
            int numPages2 = content2.Split('R', StringSplitOptions.RemoveEmptyEntries).Length;

            string newContent = pages1Content.Replace(content1, content1 + " " + content2);

            // TODO
            newContent = newContent.Replace("/Count " + numPages1, "/Count " + (numPages1 + numPages2));

            dctXref1[pages1].content = System.Text.ASCIIEncoding.ASCII.GetBytes(newContent);
        }

        private static int LookUpPages(Dictionary<int, XrefItem> dctXref1)
        {
            foreach (KeyValuePair <int, XrefItem> item in dctXref1)
            {
                foreach (string t in item.Value.text)
                    if (t.ToLower().Contains("type") 
                        && t.ToLower().Contains("pages") 
                        && t.ToLower().Contains("kids") )
                        return item.Key;
            }

            return -1;
        }

        private static void RetouchAllContent(Dictionary<int, XrefItem> dctXref2)
        {
            foreach (KeyValuePair <int, XrefItem> item in dctXref2)
            {
                List<string> lstNewText = new List<string>();

                List<byte[]> lstByte = new List<byte[]>();
                foreach (string text in item.Value.text)
                {
                    string newText = text;

                    // 1 0 obj.... => newid 0 obj...
                    int objIndex = newText.IndexOf("0 obj");
                    if (objIndex > 0 && objIndex < 10)
                        newText = item.Value.newId + " " + newText.Substring(objIndex);

                    string[] parts = text.Split(separator, StringSplitOptions.RemoveEmptyEntries);
                    int i = 0;
                    while (i < parts.Length - 3)
                    {
                        if (parts[i+1] == "0" && parts[i+2] == "R")
                        {
                            // /kids[5
                            // 0
                            // R

                            // or

                            // 5
                            // 0
                            // R

                            // first: obtain the number 5 (id)
                            string number;
                            int j = parts[i].Length-1;
                            while (j >= 0 && parts[i][j] >= '0' && parts[i][j] <= '9')
                            {
                                j--;
                            }

                            if (j < 0)
                            {
                                // 5
                                // 0
                                // R
                                //parts[i] = dctXref2[Convert.ToInt32(parts[i])].newId.ToString();
                                newText = replacerPDFNumbers(newText, Convert.ToInt32(parts[i]), dctXref2[Convert.ToInt32(parts[i])].newId);
                                //newText = newText.Replace(parts[i] + " 0 R", dctXref2[Convert.ToInt32(parts[i])].newId.ToString() + " 0 R");
                            }
                            else
                            {
                                number = parts[i].Substring(j+1);
                                //parts[i] = parts[i].Substring(0, j) + dctXref2[Convert.ToInt32(number)].newId.ToString();
                                newText = replacerPDFNumbers(newText, Convert.ToInt32(number), dctXref2[Convert.ToInt32(number)].newId);
                                //newText = newText.Replace(number + " 0 R", dctXref2[Convert.ToInt32(number)].newId.ToString() + " 0 R");
                            }
                        }
                        i++;
                    }                    

                    //string newText = string.Join((char)10, parts);                    
                    item.Value.newText.Add(newText);
                    lstByte.Add(System.Text.ASCIIEncoding.ASCII.GetBytes(newText));                    
                }

                if (lstByte.Count > 2)
                    throw new Exception("Sorry, don't know how to handle multistream objects");

                if (lstByte.Count == 1)
                {
                    item.Value.content = lstByte[0];                    
                }
                else
                {
                    byte[] rv = new byte[lstByte[0].Length + lstByte[1].Length + item.Value.streamContent.Length];
                    System.Buffer.BlockCopy(lstByte[0], 0, rv, 0, lstByte[0].Length);
                    System.Buffer.BlockCopy(item.Value.streamContent, 0, rv, lstByte[0].Length, item.Value.streamContent.Length);
                    System.Buffer.BlockCopy(lstByte[1], 0, rv, lstByte[0].Length + item.Value.streamContent.Length, lstByte[1].Length);

                    item.Value.content = rv;
                }                
            }
        }

        private static string replacerPDFNumbers(string newText, int v1, int v2)
        {
            // newtext = 1 0 r 11 0 r
            // v1 = 1
            // V2 = 5

            // result => 5 0 r 11 0 r (and not 5 0 e 15 0 r)
            int i = newText.IndexOf(" 0 R");
            while (i > 0)
            {
                int number = getNumber(newText, i);
                if (number == v1)
                {
                    newText = newText.Substring(0, i-number.ToString().Length) + v2 + newText.Substring(i);
                    i = newText.IndexOf(" 0 R", i - number.ToString().Length + v2.ToString().Length + " 0 R".Length);
                }
                else
                {
                    i = newText.IndexOf(" 0 R", i+1);
                }
            }

            return newText;
        }

        private static int getNumber(string newText, int i)
        {
            int endIndex = i;       
            i--;     
            while (newText[i] >= '0' && newText[i] <= '9' && i >=1)
            {
                i--;
            }
            int startIndex = i+1;
            
            return Convert.ToInt32(newText.Substring(startIndex, endIndex-startIndex));
        }

        private static void AssignNewIndex(Dictionary<int, XrefItem> dctXref2, int lastIndex)
        {
            foreach (KeyValuePair <int, XrefItem> item in dctXref2)
            {
                if (!item.Value.used)
                    continue;
                item.Value.newId = lastIndex;
                lastIndex++;
            }
        }

        private static void ReadAllContent(byte[] pdf1, Dictionary<int, XrefItem> dctXref1)
        {
            foreach (KeyValuePair <int, XrefItem> item in dctXref1)
            {
                if (item.Value.used)
                    ReadContent(pdf1, item);
            }
        }

        private static void ReadContent(byte[] pdf1, KeyValuePair<int, XrefItem> item)
        {
            uint startIndex = item.Value.pos;
            uint startTextIndex = startIndex;

            // hasta el stream, es texto limpio
            // stream es opcional, pero si aparece, todo bytearray hasta endstream
            // y luego hasta endobj
            
            uint endIndex = startIndex;
            

            bool isNotEnded = true;            

            while (isNotEnded)
            {
                if (pdf1[endIndex] == 's' &&
                    pdf1[endIndex+1] == 't' &&
                    pdf1[endIndex+2] == 'r' &&
                    pdf1[endIndex+3] == 'e' &&
                    pdf1[endIndex+4] == 'a' &&
                    pdf1[endIndex+5] == 'm')
                {
                    endIndex += 6;                    

                    item.Value.text.Add( GetString(pdf1, startTextIndex, endIndex));                    

                    uint startStream = 0;
                    uint endStream = 0;

                    startStream = endIndex;

                    string dictionary = GetString(pdf1, startIndex, startStream);
                    string[] parts = dictionary.Split(separator, StringSplitOptions.RemoveEmptyEntries);
                    int i = 0;
                    uint? streamLength = null;
                    while (i < parts.Length)
                    {
                        if (parts[i].ToLower() == "length")
                        {
                            streamLength = Convert.ToUInt32(parts[i+1]);
                            break;
                        }

                        i++;
                    }

                    if (streamLength == null)
                        throw new Exception("stream without length definition");

                    endStream = startStream + streamLength.Value + 1;
                    startTextIndex = endStream;
                    endIndex = endStream + 9;   // 9 = endstream
                    item.Value.streamContent = pdf1.Slice(startStream, endStream);
                    item.Value.hasAStream = true;
                }

                if (pdf1[endIndex] == 'e' &&
                    pdf1[endIndex+1] == 'n' &&
                    pdf1[endIndex+2] == 'd' &&
                    pdf1[endIndex+3] == 'o' &&
                    pdf1[endIndex+4] == 'b' &&
                    pdf1[endIndex+5] == 'j')
                {
                    endIndex += 6;
                    isNotEnded = false;
                }
                else
                    endIndex++;
            }

            
            item.Value.fullContent = pdf1.Slice(startIndex, endIndex);            
            item.Value.text.Add( GetString(pdf1, startTextIndex, endIndex));
        }

        public static char[] separator = { (char)0, (char)9, (char)10, (char)12, (char)13, (char)32
        , (char)'[', (char)']', (char)'/' };

        private static string ReadXRef(byte[] pdf1, Dictionary<int, XrefItem> dctXref)
        {
            string trailer;
            uint trailerPdf1 = (uint)pdf1.Length - 9;
            while (pdf1[trailerPdf1] != 't' ||
                    pdf1[trailerPdf1 + 1] != 'r' ||
                    pdf1[trailerPdf1 + 2] != 'a' ||
                    pdf1[trailerPdf1 + 3] != 'i' ||
                    pdf1[trailerPdf1 + 4] != 'l' ||
                    pdf1[trailerPdf1 + 5] != 'e' ||
                    pdf1[trailerPdf1 + 6] != 'r')
            {
                trailerPdf1--;
                if (trailerPdf1 == 0)
                    throw new Exception("PDF trailer not found");
            }

            trailer = GetString(pdf1, trailerPdf1, (uint)pdf1.Length);
            string[] parts = trailer.Split(separator, StringSplitOptions.RemoveEmptyEntries);

            uint pdf1XrefPos = Convert.ToUInt32(parts[parts.Length - 2]);

            // Cross ref table
            // sorry, only works with xref at the end of the file
            string xref = GetString(pdf1, pdf1XrefPos, trailerPdf1);
            parts = xref.Split(separator, StringSplitOptions.RemoveEmptyEntries);

            if (parts[0] != "xref")
                throw new Exception("no xref table found");


            int lastIndex = 1;
            while (lastIndex < parts.Length)
                lastIndex = ReadXref(parts, lastIndex, dctXref);
            return trailer;
        }

        private static int ReadXref(string[] parts, int xrefStartPointer, Dictionary<int, XrefItem> dctXref)
        {
            // el array de partes es mas grande, cada elemento es una posición
            // 0 6  <- position apunta al 0
            // 0000000003 65535 f
            // 0000000017 00000 n
            // 0000000081 00000 n
            // 0000000000 00007 f
            // 0000000331 00000 n
            // 0000000409 00000 n

            int count = 0;

            for (int xrefIndex = Convert.ToInt32(parts[xrefStartPointer]); 
                xrefIndex < Convert.ToInt32(parts[xrefStartPointer+1]); 
                xrefIndex++)
            {
                XrefItem item = new XrefItem() {
                    id = xrefIndex,
                    pos = Convert.ToUInt32(parts[xrefStartPointer + 2 + count * 3]),
                    iteration =  Convert.ToUInt32(parts[xrefStartPointer + 3 + count * 3]),
                    used =  parts[xrefStartPointer + 4 + count * 3] == "n" ? true : false
                };

                if (item.used)
                    dctXref.Add(xrefIndex, item);

                count++;
            }

            return xrefStartPointer + 4 + count * 3;
        }

        private static string GetString(byte[] pdf1, uint startXrefPos, uint startXrefPosEnd)
        {
            return System.Text.ASCIIEncoding.ASCII.GetString(pdf1.Slice(startXrefPos, startXrefPosEnd));
        }       
    }
}
