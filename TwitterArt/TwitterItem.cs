using System;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace TwitterArt
{
    public class TwitterItem
    {
        public string Username { get; set; }
        public string Message { get; set; }
        public string ImageSource { get; set; }
        public int hourCreated { get; set; }
        public int dayCreated { get; set; }
        public string ImageMood { get; set; }
        public MainPage.TweetType Mood { get; set; }
        public int MoodScore { get; set; }
    }

    // http://geekswithblogs.net/tmurphy/archive/2012/01/13/twitter-search-json-deserialization.aspx
    public class RootObject
    {
        public double completed_in { get; set; }
        public long max_id { get; set; }
        public string max_id_str { get; set; }
        public string next_page { get; set; }
        public int page { get; set; }
        public string query { get; set; }
        public string refresh_url { get; set; }
        public TwitterItem[] results { get; set; }
        public int results_per_page { get; set; }
        public int since_id { get; set; }
        public string since_id_str { get; set; }
    }
}
