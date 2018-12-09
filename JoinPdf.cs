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

using System;
using System.Collections.Generic;
using System.IO;

namespace JoinPDF
{
    public class JoinPDF 
    {
        public byte[] Join(byte[] pdf1, byte[] pdf2)
        {
            Dictionary<int, XrefItem> dctXref1 = new Dictionary<int, XrefItem>();
            Dictionary<int, XrefItem> dctXref2 = new Dictionary<int, XrefItem>();

            ReadXRef(pdf1, dctXref1);
            ReadXRef(pdf2, dctXref2);

            ReadAllContent(pdf1, dctXref1);
            ReadAllContent(pdf2, dctXref2);            

            // free elements will be not copied, so numbers are rearranged
            int lastIndexToAssign = AssignNewIndex(dctXref1, 1);

            // the pages element indicates how many pages are
            // each page point to their pages parent            
            int page1Index = LookUpPagesElement(dctXref1);
            int page2Index = LookUpPagesElement(dctXref2);
            int page1Catalog = LookUpPagesCatalogElement(dctXref1);


            // in pdf2, each page will point to pdf1 pages
            dctXref2[page2Index].newId = dctXref1[page1Index].newId;
            // in pdf2, pages is disabled
            dctXref2[page2Index].IsUsed = false;

            // pdf2 continues index count
            AssignNewIndex(dctXref2, lastIndexToAssign);
            
            RemapReferencesFromIdToNewId(dctXref1);
            RemapReferencesFromIdToNewId(dctXref2);

            JoinPages(dctXref1, dctXref2, page1Index, page2Index);

            MemoryStream output = new MemoryStream();

            uint pos = WritePDFHeader(pdf1, output);
            int elementsWritten = WriteElements(dctXref1, output, ref pos);
            elementsWritten += WriteElements(dctXref2, output, ref pos);                        
            WriteXRefHeader(output);
            WriteXRefIndex(output, elementsWritten);
            WriteXRefFreeElement(output);
            WriteXrefUsedElements(dctXref1, output);
            WriteXrefUsedElements(dctXref2, output);
            WritePDFFooter(page1Catalog, output, elementsWritten, pos + 1);

            return output.ToArray();
        }

        private int LookUpPagesCatalogElement(Dictionary<int, XrefItem> dctXref1)
        {
            // 2 0 obj <</Pages 4 0 R/ViewerPreferences 3 0 R/Type/Catalog>>endobj
            foreach (KeyValuePair <int, XrefItem> item in dctXref1)
            {
                foreach (string t in item.Value.text)
                {
                    string lowerT = t.ToLower();
                    if (lowerT.Contains("/type") 
                        && lowerT.Contains("/catalog") 
                        && lowerT.Contains("/pages") )
                        return item.Key;
                }
            }

            throw new Exception("No catalog item found in pdf!");
        }

        private void WritePDFFooter(int page1Index, MemoryStream output, int elementsWritten, uint xrefPos)
        {
            output.WriteByte(10);   // \n
            string newTrailer = "trailer <</Root " + page1Index + " 0 R /Size " + elementsWritten + ">>\nstartxref " + xrefPos + "\n%%EOF";
            byte[] textByte = System.Text.ASCIIEncoding.ASCII.GetBytes(newTrailer);
            output.Write(textByte, 0, textByte.Length);
        }

        private void WriteXrefUsedElements(Dictionary<int, XrefItem> dctXref1, MemoryStream output)
        {
            foreach (KeyValuePair<int, XrefItem> kvp in dctXref1)
            {
                if (!kvp.Value.IsUsed)
                    continue;

                output.WriteByte(10);   // \n
                byte[] textByte = System.Text.ASCIIEncoding.ASCII.GetBytes(kvp.Value.pos.ToString("D10") + " 00000 n");
                output.Write(textByte, 0, textByte.Length);
            }
        }

        private void WriteXRefFreeElement(MemoryStream output)
        {
            output.WriteByte(10);   // \n
            byte[] textByte = System.Text.ASCIIEncoding.ASCII.GetBytes("0000000000 65535 f");
            output.Write(textByte, 0, textByte.Length);
        }

        private void WriteXRefIndex(MemoryStream output, int elementsWritten)
        {
            output.WriteByte(10);   // \n
            byte[] textByte = System.Text.ASCIIEncoding.ASCII.GetBytes("0 " + elementsWritten);
            output.Write(textByte, 0, textByte.Length);
        }

