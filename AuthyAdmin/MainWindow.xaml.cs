using System.Security.Cryptography;
using System.Configuration;
using System.Windows;
using System.Windows.Media;
using System.Text;
using System.Text.RegularExpressions;
using Google.Authenticator;
using System.Windows.Media.Imaging;
using System.Net;
using System.IO;
using System;

namespace _AuthyAdmin
{
    /// Copyright (c) 2007 - Project Balance, LLC
    /// Released under the MIT license
    /// www.opensource.org/licenses/MIT

    public partial class MainWindow : Window
    {
        public string strApp = "";//Initialise the app name variable
        public string strAuthKey = "";//Initialise the secret key variable
        public string strStaticSecret = "YourSuperSecretStaticPassPhraseGoesHere";
        //^ static secret is used to encrypt your custom application secret before writing it to the config file so that it can't
        //be read in plain text.  A nice long phrase or string of random characters is best.

        public static class Encrypt
        {
            // This size of the IV (in bytes) must = (keysize / 8).  Default keysize is 256, so the IV must be
            // 32 bytes long.  Using a 16 character string here gives us 32 bytes when converted to a byte array.
            private const string initVector = "6gdtc82yxtg04hrf";
            // This constant is used to determine the keysize of the encryption algorithm
            private const int keysize = 256;
            //Encrypt
            public static string EncryptString(string plainText, string passPhrase)
            {
                byte[] initVectorBytes = Encoding.UTF8.GetBytes(initVector);
                byte[] plainTextBytes = Encoding.UTF8.GetBytes(plainText);
                PasswordDeriveBytes password = new PasswordDeriveBytes(passPhrase, null);
                byte[] keyBytes = password.GetBytes(keysize / 8);
                RijndaelManaged symmetricKey = new RijndaelManaged();
                symmetricKey.Mode = CipherMode.CBC;
                ICryptoTransform encryptor = symmetricKey.CreateEncryptor(keyBytes, initVectorBytes);
                MemoryStream memoryStream = new MemoryStream();
                CryptoStream cryptoStream = new CryptoStream(memoryStream, encryptor, CryptoStreamMode.Write);
                cryptoStream.Write(plainTextBytes, 0, plainTextBytes.Length);
                cryptoStream.FlushFinalBlock();
                byte[] cipherTextBytes = memoryStream.ToArray();
                memoryStream.Close();
                cryptoStream.Close();
                return Convert.ToBase64String(cipherTextBytes);
            }
            //Decrypt
            public static string DecryptString(string cipherText, string passPhrase)
            {
                try
                { 
                    byte[] initVectorBytes = Encoding.ASCII.GetBytes(initVector);
                    byte[] cipherTextBytes = Convert.FromBase64String(cipherText);
                    PasswordDeriveBytes password = new PasswordDeriveBytes(passPhrase, null);
                    byte[] keyBytes = password.GetBytes(keysize / 8);
                    RijndaelManaged symmetricKey = new RijndaelManaged();
                    symmetricKey.Mode = CipherMode.CBC;
                    ICryptoTransform decryptor = symmetricKey.CreateDecryptor(keyBytes, initVectorBytes);
                    MemoryStream memoryStream = new MemoryStream(cipherTextBytes);
                    CryptoStream cryptoStream = new CryptoStream(memoryStream, decryptor, CryptoStreamMode.Read);
                    byte[] plainTextBytes = new byte[cipherTextBytes.Length];
                    int decryptedByteCount = cryptoStream.Read(plainTextBytes, 0, plainTextBytes.Length);
                    memoryStream.Close();
                    cryptoStream.Close();
                    return Encoding.UTF8.GetString(plainTextBytes, 0, decryptedByteCount);
                }
                catch
                {
                    return "";
                }
            }
        }

