using System;
using System.IO;
using System.IO.IsolatedStorage;
using System.Windows;

using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Net.NetworkInformation;
using Microsoft.Phone.Controls;
using Microsoft.Phone.Tasks;
using System.Globalization;

using System.Xml;
using System.Xml.Linq;

using System.Windows.Media.Imaging; // BitmapImage
using System.Windows.Resources; // StreamResourceInfo
using System.Device.Location;

//For JSON deserlization to object
using Newtonsoft.Json.Linq;
using System.Windows.Navigation;


//Search api https://dev.twitter.com/docs/api/1/get/search
//SEARCH API https://dev.twitter.com/docs/using-search

/*
 *
 * v1 fix layout of samples
 * v1 add timestamp to samples, and maybe username
 * 
 * 
 * v1.2 save current setup as long term forecast.
 * v1.2 +add short term, today only, sort by hours.
 * v1.2 +add settings option for short term or long term
 * v1.2 group samples by day
 * v1.2 look at facebook design, try something similarly pretty: nice top banner
 */

namespace TwitterArt
{
    public partial class MainPage : PhoneApplicationPage
    {
        const int numEmotions = 4; //happy,angry,sad,nuetral
        string[] happyWords = new string[] { "happy", ":)", ":D", ":-)","=)", "great","jolly","cute","cheerful","snug","yay","pleasure","exhilarated", "wonderful", "good", "excellent", "fantastic","smile","awesome","love" }; //,"lol"
        string[] angryWords = new string[] { "angry", "mad", "hate","disgust","rage","destory","kill","fight","shoot","raging","anger","cranky","bitter","mean","annoyed", "frown", "pissed", "stupid", "annoying", "wretched", "shit", "fuck", "asshole", "dick","pussy","cunt","bitch" };        
        string[] sadWords = new string[] { "sad", "depressed", ":(", ":'(", ":-(","=(", "lonely", "death","tear","unhappy", "miserable","blue", "despondent", "desolate", "forlorn", "sorrow", "melancholy", "woeful","morbid", "abject", "deject",
                                            "suffer","sufer", "torment", "agony", "pain","fear", "distres", "grief", "angst", "affliction", "anxiety","miserable", "depres","darkness","scared","crying", "gloom","alone", "morose",  "dismal", "moping", "glum", "unhappy" };
        string[] badWords = new string[] { "shit", "fuck", "asshole", "dick", "pussy", "cunt", "bitch" };

        //v1
        string latitude, longitude;
        List<TwitterItem> sampleTweets = new List<TwitterItem>();
        int[] bestTweet = new int[numEmotions]{0,0,0,0};

        //v2
        TextBlock[] textBlockHistoryLabels = new TextBlock[numDays];


        //Todo: switch to 5 pages from just 1 day.
        //assuming 4 days of results used 
        const int numDays = 4;
        float lineHeightMultiplier;
        //each row corresponds to enum TweetType, col is time interval
        int[,] tweetTypeCount = new int[4,numDays] { { 0, 0, 0, 0 }, { 0, 0, 0, 0 }, { 0, 0, 0, 0 }, { 0, 0, 0, 0 } };
        int[,] tweetTypePercentage = new int[4, numDays] { { 0, 0, 0, 0 }, { 0, 0, 0, 0 }, { 0, 0, 0, 0 }, { 0, 0, 0, 0 } };
        int[] tweetTotalCount = new int[4] { 0, 0, 0, 0 };

        bool[] threadDayComplete = new bool[4] { false, false, false, false };

        public enum TweetType { Happy, Angry, Sad, Nuetral };
        TweetType forecast = TweetType.Nuetral;

        DateTime dateToday = new DateTime(DateTime.Now.Ticks);
        //int today = DateTime.Now.Day;
       // int month = DateTime.Now.Month;
        int tmpHappy,  tmpAngry, tmpSad;
       // int searchDay = numDays;
       // int GETpage = 1;
        const int pagesPerIteration = 1;
        const int numPages =2;

        string searchString="";
//        string sinceId;        
        GeoCoordinateWatcher watcher;
        bool useGPS=false;
        bool networkAvailable = false;
        string geocode = "";

