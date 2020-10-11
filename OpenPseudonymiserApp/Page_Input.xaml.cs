using System;
using System.Collections.Generic;
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
using System.Windows.Shapes;

namespace OpenPseudonymiser
{    
    public partial class Page_Input : Page
    {
        MainWindow_TNG parent = (MainWindow_TNG)Application.Current.Windows[1]; // because the (hidden) licence window is alway index 0

        public Page_Input()
        {
            parent.currentPage = OpenPseudonymiser.MainWindow_TNG.Page.ChooseSource;
            parent.SetPageHeader(parent.currentPage);
            InitializeComponent();
            parent.DisableBack();
            parent.DisableNext();
            parent.DisableFinish();
            ValidatePage();
        }

        private void btnSelectFile_Click(object sender, RoutedEventArgs e)
        {
            // Configure open file dialog box
            Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();
            dlg.FileName = ""; // Default file name
            dlg.DefaultExt = ".csv"; // Default file extension
            dlg.Filter = "Comma Separated Data Files(.csv)|*.csv"; // Filter files by extension

            // Show open file dialog box
            Nullable<bool> result = dlg.ShowDialog();

            // Process open file dialog box results
            if (result == true) // they clicked OK, rather than cancel
            {
                string filename = dlg.FileName;

                // See if we can use this document
                if (CanUseThisInputFile(filename))
                {
                    BuildColumns(filename);
                    SetOutputLocationToSameFolderAsInputLocation(filename);                    

                    parent.EnableNext();
                    parent.inputFileIsOK = true;                    
                }
            }
            //DetermineIfPageOneIsCorrectlyFilledIn();
        }



        /// <summary>
        /// Read the columns from the selected file, build our observable data, which is bound to the control on page 2
        /// Also populate the drop down list on page two used to select an NHS number 
        /// </summary>        
        public void BuildColumns(string filename)
        {

            FileStream fileStream = new FileStream(filename, FileMode.Open, FileAccess.Read);
            parent.ColumnCollection = new System.Collections.ObjectModel.ObservableCollection<MainWindow_TNG.ColumnData>();
            parent.ColumnCollection.Clear();

            //cmbNHSNumber.Items.Clear();

            ComboBoxItem item = new ComboBoxItem();
            item.Name = "item_0";
            item.Content = "My data has no NHS Numbers";
            //cmbNHSNumber.Items.Add(item);
            //cmbNHSNumber.SelectedIndex = 0;

            using (StreamReader streamReader = new StreamReader(fileStream))
            {
                string firstLine = streamReader.ReadLine();
                string[] cols = firstLine.Split(',');
                int i = 0;
                foreach (string colname in cols)
                {
                    i++;
                    parent.ColumnCollection.Add(new OpenPseudonymiser.MainWindow_TNG.ColumnData
                    {
                        UseForDigest = false,
                        UseForOutput = true,
                        ColumnHeading = colname,
                    });

                    item = new ComboBoxItem();
                    item.Name = "item" + i.ToString();
                    item.Content = colname;
                    //cmbNHSNumber.Items.Add(item);

                }

            }
        }


        private void SetOutputLocationToSameFolderAsInputLocation(string filename)
        {
            string path = System.IO.Path.GetDirectoryName(filename);
            parent.outputFolder = path;            
        }



        internal string GetInputFileName()
        {
            return parent.inputFile;
        }


        private void ValidatePage()
        {
            if (parent.inputFile != null)
            {
                if (CanUseThisInputFile(parent.inputFile))
                {
                    parent.EnableNext();
                }
            }
        }

        /// <summary>
        /// Call the basic checks on the input file and display messages about whether the file is suitable       
        /// </summary>        
        /// <returns>True if the file is OK to use</returns>
        private bool CanUseThisInputFile(string filename)
        {
            parent.inputFile = filename;
            lblSelectedFile.Content = filename;

            if (!CanOpenFile(filename))
            {
                lblFileDetails.Content = "File opened ....................... X";
                lblFileDetails.Content += Environment.NewLine;
                lblFileDetails.Content += "(is it already open in Excel?)";
                return false;
            }
            lblFileDetails.Content = "File opened ....................... √";
            lblFileDetails.Content += Environment.NewLine;

            int CSVCount = GetFileCSVCount(filename);
            if (CSVCount == 0)
            {
                lblFileDetails.Content += "Comma separated values detected .. X";
                lblFileDetails.Content += Environment.NewLine;
                lblFileDetails.Content += "(cannot detect commas in file)";
                return false;
            }
            lblFileDetails.Content += "Comma separated values detected .. √";
            lblFileDetails.Content += Environment.NewLine;

            if (!First100RowsConform(CSVCount, filename))
            {
                lblFileDetails.Content += "First 100 rows conform ........... X";
                lblFileDetails.Content += Environment.NewLine;
                lblFileDetails.Content += "(Jagged values in first 100 rows)";
                return false;
            }
            lblFileDetails.Content += "First 100 rows conform ........... √";
            lblFileDetails.Content += Environment.NewLine;

            lblStatusInput.Content = "File is OK";

            lblSettingFile.Content = "";

            return true;
        }


        /// <summary>
        /// Returns true if the first 100 lines in the file all have the same column count as the first column
        /// </summary>        
        private bool First100RowsConform(int CSVCount, string filename)
        {
            var fs = new FileStream(filename, FileMode.Open, FileAccess.Read);
            int i = 0;
            using (StreamReader sr = new StreamReader(fs))
            {
                string line = sr.ReadLine();
                while (line != null && line != "" && i < 100)
                {
                    i++;
                    if (line.Split(',').Length != CSVCount)
                    {
                        return false;
                    }
                    line = sr.ReadLine();
                }
            }
            return true;
        }


        /// <summary>
        /// get the number of columns in the first row on this tile
        /// </summary>        
        private int GetFileCSVCount(string filename)
        {
            var fs = new FileStream(filename, FileMode.Open, FileAccess.Read);
            using (StreamReader sr = new StreamReader(fs))
            {
                return sr.ReadLine().Split(',').Length;
            }
        }

        /// <summary>
        /// Does the file exist, can we open it?
        /// </summary>
        /// <param name="filename"></param>        
        private bool CanOpenFile(string filename)
        {
            bool ret = false;
            try
            {
                var fs = new FileStream(filename, FileMode.Open, FileAccess.Read);
                fs = null;
                ret = true;
            }
            catch
            {
                // sometimes get a filesystem error here if we try and open a file to read that is already opened by Excel
                ret = false;
            }
            return ret;
        }

    }
}
