using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.UI.Popups;
using Microsoft.WindowsAzure.Storage.Blob;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234238

namespace FacialRecognitionDoor
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class PersonGroupPage : Page
    {
        public PersonGroupPage()
        {
            this.InitializeComponent();
            AppBarButtonPersonGroupRefresh_Click(null, null);
        }
        private async void AppBarButtonPersonGroupRefresh_Click(object sender, RoutedEventArgs e)
        {
            this.IsEnabled = false;
            personGroupProgressRing.IsActive = true;
            
            //appbarEditPersonGroupButton.IsEnabled = false;
            appbarPersonGroupNextButton.IsEnabled = false;
            appbarDeletePersonGroupButton.IsEnabled = false;

            List<PersonGroups> personGroups = await PersonGroupCmds.ListPersonGroups();
            personGroupListView.ItemsSource = personGroups;
            //personGroupListView.DisplayMemberPath = "name";
            globals.gPersonGroupList = personGroups;

            personGroupProgressRing.IsActive = false;
            this.IsEnabled = true;
        }

        private async void AppBarButtonAddPersonGroup_Click(object sender, RoutedEventArgs e)
        {
            if (null == personGroupListView.SelectedItem || ((PersonGroups)personGroupListView.SelectedItem).name.Equals("..."))
            {
                if (txtPersonGroup.Text.Trim() != "")
                {
                    this.IsEnabled = false;
                    personGroupProgressRing.IsActive = true;

                    string response = await PersonGroupCmds.AddPersonGroups(txtPersonGroup.Text.ToLower().Replace(' ', '_'),
                                                                            txtPersonGroup.Text,null);
                    
                    personGroupProgressRing.IsActive = false;
                    this.IsEnabled = true;
                }
                else
                {
                    MessageDialog dialog = new MessageDialog("Add a name to person group", "Add Error");
                    await dialog.ShowAsync();
                }
            }

            AppBarButtonPersonGroupRefresh_Click(null, null);
        }

        //private async void AppBarButtonEditPersonGroup_Click(object sender, RoutedEventArgs e)
        //{
        //    if (null != personGroupListView.SelectedItem && !((PersonGroups)personGroupListView.SelectedItem).name.Equals("..."))
        //    {
        //        this.IsEnabled = false;
        //        personGroupProgressRing.IsActive = true;

        //        string response = await PersonGroupCmds.UpdatePersonGroups(globals.gPersonGroupSelected.personGroupId,
        //                                                                   txtPersonGroup.Text,null);
                
        //        personGroupProgressRing.IsActive = false;
        //        this.IsEnabled = true;
        //    }
        //    else
        //    {
        //        MessageDialog dialog = new MessageDialog("Select an existing person group and change name", "Update Error");
        //        await dialog.ShowAsync();
        //    }

        //    AppBarButtonPersonGroupRefresh_Click(null, null);
        //}
        
        private async void AppBarButtonDeletePerson_Click(object sender, RoutedEventArgs e)
        {
            //TODO: Delete all Person - Faces - Blobs - Thumbs
            if (null != personGroupListView.SelectedItem)
            {
                this.IsEnabled = false;
                personGroupProgressRing.IsActive = true;

                string response = await PersonGroupCmds.DeletePersonGroups(((PersonGroups)personGroupListView.SelectedItem).personGroupId);

                //delete blob
                CloudBlobDirectory groupdirectory = HttpHandler.blobContainer.GetDirectoryReference(globals.gPersonGroupSelected.personGroupId);
                BlobContinuationToken token = null;
                do
                {
                    BlobResultSegment resultSegment = await groupdirectory.ListBlobsSegmentedAsync(token);
                    token = resultSegment.ContinuationToken;

                    foreach (IListBlobItem item in resultSegment.Results)
                    {
                        if (item.GetType() == typeof(CloudBlobDirectory))
                        {
                            CloudBlobDirectory directory = (CloudBlobDirectory)item;
                            BlobContinuationToken token2 = null;
                            BlobResultSegment resultSegment2 = await directory.ListBlobsSegmentedAsync(token2);
                            token2 = resultSegment2.ContinuationToken;

                            foreach (IListBlobItem item2 in resultSegment2.Results)
                            {
                                if (item2.GetType() == typeof(CloudBlockBlob))
                                {
                                    CloudBlockBlob blob = (CloudBlockBlob)item2;
                                    blob.DeleteAsync();
                                }
                                break;
                            }
                        }
                    }
                } while (token != null);

                personGroupProgressRing.IsActive = false;
                this.IsEnabled = true;
            }
            else
            {
                MessageDialog dialog = new MessageDialog("Select a person group to delete!", "Delete Error");
                await dialog.ShowAsync();
            }

            AppBarButtonPersonGroupRefresh_Click(null, null);
        }

        private void peopleListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            this.IsEnabled = false;
            personGroupProgressRing.IsActive = true;
            //appbarEditPersonGroupButton.IsEnabled = true;
            appbarDeletePersonGroupButton.IsEnabled = true;
            appbarPersonGroupNextButton.IsEnabled = true;

            PersonGroups personGroup = (PersonGroups)personGroupListView.SelectedItem;
            txtPersonGroup.Text = (null == personGroup) ? "" : personGroup.name;

            personGroupProgressRing.IsActive = false;
            this.IsEnabled = true;

            globals.gPersonGroupSelected = personGroup;
            globals.gPersonSelected = null;            
        }

        private void appbarPersonGroupHomeButton_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(MainPage));
        }

        private async void appbarPersonGroupNextButton_Click(object sender, RoutedEventArgs e)
        {
            if (null != personGroupListView.SelectedItem)
            {
                Frame.Navigate(typeof(PersonPage));
            }
            else
            {
                MessageDialog dialog = new MessageDialog("Select a PersonGroup to add Person to!", "Navigation Error");
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
            Frame.Navigate(typeof(MainPage));
        }
    }
}
