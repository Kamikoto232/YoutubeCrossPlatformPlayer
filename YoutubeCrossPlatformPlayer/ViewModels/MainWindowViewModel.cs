using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using ReactiveUI;
using YoutubeExplode;
using YoutubeExplode.Search;
using YoutubeExplode.Videos.Streams;

namespace YoutubeCrossPlatformPlayer.ViewModels
{
    public class MainWindowViewModel : ViewModelBase
    {
        private readonly YoutubeClient youtube;
        private bool showProgressBar;
        private static string? searchText;
        private ObservableCollection<VideoSearchResult>? videoSearchResults;
        private const uint maxResults = 100;
        private int selectedVideoID;
        private MuxedStreamInfo[] currentVideoInfo;
        private int selectedVideoQuality { get; set; }

        public int SelectedVideoID
        {
            get => selectedVideoID;
            set
            {
                selectedVideoID = value;
                UpdateSelection();
            }
        }

        public MuxedStreamInfo[] CurrentVideoInfo
        {
            get => currentVideoInfo;
            set => this.RaiseAndSetIfChanged(ref currentVideoInfo, value);
        }

        public bool ShowProgressBar
        {
            get => showProgressBar;
            set => this.RaiseAndSetIfChanged(ref showProgressBar, value);
        }

        public static string? SearchText
        {
            get => searchText;
            set => searchText = value;
        }


        public ObservableCollection<VideoSearchResult>? VideoSearchResults
        {
            get => videoSearchResults;
            set => this.RaiseAndSetIfChanged(ref videoSearchResults, value);
        }

        public MainWindowViewModel()
        {
            youtube = new YoutubeClient();
        }

        public async void DoSearch()
        {
            var searchPhrase = SearchText;
            VideoSearchResults?.Clear();
            VideoSearchResults = new ObservableCollection<VideoSearchResult>();

            ShowProgressBar = true;
            Console.Write("Searching... " + searchPhrase);

            await foreach (ISearchResult result in youtube.Search.GetResultsAsync(searchPhrase))
            {
                if (VideoSearchResults.Count > maxResults) break;

                switch (result)
                {
                    case VideoSearchResult video:
                    {
                        VideoSearchResults.Add(video);
                        break;
                    }
                    case PlaylistSearchResult playlist:
                    {
                        var id = playlist.Id;
                        var title = playlist.Title;
                        break;
                    }
                    case ChannelSearchResult channel:
                    {
                        var id = channel.Id;
                        var title = channel.Title;
                        break;
                    }
                }

                Console.Write("Searching... end ");

                ShowProgressBar = false;
            }
        }

        private async void UpdateSelection()
        {
            if (videoSearchResults == null || selectedVideoID < 0 || selectedVideoID > videoSearchResults?.Count) return;
            try
            {
                var streamManifest = await youtube.Videos.Streams.GetManifestAsync(videoSearchResults[selectedVideoID].Url);
                CurrentVideoInfo = streamManifest.GetMuxedStreams().ToArray();
                selectedVideoQuality = 0;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                CurrentVideoInfo = Array.Empty<MuxedStreamInfo>();
            }
            
        }
        
        public void Play()
        {
            if (videoSearchResults == null) return;
            try
            {
                var streamInfo = CurrentVideoInfo[selectedVideoQuality];
            
                var process = new Process
                {
                    StartInfo =
                    {
                        FileName = @"C:\Program Files\VideoLAN\VLC\vlc.exe",
                        Arguments = streamInfo.Url
                    }
                };
                process.Start();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
            
        }
    }
}