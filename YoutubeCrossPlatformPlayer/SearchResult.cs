using System;
using ReactiveUI;
using YoutubeCrossPlatformPlayer.ViewModels;
using YoutubeExplode;
using YoutubeExplode.Channels;
using YoutubeExplode.Common;
using YoutubeExplode.Search;
using YoutubeExplode.Videos;

namespace YoutubeCrossPlatformPlayer;

public class SearchResultBase : ViewModelBase
{
    private ResultType type;

    public ResultType Type
    {
        get => type;
        set => this.RaiseAndSetIfChanged(ref type, value);
    }

    public ISearchResult SearchResult { get; set; }

    private Info inform;

    public Info Inform
    {
        get => inform;
        set => this.RaiseAndSetIfChanged(ref inform, value);
    }

    private ViewParametersData viewParameters;

    public ViewParametersData ViewParameters
    {
        get => viewParameters;
        set => this.RaiseAndSetIfChanged(ref viewParameters, value);
    }

    public SearchResultBase(ISearchResult searchResult, YoutubeClient youtubeClient)
    {
        SearchResult = searchResult;
        switch (searchResult)
        {
            case VideoSearchResult result:
                Type = result.Duration == null ? ResultType.Live : ResultType.Video;
                Inform = new Info(result.Title, result.Author, result.Thumbnails[2].Url, result.Duration);
                break;
            case PlaylistSearchResult result:
                Type = ResultType.Playlist;
                Inform = new Info(result.Title, result.Author, result.Thumbnails[2].Url);
                break;
            case ChannelSearchResult result:
                Type = ResultType.Channel;
                Inform = new Info(result.Title, new Author(result.Id, result.Title),
                    result.Thumbnails.GetWithHighestResolution().Url);
                break;
        }

        ViewParameters = new ViewParametersData(Type);
        UpdateMetadata(youtubeClient);
    }

    private async void UpdateMetadata(YoutubeClient youtubeClient)
    {
        switch (Type)
        {
            case ResultType.Playlist:
                var videos = await youtubeClient.Playlists.GetVideosAsync(SearchResult.Url);
                Inform = new Info(Inform,(ulong)videos.Count);
                break;
        }
    }

    public enum ResultType : byte
    {
        Video,
        Live,
        Playlist,
        Channel
    }

    public VideoId GetLiveID()
    {
        if ((Type == ResultType.Live))
            return ((VideoSearchResult)SearchResult).Id;

        throw new Exception();
    }

    public struct ViewParametersData
    {
        public bool IsLive { get; set; }
        public bool IsVideo { get; set; }
        public bool IsPlaylist { get; set; }
        public bool IsChannel { get; set; }

        public ViewParametersData(ResultType type)
        {
            IsLive = type == ResultType.Live;
            IsVideo = type == ResultType.Video;
            IsPlaylist = type == ResultType.Playlist;
            IsChannel = type == ResultType.Channel;
        }
    }

    public struct Info
    {
        public string Title { get; set; }
        public Author Author { get; set; }
        public string PrevievUrl { get; set; }
        public TimeSpan? Duration { get; set; }
        public ulong PlaylistLenght { get; set; }

        public Info(string title, Author? author, string previevUrl, TimeSpan? duration = null)
        {
            Title = title;
            Author = author ?? new Author(new ChannelId(), "null");
            PrevievUrl = previevUrl;
            Duration = duration;
            PlaylistLenght = 0;
        }

        public Info(Info info, ulong playlistLenght) : this()
        {
            this = info;
            PlaylistLenght = playlistLenght;
        }
    }
}