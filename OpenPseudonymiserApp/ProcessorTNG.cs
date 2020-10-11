/*
    Copyright Julia Hippisley-Cox, University of Nottingham 2011 
  
    This file is part of OpenPseudonymiser.

    OpenPseudonymiser is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    OpenPseudonymiser is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with OpenPseudonymiser.  If not, see <http://www.gnu.org/licenses/>.
 
    The university has made reasonable enquiries regarding granted and pending patent
    applications in the general area of this technology and is not aware of any
    granted or pending patent in Europe which restricts the use of this
    software. In the event that the university receives a notice of perceived patent
    infringement, then the university will inform users that their use of the
    software may need to or, if appropriate, must cease in the appropriate
    territory. The university does not make any warranties in this respect and each
    user shall be solely responsible for ensuring that they do not infringe any
    third party patent.
 
 */



using System.Windows.Threading;
using System;
using System.Linq;
using System.ComponentModel;
using System.IO;
using System.Text;
using System.Windows;
using System.Collections.Generic;
using OpenPseudonymiser.CryptoLib;
using System.Text.RegularExpressions;
using RestSharp;
using Newtonsoft.Json;



namespace OpenPseudonymiser
{

    /*        
        All the stuff  to do with the processing of the file when Finish is pressed
    */

    public partial class MainWindow_TNG : Window
    {
        public BackgroundWorker worker;


        public delegate void UpdateProgressDelegate(long recordsRead, long recordCount, long rows, long ValidNHS, long InvalidNHS, long missingNHS);
        public delegate void ErrorProgressDelegate(string errorText);
        public delegate void CancelProgressDelegate(long rows);

        
        

        private void ProcessSingleFile(string filename)
        {
            rowsProcessed = 0;
            inputFileStreamLength = 0;
            jaggedLines = 0;
            //// get the salt from the text box, which may or may not be visible..            
            //_salt = txtSalt.Text;

            System.Windows.Threading.Dispatcher dispatcher = Dispatcher;

            worker = new BackgroundWorker();
            worker.WorkerSupportsCancellation = true;

            bool wasCancelled = false;            

            // anonymous delegate, this could be moved out for readability?
            worker.DoWork += delegate(object s, DoWorkEventArgs args)
            {
                if (MainWork(filename, columnIndexSelectedAsNHSNumber, dispatcher, ref wasCancelled))
                {
                    return;
                }
            };

            worker.RunWorkerCompleted += delegate(object s, RunWorkerCompletedEventArgs args)
            {
                string configWriteLine = "";
                if (!wasCancelled)
                {
                    configWriteLine = "Processing Finished At: " + DateTime.Now;
                    UpdateProgressDelegate update = new UpdateProgressDelegate(pageOut.UpdateProgressText);
                    dispatcher.BeginInvoke(update, inputFileStreamLength, inputFileStreamLength, rowsProcessed, validNHSNumCount, inValidNHSNumCount, missingNHSNumCount);
                }
                else
                {
                    configWriteLine = "Processing Cancelled At: " + DateTime.Now;
                }

                var writeConfigStream = new FileStream(outputRunLogFileName, FileMode.Append, FileAccess.Write);
                using (StreamWriter streamConfigWriter = new StreamWriter(writeConfigStream))
                {
                    streamConfigWriter.WriteLine(configWriteLine);
                    streamConfigWriter.WriteLine("Lines Processed: " + rowsProcessed);
                    streamConfigWriter.WriteLine("Jagged Lines: " + jaggedLines);
                    if (performNHSNumberValidation)
                    {
                        streamConfigWriter.WriteLine("Valid NHSNumbers (10 character number found and passed checksum) : " + validNHSNumCount);
                        streamConfigWriter.WriteLine("Invalid NHSNumbers (data was present but failed the checksum) : " + inValidNHSNumCount);
                        streamConfigWriter.WriteLine("Missing NHSNumbers (blank string or space) : " + missingNHSNumCount);
                    }
                }

                SignRunLogFile();
            };

            worker.RunWorkerAsync();
        }