        //TextBlock tmpTxtBlock;

        // Constructor
        public MainPage()
        {
            InitializeComponent();


            textBlockAbout.Text = "Privacy Policy:\nThis application uses your current location to filter Tweets to within 25 miles of your location. Your location is sent to Twitter, but with no associated identifying information. You may disable use of your location via the Location Services above. \n\nAbout: \nThe Happiness Forecast uses some nifty artificial intelligence to sort Tweets based on mood, and then gives you a forecast for the day.\n\nComing Soon: \n+ 24 hour window\n+ Ability to group samples by day or mood";
            //tmpTxtBlock = textBlockHistory;
            for (int i = 0; i < numDays; i++)
            {
                textBlockHistoryLabels[i] = new TextBlock
                {
                    Text = "Home",
                    Margin = new Thickness(90, 25, 0, 0),
                    Height = 61,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Bottom,
                    TextAlignment = TextAlignment.Center,
                    FontSize = 20
                };

            }


                //initialize variables 
                watcher = new GeoCoordinateWatcher(GeoPositionAccuracy.Default);
                watcher.MovementThreshold = 500;

                // Add event handlers for StatusChanged and PositionChanged events
                watcher.StatusChanged += new EventHandler<GeoPositionStatusChangedEventArgs>(watcher_StatusChanged);
                watcher.PositionChanged += new EventHandler<GeoPositionChangedEventArgs<GeoCoordinate>>(watcher_PositionChanged);

                //Loading image
                if (setLoadingImage()) //false means no network connection available
                {
                    //Do first forecast
                    startForecast();
                }
       
            //add listener to grid
            grid1.SizeChanged += new SizeChangedEventHandler(gridSizeChanged);
           // historyGrid.SizeChanged += new SizeChangedEventHandler(gridSizeChanged);
        }

        private void button1_Click(object sender, RoutedEventArgs e)
        {
            //set loading image
            if (!setLoadingImage()) //false means no network connection available
            {
                return;
            }

            masterPivot.SelectedItem = forecastPivot;

            //clear chart
            historyGrid.Children.Clear();

            //empty samples
            sampleTweets.Clear();
            
            //start forecast
            startForecast();
        }

        private void startForecast()
        {
           
           // WebClient client = new WebClient();
           // client.DownloadStringCompleted += new DownloadStringCompletedEventHandler(client_DownloadStringCompleted);
            //reset search days and counts
           // searchDay = numDays-1; //if we're on the 11th and want the 8th, thats 4 days ago, but -3
            tweetTypeCount = new int[4, 4] { { 0, 0, 0, 0 }, { 0, 0, 0, 0 }, { 0, 0, 0, 0 }, { 0, 0, 0, 0 } };
            tweetTypePercentage = new int[4, 4] { { 0, 0, 0, 0 }, { 0, 0, 0, 0 }, { 0, 0, 0, 0 }, { 0, 0, 0, 0 } };
            tweetTotalCount=new int[4] { 0, 0, 0, 0 };
            
            bestTweet = new int[numEmotions] { 0, 0, 0, 0 };
            threadDayComplete = new bool[4] { false, false, false, false };

            geocode = "";

            if (useGPS)
            {
                geocode = "&geocode=" + latitude + "," + longitude + ",25mi";
            }
            //DateTime tmpDate = new DateTime();
            //tmpDate = dateToday.Subtract(TimeSpan.FromDays(searchDay));
           // string twitterGET = "http://search.twitter.com/search.json?q=" + textBox1.Text + "%20since%3A"+dateToday.Year+"-" + tmpDate.Subtract(TimeSpan.FromDays(1)).Month.ToString().PadLeft(2, '0') + "-" + (tmpDate.Subtract(TimeSpan.FromDays(1)).Day) + "%20until%3A"+dateToday.Year+"-" + tmpDate.Month.ToString().PadLeft(2, '0') + "-" + (tmpDate.Day) + "&rpp=100&lang=en&page=1" + geocode + "&result_type=recent";
            
            //client.DownloadStringAsync(new Uri(twitterGET));

            //Start all threads for all days at once.
            for (int i = numDays-1; i >= 0; i--)
            {
                nextDay( i);
            }

      
        }

