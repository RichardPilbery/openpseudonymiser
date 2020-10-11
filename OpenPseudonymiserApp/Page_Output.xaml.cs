using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;


namespace OpenPseudonymiser
{
    
    public partial class Page_Output : Page
    {
        MainWindow_TNG parent = (MainWindow_TNG)Application.Current.Windows[1]; // because the (hidden) licence window is alway index 0

        TimeSpan processingTimespan;
        public Page_Output()
        {
            InitializeComponent();            
            parent.currentPage = OpenPseudonymiser.MainWindow_TNG.Page.ChooseTarget;
            parent.SetPageHeader(parent.currentPage);
            parent.EnableBack();
            parent.DisableNext();
            parent.DisableFinish();            
            ValidatePage();            
        }        

        private void ValidatePage()
        {
            SetOutputLocation();
            parent.EnableFinish();
        }

        /// <summary>
        /// Pop open a select folder dialog so the user can select where they want the output files to go
        /// </summary>
        private void btnSelectOutputFile_Click(object sender, RoutedEventArgs e)
        {
            // select the folder to write the output files to:
            var folderDialog = new System.Windows.Forms.FolderBrowserDialog();
            folderDialog.Description = "Select folder to write output files to";
            // set the select folder to be the path of the input file
            folderDialog.SelectedPath = System.IO.Path.GetDirectoryName(parent.inputFile);

            folderDialog.ShowDialog();

            if (folderDialog.SelectedPath != "")
            {
                parent.outputFolder = folderDialog.SelectedPath;
                SetOutputLocation();            }
        }

        private void SetOutputLocation()
        {
            lblSelectedOutput.Content = parent.outputFolder;
            ShowReady();
        }

        private void ShowReady()
        {
            parent.EnableFinish();

            lblOutputDetails.Content = "Summary";
            lblOutputDetails.Content += Environment.NewLine;
            lblOutputDetails.Content += "----------";
            lblOutputDetails.Content += Environment.NewLine;
            lblOutputDetails.Content += "Input file: " +parent.inputFile;
            lblOutputDetails.Content += Environment.NewLine;

            lblStatus.Content = "OpenPseudononymiser is ready";
            lblStatus.Content += Environment.NewLine;
            lblStatus.Content += "Press 'Run' to start the process...";
        }



        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            parent.CancelWork();
        }

        /// <summary>
        /// The HyperLink for the location of the output files
        /// </summary>        
        private void OnUrlClick(object sender, RoutedEventArgs e)
        {
            var runExplorer = new System.Diagnostics.ProcessStartInfo();
            runExplorer.FileName = "explorer.exe";
            runExplorer.Arguments = parent.outputFolder;
            System.Diagnostics.Process.Start(runExplorer);

        }




        internal void SetProgressUIElementsVisibility(System.Windows.Visibility visibility)
        {
            progress.Visibility = visibility;
            btnCancel.Visibility = visibility;
            lblProgress.Visibility = visibility;
            outputLink.Visibility = visibility;        
        }


        public void UpdateProgressText(long recordsRead, long recordCount, long rows, long ValidNHS, long InvalidNHS, long missingNHS)
        {
            //lblProgress.Content = string.Format("{0} of {1} bytes", recordsRead, recordCount);
            lblProgress.Content = string.Format("{0} rows. ", rows);
            if (parent.performNHSNumberValidation)
            {
                lblProgress.Content += string.Format("Valid NHS:{0} Invalid: {1} Missing: {2}", ValidNHS, InvalidNHS, missingNHS);
            }
            int percentage = (int)(100 * ((double)recordsRead / (double)recordCount));
            progress.Value = percentage;

            // finished!
            if (recordsRead == recordCount)
            {
                lblOutputDetails.Content = "";
                // reset the progress stuff for the next run and hide it
                lblProgress.Content = "";
                progress.Value = 0;
                SetProgressUIElementsVisibility(System.Windows.Visibility.Hidden);

                // unlock the UI buttons
                LockUIElementsForProcessing(false);

                // grey the finish button
                parent.DisableFinish();

                // update the bold status
                processingTimespan = DateTime.Now.Subtract(parent.processingStartTime);
                lblStatus.Content = "OpenPseudonymiser process finished";
                lblStatus.Content += Environment.NewLine;
                lblStatus.Content += "Rows processed: " + rows;

                if (parent.performNHSNumberValidation)
                {
                    lblStatus.Content += Environment.NewLine;
                    lblStatus.Content += "Valid NHS Numbers: " + parent.validNHSNumCount;
                    lblStatus.Content += Environment.NewLine;
                    lblStatus.Content += "Invalid NHS Numbers: " + parent.inValidNHSNumCount;
                    lblStatus.Content += Environment.NewLine;
                    lblStatus.Content += "Missing NHS Numbers: " + parent.missingNHSNumCount;
                }

                lblStatus.Content += Environment.NewLine;
                lblStatus.Content += "Time taken: " + processingTimespan.Minutes + "m " + processingTimespan.Seconds + "s";

                outputLink.Visibility = System.Windows.Visibility.Visible;
            }
        }


        public void LockUIElementsForProcessing(bool locked)
        {
            if (locked)
            {
                parent.DisableBack();
                parent.DisableFinish();
                parent.DisableHelp();
            }
            else 
            {
                parent.EnableBack();
                parent.EnableFinish();
                parent.EnableHelp();
            }
            btnCancel.IsEnabled = locked;
            btnSelectOutput.IsEnabled = !locked;
            
        }
        public void CancelProgressText(long rows)
        {
            // reset the progress stuff for the next run and hide it
            lblProgress.Content = "";
            progress.Value = 0;
            SetProgressUIElementsVisibility(System.Windows.Visibility.Hidden);

            // unlock the UI buttons
            LockUIElementsForProcessing(false);

            // update the bold status
            lblOutputDetails.Content = "";
            processingTimespan = DateTime.Now.Subtract(parent.processingStartTime);
            lblStatus.Content = "OpenPseudonymiser process cancelled";
            lblStatus.Content += Environment.NewLine;
            lblStatus.Content += "Rows processed: " + rows;
            lblStatus.Content += Environment.NewLine;
            lblStatus.Content += "Time taken: " + processingTimespan.Seconds + " seconds";
        }

        public void ErrorProgressText(string errorText)
        {
            // reset the progress stuff for the next run and hide it
            lblProgress.Content = "";
            progress.Value = 0;
            SetProgressUIElementsVisibility(System.Windows.Visibility.Hidden);

            // unlock the UI buttons
            LockUIElementsForProcessing(false);

            lblOutputDetails.Content = errorText;

        }
    }
}