        private bool MainWork(string filename, int indexOfNHSNumber, Dispatcher dispatcher, ref bool wasCancelled)
        {
            
            Crypto crypto = new Crypto();

            switch (saltMethod)
            {
                case SaltMethod.NotSelected:
                    break;
                case SaltMethod.SaltFile:
                    crypto.SetEncryptedSalt(File.ReadAllBytes(encryptedSaltFile));
                    break;
                case SaltMethod.KeyServer:
                    crypto.SetEncryptedSalt(GetEncryotedSaltBLOBFromKeyServer());
                    break;
                default:
                    break;
            }
            

            FileStream fileStream = new FileStream(filename, FileMode.Open, FileAccess.Read);
            FileStream writeStream;

            try
            {
                writeStream = new FileStream(outputFolder + "\\" + "OpenPseudonymised_" + outputFileNameOnly, FileMode.Create, FileAccess.Write);
            }
            catch (IOException)
            {
                ErrorProgressDelegate error = new ErrorProgressDelegate(pageOut.ErrorProgressText);
                dispatcher.BeginInvoke(error, "OpenPseudonymiser cannot create the Output file. Is the output file already open?");
                return false;
            }

            inputFileStreamLength = fileStream.Length;
            long totalCharsRead = 0;

            SortedList<string, int> inputFields;
            SortedList<int, string> outputFields;
            GetInputAndOutputFields(out inputFields, out outputFields);

            WriteRunLogFile(inputFields, outputFields);

            using (StreamWriter streamWriter = new StreamWriter(writeStream))
            {
                using (StreamReader streamReader = new StreamReader(fileStream))
                {
                    // 'read off' the first line, these are the input columns headings
                    string[] inputHeadings = streamReader.ReadLine().Split(',');

                    // write the first line as the selected column headings
                    string lineToWrite = "Digest,";
                    foreach (int key in outputFields.Keys) // keys in the output are indexes (opposite to input SortedList)
                    {
                        lineToWrite += outputFields[key] + ",";
                    }

                    // Do we want to do any NHSNumber checksum validation?
                    if (performNHSNumberValidation)
                    {
                        lineToWrite += "validNHS,";
                    }

                    // strip trailing comma
                    lineToWrite = lineToWrite.Substring(0, lineToWrite.Length - 1);
                    streamWriter.WriteLine(lineToWrite);

                    // We have no way of knowing how many lines there are in the file without opening the whole thing, which would kill the app.
                    // So we will use a fixed size buffer and manually look for lines inside it.
                    int _bufferSize = 16384;
                    char[] readBuffer = new char[_bufferSize];

                    StringBuilder workingBuffer = new StringBuilder(); // where we will store the left over stuff after we split the read buffer into lines

                    // read into the buffer
                    long charsRead = streamReader.Read(readBuffer, 0, _bufferSize);
                    totalCharsRead += charsRead;

                    while (charsRead > 0)
                    {
                        if (worker.CancellationPending)
                        {
                            wasCancelled = true;
                            //display cancellation message
                            CancelProgressDelegate canceller = new CancelProgressDelegate(pageOut.CancelProgressText);
                            dispatcher.BeginInvoke(canceller, rowsProcessed);
                            return true;
                        }

                        // put the stuff we just read from the file into our working buffer
                        workingBuffer.Append(readBuffer);

                        // slice the workingBuffer into lines
                        string[] linesArray = workingBuffer.ToString().Split('\n');

                        // process all the lines EXCEPT THE LAST ONE in the lines array (the last one is likely to be incomplete)
                        for (int i = 0; i < (linesArray.Length - 1); i++)
                        {
                            string line = linesArray[i];
                            // the line should have the same number of columns as the ColumnCollection, if not then up the jagged lines count, and skip processing
                            string[] lineColumns = line.Split(',');


                            // if we get a jagged result here (i.e. length of columns does not match the headers) then try a split using quote delimited data
                            if (lineColumns.Length != ColumnCollection.Count)
                            {
                                // thanks to http://stackoverflow.com/questions/3776458/split-a-comma-separated-string-with-both-quoted-and-unquoted-strings
                                // for this bit of regex
                                Regex csvSplit = new Regex("(?:^|,)(\"(?:[^\"]+|\"\")*\"|[^,]*)", RegexOptions.Compiled);

                                int jag = 0;
                                lineColumns = new string[csvSplit.Matches(line).Count];
                                foreach (Match match in csvSplit.Matches(line))
                                {
                                    lineColumns[jag] = match.Value.TrimStart(',');
                                    jag++;
                                }
                            }

                            // if we're still jagged then we can't do much other than skip processing this line and increment the jagged counter
                            if (lineColumns.Length != ColumnCollection.Count)
                            {
                                jaggedLines++;
                            }
                            else
                            {
                                // get the columns for crypting using the inputFields, since this is a sorted list we always get the indexes from aphabetically ordered keys
                               // SortedList<string, string> hashNameValueCollection = new SortedList<string, string>();

                                // first column is the digest
                                /**   foreach (string key in inputFields.Keys)
                                   {
                                       string theData = lineColumns[inputFields[key]];

                                       // we always process the one they selected as NHSNumber ..
                                       if (performNHSNumberValidation)
                                       {
                                           string nhskey = inputHeadings[indexOfNHSNumber - 1];
                                           if (nhskey == key)
                                           {
                                               theData = crypto.ProcessNHSNumber(theData);
                                           }
                                       }
                                       hashNameValueCollection.Add(key, theData);
                                   }
                                   string digest = crypto.GetDigest(hashNameValueCollection);
                                   string validNHS = "";
                                   lineToWrite = digest + ",";  **/

                                // output the rest of the columns in the output list

                                int firstLine = 1;


                                foreach (int key in outputFields.Keys) // keys in the output are indexes (opposite to input SortedList)
                                {
                                    // Look for column heading that is a date..
                                    if (processDateColumns.Contains(outputFields[key]))
                                    {
                                        lineToWrite += crypto.RoundDownDate(lineColumns[key]) + ",";
                                    }
                                    else
                                    {
                                        int flag = 0;
                                        foreach (string key2 in inputFields.Keys)
                                        {
                                            System.Diagnostics.Debug.WriteLine(key2);
                                            System.Diagnostics.Debug.WriteLine(outputFields[key]);
                                            if (key2 == outputFields[key])
                                            {
                                                // This needs hashing
                                                flag = 1;
                                                SortedList<string, string> hashNameValueCollection = new SortedList<string, string>();
                                                //string theData = lineColumns[outputFields[key]];
                                                hashNameValueCollection.Add(key2, lineColumns[inputFields[key2]]);
                                                string digest = crypto.GetDigest(hashNameValueCollection);
                                                if (firstLine == 1)
                                                {
                                                    lineToWrite = digest + ",";
                                                    firstLine = 0;
                                                }
                                                else
                                                {
                                                    lineToWrite += digest + ",";
                                                }

                                            }
                                        }
                                        if (flag == 0)
                                        {
                                            if (firstLine == 1)
                                            {
                                                lineToWrite = lineColumns[key] + ",";
                                            }
                                            else
                                            {
                                                lineToWrite += lineColumns[key] + ",";
                                            }

                                        }
                                        flag = 0;
                                    }
                                }

                                // last column is the NHS Validation (if requested)                                
                                /** if (performNHSNumberValidation)
                                {
                                    // find the NHSNumber in the list of input columns and validate it
                                    string key = inputHeadings[indexOfNHSNumber - 1];
                                    {


                                        string trimmedNHSNumber = lineColumns[indexOfNHSNumber - 1].Trim();
                                        // trimmed data is length < 1 so we call this missing NHS Number
                                        if (trimmedNHSNumber.Length < 1)
                                        {
                                            validNHS = "-1";
                                            missingNHSNumCount++;
                                        }
                                        else
                                        {
                                            // we have data for the NHS field, is it valid?           
                                            string cleanedNHSNumber = crypto.ProcessNHSNumber(trimmedNHSNumber);
                                            if (NHSNumberValidator.IsValidNHSNumber(cleanedNHSNumber))
                                            {
                                                validNHS = "1";
                                                validNHSNumCount++;
                                            }
                                            else
                                            {
                                                validNHS = "0";
                                                inValidNHSNumCount++;
                                            }
                                        }
                                        lineToWrite += validNHS + ",";

                                    }

                                } **/

                                // we're done writing the output line now. Strip trailing comma.
                                lineToWrite = lineToWrite.Substring(0, lineToWrite.Length - 1);
                                // some files have a double line break at the end of the lines, remove this.
                                lineToWrite = lineToWrite.Replace(Environment.NewLine, "").Replace("\r\n", "").Replace("\n", "").Replace("\r", "");
                                streamWriter.WriteLine(lineToWrite);
                            }
                        }
                        rowsProcessed += linesArray.Length - 1;

                        // set the working buffer to be the last line, so the next pass can concatonate
                        string lastLine = linesArray[linesArray.Length - 1];
                        workingBuffer = new StringBuilder(lastLine);

                        UpdateProgressDelegate update = new UpdateProgressDelegate(pageOut.UpdateProgressText);
                        dispatcher.BeginInvoke(update, totalCharsRead, inputFileStreamLength, rowsProcessed, validNHSNumCount, inValidNHSNumCount, missingNHSNumCount);

                        // empty the readbuffer, or the last read will only partially fill it, and we'll have some old data in the tail
                        readBuffer = new char[_bufferSize];
                        // read the next lot                        
                        charsRead = streamReader.Read(readBuffer, 0, _bufferSize);
                        totalCharsRead += charsRead;
                    }
                }
            }
            return false;
        }

