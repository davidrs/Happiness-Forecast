using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using Microsoft.Phone.Controls;

using System.IO;
using System.Xml;
using System.Xml.Linq;

using System.Windows.Media.Imaging; // BitmapImage
using System.Windows.Resources; // StreamResourceInfo
using System.Device.Location;


namespace TwitterArt
{
    public partial class MainPage : PhoneApplicationPage
    {
        const int numEmotions = 4; //happy,angry,sad,nuetral
        string[] happyWords = new string[] { "happy", ":)", ":D", ":-)", "great", "wonderful", "good", "excellent", "fantastic","smile" };
        string[] angryWords = new string[] { "angry", "mad", "hate", "frown", "pissed", "stupid", "annoying", "shit", "fuck", "asshole", "dick","pussy","cunt" };
        string[] sadWords = new string[] { "sad", "depressed", ":(", ":'(", ":-(", "lonely", "crying", "gloom", "morose", "sorrow", "abject", "dejected", "dismal", "moping", "glum", "unhappy" };

        string allIds, tmpTweet, latitude, longitude;
        string[] sampleTweets = new string[numEmotions];
        int[] bestTweet = new int[numEmotions]{0,0,0,0};

        //assuming 4 days of results used 
        const int numDays = 4;
        int lineHeightMultiplier;
        //each row corresponds to enum TweetType
        int[,] tweetTypeCount = new int[4,4] { { 0, 0, 0, 0 }, { 0, 0, 0, 0 }, { 0, 0, 0, 0 }, { 0, 0, 0, 0 } };
 
        enum TweetType { Happy, Angry, Sad, Nuetral };
        TweetType forecast = TweetType.Nuetral;

        int today = DateTime.Now.Day;
        int month = DateTime.Now.Month;
        int tmpHappy,  tmpAngry, tmpSad;
        int searchDay = numDays;

        string sinceId;
        
        GeoCoordinateWatcher watcher;
        bool useGPS=false;
        string geocode = "";

        // Constructor
        public MainPage()
        {
            InitializeComponent();

            //Loading image
            setLoadingImage();
            
            //initialize variables 
            watcher = new GeoCoordinateWatcher(GeoPositionAccuracy.Default);
            watcher.MovementThreshold = 200;

            // Add event handlers for StatusChanged and PositionChanged events
            watcher.StatusChanged += new EventHandler<GeoPositionStatusChangedEventArgs>(watcher_StatusChanged);
            watcher.PositionChanged += new EventHandler<GeoPositionChangedEventArgs<GeoCoordinate>>(watcher_PositionChanged);

            // Start data acquisition
            watcher.Start();

            //Do first forecast
            //triggered by gps setup
            //startForecast();     
       
            //add listener to grid
            grid1.SizeChanged += new SizeChangedEventHandler(gridSizeChanged);
           // historyGrid.SizeChanged += new SizeChangedEventHandler(gridSizeChanged);
        }

        private void button1_Click(object sender, RoutedEventArgs e)
        {
            //set loading image
            setLoadingImage();

            //clear chart
            historyGrid.Children.Clear(); 
            
            //start forecast
            startForecast();
        }

        private void startForecast()
        {
            WebClient client = new WebClient();
            client.DownloadStringCompleted += new DownloadStringCompletedEventHandler(client_DownloadStringCompleted);
            //reset search days and counts
            searchDay = numDays;
            tweetTypeCount = new int[4, 4] { { 0, 0, 0, 0 }, { 0, 0, 0, 0 }, { 0, 0, 0, 0 }, { 0, 0, 0, 0 } };

            sampleTweets = new string[numEmotions] { "", "", "", "" };
            bestTweet = new int[numEmotions] { 0, 0, 0, 0 };

            geocode = "";

            if (useGPS)
            {
                geocode = "&geocode=" + latitude + "," + longitude + ",25mi";
            }
            string twitterGET = "http://search.twitter.com/search.json?q=" + textBox1.Text + "%20since%3A2012-" + month.ToString().PadLeft(2, '0') + "-" + (today - searchDay - 1) + "%20until%3A2012-" + month.ToString().PadLeft(2, '0') + "-" + (today - searchDay) + "&rpp=100" + geocode + "&result_type=recent";

             client.DownloadStringAsync(new Uri("http://search.twitter.com/search.json?q=" + textBox1.Text + "%20since%3A2012-" + month.ToString().PadLeft(2, '0') + "-" + (today - searchDay - 1) + "%20until%3A2012-0" + month.ToString().PadLeft(2, '0') + "-" + (today - searchDay) + "&rpp=100"+geocode+"&result_type=recent"));
            
      
        }

