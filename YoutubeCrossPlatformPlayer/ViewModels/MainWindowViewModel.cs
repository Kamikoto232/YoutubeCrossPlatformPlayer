using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Controls;
using Microsoft.CodeAnalysis.CSharp;
using ReactiveUI;
using YoutubeExplode;
using YoutubeExplode.Search;
using YoutubeExplode.Videos.Streams;

namespace YoutubeCrossPlatformPlayer.ViewModels
{
    public class MainWindowViewModel : ViewModelBase
    {
        private readonly YoutubeClient youtube;
        private const byte maxResults = 100;
        private StreamQuality streamQuality { get; set; } = new StreamQuality();

        private int selectedVideoQuality { get; set; }
        private string playerPath = @"C:\Program Files\VideoLAN\VLC\vlc.exe";

        public string PlayerPath
        {
            get => playerPath;
            set => this.RaiseAndSetIfChanged(ref playerPath, value);
        }

        private ObservableCollection<DownloadVideoInfo>? downloadVideoInfos;

        public ObservableCollection<DownloadVideoInfo>? DownloadVideoInfos
        {
            get => downloadVideoInfos;
            set => this.RaiseAndSetIfChanged(ref downloadVideoInfos, value);
        }

        private ObservableCollection<VideoData>? selectedVideos;

        public ObservableCollection<VideoData>? SelectedVideos
        {
            get => selectedVideos;
            set
            {
                selectedVideos = value;
                //UpdateSelection();
            }
        }

        private IStreamInfo[] currentStreamInfos = Array.Empty<IStreamInfo>();

        public IStreamInfo[] CurrentVideoStreamInfos
        {
            get => currentStreamInfos;
            set => this.RaiseAndSetIfChanged(ref currentStreamInfos, value);
        }

        private bool showProgressBar;

        public bool ShowProgressBar
        {
            get => showProgressBar;
            set => this.RaiseAndSetIfChanged(ref showProgressBar, value);
        }

        private bool processing;

        public bool Processing
        {
            get => processing;
            set => this.RaiseAndSetIfChanged(ref processing, value);
        }

        private string? searchText;

        public string? SearchText
        {
            get => searchText;
            set => this.RaiseAndSetIfChanged(ref searchText, value);
        }

        private ObservableCollection<VideoData>? videoSearchResults;

        public ObservableCollection<VideoData>? VideoSearchResults
        {
            get => videoSearchResults;
            set => this.RaiseAndSetIfChanged(ref videoSearchResults, value);
        }

        public MainWindowViewModel()
        {
            youtube = new YoutubeClient();
            SelectedVideos = new ObservableCollection<VideoData>();
            DownloadVideoInfos = new ObservableCollection<DownloadVideoInfo>();
            VideoSearchResults = new ObservableCollection<VideoData>();
        }

        public async void DoSearch()
        {
            if (string.IsNullOrEmpty(SearchText)) return;
            var searchPhrase = SearchText;
            VideoSearchResults?.Clear();

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
                    /*case PlaylistSearchResult playlist:
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
                    }*/
                }

