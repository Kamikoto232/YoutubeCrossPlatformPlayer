using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Avalonia.Controls;
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
        private bool showDownloadProgressBar;
        private bool allInQueue { get; set; }
        private static string? searchText;
        private ObservableCollection<VideoData>? videoSearchResults;
        private const uint maxResults = 100;
        private int selectedVideoID;
        private MuxedStreamInfo[] currentVideoStreamInfos;
        private DownloadVideoInfo downloadVideoInfo { get; set; } = new DownloadVideoInfo();
        
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

        public bool ShowDownloadProgressBar
        {
            get => showDownloadProgressBar;
            set => this.RaiseAndSetIfChanged(ref showDownloadProgressBar, value);
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
                        VideoSearchResults.Add(new VideoData(video));
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
                        try
                        {
                            var streamManifest =
                                await youtube.Videos.Streams.GetManifestAsync(videoSearchResults[selectedVideoID]
                                    .SearchResult.Url);
                            var videoStreamInfoInfo = streamManifest.GetMuxedStreams().ToArray()[selectedVideoQuality];
                            urls += videoStreamInfoInfo.Url + " ";
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine(e);
                        }
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

        public async void Save()
        {
            if (videoSearchResults == null) return;

            SaveFileDialog saveFileDialog = new SaveFileDialog();
            saveFileDialog.Filters = new List<FileDialogFilter>();
            var filter = new FileDialogFilter();
            filter.Extensions.Add(CurrentVideoStreamInfos[selectedVideoQuality].Container.Name);
            saveFileDialog.Filters.Add(filter);

            var path = await saveFileDialog.ShowAsync(new Window());
            //path += "."+CurrentVideoStreamInfos[selectedVideoQuality].Container.Name;
            if (string.IsNullOrEmpty(path)) return;
            await Task.Run(()=>DownloadTask(path, CurrentVideoStreamInfos[selectedVideoQuality].Url));
        }

        private async void DownloadTask(string path, string url)
        {
            downloadVideoInfo.Downloading = true;
           
            using (var httpClient = new HttpClient())
            {
                using (var request = new HttpRequestMessage(HttpMethod.Get, url))
                {
                    using (Stream contentStream =
                           await (await httpClient.SendAsync(request)).Content.ReadAsStreamAsync(),
                           stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        downloadVideoInfo.TotalBytes = stream.Length;
                        downloadVideoInfo.CurrentBytes = stream.Position;

                        await contentStream.CopyToAsync(stream);
                    }
                }
            }
            
            downloadVideoInfo.Downloading = false;
        }

        public class DownloadVideoInfo : ViewModelBase
        {
            public bool downloading;
            public long totalBytes;
            public long currentBytes;

            public bool Downloading
            {
                get => downloading;
                set => this.RaiseAndSetIfChanged(ref downloading, value);
            }
            public long TotalBytes
            {
                get => totalBytes;
                set => this.RaiseAndSetIfChanged(ref totalBytes, value);
            }
            public long CurrentBytes
            {
                get => currentBytes;
                set => this.RaiseAndSetIfChanged(ref currentBytes, value);
            }
        }

        public class VideoData : ViewModelBase
        {
            public VideoSearchResult SearchResult { get; set; }
            private string previewUrl { get; set; }

            public VideoData(VideoSearchResult searchResult)
            {
                SearchResult = searchResult;
                previewUrl = (SearchResult.Thumbnails[2].Url);
            }
        }
    }
}