        private void client_DownloadStringCompleted(object sender, DownloadStringCompletedEventArgs e)
        {
            if (e.Error != null)
            {
                //videoFailHandler(e.Error, e.UserState);
            }
            else
            {
                string twitterResults = e.Result;

                int startOfTweet = 0;
                int endOfTweet = 1;                

                startOfTweet = twitterResults.IndexOf("\"text\":");
                while (startOfTweet >= 0)
                {

                    startOfTweet = twitterResults.IndexOf("text\":");

                    //get tweets
                    startOfTweet += 6;
                    endOfTweet = twitterResults.IndexOf("to_user", startOfTweet) - 3;
                    tmpTweet=twitterResults.Substring(startOfTweet, (endOfTweet - startOfTweet));

                    //decide if this tweet is angry, sad, or happy
                   tmpHappy= wordCount(happyWords, tmpTweet);
                   tmpAngry= wordCount(angryWords, tmpTweet);
                   tmpSad   =wordCount(sadWords, tmpTweet);

                   if (tmpHappy == tmpAngry && tmpHappy == tmpSad)
                   {
                       tweetTypeCount[(int)TweetType.Nuetral,numDays - searchDay]++;
                       if (tmpHappy==0)
                       {                           
                           sampleTweets[(int)TweetType.Nuetral] = tmpTweet;
                       }
                   }
                   else if (tmpHappy >= tmpAngry && tmpHappy >= tmpSad) //ordering of ifs skews to favour happy: ie. happy:2 angry:2 sad:1 would be happy
                   {
                       tweetTypeCount[(int)TweetType.Happy, numDays - searchDay]++;
                       if (bestTweet[(int)TweetType.Happy] <tmpHappy)
                       {                           
                           sampleTweets[(int)TweetType.Happy] = tmpTweet;
                           bestTweet[(int)TweetType.Happy] = tmpHappy;
                       }
                   }
                   else if (tmpAngry >= tmpHappy && tmpAngry >= tmpSad)
                   {
                       tweetTypeCount[(int)TweetType.Angry, numDays - searchDay]++;
                       if (bestTweet[(int)TweetType.Angry] < tmpAngry)
                       {
                           sampleTweets[(int)TweetType.Angry] = tmpTweet;
                           bestTweet[(int)TweetType.Angry] = tmpAngry;
                       }
                   }
                   else if (tmpSad >= tmpHappy && tmpSad >= tmpAngry)
                   {
                       tweetTypeCount[(int)TweetType.Sad, numDays - searchDay]++;
                       if (bestTweet[(int)TweetType.Sad] < tmpSad)
                       {
                           sampleTweets[(int)TweetType.Sad] = tmpTweet;
                           bestTweet[(int)TweetType.Sad] = tmpSad;
                       }
                   }

                    //move on to the next tweet
                    twitterResults = twitterResults.Substring(endOfTweet + 20);//atleast 20 char before next text                      
                    startOfTweet = twitterResults.IndexOf("\"text\":");
                }

                if (searchDay > 1)
                {
                    //move on to the next day
                    nextDay();
                }
                else
                {
                    //Normalize nuetral
                    for (int i = 0; i < numDays; i++)
                    {
                        tweetTypeCount[(int)TweetType.Nuetral, i] = tweetTypeCount[(int)TweetType.Nuetral, i]/8;
                    }
                    doForecast();
                    drawChart();
                    doSamples();
                }

            }
        }

        private int wordCount(string[] wrdArray, string tmpTwt)
        {
            int tmpNum = 0;
            for (int i = 0; i < wrdArray.Length; i++)
            {
                if (tmpTwt.Contains(wrdArray[i]))
                {
                    tmpNum++;
                }
            }
            return tmpNum;

        }