        private void WriteXRefHeader(MemoryStream output)
        {
            output.WriteByte(10);   // \n
            byte[]  textByte = System.Text.ASCIIEncoding.ASCII.GetBytes("xref");
            output.Write(textByte, 0, textByte.Length);
        }

        private int WriteElements(Dictionary<int, XrefItem> dctXref1, MemoryStream output, ref uint pos)
        {
            int elementsWritten = 0;
            foreach (KeyValuePair<int, XrefItem> kvp in dctXref1)
            {
                if (!kvp.Value.IsUsed)
                    continue;

                elementsWritten++;
                kvp.Value.pos = pos + 1;
                pos += (uint)(kvp.Value.content.Length + 1);

                output.WriteByte(10);   // \n
                output.Write(kvp.Value.content, 0, kvp.Value.content.Length);
            }
            
            return elementsWritten;
        }

        private uint WritePDFHeader(byte[] pdf, MemoryStream output)
        {
            output.Write(pdf, 0, 8);   // %PDF-1.X extracted from pdf1
            return 8;
        }

        private void JoinPages(Dictionary<int, XrefItem> XRef1, Dictionary<int, XrefItem> XRef2, int page1Index, int page2Index)
        {
            // example:
            // 4 0 obj <</Kids[5 0 R 17 0 R 23 0 R 35 0 R] /Count 4 /Type/Pages>> endobj
            if (XRef2[page2Index].text.Count != 1)
                throw new Exception("page list with stream?");

            string pages1Content = XRef1[page1Index].newText[0];
            int kidsIndex1 = pages1Content.ToLower().IndexOf("kids");
            int startPages1Content = pages1Content.IndexOf("[", kidsIndex1) + 1;
            int endPages1Content = pages1Content.IndexOf("]", startPages1Content);
            string content1 = pages1Content.Substring(startPages1Content, endPages1Content - startPages1Content);
            int numPages1 = content1.Split('R', StringSplitOptions.RemoveEmptyEntries).Length;

            string pages2Content = XRef2[page2Index].newText[0];
            int kidsIndex2 = pages2Content.ToLower().IndexOf("kids");
            int startPages2Content = pages2Content.IndexOf("[", kidsIndex2) + 1;
            int endPages2Content = pages2Content.IndexOf("]", startPages2Content);
            string content2 = pages2Content.Substring(startPages2Content, endPages2Content - startPages2Content);
            int numPages2 = content2.Split('R', StringSplitOptions.RemoveEmptyEntries).Length;
            
            string newContent = XRef1[page1Index].newId + " 0 obj <</Kids[" + content1 + " " + content2 + "] /Count " + (numPages1 + numPages2) + " /Type/Pages>> endobj";
            XRef1[page1Index].content = System.Text.ASCIIEncoding.ASCII.GetBytes(newContent);
        }

        private int LookUpPagesElement(Dictionary<int, XrefItem> dctXref1)
        {
            foreach (KeyValuePair <int, XrefItem> item in dctXref1)
            {
                foreach (string t in item.Value.text)
                {
                    string lowerT = t.ToLower();
                    if (lowerT.Contains("/type") 
                        && lowerT.Contains("/pages") 
                        && lowerT.Contains("/kids") )
                        return item.Key;
                }
            }

            throw new Exception("No pages item found in pdf!");
        }

        private void RemapReferencesFromIdToNewId(Dictionary<int, XrefItem> dctXref2)
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