        private void client_DownloadStringCompleted(object sender, DownloadStringCompletedEventArgs e)
        {
            if (e.Error != null)
            {
                return; //ToDo: better error handling. print a message
            }
            else
            {
                string twitterResults = e.Result;

                JObject root  = JObject.Parse(e.Result);                
                JArray tweetsJArray = JArray.Parse(root["results"].ToString());
                List<TwitterItem> tweets = new List<TwitterItem>();

                string date = "";
                //todo: this part is sort of hacky way of finding the date.
                if (root["next_page"] != null)
                {
                   date = root["next_page"].ToString().Substring(root["next_page"].ToString().IndexOf("until%3") + 8, 10);
                }
                else if (root["refresh_url"]!=null)
                {
                    date = root["refresh_url"].ToString().Substring(root["refresh_url"].ToString().IndexOf("until%3") + 8, 10);                
                }
                else if(root["previous_page"]!=null)
                {
                    date = root["previous_page"].ToString().Substring(root["previous_page"].ToString().IndexOf("until%3") + 8, 10);
                }
                DateTime localSearchDate = dateToday;
                if (date != "")
                {
                    localSearchDate = DateTime.ParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture);  //"Sat, 28 Jan 2012 05:27:42 +0000"//"ddd, dd MMM yyyy HH:mm:ss zzz"
                }


                foreach (JObject tweet in tweetsJArray)
                {
                    tweetTotalCount[(dateToday - localSearchDate).Days]++;

                    TwitterItem tempTweet = new TwitterItem();
                    tempTweet.ImageSource = (tweet["profile_image_url"]).ToString();
                    tempTweet.Message = tweet["text"].ToString();
                    tempTweet.Username = tweet["from_user"].ToString()+":";
                    tempTweet.hourCreated = Int32.Parse(tweet["created_at"].ToString().Substring((tweet["created_at"].ToString().IndexOf(':')-2),2));                   

                    //Set mood and mood score
                    //decide if this tweet is angry, sad, or happy
                    tmpHappy = wordCount(happyWords, tempTweet.Message);
                    tmpAngry = wordCount(angryWords, tempTweet.Message);
                    tmpSad = wordCount(sadWords, tempTweet.Message);

                    if (tmpHappy == tmpAngry && tmpHappy == tmpSad)
                    {
                        tweetTypeCount[(int)TweetType.Nuetral, (dateToday-localSearchDate).Days]++;
                        tempTweet.Mood = TweetType.Nuetral;
                        tempTweet.MoodScore = tmpHappy; //all tmps equal so just choose arbitrary one
                        if (tmpHappy == 0 && (sampleTweets.Count%20==0)) //mod 12 just to prevent too many samples
                        {
                            sampleTweets.Insert(0, tempTweet); //nuetral ones are added to the end of samples
                        }
                        else if (tmpHappy == 0 && (sampleTweets.Count % 11 == 0)) //mod 12 just to prevent too many samples
                        {
                            sampleTweets.Add(tempTweet); //nuetral ones are added to the end of samples
                        }
                    }
                    else if (tmpHappy >= tmpAngry && tmpHappy >= tmpSad) //ordering of ifs skews to favour happy: ie. happy:2 angry:2 sad:1 would be happy
                    {
                        tweetTypeCount[(int)TweetType.Happy,(dateToday-localSearchDate).Days]++; //set back to *2 and 0:1
                        tempTweet.Mood = TweetType.Happy;
                        tempTweet.MoodScore = tmpHappy;
                        if (bestTweet[(int)TweetType.Happy] < tmpHappy)
                        {
                            sampleTweets.Insert(0, tempTweet); 
                            bestTweet[(int)TweetType.Happy] = tmpHappy;
                        }
                        else if (bestTweet[(int)TweetType.Happy] <= tmpHappy && tweetTotalCount[(dateToday - localSearchDate).Days] % 2 == 0)
                        {
                            sampleTweets.Add( tempTweet); ;
                            bestTweet[(int)TweetType.Happy] = tmpHappy;
                        }
                    }
                    else if (tmpAngry >= tmpHappy && tmpAngry >= tmpSad)
                    {
                        tweetTypeCount[(int)TweetType.Angry,(dateToday-localSearchDate).Days]++;
                        tempTweet.Mood = TweetType.Angry;
                        tempTweet.MoodScore = tmpAngry;
                        if (bestTweet[(int)TweetType.Angry] < tmpAngry)
                        {
                            sampleTweets.Insert(0, tempTweet);
                            bestTweet[(int)TweetType.Angry] = tmpAngry;
                        }
                        else if ((bestTweet[(int)TweetType.Angry] <= tmpAngry && tweetTotalCount[(dateToday - localSearchDate).Days] % 2 == 0))
                        {
                            sampleTweets.Add(tempTweet);
                            bestTweet[(int)TweetType.Angry] = tmpAngry;
                        }
                    }
                    else if (tmpSad >= tmpHappy && tmpSad >= tmpAngry)
                    {
                        tweetTypeCount[(int)TweetType.Sad,(dateToday-localSearchDate).Days]++;
                        tempTweet.Mood = TweetType.Sad;
                        tempTweet.MoodScore = tmpSad;
                        if (bestTweet[(int)TweetType.Sad] < tmpSad)
                        {
                            sampleTweets.Insert(0,tempTweet);
                            bestTweet[(int)TweetType.Sad] = tmpSad;
                        }
                        else if (bestTweet[(int)TweetType.Sad] <= tmpSad && tweetTotalCount[(dateToday - localSearchDate).Days] % 2 == 0)
                        {
                            sampleTweets.Add( tempTweet);
                            bestTweet[(int)TweetType.Sad] = tmpSad;
                        }
                    }

                     tempTweet.ImageMood= "Images/" + tempTweet.Mood.ToString().ToLower() + "Facesm.png";
                }

                if (Int32.Parse(root["page"].ToString()) < numPages ) 
                {
                    nextPage(localSearchDate, root["max_id_str"].ToString(), Int32.Parse(root["page"].ToString()));
                }
                else{
                    threadDayComplete[(dateToday-localSearchDate).Days]=true;
                }

              //if all threads done
                bool allDone = true;
                for (int i = 0; i < threadDayComplete.Length; i++)
                {
                    allDone = (allDone && threadDayComplete[i]);
                }
                if (allDone)
                {
                    allThreadsDone();
                }               
        }
        }

