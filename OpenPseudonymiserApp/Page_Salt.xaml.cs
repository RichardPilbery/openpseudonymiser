using System.IO.Packaging;
using System.Windows.Threading;
using RestSharp;
using System;
using System.Collections.Generic;
using System.ComponentModel;
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
using Newtonsoft.Json;


namespace OpenPseudonymiser
{
    
    public partial class Page_Salt : Page
    {
        MainWindow_TNG parent = (MainWindow_TNG)Application.Current.Windows[1]; // because the (hidden) licence window is alway index 0
        private string addNewKeyserverText = "Add New KeyServer...";

        public Page_Salt()
        {
            InitializeComponent();
            cvsKeyServerDetails.Visibility = Visibility.Hidden;
            btnLogin.Visibility = Visibility.Visible;
            //lblSelectedSaltFile.Visibility = Visibility.Hidden;
            cmbSaltFilesFromKeyServer.Visibility = Visibility.Hidden;

            cvsKeyServer.Visibility = System.Windows.Visibility.Hidden;
            parent.currentPage = OpenPseudonymiser.MainWindow_TNG.Page.ChooseSalt;
            parent.SetPageHeader(parent.currentPage);
            vbProgress.Visibility = System.Windows.Visibility.Hidden;
            lblProgress.Visibility = System.Windows.Visibility.Hidden;
            parent.EnableBack();
            parent.DisableNext();
            parent.DisableFinish();
            ValidatePage();            
        }

        private void ValidatePage()
        {
            if (parent.saltMethod != MainWindow_TNG.SaltMethod.NotSelected)
            {
                switch (parent.saltMethod)
                {
                    case MainWindow_TNG.SaltMethod.SaltFile:
                        cmbSaltType.SelectedIndex = 1;
                        
                        if (CanOpenFile(parent.encryptedSaltFile))
                        {
                            parent.saltFileIsOK = true;
                            lblSelectedSaltFile.Content = parent.encryptedSaltFile;
                            parent.EnableNext();
                        }
                        else
                        {
                            parent.saltFileIsOK = false;
                            parent.DisableNext();
                        }
                        break;

                    case MainWindow_TNG.SaltMethod.KeyServer:
                        //cmbSaltType.SelectedIndex = 0;
                        if (parent.KeyServerIsOK)
                        { 
                            parent.EnableNext();
                        }
                        else
                        {
                            parent.DisableNext();
                        }
                        break;

                    default:
                        break;
                }
            }

            BuildKeyServerComboBox();
        }

        private void BuildKeyServerComboBox()
        {
            cmbKeyServer.Items.Clear();

            if (KeyServerSettingsFileExists())
            {
                string line;
                System.IO.StreamReader file = new System.IO.StreamReader(GetKeyserverFileName());
                while ((line = file.ReadLine()) != null)
                {
                    ComboBoxItem item = new ComboBoxItem();
                    var items = line.Split('|');
                    
                    string keyServer="";
                    string userName="";
                    if (txtKeyServer != null) keyServer= items[0];
                    if (items.Length > 1) userName= items[1];

                    item.Content = keyServer + " User: " + userName;
                    if (parent.SelectedKeyServerAddress == keyServer && parent.SelectedKeyServerUserName == userName)
                    {
                        item.IsSelected = true;
                    }
                    cmbKeyServer.Items.Add(item);
                }
                file.Close();
            }
            ComboBoxItem newitem = new ComboBoxItem();            
            newitem.Content = addNewKeyserverText;
            cmbKeyServer.Items.Add(newitem);


        }