        public class EncryptedSaltDTO
        {
            public int Id { get; set; }
            public string FileName { get; set; }
            public byte[] SaltBLOB { get; set; }
            public DateTime CreatedAt { get; set; }
            public DateTime DeletedAt { get; set; }
            public string Comments { get; set; }
            public int OwnerUserId { get; set; }
            public string OwnerUserDisplayName { get; set; }
        }

        private byte[] GetEncryotedSaltBLOBFromKeyServer()
        {
            // try and connect to the KeyServer to get the public key            
            var client = new RestClient(this.SelectedKeyServerAddress);

            // call the API and get a list  of files they own            
            client.Authenticator = new HttpBasicAuthenticator(this.SelectedKeyServerUserName, this.SelectedKeyServerPassword);
            var request = new RestRequest("Salt", Method.GET);

            request.AddParameter("encryptedSaltId", this.SelectedKeyServerSaltId);
            IRestResponse<EncryptedSaltDTO> response = client.Execute<EncryptedSaltDTO>(request);

            if (response.StatusCode == System.Net.HttpStatusCode.OK)
            {
                EncryptedSaltDTO dto;
                // dto = response.Data; // this throws a convertsion exception, so we use the Newtonsoft libs to deserialize
                using (Stream ms = new MemoryStream(Encoding.UTF8.GetBytes(response.Content)))
                {
                    dto = JsonConvert.DeserializeObject<EncryptedSaltDTO>(new StreamReader(ms).ReadToEnd());
                }
                return dto.SaltBLOB;
            }
            
            return new byte[0];

        }