        private void allThreadsDone()
        {
             
                    //Set last history x axis label                                        
                    textBlockHistoryLabels[numDays - 1].Text = dateToday.DayOfWeek.ToString().Substring(0, 3);

                    //Normalize values
                    for (int i = 0; i < numDays; i++)
                    {
                        //normalize nuetral
                        tweetTypeCount[(int)TweetType.Nuetral, i] =(int)( ((float)tweetTypeCount[(int)TweetType.Nuetral, i] / (tweetTotalCount[i]==0?1:tweetTotalCount[i]))
                                        * (tweetTypeCount[(int)TweetType.Happy, i] + tweetTypeCount[(int)TweetType.Angry, i] + tweetTypeCount[(int)TweetType.Sad, i]));
                        tweetTotalCount[i] = 0;
                       //create total after normalized nuetral.
                        for (int j = 0; j < numEmotions; j++)
                        {
                            tweetTotalCount[i] += tweetTypeCount[j, i];
                            
                        }
                    }

                    //Censor samples
                    if ((bool)checkBoxCensor.IsChecked)
                    {
                        foreach (TwitterItem tempTweet in sampleTweets)
                        {
                            sanitizeSample(tempTweet);
                        }

                    }

                    //create percentage values
                    for (int i = 0; i < numDays; i++)
                    {
                        for (int j = 0; j < numEmotions; j++)
                        {
                            tweetTypePercentage[j, i] = (100*tweetTypeCount[j, i]) / (tweetTotalCount[i] == 0 ? 1 : tweetTotalCount[i]); //prevent divide by 0 errors                     
                        }
                    }

                    doForecast();
                    drawChart();
                    doSamples();
                }