                ShowProgressBar = false;
            }
        }

        // private async void UpdateSelection()
        // {
        //     if (videoSearchResults?.Count == 0 || SelectedVideoIDs.Count == 0) return;
        //     try
        //     {
        //         var streamManifest =
        //             await youtube.Videos.Streams.GetManifestAsync(videoSearchResults[SelectedVideoIDs[0]].SearchResult
        //                 .Url);
        //         CurrentVideoStreamInfos = streamManifest.GetMuxedStreams().ToArray();
        //         selectedVideoQuality = 0;
        //     }
        //     catch (Exception e)
        //     {
        //         Console.WriteLine(e);
        //         CurrentVideoStreamInfos = Array.Empty<IStreamInfo>();
        //         throw;
        //     }
        // }

        public async void Play()
        {
            if (videoSearchResults?.Count == 0 || selectedVideos?.Count == 0) return;
            Processing = true;

            try
            {
                var arrayStreamSelectionDatas = await GetVideosSelectionDatasAsync(selectedVideos.ToArray());
                var stringBuilder = new StringBuilder(arrayStreamSelectionDatas.Length);

                foreach (StreamSelectionData videoSelectionData in arrayStreamSelectionDatas)
                {
                    stringBuilder.Append($" {videoSelectionData.Url}");
                }

                string urls = stringBuilder.ToString();
                Processing = false;

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
            if (videoSearchResults?.Count == 0 || selectedVideos.Count == 0) return;

            string? path = await OpenSaveDialog();
            if (string.IsNullOrEmpty(path)) return;

            var videosOnly = selectedVideos.Where(v => v.IsLive == false).ToArray();

            foreach (var video in videosOnly)
            {
                downloadVideoInfos.Add(new DownloadVideoInfo(youtube, video.SearchResult.Title,
                    downloadVideoInfos));
            }

            if (downloadVideoInfos.Count == 0) return;


            var arrayStreamSelectionDatas = await GetVideosSelectionDatasAsync(videosOnly);

            for (int i = 0; i < arrayStreamSelectionDatas.Length; i++)
            {
                StreamSelectionData streamSelection = arrayStreamSelectionDatas[i];
                string fileName = GetValidName(streamSelection.Title);
                string filePath = GetFileName(path, fileName, streamSelection.StreamInfo.Container.Name);
                downloadVideoInfos[i].StartDownload(streamSelection.StreamInfo, filePath);
            }
        }

        private string GetFileName(string path, string Name, string container)
        {
            return $@"{path}\{Name}.{container}";
        }

        private string GetValidName(string Name)
        {
            return Path.GetInvalidFileNameChars().Aggregate(Name,
                (current, invalidChar) => current.Replace(invalidChar.ToString(), "_"));
        }

        private async Task<StreamSelectionData[]> GetVideosSelectionDatasAsync(VideoData[] selection)
        {
            var videoSelectionDatas = new List<StreamSelectionData>();
            foreach (VideoData videoData in selection)
            {
                try
                {
                    string? url = null;
                    IStreamInfo streamInfo = null;
                    if (videoData.IsLive)
                        url = await youtube.Videos.Streams.GetHttpLiveStreamUrlAsync(videoData.SearchResult.Id);
                    else
                    {
                        var streamInfos = await GetStreamInfos(videoData.SearchResult.Url);

                        streamInfo =
                            streamQuality.AudioOnly
                                ? streamInfos.GetWithHighestBitrate()
                                : streamInfos[streamQuality.GetSelectrdStreamQualityIndex((streamInfos).Length)];
                        url = streamInfo.Url;
                    }

                    var videoSelectionData = new StreamSelectionData(streamInfo, videoData.SearchResult.Title, url);
                    videoSelectionDatas.Add(videoSelectionData);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
            }

            return videoSelectionDatas.ToArray();
        }

        private async Task<IStreamInfo[]> GetStreamInfos(string url)
        {
            var streamInfos = Array.Empty<IStreamInfo>();

            try
            {
                var streamManifest = await youtube.Videos.Streams.GetManifestAsync(url);

                if (streamQuality.AudioOnly)
                {
                    streamInfos = streamManifest.GetAudioOnlyStreams().ToArray();
                }
                else
                {
                    streamInfos = streamManifest.GetMuxedStreams().ToArray();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }

            return streamInfos;
        }

        private async Task<string?> OpenSaveDialog()
        {
            return await OpenSelectFolderDialog();
        }

        private async Task<string?> OpenSaveFileDialog()
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog
            {
                Title = "Выберите место сохранения видео",
                Directory = Environment.CurrentDirectory,
                Filters = new List<FileDialogFilter>()
                {
                    new FileDialogFilter
                    {
                        Extensions = new List<string> { CurrentVideoStreamInfos[selectedVideoQuality].Container.Name }
                    }
                }
            };
            string? path = await saveFileDialog.ShowAsync(new Window());
            return path;
        }

        private async Task<string?> OpenSelectFolderDialog()
        {
            var saveFileDialog = new OpenFolderDialog
            {
                Title = "Выберите место сохранения видео",
                Directory = Environment.CurrentDirectory,
            };
            string? path = await saveFileDialog.ShowAsync(new Window());
            return path;
        }


        public class DownloadVideoInfo : ViewModelBase
        {
            private YoutubeClient youtubeClient;
            private ObservableCollection<DownloadVideoInfo> collection;

            public DownloadVideoInfo(YoutubeClient youtubeClient, string title,
                ObservableCollection<DownloadVideoInfo> collection)
            {
                this.youtubeClient = youtubeClient;
                this.collection = collection;
                this.title = title;
                Initalizing = true;
            }

            public void StartDownload(IStreamInfo streamInfo, string filePath)
            {
                Initalizing = false;
                Download(youtubeClient, streamInfo, filePath, collection);
            }

            private async void Download(YoutubeClient youtubeClient, IStreamInfo streamInfo, string filePath,
                ObservableCollection<DownloadVideoInfo> collection)
            {
                await youtubeClient.Videos.Streams.DownloadAsync(streamInfo, filePath,
                    new Progress<double>(d =>
                    {
                        Progress = d;
                        ProgressText = d.ToString("P");
                    }));
                Completed = true;
                collection.Remove(this);
            }

            private string title;

            public string Title
            {
                get => title;
                set => this.RaiseAndSetIfChanged(ref title, value);
            }

            private bool initalizing;

            public bool Initalizing
            {
                get => initalizing;
                set => this.RaiseAndSetIfChanged(ref initalizing, value);
            }


            private bool completed;

            public bool Completed
            {
                get => completed;
                set => this.RaiseAndSetIfChanged(ref completed, value);
            }

            private string progressText;

            public string ProgressText
            {
                get => progressText;
                set => this.RaiseAndSetIfChanged(ref progressText, value);
            }

            private double progress;

            public double Progress
            {
                get => progress;
                set => this.RaiseAndSetIfChanged(ref progress, value);
            }
        }

        public class VideoData : ViewModelBase
        {
            public VideoSearchResult SearchResult { get; set; }
            private string previewUrl { get; set; }

            private bool isLive = true;

            public bool IsLive
            {
                get { return isLive; }
                set { this.RaiseAndSetIfChanged(ref isLive, value); }
            }

            public VideoData(VideoSearchResult searchResult)
            {
                SearchResult = searchResult;
                previewUrl = (SearchResult.Thumbnails[2].Url);
                IsLive = SearchResult.Duration == null;
            }
        }

        public struct StreamSelectionData
        {
            public IStreamInfo StreamInfo;
            public string Title;
            public string? Url;

            public StreamSelectionData(IStreamInfo streamInfo, string title, string? url)
            {
                StreamInfo = streamInfo;
                Title = title;
                Url = url;
            }
        }

        private class StreamQuality : ViewModelBase
        {
            private bool hq = true;

            public bool HQ
            {
                get { return hq; }
                set
                {
                    QualityString = "HQ";
                    this.RaiseAndSetIfChanged(ref hq, value);
                }
            }

            private bool mq;

            public bool MQ
            {
                get { return mq; }
                set
                {
                    QualityString = "MQ";
                    this.RaiseAndSetIfChanged(ref mq, value);
                }
            }

            private bool lq;

            public bool LQ
            {
                get { return lq; }
                set
                {
                    QualityString = "LQ";
                    this.RaiseAndSetIfChanged(ref lq, value);
                }
            }

            private bool audioOnly;

            public bool AudioOnly
            {
                get { return audioOnly; }
                set
                {
                    QualityString = "Audio";
                    this.RaiseAndSetIfChanged(ref audioOnly, value);
                }
            }


            private string qualityString = "HQ";

            public string QualityString
            {
                get => qualityString;
                set => this.RaiseAndSetIfChanged(ref qualityString, value);
            }


            public int GetSelectrdStreamQualityIndex(int maxQualityIndex)
            {
                int quality = maxQualityIndex - 1;
                if (MQ) quality = 1;
                if (LQ) quality = 0;
                return quality;
            }

            public string GetSelectrdStreamQualityString()
            {
                string quality = "HQ";

                if (MQ) quality = "MQ";
                if (LQ) quality = "LQ";
                if (AudioOnly) quality = "Audio";
                return quality;
            }
        }
    }
}