        private void GetInputAndOutputFields(out SortedList<string, int> inputFields, out SortedList<int, string> outputFields)
        {
            // determine which columns to use for Digest, and which ones to use for Output. Store a list of indexes in the arrays
            inputFields = new SortedList<string, int>();    // we want this to sort on Name every time
            outputFields = new SortedList<int, string>();   // we want this to sort on index, to presenve the format of the original file

            int indexInColumnCollection = 0;
            
            // TODO setting file in version 2
            //if (_usingPreConfigSettings || _usingSettingFile)
            //{
            //    // get columns from pre-config arrays (which are also set by selecting a settings file)
            //    foreach (ColumnData columnData in ColumnCollection)
            //    {
            //        if (_preConfigInputColumns.ToList<string>().Contains(columnData.ColumnHeading))
            //        {
            //            inputFields.Add(columnData.ColumnHeading, indexInColumnCollection);
            //        }
            //        if (_preConfigOutputColumns.ToList<string>().Contains(columnData.ColumnHeading))
            //        {
            //            outputFields.Add(indexInColumnCollection, columnData.ColumnHeading);
            //        }
            //        indexInColumnCollection++;
            //    }
            //}
            //else
            //{
                // get columns from screen selection
                foreach (ColumnData columnData in ColumnCollection)
                {
                    if (columnData.UseForDigest)
                    {
                        inputFields.Add(columnData.ColumnHeading, indexInColumnCollection);
                    }
                    if (columnData.UseForOutput)
                    {
                        outputFields.Add(indexInColumnCollection, columnData.ColumnHeading);
                    }
                    indexInColumnCollection++;
                //}

            }
        }

