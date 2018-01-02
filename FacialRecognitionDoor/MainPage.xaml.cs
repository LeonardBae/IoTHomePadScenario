using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.Devices.Gpio;
using Windows.Storage;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;
using FacialRecognitionDoor.Helpers;

using Windows.UI.Popups;
using Newtonsoft.Json;
using Windows.UI.Xaml.Media;
using Microsoft.WindowsAzure.Storage.Blob;
using Windows.Storage.Streams;
using System.Net.Http;
using System.Linq;
using System.Text;
// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace FacialRecognitionDoor
{
    public static class globals
    {
        public static PersonGroups gPersonGroupSelected { get; set; }
        public static Persons gPersonSelected { get; set; }
        public static FaceData gFaceSelected { get; set; }
        public static List<PersonGroups> gPersonGroupList { get; set; }

        public static async void ShowJsonErrorPopup(string responseBody)
        {
            if (null != responseBody)
            {
                ResponseObject errorObject = JsonConvert.DeserializeObject<ResponseObject>(responseBody);
                MessageDialog dialog = new MessageDialog(errorObject.error.message,
                                                                 (null != errorObject.error.code) ?
                                                                        errorObject.error.code.ToString() :
                                                                        errorObject.error.statusCode.ToString());
                await dialog.ShowAsync();
            }
            else
            {
                MessageDialog dialog = new MessageDialog("Unknown error in operation");
                await dialog.ShowAsync();
            }
        }
    }
    public class Error
    {
        public string code { get; set; }
        public int statusCode { get; set; }
        public string message { get; set; }
    }

    public class ResponseObject
    {
        public Error error { get; set; }
    }
    public sealed partial class MainPage : Page
    {
        // Webcam Related Variables:
        private WebcamHelper webcam;
        // Speech Related Variables:
        private SpeechHelper speech;
        // GPIO Related Variables:
        private GpioHelper gpioHelper;
        private bool gpioAvailable;
        private bool doorbellJustPressed = false;
        ImageBrush backgroundImage = new ImageBrush();
        /// <summary>
        /// Called when the page is first navigated to.
        /// </summary>
        public MainPage()
        {
            this.InitializeComponent();
            
            HttpHandler.init();
            //btnFileQuery.IsEnabled = HttpHandler.initDone;
            //backgroundImage.ImageSource = new BitmapImage(new Uri("ms-appx:///Assets/ic_launcher.png"));

            if (gpioAvailable == false)
            {
                // If GPIO is not available, attempt to initialize it
                InitializeGpio();
            }
            
            // If user has set the DisableLiveCameraFeed within Constants.cs to true, disable the feed:
            if (GeneralConstants.DisableLiveCameraFeed)
            {
                LiveFeedPanel.Visibility = Visibility.Collapsed;
                DisabledFeedGrid.Visibility = Visibility.Visible;
            }
            else
            {
                LiveFeedPanel.Visibility = Visibility.Visible;
                DisabledFeedGrid.Visibility = Visibility.Collapsed;
            }
        }
        private async void DoorbellButton_Click(object sender, RoutedEventArgs e)
        {
            if (!doorbellJustPressed)
            {
                doorbellJustPressed = true;
                await DoorbellPressed();
            }
            
        }

        private async Task DoorbellPressed()
        {
            StorageFile file = null;
            if (webcam.IsInitialized())
            {
                // Stores current frame from webcam feed in a temporary folder
                file = await webcam.CapturePhoto();
                FaceQuery(file);
            }
            else
            {
                if (!webcam.IsInitialized())
                {
                    // The webcam has not been fully initialized for whatever reason:
                    Debug.WriteLine("Unable to analyze visitor at door as the camera failed to initlialize properly.");
                    await speech.Read(SpeechContants.NoCameraMessage);
                }
            }
            doorbellJustPressed = false;
            //FaceQuery(file);
        }
        //private async Task btnFileQuery_Click()
        //{
        //    StorageFile file = null;
        //    if (webcam.IsInitialized())
        //    {
        //        // Stores current frame from webcam feed in a temporary folder
        //        file = await webcam.CapturePhoto();
        //        FaceQuery(file);
        //    }
        //    else
        //    {
        //        if (!webcam.IsInitialized())
        //        {
        //            // The webcam has not been fully initialized for whatever reason:
        //            Debug.WriteLine("Unable to analyze visitor at door as the camera failed to initlialize properly.");
        //            await speech.Read(SpeechContants.NoCameraMessage);
        //        }
        //    }

            //    FaceQuery(file);
            //    btnFileQuery.IsEnabled = true;
            //}
        private async void FaceQuery(StorageFile file)
        {
            CloudBlockBlob blob = null;
            string blobFileName = null;
            if (null != file)
            {
                progressRingMainPage.IsActive = true;
                BitmapImage bitmapImage = new BitmapImage();
                IRandomAccessStream fileStream = await file.OpenAsync(FileAccessMode.Read);
                bitmapImage.SetSource(fileStream);

                blobFileName = System.Guid.NewGuid() + "." + file.Name.Split('.').Last<string>();

                await HttpHandler.tempContainer.CreateIfNotExistsAsync();
                BlobContainerPermissions permissions = new BlobContainerPermissions();
                permissions.PublicAccess = BlobContainerPublicAccessType.Blob;
                await HttpHandler.tempContainer.SetPermissionsAsync(permissions);
                blob = HttpHandler.tempContainer.GetBlockBlobReference(blobFileName);
                await blob.DeleteIfExistsAsync();
                await blob.UploadFromFileAsync(file);

                string uri = "https://api.projectoxford.ai/face/v1.0/detect?returnFaceId=true";
                string jsonString = "{\"url\":\"" + HttpHandler.storagePath + "visitors/" + blobFileName + "\"}";
                HttpContent content = new StringContent(jsonString, Encoding.UTF8, "application/json");
                HttpResponseMessage response = await HttpHandler.client.PostAsync(uri, content);
                if (response.IsSuccessStatusCode)
                {
                    string responseBody = await response.Content.ReadAsStringAsync();

                    if (null == globals.gPersonGroupList)
                        globals.gPersonGroupList = await PersonGroupCmds.ListPersonGroups();

                    List<string> names = await VisitorCmds.CheckVisitorFace(responseBody, globals.gPersonGroupList);
                    if (0 == names.Count)
                        await speech.Read(SpeechContants.VisitorNotRecognizedMessage);
                    else
                        UnlockDoor(string.Join(", ", names.ToArray()));
                }
                else
                {
                    string responseBody = await response.Content.ReadAsStringAsync();
                    globals.ShowJsonErrorPopup(responseBody);
                }

                await blob.DeleteAsync();
                progressRingMainPage.IsActive = false;
            }
        }
        public void InitializeGpio()
        {
            try
            {
                // Attempts to initialize application GPIO. 
                gpioHelper = new GpioHelper();
                gpioAvailable = gpioHelper.Initialize();
            }
            catch
            {
                // This can fail if application is run on a device, such as a laptop, that does not have a GPIO controller
                gpioAvailable = false;
                Debug.WriteLine("GPIO controller not available.");
            }

            // If initialization was successfull, attach doorbell pressed event handler
            if (gpioAvailable)
            {
                gpioHelper.GetDoorBellPin().ValueChanged += DoorBellPressed;
            }
        }
        private async void WebcamFeed_Loaded(object sender, RoutedEventArgs e)
        {
            if (webcam == null || !webcam.IsInitialized())
            {
                // Initialize Webcam Helper
                webcam = new WebcamHelper();
                await webcam.InitializeCameraAsync();

                // Set source of WebcamFeed on MainPage.xaml
                WebcamFeed.Source = webcam.mediaCapture;

                // Check to make sure MediaCapture isn't null before attempting to start preview. Will be null if no camera is attached.
                if (WebcamFeed.Source != null)
                {
                    // Start the live feed
                    await webcam.StartCameraPreview();
                }
            }
            else if (webcam.IsInitialized())
            {
                WebcamFeed.Source = webcam.mediaCapture;

                // Check to make sure MediaCapture isn't null before attempting to start preview. Will be null if no camera is attached.
                if (WebcamFeed.Source != null)
                {
                    await webcam.StartCameraPreview();
                }
            }
        }

        /// <summary>
        /// Triggered when media element used to play synthesized speech messages is loaded.
        /// Initializes SpeechHelper and greets user.
        /// </summary>
        private async void speechMediaElement_Loaded(object sender, RoutedEventArgs e)
        {
            if (speech == null)
            {
                speech = new SpeechHelper(speechMediaElement);
                await speech.Read(SpeechContants.InitialGreetingMessage);
            }
            else
            {
                // Prevents media element from re-greeting visitor
                speechMediaElement.AutoPlay = false;
            }
        }
        private async void DoorBellPressed(GpioPin sender, GpioPinValueChangedEventArgs args)
        {
            if (!doorbellJustPressed)
            {
                // Checks to see if even was triggered from a press or release of button
                if (args.Edge == GpioPinEdge.FallingEdge)
                {
                    //Doorbell was just pressed
                    doorbellJustPressed = true;

                    await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
                    {
                        await DoorbellPressed();
                    });

                }
            }
        }
        /// <summary>
        /// Unlocks door and greets visitor
        /// </summary>
        private async void UnlockDoor(string visitorName)
        {
            // Greet visitor
            await speech.Read(SpeechContants.GeneralGreetigMessage(visitorName));

            //if (gpioAvailable)
            //{
            //    // Unlock door for specified ammount of time
            //    gpioHelper.UnlockDoor();
            //}
        }
        //private async void appBarTrainButton_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        //{
        //    if (null != globals.gPersonGroupSelected && globals.gPersonGroupSelected.name.Equals("...") == false)
        //    {
        //        await PersonGroupCmds.TrainPersonGroups(globals.gPersonGroupSelected.personGroupId);
        //    }
        //    else
        //    {
        //        MessageDialog dialog = new MessageDialog("Select a PersonGroup to train", "Training Error");
        //        await dialog.ShowAsync();
        //    }
        //}
        private void appbarPersonGroupNextButton_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            Frame.Navigate(typeof(PersonGroupPage));
        }
        private void ShutdownButton_Click(object sender, RoutedEventArgs e)
        {
            // Exit app
            Application.Current.Exit();
        }
    }
}