        private void btnSelectSaltFile_Click(object sender, RoutedEventArgs e)
        {
            // Configure open file dialog box
            Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();
            dlg.FileName = ""; // Default file name
            dlg.DefaultExt = ".EncryptedSalt"; // Default file extension
            dlg.Filter = "Encrypted Salt Files(.EncryptedSalt)|*.EncryptedSalt"; // Filter files by extension

            // Show open file dialog box
            Nullable<bool> result = dlg.ShowDialog();

            // Process open file dialog box results
            if (result == true) // they clicked OK, rather than cancel
            {
                string filename = dlg.FileName;
                parent.encryptedSaltFile = filename;
                ValidatePage();
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

        /// <summary>
        /// Method "KeyServer" selected
        /// </summary>        
        private void cbi1_Selected(object sender, RoutedEventArgs e)
        {
            parent.saltMethod = MainWindow_TNG.SaltMethod.KeyServer;            
            cvsSaltFile.Visibility = System.Windows.Visibility.Hidden;
            cvsKeyServer.Visibility = System.Windows.Visibility.Visible;
        }

        /// <summary>
        /// Method "Salt File" selected
        /// </summary>        
        private void cbi2_Selected(object sender, RoutedEventArgs e)
        {
            parent.saltMethod = MainWindow_TNG.SaltMethod.SaltFile ;            
            cvsSaltFile.Visibility = System.Windows.Visibility.Visible;
            cvsKeyServer.Visibility = System.Windows.Visibility.Hidden;
        }

     
        private void btnTest_Click(object sender, RoutedEventArgs e)
        {
            btnLogin.Visibility = Visibility.Hidden;
            lblProgress.Visibility = System.Windows.Visibility.Visible;
            lblProgress.Content = "Connecting..";
            vbProgress.Visibility = System.Windows.Visibility.Visible;

            parent.SelectedKeyServerAddress = txtKeyServer.Text;
            parent.SelectedKeyServerUserName = txtUsername.Text;
            parent.SelectedKeyServerPassword=  txtPassword.Password;
            TestKeyserverConnection(parent.SelectedKeyServerAddress, parent.SelectedKeyServerUserName, parent.SelectedKeyServerPassword);            
        }




        public void TestKeyserverConnection(string address, string username, string password)
        {            
            System.Windows.Threading.Dispatcher dispatcher = Dispatcher;

            var worker = new BackgroundWorker();
            worker.WorkerSupportsCancellation = false;

            string response = "";

            worker.DoWork += delegate(object s, DoWorkEventArgs args)
            {
                response = MainWork(dispatcher, address, username, password);                
            };
            

            worker.RunWorkerCompleted += delegate(object s, RunWorkerCompletedEventArgs args)
            {
                updateLoginResponse(response);
            };

            worker.RunWorkerAsync();
        }

        public void GetSaltListFromServer(string address, string username, string password)
        {
            System.Windows.Threading.Dispatcher dispatcher = Dispatcher;

            var worker = new BackgroundWorker();
            worker.WorkerSupportsCancellation = false;

            List<EncryptedSaltDTO> response = new List<EncryptedSaltDTO>();            

            worker.DoWork += delegate(object s, DoWorkEventArgs args)
            {
                response = SaltCollectMainWork(dispatcher, address, username, password);
            };


            worker.RunWorkerCompleted += delegate(object s, RunWorkerCompletedEventArgs args)
            {
                updateSaltCollectResponse(response);
            };

            worker.RunWorkerAsync();
        }

        private void updateSaltCollectResponse(List<EncryptedSaltDTO> response)
        {
            lblKeyserverSelectSaltLabel.Visibility = Visibility.Visible;
            if (response.Count>0)
            {
                lblResult.Content = "Connected.";                
                // build combo box here...
                cmbSaltFilesFromKeyServer.Items.Clear();
                foreach(var dto in response)
                {                    
                    ComboBoxItem item = new ComboBoxItem();
                    item.Tag = dto.Id;
                    item.Content = dto.FileName;
                    cmbSaltFilesFromKeyServer.Items.Add(item);                    
                }
            }
            else
            {
                lblResult.Content = "No salts available.";                
            }
            lblProgress.Visibility = System.Windows.Visibility.Hidden;
            vbProgress.Visibility = System.Windows.Visibility.Hidden;   
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

        private List<EncryptedSaltDTO> SaltCollectMainWork(System.Windows.Threading.Dispatcher dispatcher, string address, string username, string password)
        {
            // try and connect to the KeyServer to get the public key            
            var client = new RestClient(address);            


            // call the API and get a list  of files they own            
            client.Authenticator = new HttpBasicAuthenticator(username, password);
            var request = new RestRequest("Salt", Method.GET);

            List<EncryptedSaltDTO> myDtos = new List<EncryptedSaltDTO>();
            List<EncryptedSaltDTO> shareddtos = new List<EncryptedSaltDTO>();

            IRestResponse<List<EncryptedSaltDTO>> response = client.Execute<List<EncryptedSaltDTO>>(request);
            // the restclient throws an exeception when trying to automatically deserialise our object, so we use Newtonsoft instead:                                
            using (Stream ms = new MemoryStream(Encoding.UTF8.GetBytes(response.Content)))
            {
                myDtos = JsonConvert.DeserializeObject<List<EncryptedSaltDTO>>(new StreamReader(ms).ReadToEnd());
            }

            if (response.StatusCode == System.Net.HttpStatusCode.OK)
            {
                // OK bind
            }
            else
            {
                //return errro;
            }



            // call the API and get a list  of files they can acess via shares                        
            request = new RestRequest("ShareSalt", Method.GET);

            response = client.Execute<List<EncryptedSaltDTO>>(request);
            // the restclient throws an exeception when trying to automatically deserialise our object, so we use Newtonsoft instead:                                
            using (Stream ms = new MemoryStream(Encoding.UTF8.GetBytes(response.Content)))
            {
                shareddtos = JsonConvert.DeserializeObject<List<EncryptedSaltDTO>>(new StreamReader(ms).ReadToEnd());
            }

            if (response.StatusCode == System.Net.HttpStatusCode.OK)
            {
                // OK bind
            }
            else
            {
                //return errro;
            }

            // concatenate and return
            return myDtos.Union(shareddtos).ToList();
        }

        private void updateLoginResponse(String response)
        {            
            if (response=="OK")
            {
                // they logged in OK on the API                                
                parent.KeyServerIsOK = true;
                SaveNewKeyServer(txtKeyServer.Text, txtUsername.Text);
                lblResult.Content = "Connected.";
                lblKeyserverSelectSaltLabel.Visibility = Visibility.Visible;
                
                cmbSaltFilesFromKeyServer.Visibility = Visibility.Visible;

                PullSaltFilesFromServer();
            }
            else
            {
                parent.KeyServerIsOK = false; 
                lblResult.Content = response;
                btnLogin.Visibility = Visibility.Visible;
            }
            
            lblProgress.Visibility = System.Windows.Visibility.Hidden;
            vbProgress.Visibility = System.Windows.Visibility.Hidden;
            //ValidatePage();
        }

        private void PullSaltFilesFromServer()
        {
            btnLogin.Visibility = Visibility.Hidden;
            lblProgress.Visibility = System.Windows.Visibility.Visible;
            lblProgress.Content = "Collecting Salt..";
            vbProgress.Visibility = System.Windows.Visibility.Visible;
                        
            GetSaltListFromServer(parent.SelectedKeyServerAddress, parent.SelectedKeyServerUserName, parent.SelectedKeyServerPassword);         
        }

        private string MainWork(Dispatcher dispatcher, string keyServerAddress, string userName, string passWord)
        {
            // try and connect to the KeyServer to get the public key            
            var client = new RestClient(keyServerAddress);
            var PKrequest = new RestRequest("PublicKey", Method.GET);

            RestResponse PKResponse = (RestResponse)client.Execute(PKrequest);            
            if (PKResponse.StatusCode != System.Net.HttpStatusCode.OK)
            {
                if (PKResponse.ErrorMessage != null)
                { 
                    return PKResponse.ErrorMessage;
                }                
                else
                {
                    return "KeyServer: " + PKResponse.StatusCode;
                }
            }
            string publicKey = PKResponse.Content;
                        
            client.Authenticator = new HttpBasicAuthenticator(userName, passWord);

            // try and connect as this user            
            var authRequest = new RestRequest("AuthenticateUser", Method.GET);
            // execute the request on the API
            RestResponse authResponse = (RestResponse)client.Execute(authRequest);


            if (authResponse.StatusCode != System.Net.HttpStatusCode.OK)
            {
                return "Connected to KeyServer OK. But username or password not known.";
            }

            return "OK";
        }


        
        private void SaveNewKeyServer(string keyServerAddress, string userName)
        {
            if (!KeyServerSettingsFileExists())
            {
                CreateKeyServerSettingsFile();
            }

            // read the file and ensure this line doesn't already exist
            bool credentialsAlreadyExistInFile = false;            
            using (System.IO.StreamReader readfile = new System.IO.StreamReader(GetKeyserverFileName()))
            { 
                string line;                
                while ((line = readfile.ReadLine()) != null)
                {                
                    var items = line.Split('|');
                    string existingkeyServerAddress = "";
                    string existinguserName = "";
                    existingkeyServerAddress = items[0];
                    if (items.Length > 1) existinguserName = items[1];
                    if (existingkeyServerAddress == keyServerAddress && existinguserName == userName)
                    {
                        credentialsAlreadyExistInFile = true;
                    }
                }
                readfile.Close();
            }

            if (!credentialsAlreadyExistInFile)
            { 
                string writeline = keyServerAddress + "|" + userName;
                using(System.IO.StreamWriter writefile = new System.IO.StreamWriter(GetKeyserverFileName()))
                {                 
                    writefile.WriteLine(writeline);
                    writefile.Close();
                }
            }
        }

        private static string GetKeyserverFileName()
        {
            string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "OpenPsuedonymiser");
            string keyServerFile = Path.Combine(path, ".Keyservers");
            return keyServerFile;
        }


        private bool KeyServerSettingsFileExists()
        {            
            return File.Exists(GetKeyserverFileName());
        }

        private void CreateKeyServerSettingsFile()
        {
            string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "OpenPsuedonymiser");
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
            string keyServerFile = Path.Combine(path, ".Keyservers");

            File.WriteAllText(keyServerFile, String.Format("KeyServers Settings file for OpenPseudonymiser, created on : {0}", DateTime.Now));
        }

        
        /// <summary>
        /// Fill in the details of the text boxes based on the key server selected
        /// </summary>        
        private void cmbKeyServer_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            cvsKeyServerDetails.Visibility = Visibility.Visible;
            btnLogin.Visibility = Visibility.Visible;

            lblKeyserverSelectSaltLabel.Visibility = Visibility.Hidden;
            cmbSaltFilesFromKeyServer.Visibility = Visibility.Hidden;

            // parent.DisableNext();
            string selectedItem = "";
            //txtPassword.Password = "";

            var selectedComboItem = (ComboBoxItem)cmbKeyServer.SelectedItem;            
            
            if (selectedComboItem != null)
            { 
                selectedItem = selectedComboItem.Content.ToString(); 
            }
            
            if (selectedItem == null || selectedItem == addNewKeyserverText || selectedItem == "")
            {
                txtKeyServer.Text = "";
                txtUsername.Text = "";
                txtUsername.IsEnabled = true;
                txtKeyServer.IsEnabled = true;
            }
            else
            {
                txtUsername.IsEnabled = false;
                txtKeyServer.IsEnabled = false;
                string line = selectedItem;
                line = line.Replace(" User: ", "|");
                var items = line.Split('|');
                txtKeyServer.Text = items[0];
                if (items.Length > 1) txtUsername.Text = items[1];                
            }
            
        }

        private void cmbSaltFilesFromKeyServer_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            parent.SelectedKeyServerSaltId = Int32.Parse(((ComboBoxItem)cmbSaltFilesFromKeyServer.SelectedItem).Tag.ToString());
            parent.EnableNext();
        }

        

    }
}