        public static void CopyUIElementToClipboard(FrameworkElement element)
        {
            // Copies a UI element to the clipboard as an image.
            // Element = The element to copy.
            double width = element.ActualWidth;
            double height = element.ActualHeight;
            RenderTargetBitmap bmpCopied = new RenderTargetBitmap((int)Math.Round(width), (int)Math.Round(height), 96, 96, PixelFormats.Default);
            DrawingVisual dv = new DrawingVisual();
            using (DrawingContext dc = dv.RenderOpen())
            {
                VisualBrush vb = new VisualBrush(element);
                dc.DrawRectangle(vb, null, new Rect(new Point(), new Size(width, height)));
            }
            bmpCopied.Render(dv);
            Clipboard.SetImage(bmpCopied);
        }

        public string readSetting(string strKey)
        {
            try
            {
                var appSettings = ConfigurationManager.AppSettings;
                string result = appSettings[strKey];
                return result;
            }
            catch (ConfigurationErrorsException)
            {
                Console.WriteLine("Error reading app settings");
                return "";
            }
        }

        public void writeSetting(string strKey, string strValue)
        {
            try
            {
                Configuration config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
                config.AppSettings.Settings.Add(strKey,strValue);
                config.Save(ConfigurationSaveMode.Modified);
                ConfigurationManager.RefreshSection("appSettings");
                return;
            }
            catch (ConfigurationErrorsException)
            {
                Console.WriteLine("Error writing app settings");
                return;
            }
        }

        public string getSetupCode(string strMyApp, string strUser, bool QRCode = true)
        //QRCode boolean determines whether code in string format or qr code url should be returned
        //strUser is the user's google auth account email address
        {
            TwoFactorAuthenticator tfa = new TwoFactorAuthenticator();
            var setupInfo = tfa.GenerateSetupCode(strMyApp, strUser, strAuthKey, 300, 300);
            if (QRCode)
            {
                return setupInfo.QrCodeSetupImageUrl;
            }
            else
            {
                return setupInfo.ManualEntryKey;
            }
        }

        public bool ValidatePin(string strPIN)
        {
            TwoFactorAuthenticator tfa = new TwoFactorAuthenticator();
            return tfa.ValidateTwoFactorPIN(strAuthKey, strPIN);
        }

        public Regex myregex = new Regex("[0-9]{6}");
        public MainWindow()
        {
            InitializeComponent();
            strApp = _AuthyAdmin.Properties.Settings.Default.app;//read app from app.config settings
            strAuthKey = _AuthyAdmin.Properties.Settings.Default.ak;//read authKey from app.config settings
            if (!string.IsNullOrEmpty(strApp)) { strApp = Encrypt.DecryptString(strApp, strStaticSecret); }//decrypt app
            if (!string.IsNullOrEmpty(strAuthKey)) { strAuthKey = Encrypt.DecryptString(strAuthKey, strStaticSecret); }//decrypt authKey
        }
        private void _2FAMain_Loaded(object sender, RoutedEventArgs e)
        {
            //Set fields from app.config file on load
            txtMyApp.Text = strApp;
            txtSecret.Text = strAuthKey;
            txtSecretPW.Password = strAuthKey;
        }