        private void nextDay()
        {
            WebClient clientTmp = new WebClient();
            clientTmp.DownloadStringCompleted += new DownloadStringCompletedEventHandler(client_DownloadStringCompleted);
            
            //reset search days and counts            
            searchDay--;
            clientTmp.DownloadStringAsync(new Uri("http://search.twitter.com/search.json?q=" + textBox1.Text + "%20since%3A2012-" + month.ToString().PadLeft(2, '0') + "-" + (today - searchDay - 1) + "%20until%3A2012-" + month.ToString().PadLeft(2, '0') + "-" + (today - searchDay) + "&rpp=100" + geocode + "&result_type=recent"));
            
        }
        private void setLoadingImage()
        {
            //set image
            string imageForecastString = "Images/loadingFace.png";
            Uri uri = new Uri(imageForecastString, UriKind.Relative);
            StreamResourceInfo resourceInfo = Application.GetResourceStream(uri);
            BitmapImage bmp = new BitmapImage();
            bmp.SetSource(resourceInfo.Stream);
            imageForecast.Source = bmp;  
        }
        //forecast, just do delta from yesterday to today
        private void doForecast()
        {
            //TweetType
            int[] deltas = new int[numEmotions];
            int maxDelta=0;
            int tmpDelta = 0;
            int maxIndex=0;

            for(int i=0;i<numEmotions;i++){
                 tmpDelta=tweetTypeCount[i, numDays - 1] - tweetTypeCount[i, numDays - 2];
                 if (tmpDelta > maxDelta)
                 {
                     maxDelta = tmpDelta;
                     maxIndex = i;
                 }
            }
            //set forecast type
            forecast = (TweetType)(maxIndex);

            //set image
            string imageForecastString = "Images/" + forecast.ToString().ToLower() + "Face.png";
            Uri uri = new Uri(imageForecastString, UriKind.Relative);
            StreamResourceInfo resourceInfo = Application.GetResourceStream(uri);
            BitmapImage bmp = new BitmapImage();
            bmp.SetSource(resourceInfo.Stream);

            //imageForecast.Stretch = Stretch.Uniform;
            //imageForecast.MaxHeight = grid1.ActualHeight;
            imageForecast.Source = bmp;            
        }

        //add chart to grid1
        private void drawChart()
        {
            historyGrid.Children.Clear();
            lineHeightMultiplier = 9+(int)(historyGrid.ActualHeight / 60);

            for (int i = 0; i < (numDays-1); i++)
            {
                for (int j = 0; j < numEmotions; j++)
                {
                    Line line = new Line();
                    Line accentLine = new Line();

                    line.X1 = i * (historyGrid.ActualWidth / (numDays-1) - 8) + 20;
                    line.X2 = (i + 1) * (historyGrid.ActualWidth / (numDays-1) - 8) + 20;

                    /*if (j==(int)TweetType.Nuetral)
                    {
                        line.Y1 = grid1.Height-tweetTypeCount[j, i] * lineHeightMultiplier/7;
                        line.Y2 = grid1.Height - tweetTypeCount[j, i + 1] * lineHeightMultiplier / 7;
                    }
                    else*/
                    {
                        line.Y1 = historyGrid.ActualHeight - tweetTypeCount[j, i] * lineHeightMultiplier;
                        line.Y2 = historyGrid.ActualHeight - tweetTypeCount[j, i + 1] * lineHeightMultiplier;
                    }

                    line.StrokeThickness = 5.0;

                    Color lineColor = Color.FromArgb(255,255, 255, 255);  
                    if (j == (int)TweetType.Happy)
                    {
                        lineColor = Color.FromArgb(255, 110, 210, 43);
                    }
                    else if (j == (int)TweetType.Angry)
                    {
                        lineColor = Color.FromArgb(255, 200, 38, 38);
                    }
                    else if (j == (int)TweetType.Sad)
                    {
                        lineColor = Color.FromArgb(255, 71, 161, 230);
                    }
                    else if (j == (int)TweetType.Nuetral)
                    {
                        lineColor = Color.FromArgb(255, 225, 225, 225);
                    }
                    
                    line.Stroke = new SolidColorBrush(lineColor);

                    accentLine = new Line();
                    accentLine.X1 = line.X1;
                    accentLine.X2 = line.X2;
                    accentLine.Y1 = line.Y1-1;
                    accentLine.Y2 = line.Y2-1;
                    accentLine.Stroke = new SolidColorBrush(Color.FromArgb(110,50,40,40));
                    accentLine.StrokeThickness =2;

                    historyGrid.Children.Add(line);
                    historyGrid.Children.Add(accentLine);
                }
            }

        }