        private void sanitizeSample( TwitterItem tmpTwt)
        {
            for (int i = 0; i < badWords.Length; i++)
            {
                int lastFind = tmpTwt.Message.ToLower().IndexOf(badWords[i]);
                while (lastFind != -1)
                {
                    if (lastFind > -1)
                    {
                        tmpTwt.Message = tmpTwt.Message.Replace(badWords[i], badWords[i][0] + "***");
                    }
                    lastFind = tmpTwt.Message.ToLower().IndexOf(badWords[i], lastFind + 1);
                }
            }
        }
        private int wordCount(string[] wrdArray, string tmpTwt)
        {
            int tmpNum = 0;
           
            for (int i = 0; i < wrdArray.Length; i++)
            {
                int lastFind=-1;
                lastFind=tmpTwt.IndexOf(wrdArray[i], lastFind+1);

                while(lastFind>-1)
                {
                    tmpNum++;
                    lastFind = tmpTwt.IndexOf(wrdArray[i], lastFind + 1);
                }
            }
            return tmpNum;

        }

        private void nextDay(int localSearchDay)
        {
            WebClient clientTmp = new WebClient();
            clientTmp.DownloadStringCompleted += new DownloadStringCompletedEventHandler(client_DownloadStringCompleted);

            DateTime tmpDate = new DateTime();
            tmpDate = dateToday.Subtract(TimeSpan.FromDays(localSearchDay));
            //Add current day of week to xAxis labels
            textBlockHistoryLabels[numDays - localSearchDay - 1].Text = tmpDate.DayOfWeek.ToString().Substring(0, 3);

            string maxID = ""; //should be blank if first page.
           // GETpage = 1;

            clientTmp.DownloadStringAsync(new Uri("http://search.twitter.com/search.json?q=" + textBox1.Text + "%20since%3A"+dateToday.Year+"-" + tmpDate.Subtract(TimeSpan.FromDays(1)).Month.ToString().PadLeft(2, '0') + "-" + (tmpDate.Subtract(TimeSpan.FromDays(1)).Day) + "%20until%3A"+dateToday.Year+"-" + tmpDate.Month.ToString().PadLeft(2, '0') + "-" + (tmpDate.Day)+"&rpp=100&lang=en&page=1" + maxID + geocode + "&result_type=recent"));            
        }

        private void nextPage(DateTime localSearchDate, string localMaxid, int getPage) //todo get 'next page' from root in parent and pass in and use that.
        {
            WebClient clientTmp = new WebClient();
            clientTmp.DownloadStringCompleted += new DownloadStringCompletedEventHandler(client_DownloadStringCompleted);

            string maxID = ""; //should be blank if first page.   

                getPage=getPage+pagesPerIteration;
                maxID = "&max_id=" + localMaxid;
                clientTmp.DownloadStringAsync(new Uri("http://search.twitter.com/search.json?q=" + textBox1.Text + "%20since%3A" + localSearchDate.Subtract(TimeSpan.FromDays(1)).Year.ToString() + "-" + localSearchDate.Subtract(TimeSpan.FromDays(1)).Month.ToString().PadLeft(2, '0') + "-" + (localSearchDate.Subtract(TimeSpan.FromDays(1)).Day) + "%20until%3A" + dateToday.Year + "-" + localSearchDate.Month.ToString().PadLeft(2, '0') + "-" + (localSearchDate.Day) + "&rpp=100&lang=en&page=" + getPage + maxID + geocode + "&result_type=recent"));            
         

        }


        private bool setLoadingImage()
        {
            networkAvailable = NetworkInterface.GetIsNetworkAvailable();

            //set image
            string imageForecastString = "Images/error.png";
            if (networkAvailable)
            {
                imageForecastString = "Images/loadingFace.png";
            }

            Uri uri = new Uri(imageForecastString, UriKind.Relative);
            StreamResourceInfo resourceInfo = Application.GetResourceStream(uri);
            BitmapImage bmp = new BitmapImage();
            bmp.SetSource(resourceInfo.Stream);
            imageForecast.Source = bmp;
            return networkAvailable;
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
                tmpDelta = tweetTypePercentage[i, numDays - 1] - tweetTypePercentage[i, numDays - 2];
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

            imageForecast.Source = bmp;            
        }

