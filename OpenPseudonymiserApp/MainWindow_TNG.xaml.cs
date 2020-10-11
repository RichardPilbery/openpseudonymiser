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

using System;
using System.IO;
using System.Windows;
using System.Collections.Generic;
using System.Windows.Documents;
using System.Diagnostics;
using System.Collections.ObjectModel;


namespace OpenPseudonymiser
{    
    
    /// <summary>
    /// This partial contains only UI hookups
    /// Logic is split into the different partial classes. 
    /// Start at Main.cs if you are unsure
    /// </summary>
    public partial class MainWindow_TNG : Window
    {

        public enum SaltMethod { NotSelected, SaltFile, KeyServer };
        public enum Page { ChooseSource, ChooseSalt, ChooseColumns, ChooseTarget };

        public string inputFile; // selected input file and path
        public bool saltFileIsOK; // can open the salt file
        public string encryptedSaltFile; // selected salt file

        public string outputFolder; // selected output path        
        public int columnIndexSelectedAsNHSNumber; // if they specify that a column is an NHS number then we store the index here and will do NHS number validation on it
        public bool inputFileIsOK { get; set; }

        public bool performNHSNumberValidation{ get; set; }

        // list of cols we want to process as dates
        public List<string> processDateColumns = new List<string>();


        public string SelectedKeyServerAddress { get; set; }
        public string SelectedKeyServerUserName { get; set; }
        public string SelectedKeyServerPassword { get; set; }

        #region processor vars, these could go in a class
        // processor variables, these could go in a class?
        public long rowsProcessed = 0;
        public long inputFileStreamLength = 0;
        public long jaggedLines = 0;

        public long validNHSNumCount = 0;
        public long inValidNHSNumCount = 0;
        public long missingNHSNumCount = 0;

        public DateTime processingStartTime;
        

        string outputFileNameOnly;
        string outputRunLogFileName; // a file that gets written when the processing is done (called the RunLog)
        string runLogFileHash;

        public Page_Output pageOut;

        #endregion


        public bool KeyServerIsOK { get; set; }

        public int SelectedKeyServerSaltId { get; set; }

        public class ColumnData
        {
            public bool UseForDigest { get; set; }
            public bool UseForOutput { get; set; }
            public bool ProcessAsDate { get; set; }
            public string ColumnHeading { get; set; }
        }
        // Column headings we find in the input file. Bound on page 2

        public ObservableCollection<ColumnData> ColumnCollection { get; set; }

        public SaltMethod saltMethod = SaltMethod.NotSelected;
        public Page currentPage;

        public MainWindow_TNG()
        {
            InitializeComponent();            
            btnNext.IsEnabled = false;
            _mainFrame.Navigate(new Page_Input());

            currentPage = Page.ChooseSource;
            this.Closed += (sender, e) => this.Dispatcher.InvokeShutdown();
        }
        
      

        private void btnHelp_Click(object sender, RoutedEventArgs e)
        {
            var h = new Help(currentPage);
            h.ShowDialog();            
        }

        private void btnBack_Click(object sender, RoutedEventArgs e)
        {
            switch (currentPage)
            {                
                case Page.ChooseSalt:
                    _mainFrame.Navigate(new Page_Input());
                    break;
                case Page.ChooseColumns:
                    _mainFrame.Navigate(new Page_Salt());
                    break;
                case Page.ChooseTarget:
                    _mainFrame.Navigate(new Page_Columns());
                    break;
                default:
                    break;
            }            
        }

        private void btnNext_Click(object sender, RoutedEventArgs e)
        {
            switch (currentPage)
            {
                case Page.ChooseSource:
                    _mainFrame.Navigate(new Page_Salt());
                    break;                    
                case Page.ChooseSalt:
                    _mainFrame.Navigate(new Page_Columns());
                    break;
                case Page.ChooseColumns:
                    _mainFrame.Navigate(new Page_Output());
                    break;                
                default:
                    break;
            }            
        }


        public void SetPageHeader(Page page)
        {
            switch (page)
            {
                case Page.ChooseSource:
                    lblHeader.Content = "Choose a data file";
                    lblSubHeader.Content = "Specify the CSV (Comma Separated Values) data file";
                    break;
                case Page.ChooseSalt:
                    lblHeader.Content = "Select salt";
                    lblSubHeader.Content = "Choose a salt KeyServer or local salt file";
                    break;
                case Page.ChooseColumns:
                    lblHeader.Content = "Select columns";
                    lblSubHeader.Content = "Specify which columns to use for the Digest, and which ones to output";
                    break;
                case Page.ChooseTarget:
                    lblHeader.Content = "Summary & destination folder";
                    lblSubHeader.Content = "Change the destination folder if required and review the summary";
                    break;                
                default:
                    break;
            }
        }










        private void btnFinish_Click(object sender, RoutedEventArgs e)
        {
            // always flip them to page 3, since we may press the Finish button on page 1 or 2             
            pageOut = new Page_Output();
            _mainFrame.Navigate(pageOut);
            SetPageHeader(currentPage);
            StartWork();            
        }




        public void StartWork()
        {            
            pageOut.lblStatus.Content = "OpenPseudonymiser is running....";
            processingStartTime = DateTime.Now;
            pageOut.LockUIElementsForProcessing(true);
            pageOut.SetProgressUIElementsVisibility(System.Windows.Visibility.Visible);

            outputFileNameOnly = inputFile.Substring(inputFile.LastIndexOf('\\') + 1, inputFile.Length - inputFile.LastIndexOf('\\') - 1);
            outputRunLogFileName = outputFolder + "\\" + outputFileNameOnly + ".OpenPseudonymiserRunLog";
         
            ProcessSingleFile(inputFile);
        }


        


        public void CancelWork()
        {
            //parent.worker.CancelAsync();
            //btnCancel.IsEnabled = false;
        }




        internal void EnableNext()
        {
            btnNext.IsEnabled = true;
        }
        internal void DisableNext()
        {
            btnNext.IsEnabled = false;
        }
        internal void DisableBack()
        {
            btnBack.IsEnabled = false;
        }
        internal void EnableBack()
        {
            btnBack.IsEnabled = true;
        }
        internal void EnableFinish()
        {            
            btnFinish.IsEnabled = true;
        }        
        internal void DisableFinish()
        {
            btnFinish.IsEnabled = false;
        }
        internal void EnableHelp()
        {
            btnHelp.IsEnabled = true;
        }
        internal void DisableHelp()
        {
            btnHelp.IsEnabled = false;
        }
    }
}