        private string replacerPDFNumbers(string newText, int v1, int v2)
        {
            // newtext = 1 0 r 11 0 r
            // v1 = 1
            // V2 = 5

            // result => 5 0 r 11 0 r (and not 5 0 e 15 0 r)
            int i = newText.IndexOf(" 0 R");
            while (i > 0)
            {
                int number = GetReferenceNumber(newText, i);
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

        private int GetReferenceNumber(string newText, int i)
        {
            // I'm looking for 123 0 R, i points to the space between 3 and 0
            int endIndex = i;   // endindex points to the space
            i--;               // now points at 3
            while (newText[i] >= '0' && newText[i] <= '9' && i > 0)
            {
                i--;
            }
            int startIndex = i + 1; // the previous while end's one space behind
            
            // returns the number            
            return Convert.ToInt32(newText.Substring(startIndex, endIndex-startIndex));
        }

        private int AssignNewIndex(Dictionary<int, XrefItem> Xrefs, int startIndex)
        {
            int index = startIndex;
            foreach (KeyValuePair <int, XrefItem> item in Xrefs)
            {
                if (!item.Value.IsUsed)
                    continue;

                item.Value.newId = index;
                index++;
            }

            return index;
        }

        private void ReadAllContent(byte[] pdf1, Dictionary<int, XrefItem> dctXref1)
        {
            foreach (KeyValuePair <int, XrefItem> item in dctXref1)
            {
                if (item.Value.IsUsed)
                    ReadContent(pdf1, item);
            }
        }

        private void ReadContent(byte[] pdf, KeyValuePair<int, XrefItem> item)
        {
            // |--TEXT--||- optional binary-||--TEXT-..-|            
            // OBJ.......STREAM......ENDSTREM.TEXT.ENDOBJ

            uint startIndex = item.Value.pos;
            uint startTextIndex = startIndex;
            uint currentIndex = startIndex;            
            bool continueWorking = true;

            while (continueWorking)
            {
                if (pdf[currentIndex] == 's' &&
                    pdf[currentIndex+1] == 't' &&
                    pdf[currentIndex+2] == 'r' &&
                    pdf[currentIndex+3] == 'e' &&
                    pdf[currentIndex+4] == 'a' &&
                    pdf[currentIndex+5] == 'm')
                {
                    currentIndex += 6;                    

                    item.Value.text.Add(GetString(pdf, startTextIndex, currentIndex));                    

                    uint startStream = currentIndex;

                    string dictionary = GetString(pdf, startIndex, startStream);
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

                    uint endStream = startStream + streamLength.Value + 1;
                    startTextIndex = endStream;
                    currentIndex = endStream + 9;   // 9 = endstream text length
                    item.Value.streamContent = pdf.Slice(startStream, endStream);
                }

                if (pdf[currentIndex] == 'e' &&
                    pdf[currentIndex+1] == 'n' &&
                    pdf[currentIndex+2] == 'd' &&
                    pdf[currentIndex+3] == 'o' &&
                    pdf[currentIndex+4] == 'b' &&
                    pdf[currentIndex+5] == 'j')
                {
                    currentIndex += 6;
                    continueWorking = false;
                }
                else
                    currentIndex++;
            }

            item.Value.text.Add(GetString(pdf, startTextIndex, currentIndex));
        }

        public char[] separator = { (char)0, (char)9, (char)10, (char)12, (char)13, (char)32
                                    , (char)'[', (char)']', (char)'/' };

        private void ReadXRef(byte[] pdf, Dictionary<int, XrefItem> Xref)
        {
            uint trailerIndex = (uint)pdf.Length - 7; 

            while (pdf[trailerIndex] != 't' ||
                    pdf[trailerIndex + 1] != 'r' ||
                    pdf[trailerIndex + 2] != 'a' ||
                    pdf[trailerIndex + 3] != 'i' ||
                    pdf[trailerIndex + 4] != 'l' ||
                    pdf[trailerIndex + 5] != 'e' ||
                    pdf[trailerIndex + 6] != 'r')
            {
                trailerIndex--;

                if (trailerIndex == 0)
                    throw new Exception("PDF trailer not found");
            }

            string trailer = GetString(pdf, trailerIndex, (uint)pdf.Length);
            string[] parts = trailer.Split(separator, StringSplitOptions.RemoveEmptyEntries);

            // PDF ends:
            // startxref
            // 6424     <- parts[parts.length - 2] = XRefPosition
            // %%EOF    <- parts[parts.length - 1]
            uint XrefPos = Convert.ToUInt32(parts[parts.Length - 2]);

            // Xref = Cross reference table            
            string xref = GetString(pdf, XrefPos, trailerIndex);
            parts = xref.Split(separator, StringSplitOptions.RemoveEmptyEntries);

            if (parts[0] != "xref")
                throw new Exception("no xref table found");

            int XRefIndex = 1;
            while (XRefIndex < parts.Length)
                XRefIndex = ReadXrefTable(parts, XRefIndex, Xref);
        }

        private int ReadXrefTable(string[] parts, int xrefStartPointer, Dictionary<int, XrefItem> Xref)
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
                        // iteration is ignored, the new pdf will have 0 iteration
                    IsUsed =  parts[xrefStartPointer + 4 + count * 3] == "n" ? true : false
                };

                if (item.IsUsed)
                    Xref.Add(xrefIndex, item);

                count++;
            }

            return xrefStartPointer + 4 + count * 3;
        }

        private string GetString(byte[] pdf, uint startByte, uint endByte)
        {
            return System.Text.ASCIIEncoding.ASCII.GetString(pdf.Slice(startByte, endByte));
        }       
    }
}