        int findMaxValue(int[,] arry2d)
        {
            int maxValue = 0;
            foreach (int element in arry2d)
            {
                if (element > maxValue)
                {
                    maxValue = element;
                }
            }
            return maxValue;
        }

        //add chart to grid1
        private void drawChart()
        {

            historyGrid.Children.Clear();
            //historyGrid.Children.Add(tmpTxtBlock);
            
            lineHeightMultiplier = (int)((historyGrid.ActualHeight-20 )/ findMaxValue(tweetTypePercentage));

            int numPlots = numDays - 1; //todo: *2 if <12h gradiant -1 because last point is taken care of automagically

            for (int i = 0; i < numPlots; i++)  
            {                

                // = i * (historyGrid.ActualWidth / numPlots - 8) + 20;

                for (int j = 0; j < numEmotions; j++)
                {
                    Line line = new Line();
                    Line accentLine = new Line();

                    line.X1 = i * (historyGrid.ActualWidth / numPlots - 8) + 20;
                    line.X2 = (i + 1) * (historyGrid.ActualWidth / numPlots - 8) + 20;
                                                 
                    line.Y1 = historyGrid.ActualHeight - tweetTypePercentage[j,i] * lineHeightMultiplier-20;
                    line.Y2 = historyGrid.ActualHeight - tweetTypePercentage[j, i+1] * lineHeightMultiplier-20;

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

            //Add labels
            for (int i = 0; i < (numPlots + 1); i++)
            {
                //Draw label on xaxis                
                textBlockHistoryLabels[i].Margin = new Thickness((historyGrid.ActualWidth / numPlots - 18) * i + 20, 5, 0, 0);
                historyGrid.Children.Add(textBlockHistoryLabels[i]);
            }

        }

        private void doSamples()
        {
            //To ensure proper updating. Hack
            TwitterItem[] tmpTweets = new TwitterItem[sampleTweets.Count()];
            sampleTweets.CopyTo(tmpTweets);

            listBox1.ItemsSource = tmpTweets;                      
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
                    
                    //Do first forecast
                    //startForecast();    
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
                   // startForecast();  
                    break;
                case GeoPositionStatus.Ready:
                    // The location service is working and is receiving location data
                    // Show the current position and enable the Stop Location button
                    //StatusTextBlock.Text = "receiving data, " + accuracyText;
                    //textBlockGPS.Text = "GPS: Working";
                    useGPS = true;
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

            if (networkAvailable)
            {
                //Do first forecast
                startForecast();
            }
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            var settings = IsolatedStorageSettings.ApplicationSettings;
            bool tmpCensor = false;
            if (settings.TryGetValue("searchText", out searchString))
            {
                textBox1.Text = searchString;
            }
            if (settings.TryGetValue("useGPS", out useGPS))
            {
                checkBoxGPS.IsChecked = useGPS;
            }
            if (settings.TryGetValue("censorLanguage", out tmpCensor))
            {
                checkBoxCensor.IsChecked = tmpCensor;
            }
        }


        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);


        var settings = IsolatedStorageSettings.ApplicationSettings;

        if (settings.Contains("searchText"))
            settings["searchText"] = textBox1.Text;
        else
            settings.Add("searchText", textBox1.Text);

        if (settings.Contains("useGPS"))
            settings["useGPS"] = checkBoxGPS.IsChecked.Value;
        else
            settings.Add("useGPS", checkBoxGPS.IsChecked.Value);


        if (settings.Contains("censorLanguage"))
            settings["censorLanguage"] = checkBoxCensor.IsChecked.Value;
        else
            settings.Add("censorLanguage", checkBoxCensor.IsChecked.Value);

        settings.Save();
            
        }

        private void checkBoxGPS_Checked(object sender, System.Windows.RoutedEventArgs e)
        {

                useGPS = true;
                watcher.Start();

        }
        private void checkBoxGPS_Unchecked(object sender, System.Windows.RoutedEventArgs e)
        {
            useGPS = false;
            watcher.Stop();
        }

        private void textBlockEmail_Tap(object sender, GestureEventArgs e)
        {
            new EmailComposeTask{
            Subject="Happiness Forecast Feedback",
            Body="",
            To="wp7@smewebsites.com"
            }.Show();
        }
    }
}