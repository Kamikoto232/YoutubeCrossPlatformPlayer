using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using Avalonia.Media.Imaging;
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
        private bool allInQueue;
        private static string? searchText;
        private ObservableCollection<VideoData>? videoSearchResults;
        private const uint maxResults = 100;
        private int selectedVideoID;
        private MuxedStreamInfo[] currentVideoStreamInfos;
        private int selectedVideoQuality { get; set; }
        private string playerPath = "";

        public string PlayerPath
        {
            get => playerPath;
            set => this.RaiseAndSetIfChanged(ref playerPath, value);
        }

        public int SelectedVideoID
        {
            get => selectedVideoID;
            set
            {
                selectedVideoID = value;
                UpdateSelection();
            }
        }

        public MuxedStreamInfo[] CurrentVideoStreamInfos
        {
            get => currentVideoStreamInfos;
            set => this.RaiseAndSetIfChanged(ref currentVideoStreamInfos, value);
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


        public ObservableCollection<VideoData>? VideoSearchResults
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
            VideoSearchResults = new ObservableCollection<VideoData>();

            ShowProgressBar = true;

            await foreach (ISearchResult result in youtube.Search.GetResultsAsync(searchPhrase))
            {
                if (VideoSearchResults.Count > maxResults) break;

                switch (result)
                {
                    case VideoSearchResult video:
                    {
                        VideoSearchResults.Add(new VideoData(video, this));
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

                ShowProgressBar = false;
            }
        }

        private async void UpdateSelection()
        {
            if (videoSearchResults == null || selectedVideoID < 0 ||
                selectedVideoID > videoSearchResults?.Count) return;
            try
            {
                var streamManifest =
                    await youtube.Videos.Streams.GetManifestAsync(videoSearchResults[selectedVideoID].SearchResult.Url);
                CurrentVideoStreamInfos = streamManifest.GetMuxedStreams().ToArray();
                selectedVideoQuality = 0;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                CurrentVideoStreamInfos = Array.Empty<MuxedStreamInfo>();
            }
        }

        public async void Play()
        {
            if (videoSearchResults == null) return;
            try
            {
                string urls = "";

                if (allInQueue)
                {
                    for (int i = selectedVideoID; i < videoSearchResults.Count; i++)
                    {
                        var streamManifest =
                            await youtube.Videos.Streams.GetManifestAsync(videoSearchResults[selectedVideoID]
                                .SearchResult.Url);
                        var videoStreamInfoInfo = streamManifest.GetMuxedStreams().ToArray()[selectedVideoQuality];
                        urls += videoStreamInfoInfo.Url + " ";
                    }
                }
                else
                {
                    urls = CurrentVideoStreamInfos[selectedVideoQuality].Url;
                }

                var process = new Process
                {
                    StartInfo =
                    {
                        FileName = playerPath,
                        Arguments = urls
                    }
                };
                process.Start();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        public class VideoData
        {
            public VideoSearchResult SearchResult { get; set; }
            private readonly MainWindowViewModel model;
            private Avalonia.Media.Imaging.Bitmap preview = null;

            public Avalonia.Media.Imaging.Bitmap Preview
            {
                get => preview;
                set => model.RaiseAndSetIfChanged(ref preview, value);
            }

            public VideoData(VideoSearchResult searchResult, MainWindowViewModel model)
            {
                SearchResult = searchResult;
                this.model = model;
                DownloadImage(SearchResult.Thumbnails[2].Url);
            }

            public void DownloadImage(string url)
            {
                using (WebClient client = new WebClient())
                {
                    client.DownloadDataAsync(new Uri(url));
                    client.DownloadDataCompleted += DownloadComplete;
                }
            }

            private void DownloadComplete(object sender, DownloadDataCompletedEventArgs e)
            {
                try
                {
                    byte[] bytes = e.Result;

                    Stream stream = new MemoryStream(bytes);
                    preview = Bitmap.DecodeToWidth(stream, 300);
                }
                catch (Exception ex)
                {
                    preview = null; // Could not download...
                    throw;
                }
            }
        }
    }
}