﻿#region

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Timers;
using Windows.Graphics.Imaging;
using Windows.Networking.BackgroundTransfer;
using Windows.Storage;
using Windows.Storage.Streams;
using HyPlayer.Classes;
using Microsoft.Toolkit.Uwp.Helpers;
using NeteaseCloudMusicApi;
using ATL;
using ATL.AudioData;

#endregion

namespace HyPlayer.HyPlayControl;

internal class DownloadObject
{
    private static readonly Dictionary<string, Guid> CodecIds = new()
    {
        { "image/pjpeg", BitmapDecoder.JpegDecoderId },
        { "image/x-png", BitmapDecoder.PngDecoderId },
        { "image/webp", BitmapDecoder.WebpDecoderId }
    };

    public bool completedFired;
    private PlayItem dontuseme;
    public DownloadOperation downloadOperation;

    public string filename;

    public string fullpath;
    public ulong HavedSize;
    public NCSong ncsong;

    public int progress;

    // 0 - 排队 1 - 下载中 2 - 下载完成  3 - 暂停
    public int Status;
    public ulong TotalSize;

    public DownloadObject(NCSong song)
    {
        ncsong = song;
    }

    private void Wc_DownloadFileCompleted()
    {
        DownloadManager.WritingTasks.Add(Task.Run(async () =>
        {
            if (Common.Setting.downloadLyric)
                await DownloadLyric().ConfigureAwait(false);
            if (Common.Setting.writedownloadFileInfo)
                await WriteInfoToFile().ConfigureAwait(false);
            DownloadManager.WritingTasks.RemoveAll(t => t.IsCompleted);
        }));

        /*
        try
        {
            var downloadToastContent = new ToastContent
            {
                Visual = new ToastVisual
                {
                    BindingGeneric = new ToastBindingGeneric
                    {
                        Children =
                        {
                            new AdaptiveText
                            {
                                Text = "下载完成",
                                HintStyle = AdaptiveTextStyle.Header
                            },
                            new AdaptiveText
                            {
                                Text = filename
                            }
                        }
                    }
                },
                Launch = "",
                Scenario = ToastScenario.Reminder,
                Audio = new ToastAudio { Silent = true }
            };
            var toast = new ToastNotification(downloadToastContent.GetXml())
            {
                Tag = "HyPlayerDownloadDone",
                Data = new NotificationData()
            };
            toast.Data.SequenceNumber = 0;
            var notifier = ToastNotificationManager.CreateToastNotifier();
            notifier.Show(toast);
            
        }
        catch (Exception ex)
        {
            Common.AddToTeachingTipLists(ex.Message, (ex.InnerException ?? new Exception()).Message);
        }
        */
        Common.AddToTeachingTipLists("下载完成", filename);
    }

