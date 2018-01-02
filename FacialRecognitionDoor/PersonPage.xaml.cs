using System;
using System.Collections.Generic;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Popups;
using Microsoft.WindowsAzure.Storage.Blob;
using Windows.UI.Xaml.Media.Imaging;


// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234238

namespace FacialRecognitionDoor
{
    public class PersonImage
    {
        public BitmapImage Image { get; set; }
        public string name { get; set; }
        public double MaxHeight { get; set; }
    }
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class PersonPage : Page
    {
        // GUI Related Variables:
        //private double visitorIDPhotoGridMaxWidth = 0;
        public PersonPage()
        {
            this.InitializeComponent();
            AppBarButtonPersonRefresh_Click(null, null);
        }
        //private void WhitelistedUsersGrid_Loaded(object sender, RoutedEventArgs e)
        //{
        //    visitorIDPhotoGridMaxWidth = (personListView.ActualWidth / 3) - 10;
        //}
        private async void AppBarButtonPersonRefresh_Click(object sender, RoutedEventArgs e)
        {
            textBlockPerson.Text = "People in " + globals.gPersonGroupSelected.name + " Group";
            if (null != globals.gPersonGroupSelected && (globals.gPersonGroupSelected.name.Equals("...") == false))
            {
                this.IsEnabled = false;
                personProgressRing.IsActive = true;
                
                appbarPersonNextButton.IsEnabled = false;
                appbarDeletePersonButton.IsEnabled = false;

                List<Persons> persons = await PersonsCmds.ListPersonInGroup(globals.gPersonGroupSelected.personGroupId);
                personListView.ItemsSource = persons;
                //personListView.DisplayMemberPath = "name";

                //personListView.ItemsSource = persons;

                ////load blob
                //CloudBlobDirectory personingroup_blob = HttpHandler.blobContainer.GetDirectoryReference(globals.gPersonGroupSelected.name);
                
                //// Populates subFolders list with all sub folders within the whitelist folders.
                //BlobContinuationToken token = null;
                //do
                //{
                //    BlobResultSegment resultSegment = await personingroup_blob.ListBlobsSegmentedAsync(token);
                //    token = resultSegment.ContinuationToken;
                //    foreach (IListBlobItem item in resultSegment.Results)
                //    {
                //        if (item.GetType() == typeof(CloudBlobDirectory))
                //        {
                //            CloudBlobDirectory directory = (CloudBlobDirectory)item;
                //            BlobContinuationToken token2 = null;
                //            BlobResultSegment resultSegment2 = await directory.ListBlobsSegmentedAsync(token2);
                //            token2 = resultSegment2.ContinuationToken;
                //            foreach (IListBlobItem item2 in resultSegment2.Results)
                //            {
                //                if (item2.GetType() == typeof(CloudBlockBlob))
                //                {
                //                    CloudBlockBlob blob = (CloudBlockBlob)item2;
                //                    var sas1 = blob.GetSharedAccessSignature(new SharedAccessBlobPolicy()
                //                    {
                //                        Permissions = SharedAccessBlobPermissions.Read,
                //                        SharedAccessExpiryTime = DateTime.UtcNow.AddHours(1),//Set this date/time according to your requirements
                //                    });
                //                    var imageuri = string.Format("{0}{1}", blob.Uri, sas1);
                //                    string bloburi = new Uri(imageuri).ToString();
                //                    BitmapImage visitorImage = new BitmapImage(new Uri(bloburi));
                //                    PersonImage idImageControl = new PersonImage();
                //                    idImageControl.Image = visitorImage;
                //                    string personname = blob.Parent.Prefix.ToString();
                //                    personname = personname.Replace(globals.gPersonGroupSelected.name + "/", "");
                //                    personname = personname.Remove(personname.Length - 1);
                //                    idImageControl.name = personname;
                //                    idImageControl.MaxHeight = visitorIDPhotoGridMaxWidth;
                //                    personListView.ItemsSource = idImageControl;
                //                    break;
                //                }                                
                //            }
                //        }
                //    }
                //} while (token != null);


                personProgressRing.IsActive = false;
                this.IsEnabled = true;
            }
            else
            {
                MessageDialog dialog = new MessageDialog("No person group selected", "Refresh Error");
                await dialog.ShowAsync();
            }

        }

        private async void AppBarButtonAddPerson_Click(object sender, RoutedEventArgs e)
        {
            if (null == personListView.SelectedItem || ((Persons)personListView.SelectedItem).name.Equals("..."))
            {
                if (txtPerson.Text.Trim() != "" && txtPerson.Text != "...")
                {
                    this.IsEnabled = false;
                    personProgressRing.IsActive = true;

                    string response = await PersonsCmds.CreatePerson(globals.gPersonGroupSelected.personGroupId,// txtPerson.Text.ToLower().Replace(' ', '_'),
                                                                    txtPerson.Text,null);

                    personProgressRing.IsActive = false;
                    this.IsEnabled = true;
                }
                else
                {
                    MessageDialog dialog = new MessageDialog("Add a name for person", "Add Error");
                    await dialog.ShowAsync();
                }
            }

            AppBarButtonPersonRefresh_Click(null, null);
        }

        //private async void AppBarButtonEditPerson_Click(object sender, RoutedEventArgs e)
        //{
        //    if (null != personListView.SelectedItem && !((Persons)personListView.SelectedItem).name.Equals("..."))
        //    {
        //        this.IsEnabled = false;
        //        personProgressRing.IsActive = true;

        //        string response = await PersonsCmds.UpdatePerson(globals.gPersonGroupSelected.personGroupId,
        //                                                        globals.gPersonSelected.personId,
        //                                                        txtPerson.Text,null);

        //        personProgressRing.IsActive = false;
        //        this.IsEnabled = true;
        //    }
        //    else
        //    {
        //        MessageDialog dialog = new MessageDialog("Select an existing person and change name", "Update Error");
        //        await dialog.ShowAsync();
        //    }

        //    AppBarButtonPersonRefresh_Click(null, null);
        //}

        private async void AppBarButtonDeletePerson_Click(object sender, RoutedEventArgs e)
        {
            //TODO: Delete all Faces - Blobs - Thumbs
            if (null != personListView.SelectedItem)
            {
                this.IsEnabled = false;
                personProgressRing.IsActive = true;

                string response = await PersonsCmds.DeletePerson(globals.gPersonGroupSelected.personGroupId,
                                                                globals.gPersonSelected.personId);
                //delete blob
                CloudBlobDirectory userface = HttpHandler.blobContainer.GetDirectoryReference(globals.gPersonGroupSelected.personGroupId + "/" + globals.gPersonSelected.personId);
                BlobContinuationToken token = null;
                do
                {
                    BlobResultSegment resultSegment = await userface.ListBlobsSegmentedAsync(token);
                    token = resultSegment.ContinuationToken;

                    foreach (IListBlobItem item in resultSegment.Results)
                    {
                        if (item.GetType() == typeof(CloudBlockBlob))
                        {
                            CloudBlockBlob blob = (CloudBlockBlob)item;
                            await blob.DeleteAsync();
                        }
                    }
                } while (token != null);

                personProgressRing.IsActive = false;
                this.IsEnabled = true;
            }
            else
            {
                MessageDialog dialog = new MessageDialog("Select a person group to delete!", "Delete Error");
                await dialog.ShowAsync();
            }

            AppBarButtonPersonRefresh_Click(null, null);
        }

        private void personListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            this.IsEnabled = false;
            personProgressRing.IsActive = true;
            appbarDeletePersonButton.IsEnabled = true;
            appbarPersonNextButton.IsEnabled = true;

            Persons person = (Persons)personListView.SelectedItem;
            txtPerson.Text = (null == person) ? "" : person.name;

            personProgressRing.IsActive = false;
            this.IsEnabled = true;

            globals.gPersonSelected = person;
            globals.gFaceSelected = null;
        }

        private void appbarPersonHomeButton_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(MainPage));
        }

        private async void appbarPersonNextButton_Click(object sender, RoutedEventArgs e)
        {
            if (null != personListView.SelectedItem)
            {
                Frame.Navigate(typeof(PersonFace));
            }
            else
            {
                MessageDialog dialog = new MessageDialog("Select a person to add Face!", "Navigation Error");
                await dialog.ShowAsync();
            }
        }
        private void ShutdownButton_Click(object sender, RoutedEventArgs e)
        {
            // Exit app
            Application.Current.Exit();
        }
        private void AppBarBackButton_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(PersonGroupPage));
        }
    }
}