        private void WriteSettingsFile(SortedList<string, int> inputFields, SortedList<int, string> outputFields, string settingsFile)
        {

            var writeConfigStream = new FileStream(settingsFile, FileMode.Create, FileAccess.Write);
            using (StreamWriter streamConfigWriter = new StreamWriter(writeConfigStream))
            {


                string digestCols = "";
                foreach (string key in inputFields.Keys)
                {
                    digestCols += key + ",";
                }
                if (digestCols.Length > 0)
                {
                    digestCols = digestCols.Substring(0, digestCols.Length - 1);
                }
                streamConfigWriter.WriteLine("digest:" + digestCols);



                string outputCols = "";
                foreach (string key in outputFields.Values)
                {
                    outputCols += key + ",";
                }
                if (outputCols.Length > 0)
                {
                    outputCols = outputCols.Substring(0, outputCols.Length - 1);
                }
                streamConfigWriter.WriteLine("output:" + outputCols);


                streamConfigWriter.WriteLine("processAsDate:" + string.Join(",", processDateColumns.ToArray()));

            }
        }


        private void WriteRunLogFile(SortedList<string, int> inputFields, SortedList<int, string> outputFields)
        {
            var writeConfigStream = new FileStream(outputRunLogFileName, FileMode.Create, FileAccess.Write);
            using (StreamWriter streamConfigWriter = new StreamWriter(writeConfigStream))
            {
                streamConfigWriter.WriteLine("OpenPseudonymiser - RunLog File");
                streamConfigWriter.WriteLine("----------------------------------------------------------");
                streamConfigWriter.WriteLine("Run on: " + DateTime.Now);
                streamConfigWriter.WriteLine("Input File: " + inputFile);
                streamConfigWriter.WriteLine("Input File Lengh " + inputFileStreamLength);
                streamConfigWriter.WriteLine("Output folder: " + outputFolder);
                streamConfigWriter.WriteLine("----------------------------------------------------------");
                streamConfigWriter.WriteLine("Digest Column(s) Selected:");
                foreach (string key in inputFields.Keys)
                {
                    streamConfigWriter.WriteLine(key);
                }
                streamConfigWriter.WriteLine("----------------------------------------------------------");
                streamConfigWriter.WriteLine("Output Column(s) Used:");
                foreach (string value in outputFields.Values)
                {
                    streamConfigWriter.WriteLine(value);
                }
                streamConfigWriter.WriteLine("----------------------------------------------------------");


                string saltDetails = "";
                if (saltMethod == SaltMethod.KeyServer)
                {
                    saltDetails = "KeyServer :" + SelectedKeyServerAddress + " User: " + SelectedKeyServerUserName + " SaltId: " + SelectedKeyServerSaltId;
                }
                else
                {
                    saltDetails = "Local File :" + encryptedSaltFile;
                }

                streamConfigWriter.WriteLine("Salt details: " + saltDetails);                

                streamConfigWriter.WriteLine("----------------------------------------------------------");
                streamConfigWriter.WriteLine("Processing Start: " + processingStartTime);
            }
        }

        // reads the contents of the file and appends an MD5
        private void SignRunLogFile()
        {
            var readConfigStream = new FileStream(outputRunLogFileName, FileMode.Open, FileAccess.Read);

            string hashThis = "";
            using (StreamReader streamConfigReader = new StreamReader(readConfigStream))
            {
                hashThis = streamConfigReader.ReadToEnd();
            }

            var writeConfigStream = new FileStream(outputRunLogFileName, FileMode.Append, FileAccess.Write);
            using (StreamWriter streamConfigWriter = new StreamWriter(writeConfigStream))
            {
                streamConfigWriter.WriteLine("----------------------------------------------------------");
                streamConfigWriter.WriteLine("OpenPseudonymiser Config Security: " + CryptHelper.md5encrypt(hashThis + "seal"));
            }
        }




        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            worker.CancelAsync();
            pageOut.btnCancel.IsEnabled = false;
        }








        
    }
}