    private Task WriteInfoToFile()
    {
        return Task.Run(async () =>
        {
            try
            {
                /*
                var streamAbscraction = new UwpStorageFileAbstraction(
                    (await downloadOperation.GetResultRandomAccessStreamReference().OpenReadAsync()).AsStreamForRead(),
                    await downloadOperation.ResultFile.OpenStreamForWriteAsync(), downloadOperation.ResultFile.Name);
                    */
                var resultFileStream = (await downloadOperation.ResultFile.OpenAsync(FileAccessMode.ReadWrite)).AsStream();
                Track theTrack = new Track(resultFileStream, downloadOperation.ResultFile.ContentType);
                if (Common.Setting.write163Info)
                    The163KeyHelper.TrySetMusicInfo(theTrack, dontuseme);
                //写相关信息
                theTrack.Album = ncsong.Album.name;
                theTrack.Artist = string.Join(";",ncsong.Artist.Select(t => t.name).ToArray());
                theTrack.Title = ncsong.songname;
                theTrack.TrackNumber = ncsong.TrackId == -1 ? ncsong.Order + 1 : ncsong.TrackId;
                theTrack.DiscNumber = int.Parse(Regex.Match(ncsong.CDName, "[0-9]+").Value);
                PictureInfo pic;
                if (!DownloadManager.AlbumPicturesCache.ContainsKey(ncsong.Album.id))
                {
                    var ras = RandomAccessStreamReference.CreateFromUri(new Uri(ncsong.Album.cover + "?param=" +
                        StaticSource
                            .PICSIZE_DOWNLOAD_ALBUMCOVER));
                    var httpStream = await ras.OpenReadAsync();
                    IRandomAccessStream outputStream;
                    if (httpStream.ContentType != "image/pjpeg")
                    {
                        var bitmapInput =
                            await (await BitmapDecoder.CreateAsync(CodecIds[httpStream.ContentType], httpStream))
                                .GetSoftwareBitmapAsync();
                        outputStream = new InMemoryRandomAccessStream();
                        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.JpegEncoderId, outputStream);
                        encoder.SetSoftwareBitmap(bitmapInput);
                        await encoder.FlushAsync();
                    }
                    else
                    {
                        outputStream = httpStream;
                    }
                    var tmp = outputStream.AsStream();
                    pic = PictureInfo.fromBinaryData(tmp , (int)tmp.Length , PictureInfo.PIC_TYPE.Front , MetaDataIOFactory.TagType.ANY , 05);
                    DownloadManager.AlbumPicturesCache[ncsong.Album.id] = pic;
                }
                else
                {
                    pic = DownloadManager.AlbumPicturesCache[ncsong.Album.id];
                }
                theTrack.EmbeddedPictures.Clear();
                theTrack.EmbeddedPictures.Add(pic);
                await Task.Run(() => theTrack.Save());
                resultFileStream.Close();
            }
            catch (Exception ex)
            {
                Common.ErrorMessageList.Add("写入音乐信息时出现错误" + ex.Message);
                Common.AddToTeachingTipLists("写入音乐信息时出现错误", ex.Message);
            }
        });
    }

    private Task DownloadLyric()
    {
        //下载歌词
        return Task.Run(async () =>
        {
            try
            {
                var json = await Common.ncapi.RequestAsync(CloudMusicApiProviders.Lyric,
                    new Dictionary<string, object> { { "id", ncsong.sid } });
                if (!(json.ContainsKey("nolyric") && json["nolyric"].ToString().ToLower() == "true") &&
                    !(json.ContainsKey("uncollected") && json["uncollected"].ToString().ToLower() == "true"))
                {
                    if (json["lrc"]["lyric"].ToString().Contains("[99:00.00]纯音乐，请欣赏"))
                        // 这个也是纯音乐
                        return;

                    var lrc = Utils.ConvertPureLyric(json["lrc"]["lyric"].ToString());
                    if (Common.Setting.downloadTranslation && json["tlyric"]?["lyric"] != null)
                        Utils.ConvertTranslation(json["tlyric"]["lyric"].ToString(), lrc);
                    var lrctxt = string.Join("\r\n", lrc.Select(t =>
                    {
                        if (t.HaveTranslation && !string.IsNullOrWhiteSpace(t.Translation))
                            return "[" + t.LyricTime.ToString(@"mm\:ss\.ff") + "]" + t.PureLyric + " 「" +
                                   t.Translation + "」";
                        return "[" + t.LyricTime.ToString(@"mm\:ss\.ff") + "]" + t.PureLyric;
                    }));
                    if (string.IsNullOrWhiteSpace(lrctxt)) return;
                    var sf = await (await StorageFolder.GetFolderFromPathAsync(Path.GetDirectoryName(fullpath)))
                        .CreateFileAsync(
                            Path.GetFileName(Path.ChangeExtension(fullpath, "lrc")),
                            CreationCollisionOption.ReplaceExisting);
                    if (Common.Setting.usingGBK)
                        await FileIO.WriteBytesAsync(sf,
                            Encoding.Convert(Encoding.UTF8, Encoding.GetEncoding("GBK"),
                                Encoding.UTF8.GetBytes(lrctxt)));
                    else
                        await FileIO.WriteTextAsync(sf, lrctxt);
                }
            }
            catch (Exception ex)
            {
                Common.AddToTeachingTipLists(ex.Message, (ex.InnerException ?? new Exception()).Message);
            }
        });
    }

    private void Wc_DownloadProgressChanged(DownloadOperation obj)
    {
        if (obj.Progress.TotalBytesToReceive == 0) return;
        TotalSize = obj.Progress.TotalBytesToReceive;
        HavedSize = obj.Progress.BytesReceived;
        progress = (int)(obj.Progress.BytesReceived * 100 / obj.Progress.TotalBytesToReceive);
        if (HavedSize == TotalSize)
        {
            if (Status == 2) return;
            Status = 2;
            completedFired = true;
        }
    }

    public static void DownloadStartToast(string songname)
    {
        /*
        var downloadToastContent = new ToastContent
        {
            Visual = new ToastVisual
            {
                BindingGeneric = new ToastBindingGeneric
                {
                    Children =
                    {
                        new AdaptiveText
                        {
                            Text = "下载开始",
                            HintStyle = AdaptiveTextStyle.Header
                        },
                        new AdaptiveText
                        {
                            Text = songname
                        }
                    }
                }
            },
            Launch = "",
            Scenario = ToastScenario.Reminder,
            Audio = new ToastAudio { Silent = true }
        };
        var toast = new ToastNotification(downloadToastContent.GetXml())
        {
            Tag = "HyPlayerDownloadStart",
            Data = new NotificationData()
        };

        toast.Data.SequenceNumber = 0;
        var notifier = ToastNotificationManager.CreateToastNotifier();
        notifier.Show(toast);
        */
        Common.AddToTeachingTipLists("下载开始", "歌曲" + songname + "下载开始");
    }

    public async Task StartDownload()
    {
        if (downloadOperation != null) return;
        Status = 1;
        try
        {
            filename = Common.Setting.downloadFileName
                .Replace("{$SINGER}", string.Join(';', ncsong.Artist.Select(t => t.name)).EscapeForPath())
                .Replace("{$SONGNAME}", ncsong.songname.EscapeForPath())
                .Replace("{$ALBUM}", ncsong.Album.name.EscapeForPath())
                .Replace("{$INDEX}",
                    (ncsong.TrackId == -1 ? ncsong.Order + 1 : ncsong.TrackId).ToString().EscapeForPath())
                .Replace("{$CDNAME}", ncsong.CDName?.EscapeForPath());
            var folderName = Common.Setting.downloadDir;
            var nowFolder = await StorageFolder.GetFolderFromPathAsync(folderName);
            var ses = filename.Replace('\\', '/').Split('/');
            for (var index = 0; index < ses.Length - 1; index++)
            {
                var s = ses[index];
                folderName += "/" + s;
                nowFolder = await nowFolder.CreateFolderAsync(s, CreationCollisionOption.OpenIfExists);
            }

            if (await nowFolder.FileExistsAsync(Path.GetFileName(filename + ".mp3")) ||
                await nowFolder.FileExistsAsync(Path.GetFileName(filename + ".flac")))
                switch (Common.Setting.downloadNameOccupySolution)
                {
                    case 0:
                        Common.AddToTeachingTipLists("文件已存在，自动跳过", ncsong.songname + "\n已自动将其从下载列表中移除");
                        DownloadManager.DownloadLists.Remove(DownloadManager.DownloadLists.FirstOrDefault());
                        return;
                    case 1:
                        await (await nowFolder.GetFileAsync(Path.GetFileName(filename))).DeleteAsync();
                        break;
                    case 2:
                        filename = Path.GetFileNameWithoutExtension(filename) + ncsong.sid;
                        break;
                }

            var json = await Common.ncapi.RequestAsync(CloudMusicApiProviders.SongUrl,
                new Dictionary<string, object> { { "id", ncsong.sid }, { "br", Common.Setting.downloadAudioRate } });

            if (json["data"][0]["code"].ToString() != "200")
            {
                Common.AddToTeachingTipLists("无法下载", "无法下载歌曲 " + ncsong.songname + "\n已自动将其从下载列表中移除");
                DownloadManager.DownloadLists.Remove(DownloadManager.DownloadLists.FirstOrDefault());
                return; //未获取到
            }

            filename += "." + json["data"][0]["type"].ToString().ToLowerInvariant();
            dontuseme = new PlayItem
            {
                Bitrate = json["data"][0]["br"].ToObject<int>(),
                Tag = "下载",
                Album = ncsong.Album,
                Artist = ncsong.Artist,
                SubExt = json["data"][0]["type"].ToString().ToLowerInvariant(),
                Id = ncsong.sid,
                Name = ncsong.songname,
                Type = HyPlayItemType.Netease,
                TrackId = ncsong.TrackId,
                CDName = ncsong.CDName,
                Url = json["data"][0]["url"].ToString(),
                LengthInMilliseconds = ncsong.LengthInMilliseconds,
                Size = json["data"][0]["size"].ToString()
                //md5 = json["data"][0]["md5"].ToString()
            };

            downloadOperation = DownloadManager.Downloader.CreateDownload(
                new Uri(json["data"][0]["url"].ToString()),
                await nowFolder.CreateFileAsync(Path.GetFileName(filename))
            );
            fullpath = downloadOperation.ResultFile.Path;
            //downloadOperation.IsRandomAccessRequired = true;
            var process = new Progress<DownloadOperation>(Wc_DownloadProgressChanged);
            DownloadStartToast(filename);
            await downloadOperation.StartAsync().AsTask(process);
            Wc_DownloadFileCompleted();
        }
        catch (Exception ex)
        {
            Common.AddToTeachingTipLists("无法下载歌曲 " + ncsong.songname + "\n已自动将其从下载列表中移除", ex.Message);
            DownloadManager.DownloadLists.Remove(DownloadManager.DownloadLists.FirstOrDefault());
            Common.ErrorMessageList.Add("无法下载歌曲 " + ncsong.songname + "\n已自动将其从下载列表中移除" + ex.Message);
        }
    }
}