        private void doSamples()
        {
            textBlockSample.Text = "Happy: " + sampleTweets[(int)TweetType.Happy];
            textBlockSample.Text += "\n\nAngry: " + sampleTweets[(int)TweetType.Angry];
            textBlockSample.Text += "\n\nSad: " + sampleTweets[(int)TweetType.Sad];
            textBlockSample.Text += "\n\nNuetral: " + sampleTweets[(int)TweetType.Nuetral];
        }

        private void textBox1_TextChanged(object sender, TextChangedEventArgs e)
        {

        }

        private void gridSizeChanged(object sender, SizeChangedEventArgs e)
        {
            drawChart(); //redraw chart for new orientation
            double tmpHeight = LayoutRoot.ActualHeight-170;
            if (tmpHeight > 0)
            {
                imageForecast.Height = tmpHeight;
                imageForecast.Stretch = Stretch.Uniform;
               // imageForecast.MaxHeight = tmpHeight;
            }
            // if (args.Orientation == PageOrientation.Landscape)
            {
                //imageForecast.Stretch = Stretch.Uniform;
               // imageForecast.MaxHeight = grid1.ActualHeight;
            }
        }

        protected override void OnOrientationChanged(OrientationChangedEventArgs args)
        {           
            //doForecast();
            base.OnOrientationChanged(args);
        }

        /// <summary>
        /// Handler for the StatusChanged event. This invokes MyStatusChanged on the UI thread and
        /// passes the GeoPositionStatusChangedEventArgs
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void watcher_StatusChanged(object sender, GeoPositionStatusChangedEventArgs e)
        {
            Deployment.Current.Dispatcher.BeginInvoke(() => MyStatusChanged(e));

        }
        /// <summary>
        /// Custom method called from the StatusChanged event handler
        /// </summary>
        /// <param name="e"></param>
        void MyStatusChanged(GeoPositionStatusChangedEventArgs e)
        {
            switch (e.Status)
            {
                case GeoPositionStatus.Disabled:
                    // The location service is disabled or unsupported.
                    // Alert the user
                    //StatusTextBlock.Text = "location is unsupported on this device";
                    //todo don't use gps in search
                    useGPS = false;
                    textBlockGPS.Text = "GPS: Disabled";
                    //Do first forecast
                    startForecast();    
                    break;
                case GeoPositionStatus.Initializing:
                    // The location service is initializing.
                    // Disable the Start Location button
                    //StatusTextBlock.Text = "initializing location service," + accuracyText;
                    break;
                case GeoPositionStatus.NoData:
                    // The location service is working, but it cannot get location data
                    //ToDo Alert the user and enable the Stop Location button
                     useGPS = false;
                    //Do first forecast
                    startForecast();  
                    break;
                case GeoPositionStatus.Ready:
                    // The location service is working and is receiving location data
                    // Show the current position and enable the Stop Location button
                    //StatusTextBlock.Text = "receiving data, " + accuracyText;
                    textBlockGPS.Text = "GPS: Working";
                    break;
            }
        }

        /// <summary>
        /// Handler for the PositionChanged event. This invokes MyStatusChanged on the UI thread and
        /// passes the GeoPositionStatusChangedEventArgs
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void watcher_PositionChanged(object sender, GeoPositionChangedEventArgs<GeoCoordinate> e)
        {
            Deployment.Current.Dispatcher.BeginInvoke(() => MyPositionChanged(e));
        }

        /// <summary>
        /// Custom method called from the PositionChanged event handler
        /// </summary>
        /// <param name="e"></param>
        void MyPositionChanged(GeoPositionChangedEventArgs<GeoCoordinate> e)
        {
            // Update the TextBlocks to show the current location
            latitude = e.Position.Location.Latitude.ToString("0.00000");
            longitude = e.Position.Location.Longitude.ToString("0.00000");
           //TODO: commented out until settings has gps options 
            // useGPS = true;

            //Do first forecast
            startForecast();    
        }



    }
}