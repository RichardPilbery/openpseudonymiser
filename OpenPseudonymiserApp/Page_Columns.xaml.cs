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
using System.Windows.Shapes;

namespace OpenPseudonymiser
{
    
    public partial class Page_Columns : Page
    {
        MainWindow_TNG parent = (MainWindow_TNG)Application.Current.Windows[1]; // because the (hidden) licence window is alway index 0
        
        public Page_Columns()
        {
            InitializeComponent();            
            parent.currentPage = OpenPseudonymiser.MainWindow_TNG.Page.ChooseColumns;
            parent.SetPageHeader(parent.currentPage);
            parent.EnableBack();
            parent.DisableNext();
            parent.DisableFinish();
            // binding doesnt seem to work from XAML, so doing it in code
            lvColumns.ItemsSource = parent.ColumnCollection;                        
            ValidatePage();            
        }

        /// <summary>
        /// Each checkbox on page two is wired up to this. If the user's selection is OK then the next button is enabled
        /// </summary>
        private void CheckBox_Click(object sender, RoutedEventArgs e)
        {
            ValidatePage();            
            // are the checkbox clicks setting anything in the observable collection here, or do we need to do it manually?

        }


        private void ValidatePage()
        {

            BuildNHSComboBox();

            int usedForDigest = 0;
            int usedForOutput = 0;

            foreach (OpenPseudonymiser.MainWindow_TNG.ColumnData columnData in parent.ColumnCollection)
            {
                if (columnData.UseForDigest)
                {
                    usedForDigest++;
                }
                if (columnData.UseForOutput)
                {
                    usedForOutput++;
                }
            }

            if (usedForDigest > 0 && usedForOutput > 0)
            {
                parent.EnableNext();
            }
            else
            {
                parent.DisableNext();
            }

             
            parent.processDateColumns.Clear();
            foreach (OpenPseudonymiser.MainWindow_TNG.ColumnData columnData in parent.ColumnCollection)
            {
                if (columnData.ProcessAsDate)
                {
                    parent.processDateColumns.Add(columnData.ColumnHeading);
                }
            }
        

        }

        private void BuildNHSComboBox()
        {            
            cmbNHSNumber.Items.Clear();

            ComboBoxItem item = new ComboBoxItem();
            item.Name = "item_0";
            item.Content = "My data has no NHS Numbers";
            if (parent.columnIndexSelectedAsNHSNumber == 0)
            {
                item.IsSelected = true;
            }
            cmbNHSNumber.Items.Add(item);            

            int selectedIndex = parent.columnIndexSelectedAsNHSNumber;
            int i = 0; 
            foreach (var column in parent.ColumnCollection)
            {                
                item = new ComboBoxItem();
                item.Name = "item" + i.ToString();
                item.Content = column.ColumnHeading;

                if (selectedIndex == i +1)     // +1 offset caters for the extra item we added above
                {
                    item.IsSelected = true;
                }
                cmbNHSNumber.Items.Add(item);
                i++;                
            }
        }

        private void cmbNHSNumber_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            parent.columnIndexSelectedAsNHSNumber = cmbNHSNumber.SelectedIndex;
            parent.performNHSNumberValidation = cmbNHSNumber.SelectedIndex >0;
            
        }
    }
}
