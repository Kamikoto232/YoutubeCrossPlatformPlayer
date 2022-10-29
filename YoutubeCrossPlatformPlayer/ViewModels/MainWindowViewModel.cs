using System;
using System.Collections.ObjectModel;
using System.Reactive;
using ReactiveUI;
using YoutubeExplode;
using YoutubeExplode.Search;

namespace YoutubeCrossPlatformPlayer.ViewModels
{
    public class MainWindowViewModel : ViewModelBase
    {
        private readonly YoutubeClient youtube;
        private bool showProgressBar;
        private static string? searchText;
        private ObservableCollection<VideoSearchResult>? videoSearchResults;
        private const uint maxResults = 100;

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

        public ReactiveCommand<Unit, Unit> SearchCommand { get; set; }

        public ObservableCollection<VideoSearchResult>? VideoSearchResults
        {
            get => videoSearchResults;
            set => this.RaiseAndSetIfChanged(ref videoSearchResults, value);
        }

        public MainWindowViewModel()
        {
            SearchCommand = ReactiveCommand.Create(DoSearch);
            youtube = new YoutubeClient();
        }

        private async void DoSearch()
        {
            var searchPhrase = SearchText;
            VideoSearchResults?.Clear();
            VideoSearchResults = new ObservableCollection<VideoSearchResult>();

            ShowProgressBar = true;
            Console.Write("Searching... " + searchPhrase);

            await foreach (ISearchResult result in youtube.Search.GetResultsAsync(searchPhrase))
            {
                if (VideoSearchResults.Count > maxResults) break;
                // Use pattern matching to handle different results (videos, playlists, channels)
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
    }
}