internal static class DownloadManager
{
    private static readonly Timer _timer = new(1000);
    private static bool Timered;
    public static List<DownloadObject> DownloadLists = new();
    public static BackgroundDownloader Downloader = new();
    public static List<Task> WritingTasks = new();
    public static Dictionary<string, PictureInfo> AlbumPicturesCache = new();

    public static bool CheckDownloadAbilityAndToast()
    {
        return true;
    }

    public static void AddDownload(NCSong song)
    {
        if (!CheckDownloadAbilityAndToast()) return;
        if (!Timered)
        {
            _timer.Elapsed += Timer_Elapsed;
            _timer.Start();
            Timered = true;
        }

        DownloadLists.Add(new DownloadObject(song));
    }

    private static void Timer_Elapsed(object sender, ElapsedEventArgs elapsedEventArgs)
    {
        if (DownloadLists.Count == 0) return;
        if (DownloadLists[0].Status == 1) return;
        if (DownloadLists[0].Status == 3)
            for (var i = 0; i < DownloadLists.Count; i++)
            {
                if (DownloadLists[i].Status == 2) DownloadLists.RemoveAt(i);
                if (DownloadLists[i].Status == 1) return;
                if (DownloadLists[i].Status == 0)
                {
                    DownloadLists[i].StartDownload();
                    return;
                }
            }

        if (DownloadLists[0].Status == 2)
        {
            DownloadLists.RemoveAt(0);
            if (DownloadLists.Count != 0) return;
            /*
            var downloadToastContent = new ToastContent
            {
                Visual = new ToastVisual
                {
                    BindingGeneric = new ToastBindingGeneric
                    {
                        Children =
                        {
                            new AdaptiveText
                            {
                                Text = "下载全部完成",
                                HintStyle = AdaptiveTextStyle.Header
                            }
                        }
                    }
                },
                Launch = "",
                Scenario = ToastScenario.Reminder,
                Audio = new ToastAudio { Silent = true }
            };
            var toast = new ToastNotification(downloadToastContent.GetXml())
            {
                Tag = "HyPlayerDownloadAllDone",
                Data = new NotificationData()
            };
            toast.Data.SequenceNumber = 0;
            var notifier = ToastNotificationManager.CreateToastNotifier();
            notifier.Show(toast);
            */
            Common.AddToTeachingTipLists("下载全部完成");
            AlbumPicturesCache.Clear();
            _timer.Stop();
            Timered = false;
            return;
        }

        if (DownloadLists[0].Status == 0) DownloadLists[0].StartDownload();
    }

    public static void AddDownload(List<NCSong> songs)
    {
        if (!CheckDownloadAbilityAndToast()) return;
        if (!Timered)
        {
            _timer.Elapsed += Timer_Elapsed;
            _timer.Start();
            Timered = true;
        }

        songs.ForEach(t => { DownloadLists.Add(new DownloadObject(t)); });
    }
}