        private void btnGenerateCode_Click(object sender, RoutedEventArgs e)
        {
            if (txtMyApp.Text == string.Empty || txtSecretPW.Password== string.Empty || txtEmail.Text == string.Empty) //Validate that app name and app secret is entered
            {
                MessageBox.Show("All three fields need to be filled to produce the setup code.", "Incomplete",MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            strAuthKey = txtSecret.Text;
            txtSetupCode.Visibility = Visibility.Visible;
            lblSetupCode.Visibility = Visibility.Visible;
            imgCopyQR.Visibility = Visibility.Visible;
            imgCopyManualSetup.Visibility = Visibility.Visible;

            txtSetupCode.Text = getSetupCode(txtMyApp.Text, txtEmail.Text, false);//Populate the manual setup code text box

            //Generate the QR code image
            string URI = getSetupCode(txtMyApp.Text, txtEmail.Text);
            var image = new BitmapImage();
            int BytesToRead = 100;
            WebRequest request = WebRequest.Create(new Uri(URI, UriKind.Absolute));
            request.Timeout = -1;
            WebResponse response = request.GetResponse();
            Stream responseStream = response.GetResponseStream();
            BinaryReader reader = new BinaryReader(responseStream);
            MemoryStream memoryStream = new MemoryStream();

            byte[] bytebuffer = new byte[BytesToRead];
            int bytesRead = reader.Read(bytebuffer, 0, BytesToRead);

            while (bytesRead > 0)
            {
                memoryStream.Write(bytebuffer, 0, bytesRead);
                bytesRead = reader.Read(bytebuffer, 0, BytesToRead);
            }

            image.BeginInit();
            memoryStream.Seek(0, SeekOrigin.Begin);

            image.StreamSource = memoryStream;
            image.EndInit();

            imgQR.Source = image;
        }

        private void btnShowHideSecret_click(object sender, RoutedEventArgs e)
        {
            //Show or hide the secret key box
            if (txtSecret.Visibility == Visibility.Hidden)
            {
                txtSecret.Text = txtSecretPW.Password;
                txtSecret.Visibility = Visibility.Visible;
                txtSecretPW.TabIndex = 99;
                txtSecretPW.Visibility = Visibility.Hidden;
                txtSecret.TabIndex = 1;
            }
            else
            {
                txtSecret.TabIndex = 99;
                txtSecret.Visibility = Visibility.Hidden;
                txtSecretPW.Visibility = Visibility.Visible;
                txtSecretPW.TabIndex = 1;
            }
        }

        private void editingPasswords(object sender, RoutedEventArgs e)
        {
            //Update the plain text box or secret box depending on which is visible and which is hidden at the time.
            //Both contain the password - one is masked, one not.  This is run whenever text in either changes.

            if (txtSecret.Visibility == Visibility.Hidden)
            {
                //Password box is being used - update txtSecret box
                txtSecret.Text = txtSecretPW.Password;
            }
            else if (txtSecret.Text != txtSecretPW.Password)
            {
                //txtSecret box is being used - update Password box
                txtSecretPW.Password = txtSecret.Text;
            }
            //set image placeholder in place of QR code and hide manual setup code fields when the password is changed since any QR code there will be invalid
            txtSetupCode.Visibility = Visibility.Hidden;
            lblSetupCode.Visibility = Visibility.Hidden;
            imgCopyManualSetup.Visibility = Visibility.Hidden;
            imgCopyQR.Visibility = Visibility.Hidden;
            if (imgQR.Source.ToString() != "pack://application:,,,/AuthyAdmin;component/img/imgplaceholder.png")
            {
                Uri imgQRURI = new Uri("img\\imgplaceholder.png", UriKind.Relative);
                imgQR.Source = new BitmapImage(imgQRURI);
            }
        }

        private void copyQR_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            //Copy the QR code to the clipboard
            CopyUIElementToClipboard(imgQR);
            MessageBox.Show("The QR code image has been copied to clipboard.  Send this to the relevant user via secure email or instant messaging.", "Copied", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void copyManualSetup_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            //Copy the manual setup string to the clipboard
            Clipboard.SetText(txtSetupCode.Text);
            MessageBox.Show("The manual setup code has been copied to clipboard.  Send this to the relevant user via secure email or instant messaging.", "Copied", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void _2FAMain_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            //Check for changes to the app name and auth key and ask to save if changes have been made
            if (strApp != txtMyApp.Text || strAuthKey != txtSecretPW.Password)
            {
                if (MessageBox.Show("Application name and / or secret key has changed.  Would you like to save the changes?", "Save", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    //Yes - save
                    strApp = Encrypt.EncryptString(txtMyApp.Text, strStaticSecret);//Encrypt app name
                    _AuthyAdmin.Properties.Settings.Default.app = strApp;//Update user settings for app
                    strAuthKey = Encrypt.EncryptString(txtSecretPW.Password, strStaticSecret); //Encrypt authkey
                    _AuthyAdmin.Properties.Settings.Default.ak = strAuthKey;//Update user settings for app
                    _AuthyAdmin.Properties.Settings.Default.Save();
                }
            }
        }

        private void imgProjectBalance_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            //Project balance logo click
            System.Diagnostics.Process.Start("http://www.projectbalance.com");
        }
    }
}